using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using DormManage.Shared.Extensions;
using DormManage.Shared.Services;

namespace DormManage.Admin.Filters;

/// <summary>
/// v2.13.76 RBAC 页面守卫过滤器：
/// Razor Pages 路由级权限校验。在 OnPageHandlerExecuting 阶段：
///  1. 解析当前请求路径（去掉前导斜杠，取首段作为模块 key）
///  2. 调用 IPermissionService.CurrentUserHasRoute 检查当前用户是否拥有匹配路由前缀的菜单权限
///  3. 无权限 → 重定向 /Account/Login?denied=1（带原因），由登录页显示提示
///
/// 例外（不参与守卫）：
///  - /Account/*（登录/登出/找回密码）
///  - /Error / /Privacy
///  - 首页 /（独立授权：home:view 默认所有登录用户都有）
///
/// 模块 key 与 PermissionService.CurrentUserHasRoute 的映射：
///  /Booking/*          → booking:view
///  /Dorms/*            → dorm:view
///  /Personnel/*        → personnel:view
///  /BillingStandard/*  → billing:view
///  /DormBilling/*      → dormbilling:view
///  /EmployeeBilling/*  → employeebilling:view
///  /Meter/*            → meter:view
///  /Basics/*           → basics:view
///  /Settings/*         → settings:view
/// </summary>
public class PagePermissionFilter : IAsyncPageFilter
{
    private readonly IPermissionService _perm;
    private readonly IHttpContextAccessor _http;

    public PagePermissionFilter(IPermissionService perm, IHttpContextAccessor http)
    {
        _perm = perm;
        _http = http;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";
        // 去除末尾斜杠
        if (path.Length > 1 && path.EndsWith("/")) path = path[..^1];

        // 例外白名单
        if (IsExempt(path)) return next();

        // 登录态检查
        var userId = context.HttpContext.GetCurrentUserId();
        if (userId <= 0) return next();  // 未登录交给 [Authorize] 跳转

        // v2.13.198 修复：try-catch 兜底，防止权限查询异常导致整个页面 Error
        try
        {
            // 首页允许任何登录用户访问（v2.13.199：始终放行，不检查 home:view）
            // 修复：避免 admin 登录后被错误地 DenyAccess 重定向到 Login（可能引发"无可见菜单"循环）
            if (path == "/" || path == "")
            {
                // 即使没有 home:view 权限，也允许已登录用户访问首页
                // 用户可手动在设置中分配权限
                return next();
            }

            // 路由守卫：取首段作为模块 key
            var moduleKey = path.TrimStart('/').Split('/', 2)[0];
            var requiredPermission = ModulePermissionMap.GetValueOrDefault(moduleKey);
            if (requiredPermission == null)
                return next(); // 未知模块（/Account/* 等已白名单放过）

            if (!_perm.CurrentUserHasRoute(_http, $"/{moduleKey}"))
            {
                DenyAccess(context);
                return Task.CompletedTask; // 拒绝后不再调用 next（context.Result 已设置）
            }
        }
        catch (Exception ex)
        {
            // 权限查询异常时记录日志并放行（避免整个页面崩溃）
            var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("PagePermissionFilter");
            logger?.LogWarning(ex, "[RBAC] 权限检查异常，放行访问 {Path}", path);
        }

        return next();
    }

    private static bool IsExempt(string path)
    {
        // 账号/异常/隐私页 不参与守卫
        if (path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/Error", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/Privacy", StringComparison.OrdinalIgnoreCase)) return true;
        // API 端点不走 Razor Pages 守卫（API 有自己的过滤器链）
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void DenyAccess(PageHandlerExecutingContext context)
    {
        // 写审计日志（如果有的话）
        var http = context.HttpContext;
        var userName = http.User?.Identity?.Name ?? "(unknown)";
        var logger = http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("PagePermissionFilter");
        logger?.LogWarning("[RBAC] 用户 {User} 访问 {Path} 被拒绝：缺少权限", userName, http.Request.Path);

        // 重定向到登录页（带 denied=1 标记）
        context.Result = new Microsoft.AspNetCore.Mvc.RedirectToPageResult("/Account/Login",
            new { denied = 1, from = http.Request.Path });
    }

    /// <summary>模块路由首段 → 权限码 映射</summary>
    private static readonly Dictionary<string, string> ModulePermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Booking"] = "booking:view",
        ["Dorms"] = "dorm:view",
        ["Personnel"] = "personnel:view",
        ["BillingStandard"] = "billing:view",
        ["DormBilling"] = "dormbilling:view",
        ["EmployeeBilling"] = "employeebilling:view",
        ["Meter"] = "meter:view",
        ["Basics"] = "basics:view",
        ["Settings"] = "settings:view",
    };
}