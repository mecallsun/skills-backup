using Microsoft.AspNetCore.Mvc;
using DormManage.Admin.Extensions;
using DormManage.Admin.Services;

namespace DormManage.Admin.ViewComponents;

/// <summary>
/// 导航栏菜单组件（按用户权限动态渲染）
/// </summary>
public class MenuViewComponent : ViewComponent
{
    private readonly IAuthService _authService;

    public MenuViewComponent(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.GetCurrentUserId();

        // 未登录用户显示空菜单
        if (userId <= 0)
        {
            return View("Default", new List<AuthHelperExtensions.MenuNode>());
        }

        // 根据用户角色查询权限菜单（已包含父级补齐）
        var menus = await _authService.GetUserMenusAsync(userId);

        // 只显示顶级菜单（ParentId=0）作为导航项，子菜单在 Dropdown 中渲染
        var topMenus = menus.Where(m => m.ParentId == 0).ToList();

        return View("Default", (topMenus, menus));
    }
}