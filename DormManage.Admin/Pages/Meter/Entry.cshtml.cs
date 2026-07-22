using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 手动补录抄表页面模型（v2.13.68 100% 原型对齐）
///
/// 关键规则（参考原型 entry.html v2.11.4）：
/// - status=1(正常)/status=2(已修正) 拒绝直接覆盖，提示走修正流程
/// - status=0(未完成)/3(未完成PDA)/4(已作废) 允许覆盖，旧数据追加到 Remark
/// - 三表读数必须 ≥ 上月读数，否则红色校验 + 禁用提交
/// </summary>
public class EntryModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IConfiguration _config;
    private readonly string _imageRoot;

    public EntryModel(DormDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;

        // v2.13.68：与 PdaController 一致的图片根目录解析（环境变量 → 配置 → 默认）
        _imageRoot = Environment.GetEnvironmentVariable("DormManage_IMAGE_ROOT")
            ?? config["Storage:ImageRoot"]
            ?? @"D:\MeterImages";
        if (!Path.IsPathRooted(_imageRoot))
        {
            _imageRoot = Path.Combine(AppContext.BaseDirectory, _imageRoot);
        }
        Directory.CreateDirectory(_imageRoot);
    }

    [BindProperty(SupportsGet = true)]
    public int? DormId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReadMonth { get; set; }

    /// <summary>宿舍列表（用于下拉选择）</summary>
    public List<MeterEntryDto> Dorms { get; set; } = new();

    /// <summary>上月智能抄表参考（用于上月读数提示卡片）</summary>
    public MeterRecord? LastRecord { get; set; }

    /// <summary>已存在记录警告（status=0/3/4 可覆盖；status=1/2 拒绝）</summary>
    public string? ExistWarning { get; set; }

    /// <summary>上月读数参考文本</summary>
    public string? LastReadingRef { get; set; }

    /// <summary>当前宿舍当前月份已有记录的 ID（用于 JS 预填 + 关联图片上传）</summary>
    public long? ExistingRecordId { get; set; }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrEmpty(ReadMonth))
        {
            ReadMonth = DateTime.Now.ToString("yyyy-MM");
        }

        // 加载启用的宿舍列表
        Dorms = await _db.Dorms
            .Where(d => d.IsActive)
            .OrderBy(d => d.DormCode)
            .Select(d => new MeterEntryDto
            {
                Id = d.Id,
                DormCode = d.DormCode,
                AddressText = d.AddressText ?? "-"
            })
            .ToListAsync();

        // 如果有宿舍和月份，加载上月读数参考 + 已有记录
        if (DormId.HasValue && !string.IsNullOrEmpty(ReadMonth))
        {
            await LoadReadingsAsync();
        }
    }

    /// <summary>
    /// v2.13.68 AJAX 提交抄表读数 — 返回 JsonResult（不再 Redirect）
    ///
    /// 业务规则（参考原型 entry.html v2.11.4）：
    /// 1. 表读数 ≥ 上月（无上月时允许任何值）
    /// 2. status=1/2 拒绝，提示走修正流程
    /// 3. status=0/3/4 覆盖，旧数据追加到 Remark
    /// 4. 新建：status 由 DetermineStatus 自动判定
    /// </summary>
    public async Task<IActionResult> OnPostSaveReadingsAsync(
        [FromForm] int DormId,
        [FromForm] string ReadMonth,
        [FromForm] decimal ColdMeter,
        [FromForm] decimal HotMeter,
        [FromForm] decimal ElectricMeter,
        [FromForm] string? Remark)
    {
        // 基本校验
        if (DormId <= 0) return new JsonResult(new { success = false, message = "请选择宿舍" });
        if (string.IsNullOrWhiteSpace(ReadMonth)) return new JsonResult(new { success = false, message = "请选择抄表月份" });
        if (!DateTime.TryParseExact(ReadMonth + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
            return new JsonResult(new { success = false, message = "抄表月份格式错误（应为 yyyy-MM）" });
        if (ColdMeter < 0 || HotMeter < 0 || ElectricMeter < 0)
            return new JsonResult(new { success = false, message = "表读数不能为负数" });

        // 上月读数校验
        var lastMonth = DateTime.ParseExact(ReadMonth + "-01", "yyyy-MM-dd", null).AddMonths(-1).ToString("yyyy-MM");
        var prev = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormId == DormId && r.ReadMonth == lastMonth);

        if (prev != null)
        {
            if (ColdMeter < prev.ColdMeter)
                return new JsonResult(new { success = false, message = $"冷水读数必须 ≥ 上月 {prev.ColdMeter:F2}", errorField = "ColdMeter" });
            if (HotMeter < prev.HotMeter)
                return new JsonResult(new { success = false, message = $"热水读数必须 ≥ 上月 {prev.HotMeter:F2}", errorField = "HotMeter" });
            if (ElectricMeter < prev.ElectricMeter)
                return new JsonResult(new { success = false, message = $"电表读数必须 ≥ 上月 {prev.ElectricMeter:F2}", errorField = "ElectricMeter" });
        }

        // 查找宿舍
        var dorm = await _db.Dorms.FindAsync(DormId);
        if (dorm == null)
            return new JsonResult(new { success = false, message = $"宿舍 ID {DormId} 不存在" });

        var existing = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormId == DormId && r.ReadMonth == ReadMonth);

        // v2.13.68 P0 修复：status=1(正常)/status=2(已修正) 拒绝直接覆盖
        if (existing != null && (existing.Status == (byte)MeterRecordStatus.Normal || existing.Status == (byte)MeterRecordStatus.Corrected))
        {
            var statusName = ((MeterRecordStatus)existing.Status).GetDisplayName();
            return new JsonResult(new {
                success = false,
                message = $"该宿舍 {ReadMonth} 已有【{statusName}】记录（ID: {existing.Id}），如需修改请走【修正】流程，不可重复补录。"
            });
        }

        try
        {
            if (existing != null)
            {
                // v2.13.68 P0 修复：覆盖模式 — 旧数据追加到 Remark 历史
                var snapshot = $"[{(DateTime.Now):yyyy-MM-dd HH:mm} 覆盖前] " +
                    $"cold={existing.ColdMeter:F2}, hot={existing.HotMeter:F2}, electric={existing.ElectricMeter:F2}, " +
                    $"status={existing.Status}({existing.GetStatusName()}), operator={existing.Operator ?? "(空)"}";
                existing.Remark = string.IsNullOrEmpty(existing.Remark)
                    ? snapshot
                    : $"{existing.Remark}\n{snapshot}";

                existing.ColdMeter = ColdMeter;
                existing.HotMeter = HotMeter;
                existing.ElectricMeter = ElectricMeter;
                existing.ColdUsage = prev != null ? ColdMeter - prev.ColdMeter : ColdMeter;
                existing.HotUsage = prev != null ? HotMeter - prev.HotMeter : HotMeter;
                existing.ElectricUsage = prev != null ? ElectricMeter - prev.ElectricMeter : ElectricMeter;
                existing.PreviousColdReading = prev?.ColdMeter ?? 0;
                existing.PreviousHotReading = prev?.HotMeter ?? 0;
                existing.PreviousElectricReading = prev?.ElectricMeter ?? 0;
                existing.Operator = "admin（后台补录）";
                if (!string.IsNullOrEmpty(Remark))
                {
                    existing.Remark = $"{existing.Remark}\n[管理员备注] {Remark}";
                }
                existing.ServerCreatedAt = DateTime.Now;
                existing.ReadMode = (byte)MeterReadMode.Manual;
                existing.Status = MeterRecord.DetermineStatus(ColdMeter, HotMeter, ElectricMeter);

                await _db.SaveChangesAsync();
                return new JsonResult(new { success = true, recordId = existing.Id, isUpdate = true, message = "智能抄表已覆盖（旧数据已保存到备注历史）" });
            }
            else
            {
                // 新建记录 — v2.13.68：DormDbContext 已加 HasColumnType("int") 修复 EF int↔BIGINT 读回 cast 异常
                var newRecord = new MeterRecord
                {
                    DormId = DormId,
                    DormCode = dorm.DormCode,
                    ReadMonth = ReadMonth,
                    ColdMeter = ColdMeter,
                    HotMeter = HotMeter,
                    ElectricMeter = ElectricMeter,
                    ColdUsage = prev != null ? ColdMeter - prev.ColdMeter : ColdMeter,
                    HotUsage = prev != null ? HotMeter - prev.HotMeter : HotMeter,
                    ElectricUsage = prev != null ? ElectricMeter - prev.ElectricMeter : ElectricMeter,
                    PreviousColdReading = prev?.ColdMeter ?? 0,
                    PreviousHotReading = prev?.HotMeter ?? 0,
                    PreviousElectricReading = prev?.ElectricMeter ?? 0,
                    Operator = "admin（后台补录）",
                    DeviceSn = "",
                    ClientRecordId = $"MANUAL-{Guid.NewGuid():N}".Substring(0, 32),
                    ClientCreatedAt = DateTime.Now,
                    Remark = string.IsNullOrEmpty(Remark) ? null : $"[管理员备注] {Remark}",
                    ServerCreatedAt = DateTime.Now,
                    ReadDate = DateOnly.FromDateTime(DateTime.Now),
                    ReadMode = (byte)MeterReadMode.Manual,
                    Status = MeterRecord.DetermineStatus(ColdMeter, HotMeter, ElectricMeter),
                    CreatedAt = DateTime.Now
                };
                _db.MeterRecords.Add(newRecord);
                await _db.SaveChangesAsync();
                return new JsonResult(new { success = true, recordId = newRecord.Id, isUpdate = false, message = "智能抄表已创建" });
            }
        }
        catch (Exception ex)
        {
            // v2.13.68 返回更详细的错误信息（含 inner exception）便于调试
            var innerMsg = ex.InnerException?.Message ?? "(no inner)";
            return new JsonResult(new { success = false, message = $"保存失败：{ex.Message} | 内部: {innerMsg}" });
        }
    }

    /// <summary>
    /// v2.13.68 上传抄表图片 — 单独 AJAX 端点（save readings 之后调用）
    ///
    /// URL: POST /Meter/Entry?handler=UploadImage&amp;recordId=X&amp;meterType=cold
    /// 表单: image (IFormFile)
    /// </summary>
    public async Task<IActionResult> OnPostUploadImageAsync(
        [FromQuery] long recordId,
        [FromQuery] string meterType,
        [FromForm] IFormFile? image)
    {
        if (recordId <= 0) return new JsonResult(new { success = false, message = "recordId 无效" });
        if (image == null || image.Length == 0)
            return new JsonResult(new { success = false, message = "未收到图片文件" });
        if (!new[] { "cold", "hot", "electric" }.Contains(meterType))
            return new JsonResult(new { success = false, message = $"meterType 必须是 cold/hot/electric 之一（收到：{meterType}）" });

        var record = await _db.MeterRecords.FindAsync(recordId);
        if (record == null)
            return new JsonResult(new { success = false, message = $"智能抄表 ID {recordId} 不存在" });

        try
        {
            // v2.13.68：与 PdaController 一致的存储路径 {imageRoot}/{ReadMonth}/{guid}.jpg
            var monthDir = Path.Combine(_imageRoot, record.ReadMonth);
            Directory.CreateDirectory(monthDir);

            // 计算文件哈希（用于去重）
            string hash;
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await image.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }
            using (var sha = SHA256.Create())
            {
                hash = Convert.ToHexString(sha.ComputeHash(fileBytes)).ToLowerInvariant()[..32];
            }

            // 文件名：{DormCode}_{meterType}_{hash}.jpg
            var fileName = $"{record.DormCode}_{meterType}_{hash}.jpg";
            var fullPath = Path.Combine(monthDir, fileName);
            var relativePath = $"{record.ReadMonth}/{fileName}";

            // 已存在则跳过（图片去重）
            if (!System.IO.File.Exists(fullPath))
            {
                await System.IO.File.WriteAllBytesAsync(fullPath, fileBytes);
            }

            // 写 MeterImage 表
            var meterImage = new MeterImage
            {
                RecordId = record.Id,
                MeterType = meterType,
                RelativePath = relativePath,
                AbsolutePath = fullPath,
                FileName = fileName,
                FileSize = (int)image.Length,
                FileHash = hash,
                Width = 0,
                Height = 0,
                UploadedAt = DateTime.Now
            };
            _db.MeterImages.Add(meterImage);
            await _db.SaveChangesAsync();

            return new JsonResult(new {
                success = true,
                imageId = meterImage.Id,
                imageUrl = $"/api/v1/pda/image/{relativePath}",
                message = $"{meterType} 图片上传成功"
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = $"图片上传失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// v2.13.68 异步加载"上月读数"和"已存在记录"信息（AJAX GET 端点，配合 onDormMonthChange 实时刷新）
    /// </summary>
    public async Task<IActionResult> OnGetLoadReadingsAsync([FromQuery] int dormId, [FromQuery] string readMonth)
    {
        if (dormId <= 0 || string.IsNullOrEmpty(readMonth))
            return new JsonResult(new { success = false, message = "参数缺失" });

        // 找上月读数
        var lastMonth = DateTime.ParseExact(readMonth + "-01", "yyyy-MM-dd", null).AddMonths(-1).ToString("yyyy-MM");
        var prev = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormId == dormId && r.ReadMonth == lastMonth);

        // 找当月已有记录
        var current = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormId == dormId && r.ReadMonth == readMonth);

        return new JsonResult(new {
            success = true,
            prevCold = prev?.ColdMeter ?? 0,
            prevHot = prev?.HotMeter ?? 0,
            prevElectric = prev?.ElectricMeter ?? 0,
            hasPrev = prev != null,
            currentStatus = current?.Status ?? -1,
            currentStatusName = current != null ? ((MeterRecordStatus)current.Status).GetDisplayName() : "",
            currentRecordId = current?.Id ?? 0,
            isEffective = current != null && ((MeterRecordStatus)current.Status).IsEffective()
        });
    }

    /// <summary>
    /// 加载宿舍+月份的"上月读数"和"已存在记录"（OnGetAsync 使用）
    /// </summary>
    private async Task LoadReadingsAsync()
    {
        var lastMonth = DateTime.ParseExact(ReadMonth! + "-01", "yyyy-MM-dd", null).AddMonths(-1).ToString("yyyy-MM");
        LastRecord = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormId == DormId && r.ReadMonth == lastMonth);

        if (LastRecord != null)
        {
            LastReadingRef = $"上月（{lastMonth}）：冷水 {LastRecord.ColdMeter:F2} / 热水 {LastRecord.HotMeter:F2} / 电 {LastRecord.ElectricMeter:F2}";
        }

        var current = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormId == DormId && r.ReadMonth == ReadMonth);
        if (current != null)
        {
            ExistingRecordId = current.Id;
            if (((MeterRecordStatus)current.Status).IsEffective())
            {
                ExistWarning = $"⚠️ 该宿舍 {ReadMonth} 已有【{((MeterRecordStatus)current.Status).GetDisplayName()}】记录（ID: {current.Id}），如需修改请走【修正】流程，不可重复补录。";
            }
            else
            {
                ExistWarning = $"ℹ️ 该宿舍 {ReadMonth} 已有【{((MeterRecordStatus)current.Status).GetDisplayName()}】占位记录，提交后将覆盖此记录，旧数据保存到备注历史。";
            }
        }
    }

    // 注意：原 OnPostAsync（同步表单 POST）已废弃并替换为 OnPostSaveReadingsAsync（AJAX）。
    // 为兼容任何直接 POST 场景保留 fallback：
    public async Task<IActionResult> OnPostAsync(
        [FromForm] decimal ColdMeter,
        [FromForm] decimal HotMeter,
        [FromForm] decimal ElectricMeter)
    {
        var req = HttpContext.Request.Form;
        return await OnPostSaveReadingsAsync(
            DormId ?? 0,
            req["ReadMonth"].ToString(),
            ColdMeter, HotMeter, ElectricMeter,
            req["Remark"].ToString());
    }
}

/// <summary>
/// 抄表录入宿舍数据传输对象
/// </summary>
public class MeterEntryDto
{
    public int Id { get; set; }
    public string DormCode { get; set; } = "";
    public string AddressText { get; set; } = "";
}
