using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 手动补录抄表页面模型
/// </summary>
public class EntryModel : PageModel
{
    private readonly DormDbContext _db;

    public EntryModel(DormDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int? DormId { get; set; }

    [BindProperty]
    public string? ReadMonth { get; set; }

    [BindProperty]
    public decimal ColdMeter { get; set; }

    [BindProperty]
    public decimal HotMeter { get; set; }

    [BindProperty]
    public decimal ElectricMeter { get; set; }

    [BindProperty]
    public string? Remark { get; set; }

    /// <summary>
    /// 宿舍列表（用于下拉选择）
    /// </summary>
    public List<MeterEntryDto> Dorms { get; set; } = new();

    /// <summary>
    /// 上月抄表记录参考
    /// </summary>
    public MeterRecord? LastRecord { get; set; }

    /// <summary>
    /// 已存在记录警告
    /// </summary>
    public string? ExistWarning { get; set; }

    /// <summary>
    /// 上月读数参考文本
    /// </summary>
    public string? LastReadingRef { get; set; }

    public async Task OnGetAsync()
    {
        // 默认月份为当前月
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

        // 如果有宿舍和月份，加载上月读数参考
        if (DormId.HasValue && !string.IsNullOrEmpty(ReadMonth))
        {
            await LoadLastReadingAsync();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(ReadMonth))
        {
            ModelState.AddModelError("ReadMonth", "请选择抄表月份");
            return Page();
        }

        if (ColdMeter < 0 || HotMeter < 0 || ElectricMeter < 0)
        {
            ModelState.AddModelError("", "表读数不能为负数");
            return Page();
        }

        try
        {
            // 检查是否已存在同月同房号的记录
            var existing = await _db.MeterRecords
                .FirstOrDefaultAsync(r =>
                    r.DormId == DormId &&
                    r.ReadMonth == ReadMonth);

            if (existing != null && existing.Status == 1)
            {
                ExistWarning = $"该宿舍该月份已存在正常记录（ID: {existing.Id}），覆盖后将保留历史快照";
            }

            if (existing != null)
            {
                // 覆盖模式：更新现有记录
                existing.ColdMeter = ColdMeter;
                existing.HotMeter = HotMeter;
                existing.ElectricMeter = ElectricMeter;
                existing.Operator = "admin（后台补录）";
                existing.Remark = Remark;
                existing.ServerCreatedAt = DateTime.Now;
                existing.Status = MeterRecord.DetermineStatus(ColdMeter, HotMeter, ElectricMeter);
            }
            else
            {
                // 新建记录
                var dorm = await _db.Dorms.FindAsync(DormId);
                if (dorm == null)
                {
                    ModelState.AddModelError("DormId", "请选择有效的宿舍");
                    return Page();
                }

                var newRecord = new MeterRecord
                {
                    DormId = DormId.Value,
                    DormCode = dorm.DormCode,
                    ReadMonth = ReadMonth!,
                    ColdMeter = ColdMeter,
                    HotMeter = HotMeter,
                    ElectricMeter = ElectricMeter,
                    Operator = "admin（后台补录）",
                    Remark = Remark,
                    ServerCreatedAt = DateTime.Now,
                    Status = MeterRecord.DetermineStatus(ColdMeter, HotMeter, ElectricMeter)
                };

                _db.MeterRecords.Add(newRecord);
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "抄表记录保存成功";
            return RedirectToPage("/Meter/Index");
        }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError("", $"保存失败：{ex.Message}");
            return Page();
        }
    }

    private async Task LoadLastReadingAsync()
    {
        var lastMonth = DateTime.Parse(ReadMonth!).AddMonths(-1).ToString("yyyy-MM");
        LastRecord = await _db.MeterRecords
            .FirstOrDefaultAsync(r =>
                r.DormId == DormId &&
                r.ReadMonth == lastMonth);

        if (LastRecord != null)
        {
            LastReadingRef = $"上月（{lastMonth}）：冷水 {LastRecord.ColdMeter:F2} / 热水 {LastRecord.HotMeter:F2} / 电 {LastRecord.ElectricMeter:F2}";
        }
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
