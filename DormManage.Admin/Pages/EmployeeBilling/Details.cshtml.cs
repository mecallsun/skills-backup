using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.EmployeeBilling;

/// <summary>
/// 员工账单详情（v2.13.44：拆冷水/热水 + 加分摊比例 + 同住人数）
/// URL：/EmployeeBilling/Details?id=账单ID 或 ?employeeId=xxx&amp;month=yyyy-MM
/// </summary>
public class DetailsModel : PageModel
{
    private readonly DormDbContext _db;

    public DetailsModel(DormDbContext db) { _db = db; }

    public SysEmployee? Employee { get; set; }
    public string? Month { get; set; }
    public DormManage.Shared.Models.EmployeeBilling? Bill { get; set; }
    public List<DormBooking> Bookings { get; set; } = new();
    public List<MeterConsumption> MonthlyConsumption { get; set; } = new();
    public BillingSummary Summary { get; set; } = new();
    public List<EmployeeBillingItem> BillingItems { get; set; } = new();

    public class BillingSummary
    {
        public decimal TotalAmount { get; set; }
        public int StayDays { get; set; }
        public decimal ColdAmount { get; set; }
        public decimal HotAmount { get; set; }
        public decimal ElectricAmount { get; set; }
        public decimal RentAmount { get; set; }
        /// <summary>v2.13.44 新增：分摊比例（如 0.5 表示 50%）</summary>
        public decimal ShareRatio { get; set; }
        /// <summary>v2.13.44 新增：同住人数</summary>
        public int ResidentCount { get; set; }
        /// <summary>v2.13.93 新增：本月实际补贴金额（元）</summary>
        public decimal SubsidyAmount { get; set; }
        /// <summary>v2.13.93 新增：费用合计 = max(TotalShareAmount - SubsidyAmount, 0)</summary>
        public decimal FinalAmount { get; set; }
        /// <summary>v2.13.93 新增：员工类型月补贴标准（元/人·月）</summary>
        public decimal StandardSubsidyAmount { get; set; }
        /// <summary>v2.13.93 新增：当月天数（用于补贴折算）</summary>
        public int MonthDays { get; set; }
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

    public async Task<IActionResult> OnGetAsync(int? id, int? employeeId, string? month)
    {
        // v2.13.44：优先按账单 ID 加载真实 EmployeeBilling
        if (id.HasValue)
        {
            Bill = await _db.EmployeeBillings.FirstOrDefaultAsync(b => b.Id == id.Value);
            if (Bill != null)
            {
                employeeId = Bill.EmployeeId;
                month = Bill.BillingMonth;
            }
        }

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

        var monthStart = DateTime.Parse(Month + "-01");
        var monthEnd = monthStart.AddMonths(1);

        Bookings = await _db.DormBookings
            .Where(b => b.EmployeeId == employeeId.Value)
            .OrderBy(b => b.BookingDate)
            .ToListAsync();

        var monthBookings = Bookings
            .Where(b => b.BookingDate.ToDateTime(new TimeOnly(0, 0)) >= monthStart && b.BookingDate.ToDateTime(new TimeOnly(0, 0)) < monthEnd)
            .ToList();

        foreach (var b in monthBookings.Where(b => b.Type == BookingType.CheckIn))
        {
            var end = monthBookings.FirstOrDefault(x => x.Type == BookingType.CheckOut && x.BookingDate >= b.BookingDate);
            var endDate = end != null ? end.BookingDate : DateOnly.FromDateTime(monthEnd.AddDays(-1));
            var days = endDate.DayNumber - b.BookingDate.DayNumber + 1;
            Summary.StayDays += Math.Max(1, days);
        }

        Summary.RentAmount = Summary.StayDays * 10m;

        // v2.13.44 优先用真实 EmployeeBilling 拆水分（冷水/热水）
        if (Bill != null)
        {
            Summary.ColdAmount = Bill.ColdShareAmount;
            Summary.HotAmount = Bill.HotShareAmount;
            Summary.ElectricAmount = Bill.ElectricityShareAmount;
            Summary.ShareRatio = Bill.ShareRatio;
            Summary.ResidentCount = Bill.ResidentCount;
            Summary.TotalAmount = Bill.TotalShareAmount;
            // v2.13.93 新增：补贴字段填充
            Summary.SubsidyAmount = Bill.SubsidyAmount;
            Summary.FinalAmount = Math.Max(Bill.TotalShareAmount - Bill.SubsidyAmount, 0m);
            Summary.StayDays = Bill.Days;  // 优先用真实账单 Days（v2.13.93 同步 SQL）
            // 查员工类型月补贴标准（用于显示折算依据）
            var monthFirstDay = DateOnly.ParseExact(Month + "-01", "yyyy-MM-dd");
            Summary.MonthDays = DateTime.DaysInMonth(monthFirstDay.Year, monthFirstDay.Month);
            var standard = await _db.BillingStandards.FirstOrDefaultAsync(s =>
                s.IsActive
                && s.EffectiveFrom <= monthFirstDay
                && (s.EffectiveTo == null || s.EffectiveTo >= monthFirstDay)
                && s.ApplicableTypeId == Employee.EmployeeTypeId);
            Summary.StandardSubsidyAmount = standard?.SubsidyAmount ?? 0m;
        }
        else if (!string.IsNullOrEmpty(Employee.DormCode))
        {
            var currentMeter = await _db.MeterRecords
                .FirstOrDefaultAsync(r => r.DormCode == Employee.DormCode && r.ReadMonth == Month);
            var prevMonth = monthStart.AddMonths(-1).ToString("yyyy-MM");
            var prevMeter = await _db.MeterRecords
                .FirstOrDefaultAsync(r => r.DormCode == Employee.DormCode && r.ReadMonth == prevMonth);

            if (currentMeter is not null && prevMeter is not null)
            {
                var stayCount = Math.Max(1, await _db.DormBookings
                    .CountAsync(b => b.DormCode == Employee.DormCode && b.Status == BookingStatus.Staying));
                Summary.ShareRatio = 1m / stayCount;
                Summary.ResidentCount = stayCount;
                // v2.13.44 拆分冷水/热水
                Summary.ColdAmount = (currentMeter.ColdMeter - prevMeter.ColdMeter) / stayCount * 4m;
                Summary.HotAmount = (currentMeter.HotMeter - prevMeter.HotMeter) / stayCount * 4m;
                Summary.ElectricAmount = (currentMeter.ElectricMeter - prevMeter.ElectricMeter) / stayCount * 0.6m;
            }

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

        if (Bill == null)
            Summary.TotalAmount = Summary.RentAmount + Summary.ColdAmount + Summary.HotAmount + Summary.ElectricAmount;

        BillingItems = new List<EmployeeBillingItem>
        {
            new() { Date = Month, Description = $"住宿费（{Summary.StayDays} 天 × 10 元/天）", Amount = Summary.RentAmount },
            new() { Date = Month, Description = "冷水费分摊", Amount = Summary.ColdAmount },
            new() { Date = Month, Description = "热水费分摊", Amount = Summary.HotAmount },
            new() { Date = Month, Description = "电费分摊", Amount = Summary.ElectricAmount }
        };

        // v2.13.93 新增：补贴扣减行（如有补贴才显示）
        if (Summary.SubsidyAmount > 0)
        {
            BillingItems.Add(new EmployeeBillingItem
            {
                Date = Month,
                Description = $"本月补贴扣减（{Summary.StayDays}/{Summary.MonthDays} × {Summary.StandardSubsidyAmount:F2}元）",
                Amount = -Summary.SubsidyAmount
            });
        }

        return Page();
    }
}