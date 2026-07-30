using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Services;

namespace DormManage.Api.Controllers;

/// <summary>
/// 首页数据看板 API（v2.13.24 P0-1 修复：补齐 DashboardController，对接 IDashboardService）
///
/// 端点：
/// - GET /api/v1/dashboard/kpi        → 7 项 KPI
/// - GET /api/v1/dashboard/charts     → 8 项图表
/// - GET /api/v1/dashboard/all        → KPI + Charts 一次性返回（前端 Index.cshtml 默认调用）
///
/// 数据源：DashboardService（10-首页数据看板需求-v2.11.md）
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    /// <summary>
    /// 7 项 KPI：入住人数 / 住宿入住率 / 预约人员 / 异常人员 / 本月抄表覆盖 / 人均费用 / 本月费用合计
    /// </summary>
    /// <param name="month">参考月份（yyyy-MM-dd），默认本月</param>
    [HttpGet("kpi")]
    public async Task<ActionResult<DashboardKpi>> GetKpi([FromQuery] DateTime? month = null)
    {
        var dto = await _service.GetDashboardAsync(month);
        return Ok(dto.Kpi);
    }

    /// <summary>
    /// 8 项图表：入住退房对比 / 费用变化曲线 / 费用TOP10 / 入住率TOP15 / 部门分布 / 费用类型占比 / 员工类型分布 / 抄表覆盖
    /// </summary>
    /// <param name="month">参考月份（yyyy-MM-dd），默认本月</param>
    [HttpGet("charts")]
    public async Task<ActionResult<DashboardChartsDto>> GetCharts([FromQuery] DateTime? month = null)
    {
        var dto = await _service.GetDashboardAsync(month);
        return Ok(new DashboardChartsDto
        {
            CurrentMonth = dto.CurrentMonth,
            CheckInOutMonthly = dto.CheckInOutMonthly,
            CostTrendMonthly = dto.CostTrendMonthly,
            DormCostTop10 = dto.DormCostTop10,
            OccupancyRankTop15 = dto.OccupancyRankTop15,
            DepartmentDistribution = dto.DepartmentDistribution,
            CostTypeRatio = dto.CostTypeRatio,
            EmployeeTypeDistribution = dto.EmployeeTypeDistribution,
            MeterCoverage = dto.MeterCoverage
        });
    }

    /// <summary>
    /// 一次性返回 KPI + 全部 Charts（前端 Index.cshtml 默认调用）
    /// </summary>
    /// <param name="month">参考月份（yyyy-MM-dd），默认本月</param>
    [HttpGet("all")]
    public async Task<ActionResult<DashboardDto>> GetAll([FromQuery] DateTime? month = null)
    {
        var dto = await _service.GetDashboardAsync(month);
        return Ok(dto);
    }
}

/// <summary>Dashboard Charts 子集 DTO（按 8 项图表分组）</summary>
public class DashboardChartsDto
{
    public string CurrentMonth { get; set; } = "";
    public List<MonthlyCheckInOutDto> CheckInOutMonthly { get; set; } = new();
    public List<MonthlyCostTrendDto> CostTrendMonthly { get; set; } = new();
    public List<DormCostRankDto> DormCostTop10 { get; set; } = new();
    public List<DormOccupancyRankDto> OccupancyRankTop15 { get; set; } = new();
    public List<DistributionItem> DepartmentDistribution { get; set; } = new();
    public List<DistributionItem> CostTypeRatio { get; set; } = new();
    public List<DistributionItem> EmployeeTypeDistribution { get; set; } = new();
    public List<DistributionItem> MeterCoverage { get; set; } = new();
}