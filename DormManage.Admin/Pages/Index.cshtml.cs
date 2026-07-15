using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages;

/// <summary>
/// 首页仪表盘（P1-4 + P1-5）
/// 数据源来自 DashboardService（P1-5 聚合服务）
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

    public async Task OnGetAsync()
    {
        try
        {
            Dashboard = await _dashboard.GetDashboardAsync();
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