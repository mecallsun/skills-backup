using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Services;
using DormManage.Admin.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Account;

/// <summary>
/// 个人中心页面模型（v2.13.12 新增）
///
/// 功能：
///   1. 显示当前用户基本信息（用户名/显示名/角色/手机/最近登录）
///   2. "存储筛选条件到云端"开关（前端 localStorage + 后端 SysUserFilterCache）
///   3. 已缓存的筛选模块列表 + 单个/全部清除
///   4. 最近操作记录（前 20 条 SysOpLog）
/// </summary>
public class ProfileModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly ISysUserFilterCacheService _cache;

    public ProfileModel(DormDbContext db, ISysUserFilterCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public string? Mobile { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>前端读取的"存储筛选条件"偏好（从 cookie 或 localStorage 同步）</summary>
    public bool StoreFilterPreference { get; set; } = false;

    /// <summary>已缓存的筛选模块列表</summary>
    public List<FilterCacheSummary> CachedModules { get; set; } = new();

    /// <summary>最近操作记录（前 20 条）</summary>
    public List<RecentOpItem> RecentOps { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var ctx = HttpContext;
        var userId = ctx.GetCurrentUserId();
        if (userId <= 0) return RedirectToPage("/Account/Login");

        // 加载当前用户信息
        var user = await _db.SysUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            UserName = user.UserName ?? "";
            DisplayName = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : user.UserName ?? "";
            Mobile = user.Phone;
            LastLoginAt = user.LastLoginTime;

            // 加载用户角色
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

        // 加载已缓存的筛选模块
        CachedModules = await _cache.ListAllAsync(userId);

        // 读取"存储筛选条件"偏好（从 cookie 读）
        if (ctx.Request.Cookies.TryGetValue("jinge.storeFilterPreference", out var pref))
        {
            StoreFilterPreference = pref == "true";
        }
        else
        {
            StoreFilterPreference = false;
        }

        // 加载最近操作记录（SysOpLog 实体当前仅有 Id/CreatedAt 字段，待完整化后启用）

        return Page();
    }

    /// <summary>更新"存储筛选条件"偏好（持久化到 cookie）</summary>
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
}

/// <summary>最近操作项（个人中心展示用）</summary>
public class RecentOpItem
{
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}