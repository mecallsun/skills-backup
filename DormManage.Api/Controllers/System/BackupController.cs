using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using System.IO.Compression;
// v2.13.109: 明确引用，避免 File/ZipFile/Directory 命名冲突
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 数据库备份与恢复 API（P1-12，v2.13.109 起仅支持 SQL Server）
///
/// 端点：
/// - GET    /api/v1/system/backup/list          备份文件列表
/// - POST   /api/v1/system/backup/create       立即备份
/// - POST   /api/v1/system/backup/restore      从指定文件恢复
/// - DELETE /api/v1/system/backup/{fileName}   删除备份
/// - GET    /api/v1/system/backup/download/{fn} 下载备份
///
/// 备份目录：{BaseDirectory}/backups/
/// 文件命名：dorm_backup_{yyyyMMdd_HHmmss}.zip（内含 .bak）
/// 备份内容：SQL Server BACKUP DATABASE WITH COMPRESSION（v2.13.109 移除 SQLite 路径）
/// </summary>
[ApiController]
[Route("api/v1/system/backup")]
public class BackupController : ControllerBase
{
    private readonly DormDbContext _db;
    private readonly IConfiguration _config;
    private readonly string _backupDir;

    public BackupController(DormDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
        _backupDir = IOPath.Combine(AppContext.BaseDirectory, "backups");
        Directory.CreateDirectory(_backupDir);
    }

    [HttpGet("list")]
    public async Task<ApiResponse<List<BackupFileDto>>> List()
    {
        var files = IODirectory.GetFiles(_backupDir, "*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupFileDto
            {
                FileName = f.Name,
                FileSize = f.Length,
                FileSizeDisplay = FormatSize(f.Length),
                CreatedAt = f.CreationTime,
                IsAuto = f.Name.Contains("_auto_")
            })
            .ToList();
        return ApiResponse<List<BackupFileDto>>.Ok(files);
    }

    [HttpPost("create")]
    public async Task<ApiResponse<BackupFileDto>> Create([FromBody] BackupCreateRequest? request = null)
    {
        var isAuto = request?.IsAuto ?? false;
        var prefix = isAuto ? "dorm_backup_auto_" : "dorm_backup_";
        var fileName = $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        var filePath = IOPath.Combine(_backupDir, fileName);

        var tempBakPath = string.Empty;
        try
        {
            // v2.13.109: SQLite 已移除，仅 SQL Server BACKUP DATABASE
            var connStr = Environment.GetEnvironmentVariable("DormManage_DB_CONN")
                ?? _config.GetConnectionString("Default")
                ?? "";
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
            var dbName = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName))
                return ApiResponse<BackupFileDto>.Fail("INVALID_CONFIG", "无法从连接串中解析数据库名");

            var backupFileName = IOPath.GetFileNameWithoutExtension(fileName) + ".bak";
            tempBakPath = IOPath.Combine(_backupDir, backupFileName);

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
            {
                await conn.OpenAsync();
                var sql = $"BACKUP DATABASE [{EscapeSqlIdentifier(dbName)}] TO DISK = @path WITH FORMAT, COMPRESSION";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@path", tempBakPath);
                cmd.CommandTimeout = 300;
                await cmd.ExecuteNonQueryAsync();
            }

            // zip 仅含当前 .bak（v2.13.109 修复：避免 CreateFromDirectory 把历史备份一并打包）
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(tempBakPath, IOPath.GetFileName(tempBakPath), CompressionLevel.Optimal);
            }

            // 清理临时 .bak（保留 zip 作为最终交付物）
            try { IOFile.Delete(tempBakPath); tempBakPath = string.Empty; } catch { }

            var fi = new FileInfo(filePath);
            return ApiResponse<BackupFileDto>.Ok(new BackupFileDto
            {
                FileName = fileName,
                FileSize = fi.Length,
                FileSizeDisplay = FormatSize(fi.Length),
                CreatedAt = fi.CreationTime,
                IsAuto = isAuto
            }, "备份创建成功（SQL Server）");
        }
        catch (Exception ex)
        {
            try { if (IOFile.Exists(filePath)) IOFile.Delete(filePath); } catch { }
            try { if (!string.IsNullOrEmpty(tempBakPath) && IOFile.Exists(tempBakPath)) IOFile.Delete(tempBakPath); } catch { }
            return ApiResponse<BackupFileDto>.Fail("BACKUP_FAILED", $"备份失败：{ex.Message}");
        }
    }

    [HttpPost("restore")]
    public async Task<ApiResponse> Restore([FromBody] BackupRestoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return ApiResponse.Fail("FILENAME_REQUIRED", "文件名必填");
        var filePath = IOPath.Combine(_backupDir, request.FileName);
        if (!IOFile.Exists(filePath))
            return ApiResponse.Fail("FILE_NOT_FOUND", "备份文件不存在");

        var tempDir = string.Empty;
        try
        {
            // v2.13.109: SQLite 已移除，仅 SQL Server RESTORE DATABASE
            var connStr = Environment.GetEnvironmentVariable("DormManage_DB_CONN")
                ?? _config.GetConnectionString("Default")
                ?? "";
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
            var dbName = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName))
                return ApiResponse.Fail("INVALID_CONFIG", "无法从连接串中解析数据库名");

            tempDir = IOPath.Combine(IOPath.GetTempPath(), $"restore_{Guid.NewGuid():N}");
            ZipFile.ExtractToDirectory(filePath, tempDir);
            var bakFiles = IODirectory.GetFiles(tempDir, "*.bak");
            if (bakFiles.Length == 0) return ApiResponse.Fail("INVALID_ZIP", "备份 zip 中无 .bak 文件");
            var bakFile = bakFiles.First();

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
            {
                await conn.OpenAsync();
                var sql = $@"ALTER DATABASE [{EscapeSqlIdentifier(dbName)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                             RESTORE DATABASE [{EscapeSqlIdentifier(dbName)}] FROM DISK = @path WITH REPLACE;
                             ALTER DATABASE [{EscapeSqlIdentifier(dbName)}] SET MULTI_USER;";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@path", bakFile);
                cmd.CommandTimeout = 600;
                await cmd.ExecuteNonQueryAsync();
            }

            return ApiResponse.Ok("恢复成功（SQL Server）");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail("RESTORE_FAILED", $"恢复失败：{ex.Message}");
        }
        finally
        {
            // v2.13.109 修复：临时目录 finally 清理
            if (!string.IsNullOrEmpty(tempDir))
            {
                try { if (IODirectory.Exists(tempDir)) IODirectory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [HttpDelete("{fileName}")]
    public ApiResponse Delete(string fileName)
    {
        var filePath = IOPath.Combine(_backupDir, fileName);
        if (!IOFile.Exists(filePath))
            return ApiResponse.Fail("FILE_NOT_FOUND", "备份文件不存在");

        try
        {
            IOFile.Delete(filePath);
            return ApiResponse.Ok("删除成功");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail("DELETE_FAILED", ex.Message);
        }
    }

    [HttpGet("download/{fileName}")]
    public IActionResult Download(string fileName)
    {
        var filePath = IOPath.Combine(_backupDir, fileName);
        if (!IOFile.Exists(filePath))
            return NotFound();

        var bytes = IOFile.ReadAllBytes(filePath);
        return File(bytes, "application/zip", fileName);
    }

    /// <summary>
    /// SQL Server 标识符转义（防 dbName 包含特殊字符如 ]）
    /// </summary>
    private static string EscapeSqlIdentifier(string identifier)
    {
        return identifier.Replace("]", "]]");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F2} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }
}

public class BackupFileDto
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string FileSizeDisplay { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsAuto { get; set; }
}

public class BackupCreateRequest
{
    public bool IsAuto { get; set; }
}

public class BackupRestoreRequest
{
    public string FileName { get; set; } = "";
}