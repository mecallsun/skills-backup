using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

// v2.13.26: 已迁移至 DormManage.Shared.Extensions.HttpContextExtensions
// 此文件保留兼容旧引用
namespace DormManage.Admin.Extensions;

public static class HttpContextExtensions
{
    public static int GetCurrentUserId(this HttpContext ctx)
        => DormManage.Shared.Extensions.HttpContextExtensions.GetCurrentUserId(ctx);

    public static List<string> GetRoles(this HttpContext ctx)
        => DormManage.Shared.Extensions.HttpContextExtensions.GetRoles(ctx);

    public static string? GetDisplayName(this HttpContext ctx)
        => DormManage.Shared.Extensions.HttpContextExtensions.GetDisplayName(ctx);
}