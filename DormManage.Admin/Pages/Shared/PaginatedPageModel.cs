using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DormManage.Admin.Pages.Shared;

/// <summary>
/// v2.13.105 列表分页基类 — 强制 PageSize 默认 10 + 白名单校验。
///
/// 背景：
///   用户反馈 v2.13.103 部署后，"点击主菜单导航不带 ?pageSize 参数时仍默认显示 10 条"。
///   排查发现 8 个 PageModel 均 `public int PageSize { get; set; } = 10;`，但前端 dropdown
///   默认显示 10，且 URL 切换时会被 dropdown selected 影响渲染。
///
/// 根因：
///   1. 浏览器缓存 v2.13.99 之前的 _PaginationPartial.cshtml JS（带启动回退逻辑）
///   2. 部署的 dll 仍是 v2.13.99 之前的版本（PageModel 字段未升级）
///
/// 修复（v2.13.104）：
///   - 所有列表页继承 PaginatedPageModel，OnGet 顶部强制校验 PageSize
///   - 白名单 {10, 20, 50, 100}：不在白名单 → 强制 20
///   - PageSize ≤ 0 → 强制 20
///   - 与 _PaginationPartial.cshtml 的 dropdown 选项一致
/// </summary>
public abstract class PaginatedPageModel : PageModel
{
    /// <summary>默认每页条数（用户需求：主菜单点击不带参数时显示 10 条）</summary>
    public const int DefaultPageSize = 10;

    /// <summary>合法的 pageSize 白名单（与 _PaginationPartial dropdown 一致）</summary>
    public static readonly int[] AllowedPageSizes = { 10, 20, 50, 100 };

    private int _pageSize = DefaultPageSize;

    /// <summary>
    /// v2.13.104 BUG 修复：pageSize setter 强制白名单校验。
    /// 不在白名单（如 0、负数、超过 100 等）→ 自动 fallback 到默认 10。
    /// 这样即使 [BindProperty(SupportsGet = true)] 绑定了非法值，也会被纠正。
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Array.IndexOf(AllowedPageSizes, value) >= 0 ? value : DefaultPageSize;
    }

    /// <summary>当前页码（默认 1）</summary>
    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    /// <summary>v2.13.104：在 OnGet 顶部调用，强制 PageIndex ≥ 1 + PageSize 在白名单</summary>
    protected void EnsureValidPagination()
    {
        if (PageIndex < 1) PageIndex = 1;
        if (Array.IndexOf(AllowedPageSizes, PageSize) < 0) PageSize = DefaultPageSize;
    }
}