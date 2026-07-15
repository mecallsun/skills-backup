using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DormManage.Api.Controllers.Pda;

/// <summary>
/// PDA 终端接口控制器（P1-7）
///
/// 端点：
/// - POST /api/v1/pda/upload      抄表数据 + 图片 Multipart 上传
/// - GET  /api/v1/pda/image/{fn}  图片流返回
/// - GET  /api/v1/pda/coverage    抄表覆盖率统计
///
/// 设计要点：
/// - PDA 端可能上传 0~3 张图片（冷水/热水/电表各一张）
/// - 图片存储：ApiServer.BaseDirectory/PdaImages/{yyyy-MM}/{Guid}.jpg
/// - 上传后立即入库（MeterRecord + 关联图片路径）
/// </summary>
[ApiController]
[Route("api/v1/pda")]
public class PdaController : ControllerBase
{
    private readonly DormDbContext _db;
    private readonly ILogger<PdaController> _logger;
    private readonly string _imageRoot;

    public PdaController(DormDbContext db, ILogger<PdaController> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;

        // v2.13.3：图片路径优先取托盘注入的 DormManage_IMAGE_ROOT，其次 appsettings.json，最后默认
        _imageRoot = Environment.GetEnvironmentVariable("DormManage_IMAGE_ROOT")
            ?? config["Storage:ImageRoot"]
            ?? "Storage/images";
        if (!Path.IsPathRooted(_imageRoot))
        {
            var baseDir = AppContext.BaseDirectory;
            _imageRoot = Path.Combine(baseDir, _imageRoot);
        }
        Directory.CreateDirectory(_imageRoot);
    }

    /// <summary>
    /// 抄表上传（PDA 端调用）
    /// </summary>
    /// <param name="metadata">JSON 格式抄表元数据（必填）</param>
    /// <param name="coldImage">冷水表图片（可选）</param>
    /// <param name="hotImage">热水表图片（可选）</param>
    /// <param name="electricImage">电表图片（可选）</param>
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50MB
    public async Task<ApiResponse<PdaUploadResult>> Upload(
        [FromForm] string metadata,
        [FromForm] IFormFile? coldImage,
        [FromForm] IFormFile? hotImage,
        [FromForm] IFormFile? electricImage)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return ApiResponse<PdaUploadResult>.Fail("METADATA_REQUIRED", "metadata 字段必填");

        PdaUploadMetadata? meta;
        try
        {
            meta = JsonSerializer.Deserialize<PdaUploadMetadata>(metadata,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            return ApiResponse<PdaUploadResult>.Fail("METADATA_INVALID", $"metadata JSON 解析失败：{ex.Message}");
        }

        if (meta is null || string.IsNullOrWhiteSpace(meta.DormCode) || string.IsNullOrWhiteSpace(meta.ReadMonth))
            return ApiResponse<PdaUploadResult>.Fail("INVALID_METADATA", "DormCode 和 ReadMonth 必填");

        // 1. 查找宿舍
        var dorm = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == meta.DormCode && d.IsActive);
        if (dorm is null)
            return ApiResponse<PdaUploadResult>.Fail("DORM_NOT_FOUND", $"宿舍 {meta.DormCode} 不存在或已停用");

        // 2. 查找或创建记录（按 DormCode + ReadMonth 唯一）
        var existing = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormCode == meta.DormCode && r.ReadMonth == meta.ReadMonth);

        // 3. 保存图片
        var monthDir = Path.Combine(_imageRoot, meta.ReadMonth);
        Directory.CreateDirectory(monthDir);

        string? coldPath = null, hotPath = null, electricPath = null;
        if (coldImage is not null && coldImage.Length > 0)
            coldPath = await SaveImage(coldImage, monthDir, "cold");
        if (hotImage is not null && hotImage.Length > 0)
            hotPath = await SaveImage(hotImage, monthDir, "hot");
        if (electricImage is not null && electricImage.Length > 0)
            electricPath = await SaveImage(electricImage, monthDir, "electric");

        var imagePaths = string.Join(";", new[] { coldPath, hotPath, electricPath }.Where(p => p is not null));

        if (existing is null)
        {
            // 新建记录
            var record = new MeterRecord
            {
                DormId = dorm.Id,
                DormCode = meta.DormCode,
                ReadMonth = meta.ReadMonth,
                ColdMeter = meta.ColdMeter,
                HotMeter = meta.HotMeter,
                ElectricMeter = meta.ElectricMeter,
                Operator = meta.Operator ?? "PDA",
                DeviceSn = meta.DeviceSn,
                ClientRecordId = meta.ClientRecordId,
                ClientCreatedAt = meta.ClientCreatedAt,
                ServerCreatedAt = DateTime.Now,
                Status = (byte)MeterRecordStatus.Normal,
                Remark = imagePaths.Length > 0 ? $"[图片: {imagePaths}]" : null
            };
            _db.MeterRecords.Add(record);
            await _db.SaveChangesAsync();

            return ApiResponse<PdaUploadResult>.Ok(new PdaUploadResult
            {
                RecordId = record.Id,
                IsNew = true,
                ColdImageUrl = coldPath,
                HotImageUrl = hotPath,
                ElectricImageUrl = electricPath
            }, "上传成功（新建）");
        }
        else
        {
            // 已存在记录：未完成 → 正常
            if (existing.Status == (byte)MeterRecordStatus.Incomplete || existing.Status == (byte)MeterRecordStatus.Unfinished)
            {
                existing.Status = (byte)MeterRecordStatus.Normal;
                existing.ColdMeter = meta.ColdMeter;
                existing.HotMeter = meta.HotMeter;
                existing.ElectricMeter = meta.ElectricMeter;
                existing.Operator = meta.Operator ?? existing.Operator;
                existing.DeviceSn = meta.DeviceSn ?? existing.DeviceSn;
                existing.ClientRecordId = meta.ClientRecordId ?? existing.ClientRecordId;
                existing.ClientCreatedAt = meta.ClientCreatedAt ?? existing.ClientCreatedAt;
                var appendNote = $"[PDA补录 {DateTime.Now:yyyy-MM-dd HH:mm} {imagePaths}]";
                existing.Remark = string.IsNullOrEmpty(existing.Remark) ? appendNote : $"{existing.Remark}\n{appendNote}";
                await _db.SaveChangesAsync();

                return ApiResponse<PdaUploadResult>.Ok(new PdaUploadResult
                {
                    RecordId = existing.Id,
                    IsNew = false,
                    ColdImageUrl = coldPath,
                    HotImageUrl = hotPath,
                    ElectricImageUrl = electricPath
                }, "上传成功（补录）");
            }
            else
            {
                return ApiResponse<PdaUploadResult>.Fail("ALREADY_NORMAL", $"宿舍 {meta.DormCode}/{meta.ReadMonth} 已是正常状态，请走修正流程");
            }
        }
    }

    /// <summary>
    /// 图片流返回
    /// </summary>
    [HttpGet("image/{*filePath}")]
    public IActionResult GetImage(string filePath)
    {
        // 路径安全校验：禁止 .. 跳出
        var fullPath = Path.GetFullPath(Path.Combine(_imageRoot, filePath));
        if (!fullPath.StartsWith(_imageRoot, StringComparison.OrdinalIgnoreCase))
            return BadRequest("非法路径");

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(fullPath, contentType);
    }

    /// <summary>
    /// 抄表覆盖率统计
    /// </summary>
    /// <param name="readMonth">抄表月份（yyyy-MM），默认当前月</param>
    [HttpGet("coverage")]
    public async Task<ApiResponse<MeterCoverageDto>> GetCoverage([FromQuery] string? readMonth = null)
    {
        var month = string.IsNullOrWhiteSpace(readMonth) ? DateTime.Now.ToString("yyyy-MM") : readMonth;

        var totalDorms = await _db.Dorms.CountAsync(d => d.IsActive);
        var records = await _db.MeterRecords
            .Where(r => r.ReadMonth == month)
            .Select(r => new { r.DormCode, r.Status })
            .ToListAsync();

        var normal = records.Count(r => r.Status == (byte)MeterRecordStatus.Normal || r.Status == (byte)MeterRecordStatus.Corrected);
        var incomplete = records.Count(r => r.Status == (byte)MeterRecordStatus.Incomplete || r.Status == (byte)MeterRecordStatus.Unfinished);
        var voided = records.Count(r => r.Status == (byte)MeterRecordStatus.Voided);

        var recordDorms = records.Select(r => r.DormCode).Distinct().Count();
        var pending = Math.Max(0, totalDorms - recordDorms);

        return ApiResponse<MeterCoverageDto>.Ok(new MeterCoverageDto
        {
            ReadMonth = month,
            TotalDorms = totalDorms,
            NormalCount = normal,
            IncompleteCount = incomplete,
            VoidedCount = voided,
            PendingCount = pending,
            CoverageRate = totalDorms > 0 ? Math.Round((normal + voided) * 100m / totalDorms, 1) : 0,
            EffectiveRate = totalDorms > 0 ? Math.Round(normal * 100m / totalDorms, 1) : 0
        });
    }

    private async Task<string> SaveImage(IFormFile file, string dir, string prefix)
    {
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var fileName = $"{prefix}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        // 返回相对路径（便于跨机器部署）
        return Path.GetRelativePath(_imageRoot, fullPath).Replace('\\', '/');
    }
}

public class PdaUploadMetadata
{
    public string DormCode { get; set; } = string.Empty;
    public string ReadMonth { get; set; } = string.Empty;
    public decimal ColdMeter { get; set; }
    public decimal HotMeter { get; set; }
    public decimal ElectricMeter { get; set; }
    public string? Operator { get; set; }
    public string? DeviceSn { get; set; }
    public string? ClientRecordId { get; set; }
    public DateTime? ClientCreatedAt { get; set; }
}

public class PdaUploadResult
{
    public long RecordId { get; set; }
    public bool IsNew { get; set; }
    public string? ColdImageUrl { get; set; }
    public string? HotImageUrl { get; set; }
    public string? ElectricImageUrl { get; set; }
}

public class MeterCoverageDto
{
    public string ReadMonth { get; set; } = string.Empty;
    public int TotalDorms { get; set; }
    public int NormalCount { get; set; }
    public int IncompleteCount { get; set; }
    public int VoidedCount { get; set; }
    public int PendingCount { get; set; }
    public decimal CoverageRate { get; set; }
    public decimal EffectiveRate { get; set; }
}