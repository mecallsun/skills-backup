using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
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

        // 查询用户角色关联的所有权限
        var menus = await (from perm in _db.SysPermissions
                           join rp in _db.SysRolePermissions on perm.Id equals rp.PermissionId
                           join ur in _db.SysUserRoles on rp.RoleId equals ur.RoleId
                           where ur.UserId == userId && perm.IsActive
                           orderby perm.SortOrder
                           select new AuthHelperExtensions.MenuNode
                           {
                               Id = perm.Id,
                               PermissionCode = perm.PermissionCode,
                               PermissionName = perm.PermissionName,
                               Route = perm.Route,
                               Icon = perm.Icon ?? ""
                           }).Distinct().ToListAsync();

        return menus;
    }
}