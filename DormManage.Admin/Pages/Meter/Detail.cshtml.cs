using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Meter;

public class DetailModel : PageModel
{
    private readonly DormDbContext _db;

    public DetailModel(DormDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public MeterRecordDetailDto Record { get; set; } = new();
    public UsageInfo Usage { get; set; } = new();
    public ComparisonInfo? Comparison { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var record = await _db.MeterRecords.FindAsync(Id);
        if (record == null)
        {
            TempData["ErrorMessage"] = "抄表记录不存在";
            return RedirectToPage("/Meter/Index");
        }

        Record = new MeterRecordDetailDto
        {
            Id = record.Id,
            DormCode = record.DormCode,
            ReadMonth = record.ReadMonth,
            ColdMeter = record.ColdMeter,
            HotMeter = record.HotMeter,
            ElectricMeter = record.ElectricMeter,
            Operator = record.Operator,
            Status = record.Status,
            StatusName = record.GetStatusName(),
            Remark = record.Remark
        };

        // 计算用量 — 取该宿舍所有正常记录，在内存中找上月
        var allRecords = await _db.MeterRecords
            .Where(r => r.DormCode == record.DormCode && r.Status == 1)
            .OrderByDescending(r => r.ReadMonth)
            .ToListAsync();
        var prevRecord = allRecords.Skip(1).FirstOrDefault();

        Usage.ColdUsage = Math.Max(0, record.ColdMeter - (prevRecord?.ColdMeter ?? 0));
        Usage.HotUsage = Math.Max(0, record.HotMeter - (prevRecord?.HotMeter ?? 0));
        Usage.ElectricUsage = Math.Max(0, record.ElectricMeter - (prevRecord?.ElectricMeter ?? 0));

        if (prevRecord != null)
        {
            Comparison = new ComparisonInfo
            {
                PreviousCold = prevRecord.ColdMeter,
                PreviousHot = prevRecord.HotMeter,
                PreviousElectric = prevRecord.ElectricMeter,
                CurrentCold = record.ColdMeter,
                CurrentHot = record.HotMeter,
                CurrentElectric = record.ElectricMeter
            };
        }

        return Page();
    }
}

public class MeterRecordDetailDto
{
    public long Id { get; set; }
    public string DormCode { get; set; } = "";
    public string ReadMonth { get; set; } = "";
    public decimal ColdMeter { get; set; }
    public decimal HotMeter { get; set; }
    public decimal ElectricMeter { get; set; }
    public string Operator { get; set; } = "";
    public byte Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Remark { get; set; }
}

public class UsageInfo
{
    public decimal ColdUsage { get; set; }
    public decimal HotUsage { get; set; }
    public decimal ElectricUsage { get; set; }
}

public class ComparisonInfo
{
    public decimal PreviousCold { get; set; }
    public decimal PreviousHot { get; set; }
    public decimal PreviousElectric { get; set; }
    public decimal CurrentCold { get; set; }
    public decimal CurrentHot { get; set; }
    public decimal CurrentElectric { get; set; }
}
