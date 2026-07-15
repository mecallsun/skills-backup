using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.EmployeeBilling;

/// <summary>
/// 员工账单详情（P1-15）
/// URL：/EmployeeBilling/Details?employeeId=xxx&month=yyyy-MM
/// </summary>
public class DetailsModel : PageModel
{
    private readonly DormDbContext _db;

    public DetailsModel(DormDbContext db) { _db = db; }

    public SysEmployee? Employee { get; set; }
    public string? Month { get; set; }
    public List<DormBooking> Bookings { get; set; } = new();
    public List<MeterConsumption> MonthlyConsumption { get; set; } = new();
    public BillingSummary Summary { get; set; } = new();
    public List<EmployeeBillingItem> BillingItems { get; set; } = new();

    public class BillingSummary
    {
        public decimal TotalAmount { get; set; }
        public int StayDays { get; set; }
        public decimal WaterAmount { get; set; }
        public decimal ElectricAmount { get; set; }
        public decimal RentAmount { get; set; }
    }

    public class MeterConsumption
    {
        public string Month { get; set; } = "";
        public decimal Cold { get; set; }
        public decimal Hot { get; set; }
        public decimal Electric { get; set; }
    }

    public class EmployeeBillingItem
    {
        public string Date { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? employeeId, string? month)
    {
        if (employeeId is null)
        {
            TempData["ErrorMessage"] = "缺少员工 ID 参数";
            return RedirectToPage("/EmployeeBilling/Index");
        }

        Employee = await _db.Employees.FindAsync(employeeId.Value);
        if (Employee is null)
        {
            TempData["ErrorMessage"] = $"员工 #{employeeId} 不存在";
            return RedirectToPage("/EmployeeBilling/Index");
        }

        Month = string.IsNullOrWhiteSpace(month) ? DateTime.Now.ToString("yyyy-MM") : month;

        // 当月所有相关记录
        var monthStart = DateTime.Parse(Month + "-01");
        var monthEnd = monthStart.AddMonths(1);

        Bookings = await _db.DormBookings
            .Where(b => b.EmployeeId == employeeId.Value)
            .OrderBy(b => b.BookingDate)
            .ToListAsync();

        var monthBookings = Bookings
            .Where(b => b.BookingDate.ToDateTime(new TimeOnly(0, 0)) >= monthStart && b.BookingDate.ToDateTime(new TimeOnly(0, 0)) < monthEnd)
            .ToList();

        // 住宿天数（简化：按 CheckIn 到 CheckOut 区间计算）
        foreach (var b in monthBookings.Where(b => b.Type == BookingType.CheckIn))
        {
            var end = monthBookings.FirstOrDefault(x => x.Type == BookingType.CheckOut && x.BookingDate >= b.BookingDate);
            var endDate = end != null ? end.BookingDate : DateOnly.FromDateTime(monthEnd.AddDays(-1));
            var days = endDate.DayNumber - b.BookingDate.DayNumber + 1;
            Summary.StayDays += Math.Max(1, days);
        }

        Summary.RentAmount = Summary.StayDays * 10m; // 10 元/天

        // 用量（按当前宿舍）
        if (!string.IsNullOrEmpty(Employee.DormCode))
        {
            var currentMeter = await _db.MeterRecords
                .FirstOrDefaultAsync(r => r.DormCode == Employee.DormCode && r.ReadMonth == Month);
            var prevMonth = monthStart.AddMonths(-1).ToString("yyyy-MM");
            var prevMeter = await _db.MeterRecords
                .FirstOrDefaultAsync(r => r.DormCode == Employee.DormCode && r.ReadMonth == prevMonth);

            if (currentMeter is not null && prevMeter is not null)
            {
                // 平摊到当月在宿人数
                var stayCount = Math.Max(1, await _db.DormBookings
                    .CountAsync(b => b.DormCode == Employee.DormCode && b.Status == BookingStatus.Staying));
                Summary.WaterAmount = ((currentMeter.ColdMeter - prevMeter.ColdMeter) +
                                       (currentMeter.HotMeter - prevMeter.HotMeter)) / stayCount * 4m;
                Summary.ElectricAmount = (currentMeter.ElectricMeter - prevMeter.ElectricMeter) / stayCount * 0.6m;
            }

            // 最近 6 个月用量
            var sixMonthsAgo = monthStart.AddMonths(-5);
            for (var i = 0; i < 6; i++)
            {
                var m = sixMonthsAgo.AddMonths(i).ToString("yyyy-MM");
                var record = await _db.MeterRecords.FirstOrDefaultAsync(r => r.DormCode == Employee.DormCode && r.ReadMonth == m);
                if (record is not null)
                {
                    var prev = await _db.MeterRecords.FirstOrDefaultAsync(r => r.DormCode == Employee.DormCode && r.ReadMonth == DateTime.Parse(m + "-01").AddMonths(-1).ToString("yyyy-MM"));
                    MonthlyConsumption.Add(new MeterConsumption
                    {
                        Month = m,
                        Cold = record.ColdMeter - (prev?.ColdMeter ?? 0),
                        Hot = record.HotMeter - (prev?.HotMeter ?? 0),
                        Electric = record.ElectricMeter - (prev?.ElectricMeter ?? 0)
                    });
                }
            }
        }

        Summary.TotalAmount = Summary.RentAmount + Summary.WaterAmount + Summary.ElectricAmount;

        // 账单明细
        BillingItems = new List<EmployeeBillingItem>
        {
            new() { Date = Month, Description = $"住宿费（{Summary.StayDays} 天 × 10 元/天）", Amount = Summary.RentAmount },
            new() { Date = Month, Description = "水费分摊", Amount = Summary.WaterAmount },
            new() { Date = Month, Description = "电费分摊", Amount = Summary.ElectricAmount }
        };

        return Page();
    }
}