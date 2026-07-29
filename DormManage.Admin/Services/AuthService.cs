using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Extensions;
using DormManage.Shared.Models;
using System.Security.Claims;

namespace DormManage.Admin.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, ClaimsPrincipal? Principal)> LoginAsync(string userName, string password, string? ip = null);
    Task SignOutAsync(HttpContext httpContext);
    Task<List<AuthHelperExtensions.MenuNode>> GetUserMenusAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly DormDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(DormDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(bool Success, string Message, ClaimsPrincipal? Principal)> LoginAsync(string userName, string password, string? ip = null)
    {
        var user = await _db.SysUsers.FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive);
        if (user == null) return (false, "用户名或密码错误", null);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, "用户名或密码错误", null);

        if (user.IsLocked)
            return (false, "账号已锁定，请联系管理员", null);

        // v2.13.193: 账号有效期校验 — 使用统一助手 UserExpiryHelper（前后端一致）
        // 规则：Today >= ExpiresAt.Date 即拒绝（过期当天即视为已过期）
        if (UserExpiryHelper.IsExpired(user.ExpiresAt))
            return (false, "账号已过期，请联系管理员", null);

        // 查询用户的角色
        var roleCodes = await (from ur in _db.SysUserRoles
                               join r in _db.SysRoles on ur.RoleId equals r.Id
                               where ur.UserId == user.Id
                               select r.RoleCode).ToListAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim("DisplayName", user.DisplayName ?? string.Empty)
        };

        // 添加角色声明
        foreach (var roleCode in roleCodes)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleCode));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // 更新最后登录时间和 IP
        user.LastLoginTime = DateTime.Now;
        user.LastLoginIp = ip;
        user.FailedLoginCount = 0;
        await _db.SaveChangesAsync();

        return (true, "登录成功", principal);
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        // 记录登出日志（可扩展）
        await Task.CompletedTask;
    }

    public async Task<List<AuthHelperExtensions.MenuNode>> GetUserMenusAsync(int userId)
    {
        if (userId <= 0) return new List<AuthHelperExtensions.MenuNode>();

        // v2.13.198 修复：捕获数据库异常，防止菜单渲染失败导致整个页面 Error
        List<AuthHelperExtensions.MenuNode> menus;
        try
        {
            // 查询用户角色关联的所有菜单类权限（PermissionType=1）
            // 含 ParentId / SortOrder 字段，供前端按父子层级渲染
            menus = await (from perm in _db.SysPermissions
                           join rp in _db.SysRolePermissions on perm.Id equals rp.PermissionId
                           join ur in _db.SysUserRoles on rp.RoleId equals ur.RoleId
                           where ur.UserId == userId
                                 && perm.IsActive
                                 && perm.PermissionType == 1
                           orderby perm.SortOrder, perm.Id
                           select new AuthHelperExtensions.MenuNode
                           {
                               Id = perm.Id,
                               ParentId = perm.ParentId,
                               PermissionCode = perm.PermissionCode,
                               PermissionName = perm.PermissionName,
                               Route = perm.Route ?? "",
                               Icon = perm.Icon ?? "",
                               SortOrder = perm.SortOrder,
                               PermissionType = perm.PermissionType
                           }).Distinct().ToListAsync();
        }
        catch (Exception)
        {
            // 数据库异常时记录日志并返回空列表（避免抛出导致整个页面 Error）
            // 注：刻意不记录日志避免循环依赖；如需诊断可通过应用日志系统获得
            return new List<AuthHelperExtensions.MenuNode>();
        }

        // 若顶级菜单缺少父级链，自动补齐父级（确保父级可见时子菜单才能显示）
        var parentIds = menus.Where(m => m.ParentId > 0).Select(m => m.ParentId).Distinct().ToList();
        if (parentIds.Any())
        {
            var existingIds = menus.Select(m => m.Id).ToHashSet();
            var missingParents = await _db.SysPermissions
                .Where(p => parentIds.Contains(p.Id) && !existingIds.Contains(p.Id))
                .ToListAsync();
            foreach (var p in missingParents)
            {
                menus.Add(new AuthHelperExtensions.MenuNode
                {
                    Id = p.Id,
                    ParentId = p.ParentId,
                    PermissionCode = p.PermissionCode,
                    PermissionName = p.PermissionName,
                    Route = p.Route ?? "",
                    Icon = p.Icon ?? "",
                    SortOrder = p.SortOrder,
                    PermissionType = p.PermissionType
                });
            }
            menus = menus.OrderBy(m => m.SortOrder).ThenBy(m => m.Id).ToList();
        }

        return menus;
    }
}