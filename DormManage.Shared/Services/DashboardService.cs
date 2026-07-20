using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 首页数据看板聚合服务（M1 模块 — 按 04-HTML原型/index.html 1:1 实施）
///
/// v2.13.30 CRITICAL FIX：所有图表数据 100% 来自真实数据库（DormDbContext）
/// - 删除 v2.13.29 之前的演示数据回退（MockDormBill / MockEmployeeBill 随机生成）
/// - 数据源统一：KPI 1-5 查 Employees/Dorms/DormBookings/MeterRecords
/// - KPI 6-7 + 图表 2/3/6 查 DormBillings / EmployeeBillings 真实账单表
/// - 图表 1/4/5/7/8 查 DormBookings/Dorms/Employees/EmployeeTypes 真实表
///
/// 严格按需求规格：
/// - 01-首页数据看板需求-v2.11.3.md（v2.11.6 KPI 数据逻辑修复）
/// - 14-首页数据看板需求-v2.11.2增补.md（v2.11.4 新增 KPI 6 人均费用）
/// - 19-数据看板月度选择-v2.11.2.md
/// - 21-入住率最低TOP10-v2.11.2.md（v2.11.5 TOP 20→TOP 15 + 条形图 UI）
///
/// 一次性查询多张表，输出 DashboardDto：
/// - 7 个 KPI（入住人数 / 宿舍入住率 / 预约人员 / 异常人员 / 本月抄表覆盖 / 人均费用 / 本月费用合计）
/// - 8 个图表（入住退房对比 / 费用变化曲线 / 费用TOP10 / 入住率TOP15 / 部门分布 / 费用类型占比 / 员工类型分布 / 抄表覆盖）
/// </summary>
public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(DateTime? month = null);
}

public class DashboardService : IDashboardService
{
    private readonly DormDbContext _db;

    public DashboardService(DormDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync(DateTime? month = null)
    {
        var refMonth = month ?? DateTime.Now;
        var monthStr = refMonth.ToString("yyyy-MM");
        var monthStart = new DateTime(refMonth.Year, refMonth.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var dto = new DashboardDto
        {
            ReferenceMonth = refMonth,
            CurrentMonth = monthStr,
            Kpi = await BuildKpiAsync(monthStart, monthEnd, today, monthStr),
            CheckInOutMonthly = await BuildCheckInOutMonthlyAsync(monthStart),
            CostTrendMonthly = await BuildCostTrendMonthlyAsync(monthStart),
            DormCostTop10 = await BuildDormCostTop10Async(monthStr),
            OccupancyRankTop15 = await BuildOccupancyRankTop15Async(),
            DepartmentDistribution = await BuildDepartmentDistributionAsync(),
            CostTypeRatio = await BuildCostTypeRatioAsync(monthStr),
            EmployeeTypeDistribution = await BuildEmployeeTypeDistributionAsync(),
            MeterCoverage = await BuildMeterCoverageAsync(monthStr)
        };

        return dto;
    }

    #region KPI 7 项（v2.13.30 全部使用真实数据源）

    /// <summary>
    /// KPI 计算（v2.11.6 数据逻辑修复 + v2.13.30 数据源统一）
    /// </summary>
    private async Task<DashboardKpi> BuildKpiAsync(DateTime monthStart, DateTime monthEnd, DateOnly today, string monthStr)
    {
        // === KPI 1: 入住人数（v2.13.30 修复：DormBooking.Status=Staying 关联 + 兜底 DormCode）===
        // 双重统计：以 DormBookings 在宿记录为权威，DormCode 为兜底
        var residentFromBooking = await _db.DormBookings
            .Where(b => b.Status == BookingStatus.Staying)
            .Select(b => b.EmployeeId)
            .Distinct()
            .CountAsync();
        var residentFromDormCode = await _db.Employees
            .CountAsync(e => e.DormCode != null && e.DormCode != "");
        // 取较大值（数据可能尚未完全同步）
        var residentCount = Math.Max(residentFromBooking, residentFromDormCode);

        var totalPersonnel = await _db.Employees.CountAsync();

        // === KPI 2: 宿舍入住率（入住人数 / 总容量 × 100%）===
        var totalBeds = await _db.Dorms.SumAsync(d => (int?)d.Capacity) ?? 0;
        var occupancyRate = totalBeds > 0 ? Math.Round((decimal)residentCount / totalBeds * 100, 0) : 0;

        // === KPI 3: 预约人员（BOOKINGS.status=1/Reserved）===
        var bookingCount = await _db.DormBookings
            .CountAsync(b => b.Status == BookingStatus.Reserved);

        // === KPI 4: 异常人员 A+B+C ===
        // A: 已离职但仍入住（EmploymentStatusId=3 && DormCode 非空）
        var abnormalA = await _db.Employees
            .CountAsync(e => e.EmploymentStatusId == EmployeeStatus.Left && e.DormCode != null && e.DormCode != "");
        // B: 未到入职日期提前入住（EmploymentStatusId=1 && DormCode 非空 && HireDate > today）
        var abnormalB = await _db.Employees
            .CountAsync(e => e.EmploymentStatusId == EmployeeStatus.Active
                          && e.DormCode != null && e.DormCode != ""
                          && e.HireDate.HasValue && e.HireDate.Value > today);
        // C: 超期未办（BOOKINGS.status=Reserved && BookingDate < today）
        var abnormalC = await _db.DormBookings
            .CountAsync(b => b.Status == BookingStatus.Reserved && b.BookingDate < today);

        // === KPI 5: 本月抄表覆盖 ===
        var meterReadCount = await _db.MeterRecords
            .Where(r => r.ReadMonth == monthStr && r.Status != (byte)MeterRecordStatus.Voided)
            .Select(r => r.DormCode)
            .Distinct()
            .CountAsync();
        var meterTotalDorms = await _db.Dorms.CountAsync();
        var readDormCodes = await _db.MeterRecords
            .Where(r => r.ReadMonth == monthStr && r.Status != (byte)MeterRecordStatus.Voided)
            .Select(r => r.DormCode)
            .Distinct()
            .ToListAsync();
        var unreadDormCodes = await _db.Dorms
            .Where(d => !readDormCodes.Contains(d.DormCode))
            .OrderBy(d => d.DormCode)
            .Take(5)
            .Select(d => d.DormCode)
            .ToListAsync();

        // === KPI 6: 人均费用（v2.13.30 修复：真实 EmployeeBilling 表，无数据返回 0）===
        var employeeBills = await _db.EmployeeBillings
            .Where(b => b.BillingMonth == monthStr)
            .ToListAsync();
        var avgFee = employeeBills.Count > 0
            ? Math.Round(employeeBills.Average(b => b.TotalShareAmount), 2)
            : 0;

        // === KPI 7: 本月费用合计（v2.13.30 修复：真实 DormBilling 表）===
        var dormBills = await _db.DormBillings
            .Where(b => b.BillingMonth == monthStr)
            .ToListAsync();
        var totalFee = dormBills.Sum(b => b.TotalAmount);
        var feeRooms = dormBills.Count;

        return new DashboardKpi
        {
            ResidentCount = residentCount,
            TotalPersonnelCount = totalPersonnel,
            OccupancyRate = occupancyRate,
            OccupiedBeds = residentCount,
            TotalBeds = totalBeds,
            BookingCount = bookingCount,
            AbnormalCount = abnormalA + abnormalB + abnormalC,
            AbnormalA = abnormalA,
            AbnormalB = abnormalB,
            AbnormalC = abnormalC,
            MeterReadCount = meterReadCount,
            MeterTotalCount = meterTotalDorms,
            UnreadDormCodes = unreadDormCodes,
            AvgFee = avgFee,
            TotalFee = totalFee,
            FeeRooms = feeRooms
        };
    }

    #endregion

    #region 图表 8 项（v2.13.30 全部使用真实数据源）

    /// <summary>
    /// 图表 1：入住/退房人数对比（近 12 月）— 真实 DormBookings 表
    /// </summary>
    private async Task<List<MonthlyCheckInOutDto>> BuildCheckInOutMonthlyAsync(DateTime refMonthStart)
    {
        var result = new List<MonthlyCheckInOutDto>();
        for (int i = 11; i >= 0; i--)
        {
            var m = refMonthStart.AddMonths(-i);
            var mStart = new DateTime(m.Year, m.Month, 1);
            var mEnd = mStart.AddMonths(1);
            var checkIn = await _db.DormBookings
                .CountAsync(b => b.BookingDate >= DateOnly.FromDateTime(mStart)
                              && b.BookingDate < DateOnly.FromDateTime(mEnd)
                              && b.Type == BookingType.CheckIn);
            var checkOut = await _db.DormBookings
                .CountAsync(b => b.BookingDate >= DateOnly.FromDateTime(mStart)
                              && b.BookingDate < DateOnly.FromDateTime(mEnd)
                              && b.Type == BookingType.CheckOut);
            result.Add(new MonthlyCheckInOutDto
            {
                Month = m.ToString("yyyy-MM"),
                CheckIn = checkIn,
                CheckOut = checkOut
            });
        }
        return result;
    }

    /// <summary>
    /// 图表 2：每月总费用变化曲线（近 12 月 + 上年同月对比）— 真实 DormBilling 表
    /// </summary>
    private async Task<List<MonthlyCostTrendDto>> BuildCostTrendMonthlyAsync(DateTime refMonthStart)
    {
        var result = new List<MonthlyCostTrendDto>();
        for (int i = 11; i >= 0; i--)
        {
            var m = refMonthStart.AddMonths(-i);
            var mStr = m.ToString("yyyy-MM");
            var currentBills = await _db.DormBillings
                .Where(b => b.BillingMonth == mStr)
                .ToListAsync();
            var lastYearStr = m.AddYears(-1).ToString("yyyy-MM");
            var lastYearBills = await _db.DormBillings
                .Where(b => b.BillingMonth == lastYearStr)
                .ToListAsync();
            result.Add(new MonthlyCostTrendDto
            {
                Month = mStr,
                CurrentYear = currentBills.Sum(b => b.TotalAmount),
                LastYear = lastYearBills.Sum(b => b.TotalAmount)
            });
        }
        return result;
    }

    /// <summary>
    /// 图表 3：宿舍费用排名 TOP 10 — 真实 DormBilling 表（按 TotalAmount 降序）
    /// </summary>
    private async Task<List<DormCostRankDto>> BuildDormCostTop10Async(string monthStr)
    {
        var bills = await _db.DormBillings
            .Where(b => b.BillingMonth == monthStr)
            .GroupBy(b => b.DormCode)
            .Select(g => new DormCostRankDto
            {
                DormCode = g.Key,
                ColdAmount = g.Sum(b => b.ColdAmount),
                HotAmount = g.Sum(b => b.HotAmount),
                ElectricityAmount = g.Sum(b => b.ElectricityAmount)
            })
            .OrderByDescending(d => d.ColdAmount + d.HotAmount + d.ElectricityAmount)
            .Take(10)
            .ToListAsync();
        return bills;
    }

    /// <summary>
    /// 图表 4：入住率排名 TOP 15 — 真实 Dorms + DormBookings 表
    /// </summary>
    private async Task<List<DormOccupancyRankDto>> BuildOccupancyRankTop15Async()
    {
        var dorms = await _db.Dorms
            .Where(d => d.Capacity > 0)
            .Select(d => new
            {
                d.DormCode,
                d.Capacity,
                CurrentResidents = _db.DormBookings
                    .Count(b => b.DormCode == d.DormCode && b.Status == BookingStatus.Staying)
            })
            .ToListAsync();

        return dorms
            .Select(d => new DormOccupancyRankDto
            {
                DormCode = d.DormCode,
                Capacity = d.Capacity,
                CurrentResidents = d.CurrentResidents,
                OccupancyRate = d.Capacity > 0
                    ? Math.Round((decimal)d.CurrentResidents / d.Capacity * 100, 0)
                    : 0
            })
            .OrderBy(d => d.OccupancyRate)
            .ThenBy(d => d.DormCode)
            .Take(15)
            .ToList();
    }

    /// <summary>
    /// 图表 5：部门分布 — 真实 Employees 表
    /// </summary>
    private async Task<List<DistributionItem>> BuildDepartmentDistributionAsync()
    {
        var groups = await _db.Employees
            .Where(e => !string.IsNullOrEmpty(e.Department))
            .GroupBy(e => e.Department)
            .Select(g => new { Name = g.Key!, Count = g.Count() })
            .ToListAsync();
        return groups
            .Select(x => new DistributionItem { Label = x.Name, Value = x.Count })
            .OrderByDescending(d => d.Value)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// 图表 6：费用类型占比 — 真实 DormBilling 表
    /// </summary>
    private async Task<List<DistributionItem>> BuildCostTypeRatioAsync(string monthStr)
    {
        var bills = await _db.DormBillings
            .Where(b => b.BillingMonth == monthStr)
            .ToListAsync();
        return new List<DistributionItem>
        {
            new() { Label = "电费", Value = (int)bills.Sum(b => b.ElectricityAmount) },
            new() { Label = "热水费", Value = (int)bills.Sum(b => b.HotAmount) },
            new() { Label = "冷水费", Value = (int)bills.Sum(b => b.ColdAmount) }
        };
    }

    /// <summary>
    /// 图表 7：员工类型分布 — 真实 Employees + EmployeeTypes 表
    /// </summary>
    private async Task<List<DistributionItem>> BuildEmployeeTypeDistributionAsync()
    {
        var groups = await _db.Employees
            .Where(e => e.EmployeeTypeId > 0)
            .GroupBy(e => e.EmployeeTypeId)
            .Select(g => new
            {
                TypeId = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var types = await _db.EmployeeTypes.ToDictionaryAsync(et => et.Id, et => et.Name);
        return groups
            .Select(x => new DistributionItem
            {
                Label = types.TryGetValue(x.TypeId, out var n) ? n : "未知",
                Value = x.Count
            })
            .OrderByDescending(d => d.Value)
            .ToList();
    }

    /// <summary>
    /// 图表 8：本月抄表覆盖（环形图：已抄 vs 未抄）— 真实 MeterRecords 表
    /// </summary>
    private async Task<List<DistributionItem>> BuildMeterCoverageAsync(string monthStr)
    {
        var total = await _db.Dorms.CountAsync();
        var read = await _db.MeterRecords
            .Where(r => r.ReadMonth == monthStr && r.Status != (byte)MeterRecordStatus.Voided)
            .Select(r => r.DormCode)
            .Distinct()
            .CountAsync();
        return new List<DistributionItem>
        {
            new() { Label = "已抄", Value = read },
            new() { Label = "未抄", Value = Math.Max(0, total - read) }
        };
    }

    #endregion
}

#region DTO

/// <summary>
/// 首页数据看板完整 DTO（按 HTML 原型 1:1）
/// </summary>
public class DashboardDto
{
    public DateTime ReferenceMonth { get; set; }
    public string CurrentMonth { get; set; } = "";
    public DashboardKpi Kpi { get; set; } = new();

    /// <summary>1. 入住/退房人数对比（近 12 月）</summary>
    public List<MonthlyCheckInOutDto> CheckInOutMonthly { get; set; } = new();

    /// <summary>2. 每月总费用变化曲线（近 12 月 + 上年对比）</summary>
    public List<MonthlyCostTrendDto> CostTrendMonthly { get; set; } = new();

    /// <summary>3. 宿舍费用排名 TOP 10（水平堆叠柱状图）</summary>
    public List<DormCostRankDto> DormCostTop10 { get; set; } = new();

    /// <summary>4. 入住率排名 TOP 15（水平柱状图）</summary>
    public List<DormOccupancyRankDto> OccupancyRankTop15 { get; set; } = new();

    /// <summary>5. 部门分布</summary>
    public List<DistributionItem> DepartmentDistribution { get; set; } = new();

    /// <summary>6. 费用类型占比</summary>
    public List<DistributionItem> CostTypeRatio { get; set; } = new();

    /// <summary>7. 员工类型分布</summary>
    public List<DistributionItem> EmployeeTypeDistribution { get; set; } = new();

    /// <summary>8. 抄表覆盖</summary>
    public List<DistributionItem> MeterCoverage { get; set; } = new();
}

/// <summary>7 项 KPI</summary>
public class DashboardKpi
{
    /// <summary>入住人数（v2.13.30：DormBooking.Status=Staying ∪ DormCode 非空）</summary>
    public int ResidentCount { get; set; }

    /// <summary>人员总数</summary>
    public int TotalPersonnelCount { get; set; }

    /// <summary>宿舍入住率（百分比）</summary>
    public decimal OccupancyRate { get; set; }

    /// <summary>已入住床位数</summary>
    public int OccupiedBeds { get; set; }

    /// <summary>总床位数</summary>
    public int TotalBeds { get; set; }

    /// <summary>预约人员数（BOOKINGS.status=1）</summary>
    public int BookingCount { get; set; }

    /// <summary>异常人员总数（A+B+C）</summary>
    public int AbnormalCount { get; set; }

    /// <summary>异常 A：已离职仍入住</summary>
    public int AbnormalA { get; set; }

    /// <summary>异常 B：未到入职日期提前入住</summary>
    public int AbnormalB { get; set; }

    /// <summary>异常 C：超期未办</summary>
    public int AbnormalC { get; set; }

    /// <summary>本月已抄宿舍数</summary>
    public int MeterReadCount { get; set; }

    /// <summary>总宿舍数</summary>
    public int MeterTotalCount { get; set; }

    /// <summary>未抄宿舍代码（前 5 个）</summary>
    public List<string> UnreadDormCodes { get; set; } = new();

    /// <summary>本月人均费用（v2.13.30：真实 EmployeeBilling）</summary>
    public decimal AvgFee { get; set; }

    /// <summary>本月费用合计（v2.13.30：真实 DormBilling）</summary>
    public decimal TotalFee { get; set; }

    /// <summary>本月账单房间数</summary>
    public int FeeRooms { get; set; }
}

/// <summary>1. 入住/退房月度对比</summary>
public class MonthlyCheckInOutDto
{
    public string Month { get; set; } = "";
    public int CheckIn { get; set; }
    public int CheckOut { get; set; }
}

/// <summary>2. 每月总费用变化曲线</summary>
public class MonthlyCostTrendDto
{
    public string Month { get; set; } = "";
    public decimal CurrentYear { get; set; }
    public decimal LastYear { get; set; }
}

/// <summary>3. 宿舍费用排名</summary>
public class DormCostRankDto
{
    public string DormCode { get; set; } = "";
    public decimal ColdAmount { get; set; }
    public decimal HotAmount { get; set; }
    public decimal ElectricityAmount { get; set; }
}

/// <summary>4. 入住率排名</summary>
public class DormOccupancyRankDto
{
    public string DormCode { get; set; } = "";
    public int Capacity { get; set; }
    public int CurrentResidents { get; set; }
    public decimal OccupancyRate { get; set; }
}

/// <summary>分布图通用项（部门/费用类型/员工类型/抄表覆盖）</summary>
public class DistributionItem
{
    public string Label { get; set; } = "";
    public int Value { get; set; }
}

#endregion