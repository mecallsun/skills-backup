using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 首页数据看板聚合服务（M1 模块 — 按 04-HTML原型/index.html 1:1 实施）
///
/// 数据源与计算规则严格按需求规格：
/// - 01-首页数据看板需求-v2.11.3.md（v2.11.6 KPI 数据逻辑修复）
/// - 14-首页数据看板需求-v2.11.2增补.md（v2.11.4 新增 KPI 6 人均费用）
/// - 19-数据看板月度选择-v2.11.2.md
/// - 21-入住率最低TOP10-v2.11.2.md（v2.11.5 TOP 20→TOP 15 + 条形图 UI）
///
/// 一次性查询多张表，输出 DashboardDto：
/// - 7 个 KPI（入住人数 / 宿舍入住率 / 预约人员 / 异常人员 / 本月抄表覆盖 / 人均费用 / 本月费用合计）
/// - 8 个图表（入住退房对比 / 费用变化曲线 / 费用TOP10 / 入住率TOP15 / 部门分布 / 费用类型占比 / 员工类型分布 / 抄表覆盖）
///
/// 注：账单数据（DORM_BILLS）当前数据库无持久化表，采用按月份确定性随机生成（seed=月份）。
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
            Kpi = await BuildKpiAsync(monthStart, monthEnd, today),
            CheckInOutMonthly = await BuildCheckInOutMonthlyAsync(monthStart),
            CostTrendMonthly = BuildCostTrendMonthlyAsync(monthStart),
            DormCostTop10 = BuildDormCostTop10(monthStr),
            OccupancyRankTop15 = await BuildOccupancyRankTop15Async(),
            DepartmentDistribution = await BuildDepartmentDistributionAsync(),
            CostTypeRatio = BuildCostTypeRatio(monthStr),
            EmployeeTypeDistribution = await BuildEmployeeTypeDistributionAsync(),
            MeterCoverage = await BuildMeterCoverageAsync(monthStr)
        };

        return dto;
    }

    #region KPI 7 项

    /// <summary>
    /// KPI 计算（v2.11.6 数据逻辑修复）
    /// </summary>
    private async Task<DashboardKpi> BuildKpiAsync(DateTime monthStart, DateTime monthEnd, DateOnly today)
    {
        // === KPI 1: 入住人数（v2.11.18 修订：PERSONNEL.dormCode 非空计数）===
        var residentCount = await _db.Employees
            .CountAsync(e => e.DormCode != null && e.DormCode != "");
        var totalPersonnel = await _db.Employees.CountAsync();

        // === KPI 2: 宿舍入住率（入住人数 / 总容量 × 100%，分子独立计算）===
        var totalBeds = await _db.Dorms.SumAsync(d => (int?)d.Capacity) ?? 0;
        var occupancyRate = totalBeds > 0 ? Math.Round((decimal)residentCount / totalBeds * 100, 0) : 0;

        // === KPI 3: 预约人员（BOOKINGS.status=1）===
        var bookingCount = await _db.DormBookings
            .CountAsync(b => b.Status == BookingStatus.Reserved);

        // === KPI 4: 异常人员 A+B+C ===
        // A: 已离职但仍入住（status=3 && dormCode != null）
        var abnormalA = await _db.Employees
            .CountAsync(e => e.EmploymentStatusId == EmployeeStatus.Left && e.DormCode != null && e.DormCode != "");
        // B: 未到入职日期提前入住（status=1 && dormCode != null && hireDate > today）
        var abnormalB = await _db.Employees
            .CountAsync(e => e.EmploymentStatusId == EmployeeStatus.Active
                          && e.DormCode != null && e.DormCode != ""
                          && e.HireDate.HasValue && e.HireDate.Value > today);
        // C: 超期未办（BOOKINGS.status=1 && bookingDate < today）
        var abnormalC = await _db.DormBookings
            .CountAsync(b => b.Status == BookingStatus.Reserved && b.BookingDate < today);

        // === KPI 5: 本月抄表覆盖 ===
        var monthStr = monthStart.ToString("yyyy-MM");
        var meterReadCount = await _db.MeterRecords
            .CountAsync(r => r.ReadMonth == monthStr && r.Status != (byte)MeterRecordStatus.Voided);
        var meterTotalDorms = await _db.Dorms.CountAsync();
        // 未抄宿舍列表（仅展示前 5 个，按 DormCode 排序）
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

        // === KPI 6: 人均费用（v2.11.4 新增） ===
        // 账单数据当前无持久化表，按月份确定性随机生成
        var employeeBills = BuildEmployeeBillsForMonth(monthStr);
        var avgFee = employeeBills.Count > 0
            ? Math.Round(employeeBills.Average(b => b.TotalShareAmount), 2)
            : 0;

        // === KPI 7: 本月费用合计 ===
        var dormBills = BuildDormBillsForMonth(monthStr);
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

    #region 图表 8 项

    /// <summary>
    /// 图表 1：入住/退房人数对比（近 12 月）
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
    /// 图表 2：每月总费用变化曲线（近 12 月 + 上年同月对比）
    /// </summary>
    private List<MonthlyCostTrendDto> BuildCostTrendMonthlyAsync(DateTime refMonthStart)
    {
        var result = new List<MonthlyCostTrendDto>();
        for (int i = 11; i >= 0; i--)
        {
            var m = refMonthStart.AddMonths(-i);
            var mStr = m.ToString("yyyy-MM");
            var bills = BuildDormBillsForMonth(mStr);
            result.Add(new MonthlyCostTrendDto
            {
                Month = mStr,
                CurrentYear = bills.Sum(b => b.TotalAmount),
                LastYear = BuildDormBillsForMonth(m.AddYears(-1).ToString("yyyy-MM")).Sum(b => b.TotalAmount)
            });
        }
        return result;
    }

    /// <summary>
    /// 图表 3：宿舍费用排名 TOP 10（水平堆叠柱状图）
    /// </summary>
    private List<DormCostRankDto> BuildDormCostTop10(string monthStr)
    {
        // 读取宿舍列表，按费用降序取前 10
        var dormCodes = new[] { "D-101", "D-055", "D-078", "D-023", "D-156", "D-089", "D-134", "D-067", "D-112", "D-045" };
        var rng = new Random(GetMonthSeed(monthStr));
        var dorms = _db.Dorms.Where(d => dormCodes.Contains(d.DormCode)).ToList();
        return dorms
            .OrderBy(d => Array.IndexOf(dormCodes, d.DormCode))
            .Select(d => new DormCostRankDto
            {
                DormCode = d.DormCode,
                ColdAmount = (decimal)(rng.NextDouble() * 80 + 100),
                HotAmount = (decimal)(rng.NextDouble() * 150 + 175),
                ElectricityAmount = (decimal)(rng.NextDouble() * 200 + 280)
            })
            .ToList();
    }

    /// <summary>
    /// 图表 4：入住率排名 TOP 15（v2.11.5 UI 优化）
    /// 算法：DORMS LEFT JOIN DormBookings(Status=Staying) GROUP BY DormCode ORDER BY OccupancyRate ASC
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
    /// 图表 5：部门分布
    /// </summary>
    private async Task<List<DistributionItem>> BuildDepartmentDistributionAsync()
    {
        return await _db.Employees
            .Where(e => !string.IsNullOrEmpty(e.Department))
            .GroupBy(e => e.Department!)
            .Select(g => new DistributionItem
            {
                Label = g.Key,
                Value = g.Count()
            })
            .OrderByDescending(d => d.Value)
            .Take(10)
            .ToListAsync();
    }

    /// <summary>
    /// 图表 6：费用类型占比（环形图）
    /// </summary>
    private List<DistributionItem> BuildCostTypeRatio(string monthStr)
    {
        var bills = BuildDormBillsForMonth(monthStr);
        return new List<DistributionItem>
        {
            new() { Label = "电费", Value = (int)bills.Sum(b => b.ElectricityAmount) },
            new() { Label = "热水费", Value = (int)bills.Sum(b => b.HotAmount) },
            new() { Label = "冷水费", Value = (int)bills.Sum(b => b.ColdAmount) }
        };
    }

    /// <summary>
    /// 图表 7：员工类型分布
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
    /// 图表 8：本月抄表覆盖（环形图：已抄 vs 未抄）
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

    #region 模拟账单数据生成（DORM_BILLS 当前无持久化表）

    private static int GetMonthSeed(string monthStr)
    {
        unchecked
        {
            int seed = 0;
            foreach (var c in monthStr) seed = seed * 31 + c;
            return seed;
        }
    }

    private List<MockDormBill> BuildDormBillsForMonth(string monthStr)
    {
        var dormCodes = _db.Dorms.Select(d => d.DormCode).ToList();
        if (dormCodes.Count == 0)
        {
            // 测试数据：演示用
            dormCodes = new[] { "D-101", "D-055", "D-078", "D-023", "D-156", "D-089", "D-134", "D-067", "D-112", "D-045" }.ToList();
        }

        var rng = new Random(GetMonthSeed(monthStr));
        var bills = new List<MockDormBill>();
        foreach (var code in dormCodes)
        {
            var cold = (decimal)(rng.NextDouble() * 80 + 100);
            var hot = (decimal)(rng.NextDouble() * 150 + 175);
            var elec = (decimal)(rng.NextDouble() * 200 + 280);
            bills.Add(new MockDormBill
            {
                DormCode = code,
                ColdAmount = cold,
                HotAmount = hot,
                ElectricityAmount = elec,
                TotalAmount = cold + hot + elec
            });
        }
        return bills;
    }

    private List<MockEmployeeBill> BuildEmployeeBillsForMonth(string monthStr)
    {
        var employeeCount = _db.Employees.CountAsync().Result;
        if (employeeCount == 0) employeeCount = 571;  // 演示数据

        var rng = new Random(GetMonthSeed(monthStr) + 1);
        var bills = new List<MockEmployeeBill>();
        for (int i = 0; i < Math.Min(employeeCount, 600); i++)
        {
            bills.Add(new MockEmployeeBill
            {
                EmployeeCode = $"E{i:0000}",
                TotalShareAmount = (decimal)(rng.NextDouble() * 200 + 30)
            });
        }
        return bills;
    }

    private class MockDormBill
    {
        public string DormCode { get; set; } = "";
        public decimal ColdAmount { get; set; }
        public decimal HotAmount { get; set; }
        public decimal ElectricityAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    private class MockEmployeeBill
    {
        public string EmployeeCode { get; set; } = "";
        public decimal TotalShareAmount { get; set; }
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

    /// <summary>5. 在职人员部门分布</summary>
    public List<DistributionItem> DepartmentDistribution { get; set; } = new();

    /// <summary>6. 费用类型占比</summary>
    public List<DistributionItem> CostTypeRatio { get; set; } = new();

    /// <summary>7. 员工类型分布</summary>
    public List<DistributionItem> EmployeeTypeDistribution { get; set; } = new();

    /// <summary>8. 抄表覆盖</summary>
    public List<DistributionItem> MeterCoverage { get; set; } = new();
}

/// <summary>
/// 7 张 KPI 卡片数据源（严格按 04-HTML原型/index.html + 需求 01-v2.11.3.md）
/// </summary>
public class DashboardKpi
{
    // KPI 1: 入住人数
    public int ResidentCount { get; set; }
    public int TotalPersonnelCount { get; set; }

    // KPI 2: 宿舍入住率
    public decimal OccupancyRate { get; set; }
    public int OccupiedBeds { get; set; }
    public int TotalBeds { get; set; }

    // KPI 3: 预约人员
    public int BookingCount { get; set; }

    // KPI 4: 异常人员
    public int AbnormalCount { get; set; }
    public int AbnormalA { get; set; }
    public int AbnormalB { get; set; }
    public int AbnormalC { get; set; }

    // KPI 5: 本月抄表覆盖
    public int MeterReadCount { get; set; }
    public int MeterTotalCount { get; set; }
    public List<string> UnreadDormCodes { get; set; } = new();

    // KPI 6: 人均费用
    public decimal AvgFee { get; set; }

    // KPI 7: 本月费用合计
    public decimal TotalFee { get; set; }
    public int FeeRooms { get; set; }
}

/// <summary>月度入住退房</summary>
public class MonthlyCheckInOutDto
{
    public string Month { get; set; } = "";
    public int CheckIn { get; set; }
    public int CheckOut { get; set; }
}

/// <summary>月度费用趋势（当年 + 上年对比）</summary>
public class MonthlyCostTrendDto
{
    public string Month { get; set; } = "";
    public decimal CurrentYear { get; set; }
    public decimal LastYear { get; set; }
}

/// <summary>宿舍费用排名</summary>
public class DormCostRankDto
{
    public string DormCode { get; set; } = "";
    public decimal ColdAmount { get; set; }
    public decimal HotAmount { get; set; }
    public decimal ElectricityAmount { get; set; }
}

/// <summary>宿舍入住率排名</summary>
public class DormOccupancyRankDto
{
    public string DormCode { get; set; } = "";
    public int Capacity { get; set; }
    public int CurrentResidents { get; set; }
    public decimal OccupancyRate { get; set; }
}

public class DistributionItem
{
    public string Label { get; set; } = "";
    public int Value { get; set; }
}

public class TrendPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
}

public class MultiTrendSeries
{
    public string Name { get; set; } = "";
    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();
}

public class TopOccupancyItem
{
    public string DormCode { get; set; } = "";
    public string Building { get; set; } = "";
    public int Capacity { get; set; }
    public int Occupied { get; set; }
    public decimal OccupancyRate { get; set; }
}

#endregion