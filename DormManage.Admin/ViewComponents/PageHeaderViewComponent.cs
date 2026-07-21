using Microsoft.AspNetCore.Mvc;

namespace DormManage.Admin.ViewComponents;

/// <summary>
/// 共用页头组件（P2-1）
///
/// 使用示例：
/// <code>
/// @await Component.InvokeAsync("PageHeader", new {
///     icon = "bi-people",
///     title = "人员清单",
///     count = Model.Result?.Total ?? 0,
///     countLabel = "人",
///     primaryAction = new PageAction { Label = "新增人员", Url = "/Personnel/Create", Icon = "bi-plus-lg" }
/// })
/// </code>
/// </summary>
public class PageHeaderViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string icon,
        string title,
        int? count = null,
        string? countLabel = null,
        string? subtitle = null,
        PageAction? primaryAction = null,
        List<PageAction>? actions = null,
        List<TabItem>? tabs = null,
        string? activeTab = null)
    {
        var model = new PageHeaderModel
        {
            Icon = icon,
            Title = title,
            Count = count,
            CountLabel = countLabel,
            Subtitle = subtitle,
            PrimaryAction = primaryAction,
            Actions = actions ?? new List<PageAction>(),
            Tabs = tabs ?? new List<TabItem>(),
            ActiveTab = activeTab ?? HttpContext.Request.Query["tab"].ToString()
        };
        return View("Default", model);
    }
}

public class PageHeaderModel
{
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public int? Count { get; set; }
    public string? CountLabel { get; set; }
    public string? Subtitle { get; set; }
    public PageAction? PrimaryAction { get; set; }
    public List<PageAction> Actions { get; set; } = new();
    public List<TabItem> Tabs { get; set; } = new();
    public string ActiveTab { get; set; } = "";
}

public class PageAction
{
    public string Label { get; set; } = "";
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public string? Style { get; set; } = "primary"; // primary/secondary/success/danger/warning
    public string? OnClick { get; set; }
    /// <summary>v2.13.76 RBAC：权限码（拥有该权限才渲染按钮）。空/不设置 → 始终显示</summary>
    public string? PermissionCode { get; set; }
}

public class TabItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Icon { get; set; }
    public string? Badge { get; set; }
    public int? BadgeColor { get; set; } // Bootstrap color: success/warning/danger/info
}