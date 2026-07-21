using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DormManage.Admin.Services;
using DormManage.Shared.Extensions;

namespace DormManage.Admin.ViewComponents;

/// <summary>
/// v2.13.76 RBAC 主菜单 Tab 渲染组件：
/// 调用 IAuthService.GetUserMenusAsync 加载当前用户的菜单权限（含父级自动补齐），
/// 渲染为 Tier 2 Tab 页签栏（按 SortOrder 排序，仅显示当前用户有权限的 tab）。
/// </summary>
public class MenuViewComponent : ViewComponent
{
    private readonly IAuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MenuViewComponent(IAuthService authService, IHttpContextAccessor httpContextAccessor)
    {
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var http = _httpContextAccessor.HttpContext;
        var userId = http?.GetCurrentUserId() ?? 0;
        var currentPath = http?.Request.Path.Value ?? "";

        // 未登录：返回空模型（_Layout 已通过 AuthorizeFolder 守卫，此分支为兜底）
        if (userId <= 0) return View(new MenuViewModel());

        var menus = await _authService.GetUserMenusAsync(userId);

        // 仅保留 PermissionType=1 顶级菜单（ParentId=0）
        var topMenus = menus.Where(m => m.PermissionType == 1 && m.ParentId == 0).ToList();

        return View(new MenuViewModel
        {
            TopMenus = topMenus,
            CurrentPath = currentPath
        });
    }
}

/// <summary>MenuViewComponent 视图模型</summary>
public class MenuViewModel
{
    public List<AuthHelperExtensions.MenuNode> TopMenus { get; set; } = new();
    public string CurrentPath { get; set; } = "";
}