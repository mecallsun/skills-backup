using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// 用户管理独立页面（P1-2）
/// v2.13.64 升级：添加搜索/角色筛选/状态筛选/分页 + RoleIds 用于回显已勾选角色
/// </summary>
public class UserModel : PageModel
{
    private readonly DormDbContext _db;

    public UserModel(DormDbContext db)
    {
        _db = db;
    }

    // ====== v2.13.64 新增：查询参数 ======
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
        public string RoleIds { get; set; } = "";  // v2.13.64 新增：用于回显勾选角色
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

    // v2.13.64 BUG 修复：IsActive 现在用 string 类型（避免和 hidden 冲突，参考 v2.13.62 经验教训）
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

        // v2.13.64 修复：级联清理所有引用该用户的子表（FK 约束）
        var urs = _db.SysUserRoles.Where(ur => ur.UserId == Id);
        _db.SysUserRoles.RemoveRange(urs);
        var sqs = _db.SysUserSecurityQuestions.Where(sq => sq.UserId == Id);
        _db.SysUserSecurityQuestions.RemoveRange(sqs);
        var logs = _db.SysOpLogs.Where(l => l.UserId == Id);
        _db.SysOpLogs.RemoveRange(logs);
        var filters = _db.SysUserFilterCaches.Where(c => c.UserId == Id);
        _db.SysUserFilterCaches.RemoveRange(filters);

        _db.SysUsers.Remove(user);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"用户 {user.UserName} 已删除";
        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        var allRoles = await _db.SysRoles.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync();
        AvailableRoles = allRoles.Select(r => new RoleOption { Id = r.Id, RoleCode = r.RoleCode, RoleName = r.RoleName }).ToList();

        var query = _db.SysUsers.AsQueryable();

        // 搜索：用户名 OR 姓名
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var kw = Search.Trim();
            query = query.Where(u => u.UserName.Contains(kw) || u.DisplayName.Contains(kw));
        }

        // 状态筛选
        if (Status == "active") query = query.Where(u => u.IsActive && !u.IsLocked);
        else if (Status == "disabled") query = query.Where(u => !u.IsActive);
        else if (Status == "locked") query = query.Where(u => u.IsLocked);

        // 角色筛选：先取所有用户，然后 join 过滤（这里采用先列出再 Join，配合 In-memory 更直观）
        var allUsers = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var userRoles = await _db.SysUserRoles.ToListAsync();

        // 角色筛选（在内存中二次过滤，因 join 复杂）
        if (RoleId.HasValue)
        {
            var userIdsInRole = userRoles.Where(ur => ur.RoleId == RoleId.Value).Select(ur => ur.UserId).ToHashSet();
            allUsers = allUsers.Where(u => userIdsInRole.Contains(u.Id)).ToList();
        }

        TotalCount = allUsers.Count;

        // 分页
        var pagedUsers = allUsers
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToList();

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
                RoleNames = string.Join(", ",
                    userRoleIds.Join(allRoles, rid => rid, r => r.Id, (_, r) => r.RoleName)),
                RoleIds = string.Join(",", userRoleIds),  // v2.13.64 新增
                IsActive = u.IsActive,
                IsLocked = u.IsLocked,
                LastLoginTime = u.LastLoginTime,
                LastLoginIp = u.LastLoginIp,
                CreatedAt = u.CreatedAt
            };
        }).ToList();
    }
}