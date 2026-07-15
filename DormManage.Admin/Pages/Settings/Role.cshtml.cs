using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// 角色管理独立页面（P1-2 + P1-3）
/// 角色列表 + 权限矩阵（从数据库动态加载）
/// </summary>
public class RoleModel : PageModel
{
    private readonly DormDbContext _db;

    public RoleModel(DormDbContext db)
    {
        _db = db;
    }

    public List<RoleViewModel> Roles { get; set; } = new();
    public List<PermissionGroup> PermissionGroups { get; set; } = new();
    public Dictionary<int, HashSet<int>> RolePermissions { get; set; } = new(); // roleId -> permissionIds

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class RoleViewModel
    {
        public int Id { get; set; }
        public string RoleCode { get; set; } = "";
        public string RoleName { get; set; } = "";
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public int UserCount { get; set; }
        public int PermissionCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PermissionGroup
    {
        public string GroupName { get; set; } = "";
        public List<PermissionItem> Items { get; set; } = new();
    }

    public class PermissionItem
    {
        public int Id { get; set; }
        public string PermissionCode { get; set; } = "";
        public string PermissionName { get; set; } = "";
        public byte PermissionType { get; set; }
        public int ParentId { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
        if (TempData["SuccessMessage"] is string s) SuccessMessage = s;
        if (TempData["ErrorMessage"] is string e) ErrorMessage = e;
    }

    public async Task<IActionResult> OnPostCreateAsync(string RoleCode, string RoleName, string Description, int SortOrder)
    {
        if (string.IsNullOrWhiteSpace(RoleCode) || string.IsNullOrWhiteSpace(RoleName))
        {
            TempData["ErrorMessage"] = "角色编码和角色名称为必填项";
            return RedirectToPage();
        }
        if (await _db.SysRoles.AnyAsync(r => r.RoleCode == RoleCode))
        {
            TempData["ErrorMessage"] = $"角色编码 {RoleCode} 已存在";
            return RedirectToPage();
        }

        _db.SysRoles.Add(new SysRole
        {
            RoleCode = RoleCode.Trim(),
            RoleName = RoleName.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            SortOrder = SortOrder,
            IsActive = true,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"角色 {RoleName} 创建成功";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int Id, string RoleName, string Description, int SortOrder, bool IsActive)
    {
        var role = await _db.SysRoles.FindAsync(Id);
        if (role is null)
        {
            TempData["ErrorMessage"] = "角色不存在";
            return RedirectToPage();
        }

        role.RoleName = RoleName.Trim();
        role.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        role.SortOrder = SortOrder;
        role.IsActive = IsActive;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"角色 {role.RoleName} 更新成功";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int Id)
    {
        var role = await _db.SysRoles.FindAsync(Id);
        if (role is null)
        {
            TempData["ErrorMessage"] = "角色不存在";
            return RedirectToPage();
        }
        if (role.RoleCode == "admin")
        {
            TempData["ErrorMessage"] = "内置 admin 角色不允许删除";
            return RedirectToPage();
        }

        // 检查是否有用户使用此角色
        var hasUsers = await _db.SysUserRoles.AnyAsync(ur => ur.RoleId == Id);
        if (hasUsers)
        {
            TempData["ErrorMessage"] = $"角色 {role.RoleName} 仍被用户引用，请先解除关联";
            return RedirectToPage();
        }

        // 级联删除：先移除角色-权限关联
        var rps = _db.SysRolePermissions.Where(rp => rp.RoleId == Id);
        _db.SysRolePermissions.RemoveRange(rps);
        _db.SysRoles.Remove(role);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"角色 {role.RoleName} 已删除";
        return RedirectToPage();
    }

    /// <summary>
    /// 批量更新角色权限矩阵（P1-3 动态加载的核心端点）
    /// </summary>
    public async Task<IActionResult> OnPostSavePermissionsAsync(int RoleId, int[] PermissionIds)
    {
        var role = await _db.SysRoles.FindAsync(RoleId);
        if (role is null)
        {
            TempData["ErrorMessage"] = "角色不存在";
            return RedirectToPage();
        }

        // 删除旧关联
        var oldRps = _db.SysRolePermissions.Where(rp => rp.RoleId == RoleId);
        _db.SysRolePermissions.RemoveRange(oldRps);

        // 添加新关联
        if (PermissionIds?.Length > 0)
        {
            foreach (var pid in PermissionIds.Distinct())
            {
                _db.SysRolePermissions.Add(new SysRolePermission
                {
                    RoleId = RoleId,
                    PermissionId = pid,
                    CreatedAt = DateTime.Now
                });
            }
        }
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"角色 {role.RoleName} 的权限已更新（{PermissionIds?.Length ?? 0} 项）";
        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        var roles = await _db.SysRoles.OrderBy(r => r.SortOrder).ToListAsync();
        var userCounts = await _db.SysUserRoles.GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() }).ToListAsync();
        var rolePerms = await _db.SysRolePermissions.ToListAsync();
        var allPerms = await _db.SysPermissions.OrderBy(p => p.SortOrder).ToListAsync();

        Roles = roles.Select(r => new RoleViewModel
        {
            Id = r.Id,
            RoleCode = r.RoleCode,
            RoleName = r.RoleName,
            Description = r.Description,
            SortOrder = r.SortOrder,
            IsActive = r.IsActive,
            UserCount = userCounts.FirstOrDefault(uc => uc.RoleId == r.Id)?.Count ?? 0,
            PermissionCount = rolePerms.Count(rp => rp.RoleId == r.Id),
            CreatedAt = r.CreatedAt
        }).ToList();

        foreach (var rid in roles.Select(r => r.Id))
        {
            RolePermissions[rid] = rolePerms.Where(rp => rp.RoleId == rid).Select(rp => rp.PermissionId).ToHashSet();
        }

        // 分组：按 ParentId 为 0 的顶级权限分组，子权限归入对应顶级
        var topPerms = allPerms.Where(p => p.ParentId == 0).ToList();
        foreach (var top in topPerms)
        {
            var group = new PermissionGroup { GroupName = top.PermissionName };
            group.Items.Add(new PermissionItem
            {
                Id = top.Id,
                PermissionCode = top.PermissionCode,
                PermissionName = top.PermissionName,
                PermissionType = top.PermissionType,
                ParentId = 0
            });
            var children = allPerms.Where(p => p.ParentId == top.Id).ToList();
            foreach (var c in children)
            {
                group.Items.Add(new PermissionItem
                {
                    Id = c.Id,
                    PermissionCode = c.PermissionCode,
                    PermissionName = "└ " + c.PermissionName,
                    PermissionType = c.PermissionType,
                    ParentId = c.ParentId
                });
            }
            PermissionGroups.Add(group);
        }
    }
}