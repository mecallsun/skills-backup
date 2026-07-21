using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// 用户管理独立页面
/// v2.13.65 改造：PageModel handler 改为返回 JSON (ApiResponse 风格) 而非 RedirectToPage，
/// 让前端 JS fetch 能正确判断成功/失败并显示 feedback；GET 仍然走 OnGetAsync 返回 Page。
/// </summary>
public class UserModel : PageModel
{
    private readonly DormDbContext _db;

    public UserModel(DormDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? RoleId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public List<UserViewModel> Users { get; set; } = new();
    public List<RoleOption> AvailableRoles { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

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
    }

    public class RoleOption
    {
        public int Id { get; set; }
        public string RoleCode { get; set; } = "";
        public string RoleName { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
        if (TempData["SuccessMessage"] is string s) SuccessMessage = s;
        if (TempData["ErrorMessage"] is string e) ErrorMessage = e;
    }

    // v2.13.65：handler 改为返回 JSON，便于 AJAX 接收；前端 createUserForm.fetch → 解析 JSON → 显示 feedback
    public async Task<IActionResult> OnPostCreateAsync(string UserName, string DisplayName, string Password, string Email, string Phone, int[] SelectedRoleIds)
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(DisplayName))
            return new JsonResult(new { success = false, message = "用户名、姓名、密码为必填项" });

        if (await _db.SysUsers.AnyAsync(u => u.UserName == UserName))
            return new JsonResult(new { success = false, message = $"用户名 {UserName} 已存在" });

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
            CreatedAt = DateTime.Now
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

    public async Task<IActionResult> OnPostUpdateAsync(int Id, string DisplayName, string Email, string Phone, bool IsActive, int[] SelectedRoleIds)
    {
        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
            return new JsonResult(new { success = false, message = "用户不存在" });

        user.DisplayName = DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        user.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        user.IsActive = IsActive;
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

    public async Task<IActionResult> OnPostResetPasswordAsync(int Id, string NewPassword)
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

    public async Task<IActionResult> OnPostDeleteAsync(int Id)
    {
        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
            return new JsonResult(new { success = false, message = "用户不存在" });
        if (user.UserName == "admin")
            return new JsonResult(new { success = false, message = "内置 admin 账号不允许删除" });

        // v2.13.65 修复：使用 ExecuteDeleteAsync 避免 EF Core 并发检查（DbUpdateConcurrencyException）
        // EF Core 在 Remove + SaveChangesAsync 时会携带原值做 WHERE，并发时 0 rows affected 抛异常
        // ExecuteDeleteAsync 是 SQL DELETE 直发，无并发检查
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

    private async Task LoadDataAsync()
    {
        var allRoles = await _db.SysRoles.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync();
        AvailableRoles = allRoles.Select(r => new RoleOption { Id = r.Id, RoleCode = r.RoleCode, RoleName = r.RoleName }).ToList();

        var query = _db.SysUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var kw = Search.Trim();
            query = query.Where(u => u.UserName.Contains(kw) || u.DisplayName.Contains(kw));
        }

        if (Status == "active") query = query.Where(u => u.IsActive && !u.IsLocked);
        else if (Status == "disabled") query = query.Where(u => !u.IsActive);
        else if (Status == "locked") query = query.Where(u => u.IsLocked);

        var allUsers = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var userRoles = await _db.SysUserRoles.ToListAsync();

        if (RoleId.HasValue)
        {
            var userIdsInRole = userRoles.Where(ur => ur.RoleId == RoleId.Value).Select(ur => ur.UserId).ToHashSet();
            allUsers = allUsers.Where(u => userIdsInRole.Contains(u.Id)).ToList();
        }

        TotalCount = allUsers.Count;
        var pagedUsers = allUsers.Skip((PageIndex - 1) * PageSize).Take(PageSize).ToList();

        Users = pagedUsers.Select(u =>
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
                CreatedAt = u.CreatedAt
            };
        }).ToList();
    }
}