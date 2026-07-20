using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DormManage.Shared.Extensions;
public static class HttpContextExtensions
{
    public static int GetCurrentUserId(this HttpContext ctx)
    {
        var c = ctx.User?.FindFirst(ClaimTypes.NameIdentifier);
        return c != null && int.TryParse(c.Value, out var id) ? id : 0;
    }

    public static List<string> GetRoles(this HttpContext ctx)
    {
        return ctx.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();
    }

    public static string? GetDisplayName(this HttpContext ctx)
    {
        return ctx.User?.FindFirst("DisplayName")?.Value;
    }

    /// <summary>
    /// v2.13.29: 获取当前登录用户名（用于操作日志、办理登记人等场景）
    /// 优先从 X-User-Name Header 获取（API 调用方），其次从 Cookie/Claims 获取
    /// </summary>
    public static string GetCurrentUserName(this HttpContext ctx)
    {
        // 优先 X-User-Name Header（API 调用方可能未走 Cookie 认证）
        var headerName = ctx.Request.Headers["X-User-Name"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerName)) return headerName;

        // 其次从 Claims 中获取
        var claimName = ctx.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(claimName)) return claimName;

        return "system";
    }
}