using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Services;
using DormManage.Admin.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Account;

/// <summary>
/// 个人中心页面模型（v2.13.26 完整版）
///
/// 功能：
/// - 基本资料：编辑显示名/手机/邮箱
/// - 账号安全：修改密码 + 设置安全问题 + 微信绑定/解绑
/// - 偏好设置：筛选条件持久化
/// </summary>
public class ProfileModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly ISysUserFilterCacheService _cache;
    private readonly ISysUserSelfService _self;

    public ProfileModel(DormDbContext db, ISysUserFilterCacheService cache, ISysUserSelfService self)
    {
        _db = db;
        _cache = cache;
        _self = self;
    }

    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }

    /// <summary>微信绑定状态</summary>
    public bool IsWeChatBound { get; set; }
    public string? WeChatOpenId { get; set; }
    public DateTime? WeChatBindAt { get; set; }
    public string MaskedWeChatOpenId { get; set; } = "";

    /// <summary>前端读取的"存储筛选条件"偏好</summary>
    public bool StoreFilterPreference { get; set; } = false;

    /// <summary>已缓存的筛选模块列表</summary>
    public List<FilterCacheSummary> CachedModules { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var ctx = HttpContext;
        var userId = ctx.GetCurrentUserId();
        if (userId <= 0) return RedirectToPage("/Account/Login");

        var user = await _db.SysUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            UserName = user.UserName ?? "";
            DisplayName = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : user.UserName ?? "";
            Mobile = user.Phone;
            Email = user.Email;
            LastLoginAt = user.LastLoginTime;
            LastLoginIp = user.LastLoginIp;

            IsWeChatBound = !string.IsNullOrEmpty(user.WeChatOpenId);
            WeChatOpenId = user.WeChatOpenId;
            WeChatBindAt = user.WeChatBindAt;
            MaskedWeChatOpenId = MaskOpenId(user.WeChatOpenId);

            var roleIds = await _db.SysUserRoles.AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();
            if (roleIds.Any())
            {
                Roles = await _db.SysRoles.AsNoTracking()
                    .Where(r => roleIds.Contains(r.Id))
                    .Select(r => r.RoleName ?? "")
                    .ToListAsync();
            }
        }

        CachedModules = await _cache.ListAllAsync(userId);

        if (ctx.Request.Cookies.TryGetValue("jinge.storeFilterPreference", out var pref))
        {
            StoreFilterPreference = pref == "true";
        }

        return Page();
    }

    public IActionResult OnPostSetPreferenceAsync([FromForm] bool enabled)
    {
        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(365),
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        };
        Response.Cookies.Append("jinge.storeFilterPreference", enabled ? "true" : "false", cookieOptions);
        return new JsonResult(new { success = true, enabled });
    }

    /// <summary>OpenID 脱敏显示</summary>
    private static string MaskOpenId(string? openId)
    {
        if (string.IsNullOrEmpty(openId)) return "";
        if (openId.Length <= 6) return "***";
        return openId.Substring(0, 4) + "****" + openId.Substring(openId.Length - 4);
    }
}

public class RecentOpItem
{
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}