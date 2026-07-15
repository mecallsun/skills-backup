using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 首页数据看板聚合服务（P1-5）
///
/// 一次性查询多张表，输出 DashboardDto（KPI + 7 个图表数据）。
/// 调用方：Pages/Index.cshtml.cs。
///
/// 性能考虑：
/// - 全部用 EF Core LINQ 投影，避免加载完整实体
/// - 多次小查询（每图 1 次），单图延迟低
/// - 不做内存缓存（数据变更频繁，月度趋势 1 小时缓存可后续加）
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
        var monthStart = new DateTime(refMonth.Year, refMonth.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var dto = new DashboardDto
        {
            ReferenceMonth = refMonth,
            Kpi = await BuildKpiAsync(),
            OccupancyTrend = await BuildOccupancyTrendAsync(monthStart),
            DepartmentDistribution = await BuildDepartmentDistributionAsync(),
            BuildingOccupancy = await BuildBuildingOccupancyAsync(),
            BookingMonthlyTrend = await BuildBookingMonthlyTrendAsync(monthStart),
            MeterMonthlyCount = await BuildMeterMonthlyCountAsync(monthStart),
            EmployeeTypeDistribution = await BuildEmployeeTypeDistributionAsync(),
            TopOccupancyDorms = await BuildTopOccupancyDormsAsync(10)
        };

        return dto;
    }

    private async Task<DashboardKpi> BuildKpiAsync()
    {
        var totalDorms = await _db.Dorms.CountAsync();
        var totalBeds = await _db.Dorms.SumAsync(d => (int?)d.Capacity) ?? 0;
        var currentOccupancy = await _db.DormBookings.CountAsync(b => b.Status == BookingStatus.Staying);
        var totalEmployees = await _db.Employees.CountAsync();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var todayCheckIns = await _db.DormBookings.CountAsync(b => b.BookingDate == today && b.Type == BookingType.CheckIn);
        var todayCheckOuts = await _db.DormBookings.CountAsync(b => b.BookingDate == today && b.Type == BookingType.CheckOut);

        return new DashboardKpi
        {
            TotalDorms = totalDorms,
            TotalBeds = totalBeds,
            CurrentOccupancy = currentOccupancy,
            OccupancyRate = totalBeds > 0 ? Math.Round(currentOccupancy * 100m / totalBeds, 1) : 0,
            TotalEmployees = totalEmployees,
            TodayCheckIns = todayCheckIns,
            TodayCheckOuts = todayCheckOuts
        };
    }

    /// <summary>入住率趋势（最近 12 个月）</summary>
    private async Task<List<TrendPoint>> BuildOccupancyTrendAsync(DateTime refMonthStart)
    {
        var points = new List<TrendPoint>();
        for (int i = 11; i >= 0; i--)
        {
            var m = refMonthStart.AddMonths(-i);
            var mEnd = m.AddMonths(1);
            // 月末在宿人数 = 该月最后一天 ≤ 当日的在宿记录
            var snapshot = await _db.DormBookings
                .Where(b => b.BookingDate <= DateOnly.FromDateTime(mEnd.AddDays(-1)))
                .Where(b => b.Status == BookingStatus.Staying ||
                            b.Status == BookingStatus.CheckedOut && b.RegistrationDate >= m)
                .CountAsync();
            // 简化：使用当前在宿人数作为近似
            var totalBeds = await _db.Dorms.SumAsync(d => (int?)d.Capacity) ?? 1;
            var rate = Math.Round(snapshot * 100m / Math.Max(1, totalBeds), 1);
            points.Add(new TrendPoint
            {
                Label = m.ToString("yyyy-MM"),
                Value = (double)rate
            });
        }
        return points;
    }

    /// <summary>部门人员分布（Top 10）</summary>
    private async Task<List<DistributionItem>> BuildDepartmentDistributionAsync()
    {
        // SysEmployee.Department 是字符串字段，不是导航属性
        var data = await _db.Employees
            .Where(e => !string.IsNullOrEmpty(e.Department))
            .GroupBy(e => e.Department!)
            .Select(g => new DistributionItem
            {
                Label = g.Key ?? "未分配",
                Value = g.Count()
            })
            .OrderByDescending(d => d.Value)
            .Take(10)
            .ToListAsync();
        return data;
    }

    /// <summary>楼栋入住率</summary>
    private async Task<List<DistributionItem>> BuildBuildingOccupancyAsync()
    {
        var data = await _db.Dorms
            .GroupBy(d => d.BuildingName ?? "未分配")
            .Select(g => new DistributionItem
            {
                Label = g.Key,
                Value = g.Sum(d => d.Capacity)
            })
            .OrderByDescending(d => d.Value)
            .ToListAsync();
        return data;
    }

    /// <summary>月度办理趋势（最近 12 个月）</summary>
    private async Task<List<MultiTrendSeries>> BuildBookingMonthlyTrendAsync(DateTime refMonthStart)
    {
        var labels = new List<string>();
        var checkIns = new List<int>();
        var checkOuts = new List<int>();

        for (int i = 11; i >= 0; i--)
        {
            var m = refMonthStart.AddMonths(-i);
            var mEnd = m.AddMonths(1);
            var ci = await _db.DormBookings
                .CountAsync(b => b.BookingDate >= DateOnly.FromDateTime(m) && b.BookingDate < DateOnly.FromDateTime(mEnd) && b.Type == BookingType.CheckIn);
            var co = await _db.DormBookings
                .CountAsync(b => b.BookingDate >= DateOnly.FromDateTime(m) && b.BookingDate < DateOnly.FromDateTime(mEnd) && b.Type == BookingType.CheckOut);
            labels.Add(m.ToString("yyyy-MM"));
            checkIns.Add(ci);
            checkOuts.Add(co);
        }
        return new List<MultiTrendSeries>
        {
            new() { Name = "入住", Labels = labels, Values = checkIns.Select(v => (double)v).ToList() },
            new() { Name = "退房", Labels = labels, Values = checkOuts.Select(v => (double)v).ToList() }
        };
    }

    /// <summary>月度抄表统计（按 ReadMonth 字符串聚合）</summary>
    private async Task<List<TrendPoint>> BuildMeterMonthlyCountAsync(DateTime refMonthStart)
    {
        var points = new List<TrendPoint>();
        for (int i = 11; i >= 0; i--)
        {
            var monthLabel = refMonthStart.AddMonths(-i).ToString("yyyy-MM");
            var cnt = await _db.MeterRecords
                .CountAsync(r => r.ReadMonth == monthLabel);
            points.Add(new TrendPoint
            {
                Label = monthLabel,
                Value = cnt
            });
        }
        return points;
    }

    /// <summary>员工类型分布</summary>
    private async Task<List<DistributionItem>> BuildEmployeeTypeDistributionAsync()
    {
        var data = await _db.Employees
            .Where(e => e.EmployeeTypeId != null)
            .GroupBy(e => e.EmployeeType!.Name)
            .Select(g => new DistributionItem
            {
                Label = g.Key ?? "未分类",
                Value = g.Count()
            })
            .OrderByDescending(d => d.Value)
            .ToListAsync();
        return data;
    }

    /// <summary>入住率 TOP 10 宿舍</summary>
    private async Task<List<TopOccupancyItem>> BuildTopOccupancyDormsAsync(int top)
    {
        var dorms = await _db.Dorms
            .OrderBy(d => d.Capacity == 0 ? int.MaxValue : d.Capacity)  // 小房间优先
            .Take(top * 3)  // 多取一些以便筛选
            .ToListAsync();

        var codes = dorms.Select(d => d.DormCode).ToList();
        var occupancies = await _db.DormBookings
            .Where(b => codes.Contains(b.DormCode) && b.Status == BookingStatus.Staying)
            .GroupBy(b => b.DormCode)
            .Select(g => new { DormCode = g.Key, Count = g.Count() })
            .ToListAsync();

        var occMap = occupancies.ToDictionary(x => x.DormCode, x => x.Count);

        return dorms
            .Select(d => new TopOccupancyItem
            {
                DormCode = d.DormCode,
                Building = d.BuildingName ?? "",
                Capacity = d.Capacity,
                Occupied = occMap.GetValueOrDefault(d.DormCode),
                OccupancyRate = d.Capacity > 0 ? Math.Round(occMap.GetValueOrDefault(d.DormCode) * 100m / d.Capacity, 1) : 0
            })
            .OrderByDescending(d => d.OccupancyRate)
            .Take(top)
            .ToList();
    }
}

/// <summary>首页数据看板完整 DTO</summary>
public class DashboardDto
{
    public DateTime ReferenceMonth { get; set; }
    public DashboardKpi Kpi { get; set; } = new();

    /// <summary>1. 入住率趋势（折线图）</summary>
    public List<TrendPoint> OccupancyTrend { get; set; } = new();

    /// <summary>2. 部门人员分布（饼图）</summary>
    public List<DistributionItem> DepartmentDistribution { get; set; } = new();

    /// <summary>3. 楼栋床位分布（柱状图）</summary>
    public List<DistributionItem> BuildingOccupancy { get; set; } = new();

    /// <summary>4. 月度办理趋势（折线图 + 双系列）</summary>
    public List<MultiTrendSeries> BookingMonthlyTrend { get; set; } = new();

    /// <summary>5. 月度抄表统计（柱状图）</summary>
    public List<TrendPoint> MeterMonthlyCount { get; set; } = new();

    /// <summary>6. 员工类型分布（饼图）</summary>
    public List<DistributionItem> EmployeeTypeDistribution { get; set; } = new();

    /// <summary>7. 入住率 TOP 10 宿舍（横向条形图）</summary>
    public List<TopOccupancyItem> TopOccupancyDorms { get; set; } = new();
}

public class DashboardKpi
{
    public int TotalDorms { get; set; }
    public int TotalBeds { get; set; }
    public int CurrentOccupancy { get; set; }
    public decimal OccupancyRate { get; set; }
    public int TotalEmployees { get; set; }
    public int TodayCheckIns { get; set; }
    public int TodayCheckOuts { get; set; }
}

public class TrendPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
}

public class DistributionItem
{
    public string Label { get; set; } = "";
    public int Value { get; set; }
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