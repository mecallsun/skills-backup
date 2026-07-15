using DormManage.Admin.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
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
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return (false, "用户名或密码错误", null);
        if (user.IsLocked) return (false, "账号已锁定", null);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("DisplayName", user.DisplayName ?? string.Empty)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return (true, "登录成功", new ClaimsPrincipal(identity));
    }
    public Task SignOutAsync(HttpContext httpContext) => Task.CompletedTask;
    public Task<List<AuthHelperExtensions.MenuNode>> GetUserMenusAsync(int userId) =>
        Task.FromResult(new List<AuthHelperExtensions.MenuNode>());
}
