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

        // 根据用户角色查询权限菜单
        var menus = await _authService.GetUserMenusAsync(userId);

        // 只显示 PermissionType=1 的菜单项
        return View("Default", menus.Where(m => !string.IsNullOrEmpty(m.Route)).ToList());
    }
}