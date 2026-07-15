using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.DormBilling;

/// <summary>
/// 宿舍账单详情（P1-15）
/// URL：/DormBilling/Details?dormCode=xxx&month=yyyy-MM
/// </summary>
public class DetailsModel : PageModel
{
    private readonly DormDbContext _db;

    public DetailsModel(DormDbContext db) { _db = db; }

    public string? DormCode { get; set; }
    public string? Month { get; set; }
    public Dorm? Dorm { get; set; }
    public List<MeterRecord> MeterRecords { get; set; } = new();
    public List<DormBooking> Bookings { get; set; } = new();
    public BillingSummary Summary { get; set; } = new();
    public List<MeterConsumption> MonthlyConsumption { get; set; } = new();

    public class BillingSummary
    {
        public decimal ColdTotal { get; set; }
        public decimal HotTotal { get; set; }
        public decimal ElectricTotal { get; set; }
        public decimal RentAmount { get; set; }
        public decimal WaterAmount { get; set; }
        public decimal ElectricAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class MeterConsumption
    {
        public string Month { get; set; } = "";
        public decimal Cold { get; set; }
        public decimal Hot { get; set; }
        public decimal Electric { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? dormCode, string? month)
    {
        if (string.IsNullOrWhiteSpace(dormCode))
        {
            TempData["ErrorMessage"] = "缺少宿舍号参数";
            return RedirectToPage("/DormBilling/Index");
        }

        DormCode = dormCode;
        Month = string.IsNullOrWhiteSpace(month) ? DateTime.Now.ToString("yyyy-MM") : month;

        Dorm = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == dormCode);
        if (Dorm is null)
        {
            TempData["ErrorMessage"] = $"宿舍 {dormCode} 不存在";
            return RedirectToPage("/DormBilling/Index");
        }

        // 当月抄表
        var currentMeter = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormCode == dormCode && r.ReadMonth == Month);
        // 上月抄表（用于计算用量）
        var prevMonth = DateTime.Parse(Month + "-01").AddMonths(-1).ToString("yyyy-MM");
        var prevMeter = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormCode == dormCode && r.ReadMonth == prevMonth);

        if (currentMeter is not null)
        {
            MeterRecords.Add(currentMeter);
            Summary.ColdTotal = currentMeter.ColdMeter - (prevMeter?.ColdMeter ?? 0);
            Summary.HotTotal = currentMeter.HotMeter - (prevMeter?.HotMeter ?? 0);
            Summary.ElectricTotal = currentMeter.ElectricMeter - (prevMeter?.ElectricMeter ?? 0);
            Summary.WaterAmount = (Summary.ColdTotal + Summary.HotTotal) * 4m; // 4 元/吨
            Summary.ElectricAmount = Summary.ElectricTotal * 0.6m; // 0.6 元/度
        }

        // 当月住宿记录
        Bookings = await _db.DormBookings
            .Where(b => b.DormCode == dormCode && b.BookingDate.ToString().StartsWith(Month))
            .OrderBy(b => b.BookingDate)
            .ToListAsync();

        // 简化的房租计算（每人每月 200 元）
        Summary.RentAmount = Bookings.Count(b => b.Type == BookingType.CheckIn && b.Status != BookingStatus.CheckedOut) * 200m;
        Summary.TotalAmount = Summary.RentAmount + Summary.WaterAmount + Summary.ElectricAmount;

        // 最近 6 个月用量趋势
        var sixMonthsAgo = DateTime.Parse(Month + "-01").AddMonths(-5);
        for (var i = 0; i < 6; i++)
        {
            var m = sixMonthsAgo.AddMonths(i).ToString("yyyy-MM");
            var record = await _db.MeterRecords.FirstOrDefaultAsync(r => r.DormCode == dormCode && r.ReadMonth == m);
            if (record is not null)
            {
                var prev = await _db.MeterRecords.FirstOrDefaultAsync(r => r.DormCode == dormCode && r.ReadMonth == DateTime.Parse(m + "-01").AddMonths(-1).ToString("yyyy-MM"));
                MonthlyConsumption.Add(new MeterConsumption
                {
                    Month = m,
                    Cold = record.ColdMeter - (prev?.ColdMeter ?? 0),
                    Hot = record.HotMeter - (prev?.HotMeter ?? 0),
                    Electric = record.ElectricMeter - (prev?.ElectricMeter ?? 0)
                });
            }
        }

        return Page();
    }
}