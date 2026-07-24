using System;
using System.Text.Json;
using System.Threading.Tasks;
using DormManage.Shared.Security;
using Microsoft.AspNetCore.Http;

namespace DormManage.Admin.Middleware;

/// <summary>
/// v2.13.136 全局只读中间件（Admin 端）
///
/// 业务规则：注册失败/过期时，所有 POST/PUT/DELETE/PATCH 请求返回 403。
/// 放行清单：
/// - GET / HEAD（只读查看保持可用）
/// - 静态资源（/css, /js, /lib, /wwwroot, *.css, *.js, *.png 等）
/// - 登录/退出/错误页（/Account/*, /Error, /Privacy）
/// - 暗桩 503 页面（/TamperUnlock）
///
/// 设计原则：
/// - 中间件层（不是 ActionFilter / PageModel）—— 一处生效全栈覆盖
/// - Razor Page POST 重定向到 /Error?code=LICENSE_READONLY
/// - API 端点 + XHR 返回 403 JSON
/// - 进程内缓存由 LicenseGuard 提供，避免每次请求都查注册表
/// </summary>
public class LicenseReadOnlyMiddleware
{
    private readonly RequestDelegate _next;

    public LicenseReadOnlyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // 1) 检测注册状态（注册到 HttpContext.Items 供下游 PageModel 使用）
        var isReadOnly = LicenseGuard.IsReadOnly();
        context.Items["__LICENSE_READONLY__"] = isReadOnly;

        if (!isReadOnly)
        {
            // 注册有效 → 放行
            await _next(context);
            return;
        }

        // 2) 放行 GET / HEAD（保持只读查看）
        var method = context.Request.Method;
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 3) 放行白名单路径（登录页 / 静态资源 / 错误页 / 暗桩）
        var path = context.Request.Path.Value ?? "";
        if (IsWhitelisted(path))
        {
            await _next(context);
            return;
        }

        // 4) 拒绝写入操作
        await RejectWrite(context, path);
    }

    /// <summary>
    /// 拒绝写入请求（统一 403 + JSON 或重定向）
    /// </summary>
    private static async Task RejectWrite(HttpContext context, string path)
    {
        context.Response.StatusCode = 403;

        // API 端点 / AJAX 请求 → 返回 JSON
        var isAjax = string.Equals(
            context.Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || isAjax)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = new
            {
                success = false,
                code = "LICENSE_READONLY",
                message = "软件未注册或注册已过期，所有修改类操作已禁用。请联系信息科进行注册。"
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            return;
        }

        // Razor Page POST → 重定向到错误页
        context.Response.Redirect(
            "/Error?code=LICENSE_READONLY&msg=" +
            Uri.EscapeDataString("注册未通过，禁止修改类操作。请联系信息科进行注册。"));
    }

    /// <summary>
    /// 白名单路径判定
    /// </summary>
    private static bool IsWhitelisted(string path)
    {
        // 静态资源目录
        if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/fonts", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/wwwroot", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 静态资源扩展名
        if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 登录 / 退出 / 错误页 / 隐私页（用户必须能登录 + 看到错误）
        if (path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/Error", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/Privacy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 暗桩 503 页面（与全局只读机制并存 —— 暗桩优先）
        if (path.Equals("/TamperUnlock", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}