using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DormManage.Admin.Pages.Basics;

/// <summary>
/// 基础资料页面模型（v2.13.13 重构：支持 tab 参数持久化）
/// </summary>
public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    /// <summary>
    /// 当前激活的 tab（用于视图高亮）
    /// </summary>
    public string ActiveTab => Tab ?? "dept";

    public void OnGet()
    {
        // 页面初始加载
    }
}
