using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// v2.13.67 角色管理 Tab — 完整 CRUD 嵌入 Settings/Index
/// 原 /Settings/Role 独立页面已合并为 Settings 的子 Tab，本类为 IndexModel 的 partial class。
///
/// 字段（角色列表 + 权限矩阵）：
/// - Roles / PermissionGroups / RolePermissions (roleId -> permissionIds)
///
/// Handler（命名加 Role 前缀）：
/// - OnPostRoleCreateAsync(string RoleCode, string RoleName, string Description, int SortOrder)
/// - OnPostRoleUpdateAsync(int Id, string RoleName, string Description, int SortOrder, bool IsActive)
/// - OnPostRoleDeleteAsync(int Id)
/// - OnPostRoleSavePermissionsAsync(int RoleId, int[] PermissionIds)
///
/// URL 调用：/Settings?handler=RoleCreate 等。
/// </summary>
public partial class IndexModel
{
    // ====================== 角色管理子 Tab 字段 ======================

    public List<RoleListViewModel> Roles { get; set; } = new();
    public List<PermissionGroupViewModel> PermissionGroups { get; set; } = new();
    public Dictionary<int, HashSet<int>> RolePermissions { get; set; } = new();

    public class RoleListViewModel
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

    public class PermissionGroupViewModel
    {
        public string GroupName { get; set; } = "";
        public List<PermissionItemViewModel> Items { get; set; } = new();
    }

    public class PermissionItemViewModel
    {
        public int Id { get; set; }
        public string PermissionCode { get; set; } = "";
        public string PermissionName { get; set; } = "";
        public byte PermissionType { get; set; }
        public int ParentId { get; set; }
    }

    /// <summary>
    /// 加载角色管理子 Tab 数据（供 OnGetAsync 调用）
    /// </summary>
    public async Task LoadRolePanelAsync()
    {
        var roles = await _db.SysRoles.OrderBy(r => r.SortOrder).ToListAsync();
        var userCounts = await _db.SysUserRoles.GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() }).ToListAsync();
        var rolePerms = await _db.SysRolePermissions.ToListAsync();
        var allPerms = await _db.SysPermissions.OrderBy(p => p.SortOrder).ToListAsync();

        // v2.13.92 加载字段权限相关上下文（RolePrivacyFieldEnabled 供 permMatrixModal 渲染）
        var privacyPermId = allPerms.FirstOrDefault(p => p.PermissionCode == "privacy:field:enable")?.Id;
        var privacyRoleIds = privacyPermId.HasValue
            ? rolePerms.Where(rp => rp.PermissionId == privacyPermId.Value).Select(rp => rp.RoleId).ToHashSet()
            : new HashSet<int>();

        // 默认从第一行的角色 ID 取（页面渲染时会随 permMatrixModal 打开时覆盖）
        var firstRid = roles.FirstOrDefault()?.Id ?? 0;
        RolePrivacyFieldEnabled = firstRid > 0 && privacyRoleIds.Contains(firstRid);

        Roles = roles.Select(r => new RoleListViewModel
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

        var topPerms = allPerms.Where(p => p.ParentId == 0).ToList();
        foreach (var top in topPerms)
        {
            var group = new PermissionGroupViewModel { GroupName = top.PermissionName };
            group.Items.Add(new PermissionItemViewModel
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
                group.Items.Add(new PermissionItemViewModel
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

    // ====================== 角色管理 Handler ======================

    public async Task<IActionResult> OnPostRoleCreateAsync(string RoleCode, string RoleName, string Description, int SortOrder)
    {
        if (string.IsNullOrWhiteSpace(RoleCode) || string.IsNullOrWhiteSpace(RoleName))
            return new JsonResult(new { success = false, message = "角色编码和角色名称为必填项" });

        if (await _db.SysRoles.AnyAsync(r => r.RoleCode == RoleCode))
            return new JsonResult(new { success = false, message = $"角色编码 {RoleCode} 已存在" });

        var role = new SysRole
        {
            RoleCode = RoleCode.Trim(),
            RoleName = RoleName.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            SortOrder = SortOrder,
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        _db.SysRoles.Add(role);
        await _db.SaveChangesAsync();

        return new JsonResult(new { success = true, message = $"角色 {RoleName} 创建成功", roleId = role.Id });
    }

    public async Task<IActionResult> OnPostRoleUpdateAsync(int Id, string RoleName, string Description, int SortOrder, bool IsActive)
    {
        var role = await _db.SysRoles.FindAsync(Id);
        if (role is null)
            return new JsonResult(new { success = false, message = "角色不存在" });

        role.RoleName = RoleName.Trim();
        role.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        role.SortOrder = SortOrder;
        role.IsActive = IsActive;
        await _db.SaveChangesAsync();

        return new JsonResult(new { success = true, message = $"角色 {role.RoleName} 更新成功" });
    }

    public async Task<IActionResult> OnPostRoleDeleteAsync(int Id)
    {
        var role = await _db.SysRoles.FindAsync(Id);
        if (role is null)
            return new JsonResult(new { success = false, message = "角色不存在" });
        if (role.RoleCode == "admin")
            return new JsonResult(new { success = false, message = "内置 admin 角色不允许删除" });

        var hasUsers = await _db.SysUserRoles.AnyAsync(ur => ur.RoleId == Id);
        if (hasUsers)
            return new JsonResult(new { success = false, message = $"角色 {role.RoleName} 仍被用户引用，请先解除关联" });

        var rps = _db.SysRolePermissions.Where(rp => rp.RoleId == Id);
        _db.SysRolePermissions.RemoveRange(rps);
        _db.SysRoles.Remove(role);
        await _db.SaveChangesAsync();

        return new JsonResult(new { success = true, message = $"角色 {role.RoleName} 已删除" });
    }

    public async Task<IActionResult> OnPostRoleSavePermissionsAsync(int RoleId, int[] PermissionIds, bool PrivacyFieldEnabled = false)
    {
        var role = await _db.SysRoles.FindAsync(RoleId);
        if (role is null)
            return new JsonResult(new { success = false, message = "角色不存在" });

        var oldRps = _db.SysRolePermissions.Where(rp => rp.RoleId == RoleId);
        _db.SysRolePermissions.RemoveRange(oldRps);

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

        // v2.13.92 字段权限：单独处理 privacy:field:enable 权限（数据权限 PermissionType=3）
        var privacyPerm = await _db.SysPermissions.FirstOrDefaultAsync(p => p.PermissionCode == "privacy:field:enable");
        if (privacyPerm != null)
        {
            var hasPrivacy = await _db.SysRolePermissions.AnyAsync(rp => rp.RoleId == RoleId && rp.PermissionId == privacyPerm.Id);
            if (PrivacyFieldEnabled && !hasPrivacy)
            {
                _db.SysRolePermissions.Add(new SysRolePermission
                {
                    RoleId = RoleId,
                    PermissionId = privacyPerm.Id,
                    CreatedAt = DateTime.Now
                });
            }
        }

        await _db.SaveChangesAsync();

        var msg = $"角色 {role.RoleName} 的权限已更新（{PermissionIds?.Length ?? 0} 项" + (PrivacyFieldEnabled ? " + 隐私字段保护" : "") + "）";
        return new JsonResult(new { success = true, message = msg });
    }
}