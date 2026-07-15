using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// 用户管理独立页面（P1-2）
/// 从 Settings Index 拆分，提供完整的用户 CRUD
/// </summary>
public class UserModel : PageModel
{
    private readonly DormDbContext _db;

    public UserModel(DormDbContext db)
    {
        _db = db;
    }

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

    public async Task<IActionResult> OnPostCreateAsync(string UserName, string DisplayName, string Password, string Email, string Phone, int[] SelectedRoleIds)
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(DisplayName))
        {
            TempData["ErrorMessage"] = "用户名、姓名、密码为必填项";
            return RedirectToPage();
        }

        if (await _db.SysUsers.AnyAsync(u => u.UserName == UserName))
        {
            TempData["ErrorMessage"] = $"用户名 {UserName} 已存在";
            return RedirectToPage();
        }

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
            {
                _db.SysUserRoles.Add(new SysUserRole { UserId = user.Id, RoleId = rid });
            }
            await _db.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = $"用户 {UserName} 创建成功";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int Id, string DisplayName, string Email, string Phone, bool IsActive, int[] SelectedRoleIds)
    {
        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "用户不存在";
            return RedirectToPage();
        }

        user.DisplayName = DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        user.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        user.IsActive = IsActive;
        user.UpdatedAt = DateTime.Now;

        // 替换用户-角色关联
        var oldRoles = _db.SysUserRoles.Where(ur => ur.UserId == Id);
        _db.SysUserRoles.RemoveRange(oldRoles);
        if (SelectedRoleIds?.Length > 0)
        {
            foreach (var rid in SelectedRoleIds.Distinct())
            {
                _db.SysUserRoles.Add(new SysUserRole { UserId = Id, RoleId = rid });
            }
        }
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"用户 {user.UserName} 更新成功";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(int Id, string NewPassword)
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "密码长度至少 6 位";
            return RedirectToPage();
        }

        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "用户不存在";
            return RedirectToPage();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
        user.FailedLoginCount = 0;
        user.IsLocked = false;
        user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"用户 {user.UserName} 密码已重置";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int Id)
    {
        var user = await _db.SysUsers.FindAsync(Id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "用户不存在";
            return RedirectToPage();
        }
        if (user.UserName == "admin")
        {
            TempData["ErrorMessage"] = "内置 admin 账号不允许删除";
            return RedirectToPage();
        }

        // 级联删除：先移除用户-角色关联
        var urs = _db.SysUserRoles.Where(ur => ur.UserId == Id);
        _db.SysUserRoles.RemoveRange(urs);
        _db.SysUsers.Remove(user);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"用户 {user.UserName} 已删除";
        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        var users = await _db.SysUsers.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var userRoles = await _db.SysUserRoles.ToListAsync();
        var roles = await _db.SysRoles.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync();

        Users = users.Select(u => new UserViewModel
        {
            Id = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Email = u.Email ?? "",
            Phone = u.Phone ?? "",
            RoleNames = string.Join(", ",
                userRoles.Where(ur => ur.UserId == u.Id)
                    .Join(roles, ur => ur.RoleId, r => r.Id, (_, r) => r.RoleName)),
            IsActive = u.IsActive,
            IsLocked = u.IsLocked,
            LastLoginTime = u.LastLoginTime,
            LastLoginIp = u.LastLoginIp,
            CreatedAt = u.CreatedAt
        }).ToList();

        AvailableRoles = roles.Select(r => new RoleOption { Id = r.Id, RoleCode = r.RoleCode, RoleName = r.RoleName }).ToList();
    }
}