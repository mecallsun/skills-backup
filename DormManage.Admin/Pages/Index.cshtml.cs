using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages;

/// <summary>
/// 首页经营概览 — M1 模块（M1-A→M1-D 阶段一交付）
///
/// 严格按 04-HTML原型/index.html 1:1 实施：
/// - 7 KPI 卡片（入住人数/宿舍入住率/预约人员/异常人员/本月抄表覆盖/人均费用/本月费用合计）
/// - 8 图表（入住退房对比/费用变化曲线/费用TOP10/入住率TOP15/部门分布/费用类型占比/员工类型分布/抄表覆盖）
///
/// 数据源：DormManage.Shared.Services.IDashboardService（P1-5 + M1 重构）
/// 月份选择：通过 ?month=yyyy-MM URL 参数控制（M1-C 重写为原生 select + 服务端回发）
/// </summary>
public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboard;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IDashboardService dashboard, ILogger<IndexModel> logger)
    {
        _dashboard = dashboard;
        _logger = logger;
    }

    public DashboardDto Dashboard { get; set; } = new();
    public string DashboardJson { get; set; } = "{}";
    public string CurrentMonth { get; set; } = "";
    public List<string> AvailableMonths { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Month { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            CurrentMonth = string.IsNullOrWhiteSpace(Month) ? DateTime.Now.ToString("yyyy-MM") : Month;
            if (!DateTime.TryParseExact(CurrentMonth, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var dt))
            {
                CurrentMonth = DateTime.Now.ToString("yyyy-MM");
                dt = DateTime.Now;
            }

            // 生成最近 12 个月选项
            AvailableMonths = Enumerable.Range(0, 12)
                .Select(i => dt.AddMonths(-i).ToString("yyyy-MM"))
                .ToList();

            Dashboard = await _dashboard.GetDashboardAsync(dt);
            // camelCase 序列化与前端 JS 字段对应
            DashboardJson = System.Text.Json.JsonSerializer.Serialize(Dashboard, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 Dashboard 失败（month={Month}）", Month);
            Dashboard = new DashboardDto();
            DashboardJson = "{}";
        }
    }
}