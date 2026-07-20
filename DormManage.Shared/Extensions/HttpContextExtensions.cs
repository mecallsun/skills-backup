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
}