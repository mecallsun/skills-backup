using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Extensions;

namespace DormManage.Shared.Services;

/// <summary>
/// v2.13.76 RBAC 权限服务：查询当前用户的权限码 / 权限 ID 集合，
/// 用于菜单过滤、页面守卫、按钮权限控制。
/// </summary>
public interface IPermissionService
{
    /// <summary>获取用户的所有权限码集合（如 { "home:view", "booking:view", "booking:checkin" }）</summary>
    Task<HashSet<string>> GetUserPermissionCodesAsync(int userId);

    /// <summary>获取用户的所有权限 ID 集合（用于矩阵预勾选）</summary>
    Task<HashSet<int>> GetUserPermissionIdsAsync(int userId);

    /// <summary>判断用户是否拥有指定权限码</summary>
    Task<bool> HasPermissionCodeAsync(int userId, string code);

    /// <summary>判断用户是否拥有匹配指定路由前缀的菜单权限（用于页面守卫）</summary>
    Task<bool> HasPermissionRouteAsync(int userId, string routePrefix);

    /// <summary>判断用户是否拥有指定权限 ID</summary>
    Task<bool> HasPermissionIdAsync(int userId, int permissionId);

    /// <summary>当前 HttpContext 用户是否有权限码（同步 + 缓存版）</summary>
    bool CurrentUserHasCode(IHttpContextAccessor accessor, string code);

    /// <summary>当前 HttpContext 用户是否有匹配路由的菜单权限</summary>
    bool CurrentUserHasRoute(IHttpContextAccessor accessor, string routePrefix);
}

public class PermissionService : IPermissionService
{
    private readonly DormDbContext _db;

    public PermissionService(DormDbContext db) => _db = db;

    public async Task<HashSet<string>> GetUserPermissionCodesAsync(int userId)
    {
        if (userId <= 0) return new HashSet<string>();
        var codes = await (
            from p in _db.SysPermissions
            join rp in _db.SysRolePermissions on p.Id equals rp.PermissionId
            join ur in _db.SysUserRoles on rp.RoleId equals ur.RoleId
            where ur.UserId == userId && p.IsActive
            select p.PermissionCode
        ).Distinct().ToListAsync();
        return new HashSet<string>(codes);
    }

    public async Task<HashSet<int>> GetUserPermissionIdsAsync(int userId)
    {
        if (userId <= 0) return new HashSet<int>();
        var ids = await (
            from p in _db.SysPermissions
            join rp in _db.SysRolePermissions on p.Id equals rp.PermissionId
            join ur in _db.SysUserRoles on rp.RoleId equals ur.RoleId
            where ur.UserId == userId && p.IsActive
            select p.Id
        ).Distinct().ToListAsync();
        return new HashSet<int>(ids);
    }

    public async Task<bool> HasPermissionCodeAsync(int userId, string code)
    {
        if (string.IsNullOrEmpty(code) || userId <= 0) return false;
        var codes = await GetUserPermissionCodesAsync(userId);
        return codes.Contains(code);
    }

    public async Task<bool> HasPermissionRouteAsync(int userId, string routePrefix)
    {
        if (string.IsNullOrEmpty(routePrefix) || userId <= 0) return false;
        var matched = await (
            from p in _db.SysPermissions
            join rp in _db.SysRolePermissions on p.Id equals rp.PermissionId
            join ur in _db.SysUserRoles on rp.RoleId equals ur.RoleId
            where ur.UserId == userId && p.IsActive && p.PermissionType == 1
                  && (p.Route == routePrefix || (p.Route != null && p.Route.StartsWith(routePrefix + "/")))
            select p.Id
        ).AnyAsync();
        return matched;
    }

    public async Task<bool> HasPermissionIdAsync(int userId, int permissionId)
    {
        if (permissionId <= 0 || userId <= 0) return false;
        var ids = await GetUserPermissionIdsAsync(userId);
        return ids.Contains(permissionId);
    }

    /// <summary>
    /// 当前请求用户是否有指定权限码：每请求一次性缓存到 Items 字典，
    /// 避免页面渲染时对每个按钮重复查询数据库。
    /// </summary>
    public bool CurrentUserHasCode(IHttpContextAccessor accessor, string code)
    {
        if (accessor?.HttpContext == null || string.IsNullOrEmpty(code)) return false;
        var ctx = accessor.HttpContext;
        var userId = ctx.GetCurrentUserId();
        if (userId <= 0) return false;

        var cacheKey = "__PERM_CODES__";
        if (ctx.Items.TryGetValue(cacheKey, out var cached) && cached is HashSet<string> codes)
        {
            return codes.Contains(code);
        }

        // 第一次访问：同步阻塞查询（页面渲染期，可接受）
        var fresh = GetUserPermissionCodesAsync(userId).GetAwaiter().GetResult();
        ctx.Items[cacheKey] = fresh;
        return fresh.Contains(code);
    }

    public bool CurrentUserHasRoute(IHttpContextAccessor accessor, string routePrefix)
    {
        if (accessor?.HttpContext == null || string.IsNullOrEmpty(routePrefix)) return false;
        var ctx = accessor.HttpContext;
        var userId = ctx.GetCurrentUserId();
        if (userId <= 0) return false;

        var cacheKey = "__PERM_ROUTES__";
        if (ctx.Items.TryGetValue(cacheKey, out var cached) && cached is HashSet<string> routes)
        {
            return routes.Any(r => r == routePrefix || r.StartsWith(routePrefix + "/"));
        }

        var ids = GetUserPermissionIdsAsync(userId).GetAwaiter().GetResult();
        var matchedRoutes = (
            from p in _db.SysPermissions
            where ids.Contains(p.Id) && p.IsActive && p.PermissionType == 1 && p.Route != null
            select p.Route
        ).ToHashSet();
        ctx.Items[cacheKey] = matchedRoutes;
        return matchedRoutes.Any(r => r == routePrefix || r.StartsWith(routePrefix + "/"));
    }
}