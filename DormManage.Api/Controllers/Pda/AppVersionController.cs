using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Api.Controllers.Pda;

/// <summary>
/// PDA 版本管理 API（P2-7）
///
/// 端点：
/// - GET    /api/v1/appversion/list          版本列表
/// - GET    /api/v1/appversion/latest        最新版本（PDA 启动时调用）
/// - GET    /api/v1/appversion/{id}          详情
/// - POST   /api/v1/appversion               创建版本（含 APK 上传）
/// - PUT    /api/v1/appversion/{id}          更新元数据
/// - POST   /api/v1/appversion/{id}/enable   启用/禁用
/// - DELETE /api/v1/appversion/{id}          删除（含 APK 文件）
/// - GET    /api/v1/appversion/{id}/download 下载 APK
/// </summary>
[ApiController]
[Route("api/v1/appversion")]
public class AppVersionController : ControllerBase
{
    private readonly DormDbContext _db;
    private readonly string _storageDir;

    public AppVersionController(DormDbContext db)
    {
        _db = db;
        _storageDir = Path.Combine(AppContext.BaseDirectory, "pda-apps");
        Directory.CreateDirectory(_storageDir);
    }

    [HttpGet("list")]
    public async Task<ApiResponse<List<AppVersionDto>>> List()
    {
        var versions = await _db.AppVersions.OrderByDescending(v => v.ReleaseDate).ToListAsync();
        return ApiResponse<List<AppVersionDto>>.Ok(versions.Select(MapToDto).ToList());
    }

    [HttpGet("latest")]
    public async Task<ApiResponse<AppVersionDto>> Latest()
    {
        var v = await _db.AppVersions.FirstOrDefaultAsync(x => x.IsLatest && x.IsEnabled);
        if (v is null)
            return ApiResponse<AppVersionDto>.Fail("NOT_FOUND", "暂无最新版本");
        return ApiResponse<AppVersionDto>.Ok(MapToDto(v));
    }

    [HttpGet("{id:int}")]
    public async Task<ApiResponse<AppVersionDto>> GetById(int id)
    {
        var v = await _db.AppVersions.FindAsync(id);
        if (v is null) return ApiResponse<AppVersionDto>.Fail("NOT_FOUND", "版本不存在");
        return ApiResponse<AppVersionDto>.Ok(MapToDto(v));
    }

    [HttpPost]
    [RequestSizeLimit(200_000_000)] // 200MB
    public async Task<ApiResponse<AppVersionDto>> Create([FromForm] AppVersionCreateRequest request, IFormFile? apkFile)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
            return ApiResponse<AppVersionDto>.Fail("VERSION_REQUIRED", "版本号必填");
        if (await _db.AppVersions.AnyAsync(v => v.Version == request.Version))
            return ApiResponse<AppVersionDto>.Fail("DUPLICATE_VERSION", $"版本 {request.Version} 已存在");

        string? savedFile = null;
        long fileSize = 0;
        string? md5 = null;

        if (apkFile is not null && apkFile.Length > 0)
        {
            savedFile = $"dorm-pda-{request.Version}-{Guid.NewGuid():N}.apk";
            var fullPath = Path.Combine(_storageDir, savedFile);
            using (var fs = new FileStream(fullPath, FileMode.Create))
            {
                await apkFile.CopyToAsync(fs);
            }
            fileSize = apkFile.Length;
            md5 = await ComputeMd5Async(fullPath);
        }

        var entity = new AppVersion
        {
            Version = request.Version,
            FileName = savedFile,
            FileSize = fileSize,
            ReleaseNotes = request.ReleaseNotes,
            IsLatest = request.IsLatest,
            IsEnabled = request.IsEnabled ?? true,
            IsForceUpdate = request.IsForceUpdate,
            MinCompatibleVersion = request.MinCompatibleVersion,
            Md5 = md5,
            ReleaseDate = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        _db.AppVersions.Add(entity);

        if (request.IsLatest)
        {
            var others = await _db.AppVersions.Where(v => v.Id != entity.Id && v.IsLatest).ToListAsync();
            foreach (var o in others) o.IsLatest = false;
        }

        await _db.SaveChangesAsync();
        return ApiResponse<AppVersionDto>.Ok(MapToDto(entity), "版本创建成功");
    }

    [HttpPut("{id:int}")]
    public async Task<ApiResponse<AppVersionDto>> Update(int id, [FromBody] AppVersionUpdateRequest request)
    {
        var v = await _db.AppVersions.FindAsync(id);
        if (v is null) return ApiResponse<AppVersionDto>.Fail("NOT_FOUND", "版本不存在");

        v.ReleaseNotes = request.ReleaseNotes ?? v.ReleaseNotes;
        v.IsEnabled = request.IsEnabled ?? v.IsEnabled;
        v.IsForceUpdate = request.IsForceUpdate;
        v.MinCompatibleVersion = request.MinCompatibleVersion ?? v.MinCompatibleVersion;

        if (request.IsLatest == true && !v.IsLatest)
        {
            var others = await _db.AppVersions.Where(x => x.Id != id && x.IsLatest).ToListAsync();
            foreach (var o in others) o.IsLatest = false;
            v.IsLatest = true;
        }
        v.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResponse<AppVersionDto>.Ok(MapToDto(v), "更新成功");
    }

    [HttpPost("{id:int}/enable")]
    public async Task<ApiResponse> Enable(int id, [FromBody] EnableRequest request)
    {
        var v = await _db.AppVersions.FindAsync(id);
        if (v is null) return ApiResponse.Fail("NOT_FOUND", "版本不存在");
        v.IsEnabled = request.IsEnabled;
        v.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse.Ok(request.IsEnabled ? "已启用" : "已禁用");
    }

    [HttpDelete("{id:int}")]
    public async Task<ApiResponse> Delete(int id)
    {
        var v = await _db.AppVersions.FindAsync(id);
        if (v is null) return ApiResponse.Fail("NOT_FOUND", "版本不存在");
        if (!string.IsNullOrEmpty(v.FileName))
        {
            var fullPath = Path.Combine(_storageDir, v.FileName);
            try { if (global::System.IO.File.Exists(fullPath)) global::System.IO.File.Delete(fullPath); } catch { }
        }
        _db.AppVersions.Remove(v);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    [HttpGet("{id:int}/download")]
    public IActionResult Download(int id)
    {
        var v = _db.AppVersions.Find(id);
        if (v is null) return NotFound();
        if (string.IsNullOrEmpty(v.FileName)) return BadRequest("该版本无 APK 文件");
        var fullPath = Path.Combine(_storageDir, v.FileName);
        if (!global::System.IO.File.Exists(fullPath)) return NotFound("APK 文件不存在");
        return PhysicalFile(fullPath, "application/vnd.android.package-archive", v.FileName);
    }

    /// <summary>
    /// 生成占位 APK（v2.13.3 测试用）：创建一个假的 APK 文件用于演示上传/下载流程
    /// </summary>
    [HttpPost("generate-placeholder")]
    public async Task<ApiResponse<AppVersionDto>> GeneratePlaceholder([FromBody] PlaceholderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
            return ApiResponse<AppVersionDto>.Fail("VERSION_REQUIRED", "版本号必填");

        var entity = new AppVersion
        {
            Version = request.Version,
            FileName = $"dorm-pda-{request.Version}-placeholder.apk",
            FileSize = request.SizeBytes ?? (5 * 1024 * 1024), // 默认 5MB
            ReleaseNotes = request.ReleaseNotes ?? "占位 APK（演示用，非真实文件）",
            IsLatest = request.IsLatest,
            IsEnabled = request.IsEnabled ?? true,
            IsForceUpdate = request.IsForceUpdate,
            MinCompatibleVersion = request.MinCompatibleVersion,
            Md5 = null,
            ReleaseDate = DateTime.Now,
            CreatedAt = DateTime.Now
        };

        // 写入一个伪 APK 文件（包含版本信息的文本内容）
        var fullPath = Path.Combine(_storageDir, entity.FileName);
        var content = $"DormManage PDA App Placeholder APK\nVersion: {request.Version}\nSize: {entity.FileSize}\nGenerated: {DateTime.Now}\nNote: This is a placeholder for testing. Replace with real APK from build pipeline.";
        await global::System.IO.File.WriteAllTextAsync(fullPath, content);

        _db.AppVersions.Add(entity);
        if (request.IsLatest)
        {
            var others = await _db.AppVersions.Where(v => v.Id != entity.Id && v.IsLatest).ToListAsync();
            foreach (var o in others) o.IsLatest = false;
        }
        await _db.SaveChangesAsync();

        entity.Md5 = await ComputeMd5Async(fullPath);

        return ApiResponse<AppVersionDto>.Ok(MapToDto(entity), $"占位 APK 已生成（{entity.FileSize} bytes）");
    }

    private static AppVersionDto MapToDto(AppVersion v) => new()
    {
        Id = v.Id,
        Version = v.Version,
        FileName = v.FileName,
        FileSize = v.FileSize,
        FileSizeDisplay = FormatSize(v.FileSize),
        ReleaseNotes = v.ReleaseNotes,
        IsLatest = v.IsLatest,
        IsEnabled = v.IsEnabled,
        IsForceUpdate = v.IsForceUpdate,
        MinCompatibleVersion = v.MinCompatibleVersion,
        Md5 = v.Md5,
        ReleaseDate = v.ReleaseDate
    };

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F2} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }

    private static async Task<string> ComputeMd5Async(string filePath)
    {
        using var md5 = global::System.Security.Cryptography.MD5.Create();
        await using var stream = global::System.IO.File.OpenRead(filePath);
        var hash = await md5.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public class AppVersionDto
{
    public int Id { get; set; }
    public string Version { get; set; } = "";
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public string FileSizeDisplay { get; set; } = "";
    public string? ReleaseNotes { get; set; }
    public bool IsLatest { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsForceUpdate { get; set; }
    public string? MinCompatibleVersion { get; set; }
    public string? Md5 { get; set; }
    public DateTime ReleaseDate { get; set; }
}

public class AppVersionCreateRequest
{
    public string Version { get; set; } = "";
    public string? ReleaseNotes { get; set; }
    public bool IsLatest { get; set; }
    public bool? IsEnabled { get; set; } = true;
    public bool IsForceUpdate { get; set; }
    public string? MinCompatibleVersion { get; set; }
}

public class AppVersionUpdateRequest
{
    public string? ReleaseNotes { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsForceUpdate { get; set; }
    public string? MinCompatibleVersion { get; set; }
    public bool? IsLatest { get; set; }
}

public class EnableRequest
{
    public bool IsEnabled { get; set; }
}

public class PlaceholderRequest
{
    public string Version { get; set; } = "";
    public string? ReleaseNotes { get; set; }
    public bool IsLatest { get; set; }
    public bool? IsEnabled { get; set; } = true;
    public bool IsForceUpdate { get; set; }
    public string? MinCompatibleVersion { get; set; }
    public long? SizeBytes { get; set; }
}