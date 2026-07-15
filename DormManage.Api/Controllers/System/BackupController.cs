using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using System.IO.Compression;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 数据库备份与恢复 API（P1-12）
///
/// 端点：
/// - GET    /api/v1/system/backup/list          备份文件列表
/// - POST   /api/v1/system/backup/create       立即备份
/// - POST   /api/v1/system/backup/restore      从指定文件恢复
/// - DELETE /api/v1/system/backup/{fileName}   删除备份
/// - GET    /api/v1/system/backup/download/{fn} 下载备份
///
/// 备份目录：{BaseDirectory}/backups/
/// 文件命名：dorm_backup_{yyyyMMdd_HHmmss}.zip
/// 备份内容：SQLite db 文件（生产环境为 SQL Server，建议走 DBA 工具）
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
        _backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
        Directory.CreateDirectory(_backupDir);
    }

    [HttpGet("list")]
    public async Task<ApiResponse<List<BackupFileDto>>> List()
    {
        var files = Directory.GetFiles(_backupDir, "*.zip")
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
        var filePath = Path.Combine(_backupDir, fileName);

        try
        {
            var dbProvider = (_config["Database:Provider"] ?? "SqlServer").ToLowerInvariant();
            if (dbProvider == "sqlite")
            {
                var dbPathEnv = Environment.GetEnvironmentVariable("DormManage_DB_PATH");
                var dbPath = !string.IsNullOrEmpty(dbPathEnv)
                    ? dbPathEnv
                    : (_config.GetConnectionString("Default")?.Replace("Data Source=", "") ?? "dorm.db");
                if (!Path.IsPathRooted(dbPath))
                    dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);

                if (!global::System.IO.File.Exists(dbPath))
                    return ApiResponse<BackupFileDto>.Fail("DB_NOT_FOUND", $"SQLite 数据库文件不存在：{dbPath}");

                using (var zip = global::System.IO.Compression.ZipFile.Open(filePath, global::System.IO.Compression.ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(dbPath, Path.GetFileName(dbPath));
                }

                var fi = new FileInfo(filePath);
                return ApiResponse<BackupFileDto>.Ok(new BackupFileDto
                {
                    FileName = fileName,
                    FileSize = fi.Length,
                    FileSizeDisplay = FormatSize(fi.Length),
                    CreatedAt = fi.CreationTime,
                    IsAuto = isAuto
                }, "备份创建成功");
            }
            else
            {
                var connStr = Environment.GetEnvironmentVariable("DormManage_DB_CONN")
                    ?? _config.GetConnectionString("Default")
                    ?? "";
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
                var dbName = builder.InitialCatalog;
                var backupFileName = Path.GetFileNameWithoutExtension(fileName) + ".bak";
                var sqlBackupPath = Path.Combine(_backupDir, backupFileName);

                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                await conn.OpenAsync();
                var sql = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, COMPRESSION";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@path", sqlBackupPath);
                cmd.CommandTimeout = 300;
                await cmd.ExecuteNonQueryAsync();

                var fi = new FileInfo(sqlBackupPath);
                global::System.IO.Compression.ZipFile.CreateFromDirectory(
                    Path.GetDirectoryName(sqlBackupPath)!,
                    filePath,
                    global::System.IO.Compression.CompressionLevel.Optimal,
                    includeBaseDirectory: false,
                    entryNameEncoding: global::System.Text.Encoding.UTF8);

                return ApiResponse<BackupFileDto>.Ok(new BackupFileDto
                {
                    FileName = fileName,
                    FileSize = fi.Length,
                    FileSizeDisplay = FormatSize(fi.Length),
                    CreatedAt = fi.CreationTime,
                    IsAuto = isAuto
                }, "备份创建成功（SQL Server）");
            }
        }
        catch (Exception ex)
        {
            try { if (global::System.IO.File.Exists(filePath)) global::System.IO.File.Delete(filePath); } catch { }
            return ApiResponse<BackupFileDto>.Fail("BACKUP_FAILED", $"备份失败：{ex.Message}");
        }
    }

    [HttpPost("restore")]
    public async Task<ApiResponse> Restore([FromBody] BackupRestoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return ApiResponse.Fail("FILENAME_REQUIRED", "文件名必填");
        var filePath = Path.Combine(_backupDir, request.FileName);
        if (!global::System.IO.File.Exists(filePath))
            return ApiResponse.Fail("FILE_NOT_FOUND", "备份文件不存在");

        try
        {
            var dbProvider = (_config["Database:Provider"] ?? "SqlServer").ToLowerInvariant();
            if (dbProvider == "sqlite")
            {
                var dbPathEnv = Environment.GetEnvironmentVariable("DormManage_DB_PATH");
                var dbPath = !string.IsNullOrEmpty(dbPathEnv)
                    ? dbPathEnv
                    : (_config.GetConnectionString("Default")?.Replace("Data Source=", "") ?? "dorm.db");
                if (!Path.IsPathRooted(dbPath))
                    dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);

                using var zip = global::System.IO.Compression.ZipFile.OpenRead(filePath);
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".db", StringComparison.OrdinalIgnoreCase));
                if (entry is null) return ApiResponse.Fail("INVALID_ZIP", "备份 zip 中无 .db 文件");

                if (global::System.IO.File.Exists(dbPath))
                {
                    var preRestoreBackup = Path.Combine(_backupDir, $"pre_restore_{DateTime.Now:HHmmss}.db");
                    global::System.IO.File.Copy(dbPath, preRestoreBackup, overwrite: true);
                }

                entry.ExtractToFile(dbPath, overwrite: true);
                return ApiResponse.Ok("恢复成功（SQLite），请重启服务以生效");
            }
            else
            {
                var connStr = Environment.GetEnvironmentVariable("DormManage_DB_CONN")
                    ?? _config.GetConnectionString("Default")
                    ?? "";
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
                var dbName = builder.InitialCatalog;

                var tempDir = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid():N}");
                global::System.IO.Compression.ZipFile.ExtractToDirectory(filePath, tempDir);
                var bakFile = Directory.GetFiles(tempDir, "*.bak").FirstOrDefault();
                if (bakFile is null) return ApiResponse.Fail("INVALID_ZIP", "备份 zip 中无 .bak 文件");

                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                await conn.OpenAsync();
                var sql = $@"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                             RESTORE DATABASE [{dbName}] FROM DISK = @path WITH REPLACE;
                             ALTER DATABASE [{dbName}] SET MULTI_USER;";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@path", bakFile);
                cmd.CommandTimeout = 600;
                await cmd.ExecuteNonQueryAsync();
                Directory.Delete(tempDir, recursive: true);

                return ApiResponse.Ok("恢复成功（SQL Server）");
            }
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail("RESTORE_FAILED", $"恢复失败：{ex.Message}");
        }
    }

    [HttpDelete("{fileName}")]
    public ApiResponse Delete(string fileName)
    {
        var filePath = Path.Combine(_backupDir, fileName);
        if (!global::System.IO.File.Exists(filePath))
            return ApiResponse.Fail("FILE_NOT_FOUND", "备份文件不存在");

        try
        {
            global::System.IO.File.Delete(filePath);
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
        var filePath = Path.Combine(_backupDir, fileName);
        if (!global::System.IO.File.Exists(filePath))
            return NotFound();

        var bytes = global::System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "application/zip", fileName);
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