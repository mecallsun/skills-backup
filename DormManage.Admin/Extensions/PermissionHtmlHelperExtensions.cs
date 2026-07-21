using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using DormManage.Shared.Services;

namespace DormManage.Admin.Extensions;

/// <summary>
/// v2.13.76 RBAC 按钮权限 HtmlHelper 扩展：
/// 在 Razor 视图中根据当前用户的权限码决定是否渲染元素。
///
/// 用法：
///   @if (await Html.HasPermissionAsync("booking:checkin"))
///   {
///       <button>入住办理</button>
///   }
///
/// 或内联三元：
///   &lt;button hidden="@(await Html.HasPermissionAsync("booking:checkin") ? null : "")"&gt;...&lt;/button&gt;
/// </summary>
public static class PermissionHtmlHelperExtensions
{
    /// <summary>当前用户是否拥有指定权限码</summary>
    public static Task<bool> HasPermissionAsync(this IHtmlHelper html, string code)
        => HasPermissionAsync(html, code, routePrefix: null);

    /// <summary>当前用户是否拥有匹配指定路由的菜单权限</summary>
    public static Task<bool> HasPermissionRouteAsync(this IHtmlHelper html, string routePrefix)
        => HasPermissionAsync(html, code: null, routePrefix: routePrefix);

    private static Task<bool> HasPermissionAsync(IHtmlHelper html, string? code, string? routePrefix)
    {
        var accessor = (IHttpContextAccessor?)html.ViewContext.HttpContext.RequestServices
            .GetService(typeof(IHttpContextAccessor));
        var perm = (IPermissionService?)html.ViewContext.HttpContext.RequestServices
            .GetService(typeof(IPermissionService));

        if (accessor == null || perm == null) return Task.FromResult(true); // 兜底：有异常时允许显示

        if (!string.IsNullOrEmpty(code))
            return Task.FromResult(perm.CurrentUserHasCode(accessor, code));
        if (!string.IsNullOrEmpty(routePrefix))
            return Task.FromResult(perm.CurrentUserHasRoute(accessor, routePrefix));
        return Task.FromResult(false);
    }
}