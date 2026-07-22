using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// v2.13.67 用户管理 Tab — 完整 CRUD 嵌入 Settings/Index
/// 原 /Settings/User 独立页面已合并为 Settings 的子 Tab，本类为 IndexModel 的 partial class。
///
/// 字段（User 列表 + 筛选分页 + 角色下拉）：
/// - UserSearch / UserRoleId / UserStatus / UserPageIndex / UserPageSize
/// - UserTotalCount / UserTotalPages / UserUsers / UserAvailableRoles
///
/// Handler（命名加 User 前缀，避免与其他 handler 冲突）：
/// - OnPostUserCreateAsync(string UserName, string DisplayName, string Password, string Email, string Phone, int[] SelectedRoleIds)
/// - OnPostUserUpdateAsync(int Id, string DisplayName, string Email, string Phone, bool IsActive, int[] SelectedRoleIds)
/// - OnPostUserResetPasswordAsync(int Id, string NewPassword)
/// - OnPostUserDeleteAsync(int Id)
///
/// URL 调用：/Settings?handler=UserCreate 等。
/// </summary>
public partial class IndexModel
{
    // ====================== 用户管理子 Tab 字段 ======================

    [BindProperty(SupportsGet = true, Name = "uSearch")]
    public string? UserSearch { get; set; }

    [BindProperty(SupportsGet = true, Name = "uRoleId")]
    public int? UserRoleId { get; set; }

    [BindProperty(SupportsGet = true, Name = "uStatus")]
    public string? UserStatus { get; set; }

    /// <summary>v2.13.74 BUG 修复：使用标准 pageIndex / pageSize（与 _PaginationPartial 一致）</summary>
    [BindProperty(SupportsGet = true, Name = "uPage")]
    public int UserPageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "pageIndex")]
    public int? PageIndexAlias
    {
        get => UserPageIndex;
        set { if (value.HasValue) UserPageIndex = value.Value; }
    }

    [BindProperty(SupportsGet = true, Name = "uSize")]
    public int UserPageSize { get; set; } = 10;

    [BindProperty(SupportsGet = true, Name = "pageSize")]
    public int? PageSizeAlias
    {
        get => UserPageSize;
        set { if (value.HasValue && value.Value > 0) UserPageSize = value.Value; }
    }

    public int UserTotalCount { get; set; }
    public int UserTotalPages => Math.Max(1, (int)Math.Ceiling(UserTotalCount / (double)UserPageSize));

    public List<UserViewModel> UserUsers { get; set; } = new();
    public List<RoleOption> UserAvailableRoles { get; set; } = new();
    public string? UserErrorMessage { get; set; }
    public string? UserSuccessMessage { get; set; }

    public class UserViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string RoleNames { get; set; } = "";
        public string RoleIds { get; set; } = "";
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public string? LastLoginIp { get; set; }
        public DateTime CreatedAt { get; set; }
        /// <summary>v2.13.93 新增：账号有效期至（NULL = 永久）</summary>
        public DateTime? ExpiresAt { get; set; }
    }

    public class RoleOption
    {
        public int Id { get; set; }
        public string RoleCode { get; set; } = "";
        public string RoleName { get; set; } = "";
    }

    /// <summary>
    /// 加载用户管理子 Tab 数据（供 OnGetAsync 调用）
    /// </summary>
    public async Task LoadUserPanelAsync()
    {
        var allRoles = await _db.SysRoles.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync();
        UserAvailableRoles = allRoles.Select(r => new RoleOption { Id = r.Id, RoleCode = r.RoleCode, RoleName = r.RoleName }).ToList();

        var query = _db.SysUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(UserSearch))
        {
            var kw = UserSearch.Trim();
            query = query.Where(u => u.UserName.Contains(kw) || u.DisplayName.Contains(kw));
        }

        if (UserStatus == "active") query = query.Where(u => u.IsActive && !u.IsLocked);
        else if (UserStatus == "disabled") query = query.Where(u => !u.IsActive);
        else if (UserStatus == "locked") query = query.Where(u => u.IsLocked);

        var allUsers = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var userRoles = await _db.SysUserRoles.ToListAsync();

        if (UserRoleId.HasValue)
        {
            var userIdsInRole = userRoles.Where(ur => ur.RoleId == UserRoleId.Value).Select(ur => ur.UserId).ToHashSet();
            allUsers = allUsers.Where(u => userIdsInRole.Contains(u.Id)).ToList();
        }

        UserTotalCount = allUsers.Count;
        var pagedUsers = allUsers.Skip((UserPageIndex - 1) * UserPageSize).Take(UserPageSize).ToList();

        UserUsers = pagedUsers.Select(u =>
        {
            var userRoleIds = userRoles.Where(ur => ur.UserId == u.Id).Select(ur => ur.RoleId).ToList();
            return new UserViewModel
            {
                Id = u.Id,
                UserName = u.UserName,
                DisplayName = u.DisplayName,
                Email = u.Email ?? "",
                Phone = u.Phone ?? "",
                RoleNames = string.Join(", ", userRoleIds.Join(allRoles, rid => rid, r => r.Id, (_, r) => r.RoleName)),
                RoleIds = string.Join(",", userRoleIds),
                IsActive = u.IsActive,
                IsLocked = u.IsLocked,
                LastLoginTime = u.LastLoginTime,
                LastLoginIp = u.LastLoginIp,
                CreatedAt = u.CreatedAt,
                // v2.13.93 新增：账号有效期至
                ExpiresAt = u.ExpiresAt
            };
        }).ToList();
    }

    // ====================== 用户管理 Handler（v2.13.65 + v2.13.66 同模式） ======================

    public async Task<IActionResult> OnPostUserCreateAsync(string UserName, string DisplayName, string Password, string Email, string Phone, int[] SelectedRoleIds, string? ExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(DisplayName))
            return new JsonResult(new { success = false, message = "用户名、姓名、密码为必填项" });

        if (await _db.SysUsers.AnyAsync(u => u.UserName == UserName))
            return new JsonResult(new { success = false, message = $"用户名 {UserName} 已存在" });

        // v2.13.93 新增：解析有效期至（datetime-local 提交格式 yyyy-MM-ddTHH:mm）
        DateTime? expiresAtValue = null;
        if (!string.IsNullOrWhiteSpace(ExpiresAt) && DateTime.TryParse(ExpiresAt, out var parsed))
            expiresAtValue = parsed;

        var user = new SysUser
        {
            UserName = UserName.Trim(),
            DisplayName = DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            IsActive = true,
            IsLocked = false,
            FailedLoginCount = 0,
            CreatedAt = DateTime.Now,
            // v2.13.93 新增：账号有效期至
            ExpiresAt = expiresAtValue
        };
        _db.SysUsers.Add(user);
        await _db.SaveChangesAsync();

        if (SelectedRoleIds?.Length > 0)
        {
            foreach (var rid in SelectedRoleIds.Distinct())
                _db.SysUserRoles.Add(new SysUserRole { UserId = user.Id, RoleId = rid });
            await _db.SaveChangesAsync();
        }

        return new JsonResult(new { success = true, message = $"用户 {UserName} 创建成功", userId = user.Id });
    }

    public async Task<IActionResult> OnPostUserUpdateAsync(int Id, string DisplayName, string Email, string Phone, bool IsActive, int[] SelectedRoleIds, string? ExpiresAt)
    {
        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
            return new JsonResult(new { success = false, message = "用户不存在" });

        user.DisplayName = DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        user.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        user.IsActive = IsActive;
        // v2.13.93 新增：账号有效期至（空字符串视为清空；否则按 datetime-local 解析）
        if (string.IsNullOrWhiteSpace(ExpiresAt))
            user.ExpiresAt = null;
        else if (DateTime.TryParse(ExpiresAt, out var parsed))
            user.ExpiresAt = parsed;
        user.UpdatedAt = DateTime.Now;

        var oldRoles = _db.SysUserRoles.Where(ur => ur.UserId == Id);
        _db.SysUserRoles.RemoveRange(oldRoles);
        if (SelectedRoleIds?.Length > 0)
        {
            foreach (var rid in SelectedRoleIds.Distinct())
                _db.SysUserRoles.Add(new SysUserRole { UserId = Id, RoleId = rid });
        }
        await _db.SaveChangesAsync();

        return new JsonResult(new { success = true, message = $"用户 {user.UserName} 更新成功" });
    }

    public async Task<IActionResult> OnPostUserResetPasswordAsync(int Id, string NewPassword)
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
            return new JsonResult(new { success = false, message = "密码长度至少 6 位" });

        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
            return new JsonResult(new { success = false, message = "用户不存在" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
        user.FailedLoginCount = 0;
        user.IsLocked = false;
        user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return new JsonResult(new { success = true, message = $"用户 {user.UserName} 密码已重置" });
    }

    public async Task<IActionResult> OnPostUserDeleteAsync(int Id)
    {
        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
            return new JsonResult(new { success = false, message = "用户不存在" });
        if (user.UserName == "admin")
            return new JsonResult(new { success = false, message = "内置 admin 账号不允许删除" });

        try
        {
            await _db.SysUserRoles.Where(ur => ur.UserId == Id).ExecuteDeleteAsync();
            await _db.SysUserSecurityQuestions.Where(sq => sq.UserId == Id).ExecuteDeleteAsync();
            await _db.SysOpLogs.Where(l => l.UserId == Id).ExecuteDeleteAsync();
            await _db.SysUserFilterCaches.Where(c => c.UserId == Id).ExecuteDeleteAsync();
            await _db.SysUsers.Where(u => u.Id == Id).ExecuteDeleteAsync();
            return new JsonResult(new { success = true, message = $"用户 {user.UserName} 已删除" });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = $"删除失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 为 User 子 Tab 构建分页 URL（与 Index 当前 tab 保持一致）
    /// </summary>
    public string BuildUserPageUrl(int pageIndex)
    {
        var query = new List<string> { "tab=users" };
        if (!string.IsNullOrEmpty(UserSearch)) query.Add($"uSearch={Uri.EscapeDataString(UserSearch)}");
        if (UserRoleId.HasValue) query.Add($"uRoleId={UserRoleId}");
        if (!string.IsNullOrEmpty(UserStatus)) query.Add($"uStatus={Uri.EscapeDataString(UserStatus)}");
        query.Add($"uPage={pageIndex}");
        query.Add($"uSize={UserPageSize}");
        return "/Settings?" + string.Join("&", query);
    }
}