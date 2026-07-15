using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages;

/// <summary>
/// 首页仪表盘（P1-4 + P1-5 + P2-2）
/// 数据源来自 DashboardService（P1-5 聚合服务）
/// 支持 ?month=yyyy-MM 选择月份（P2-2）
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
            DashboardJson = System.Text.Json.JsonSerializer.Serialize(Dashboard, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 Dashboard 失败");
            Dashboard = new DashboardDto();
        }
    }
}