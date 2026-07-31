using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// v2.13.67 角色管理 Tab — 完整 CRUD 嵌入 Settings/Index
/// 原 /Settings/Role 独立页面已合并为 Settings 的子 Tab，本类为 IndexModel 的 partial class。
///
/// 字段（角色列表 + 权限矩阵）：
/// - Roles / PermissionGroups / RolePermissions (roleId -> permissionIds)
/// - SeedIntegrity（v2.13.102 新增：DB seed 完整性快照，供 permMatrixModal banner 渲染）
///
/// Handler（命名加 Role 前缀）：
/// - OnPostRoleCreateAsync(string RoleCode, string RoleName, string Description, int SortOrder)
/// - OnPostRoleUpdateAsync(int Id, string RoleName, string Description, int SortOrder, bool IsActive)
/// - OnPostRoleDeleteAsync(int Id)
/// - OnPostRoleSavePermissionsAsync(int RoleId, int[] PermissionIds)
/// - OnGetRoleSeedIntegrityAsync()         v2.13.102：banner AJAX 重新检查（reload）
/// - OnPostRoleSeedRepairAsync()           v2.13.102：banner AJAX 一键修复（不重启 Admin）
///
/// URL 调用：/Settings?handler=RoleCreate 等。
/// </summary>
public partial class IndexModel
{
    // ====================== 角色管理子 Tab 字段 ======================

    public List<RoleListViewModel> Roles { get; set; } = new();
    public List<PermissionGroupViewModel> PermissionGroups { get; set; } = new();
    public Dictionary<int, HashSet<int>> RolePermissions { get; set; } = new();

    /// <summary>v2.13.102：DB seed 完整性快照（每次 OnGetAsync 重查询）</summary>
    public SeedIntegrityReport? SeedIntegrity { get; set; }

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

        // 隐私字段总开关权限（privacy:field:enable）作为特殊权限，不应在普通权限矩阵中显示
        // 它由独立的 checkbox 控制（见下方 RolePrivacyFieldEnabled），避免与权限树重复
        var privacyPerm = allPerms.FirstOrDefault(p => p.PermissionCode == "privacy:field:enable");
        var permsForMatrix = allPerms.Where(p => privacyPerm == null || p.Id != privacyPerm.Id).ToList();

        // v2.13.92 加载字段权限相关上下文（RolePrivacyFieldEnabled 供 permMatrixModal 渲染）
        var privacyPermId = privacyPerm?.Id;
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

        var topPerms = permsForMatrix.Where(p => p.ParentId == 0).ToList();
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
            var children = permsForMatrix.Where(p => p.ParentId == top.Id).ToList();
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

        // v2.13.102 新增：DB seed 完整性快照（permMatrixModal banner 用）
        // 失败不阻塞 Tab 渲染——返回 null 让 banner 显示「完整性检查未运行」
        try
        {
            SeedIntegrity = await DatabaseInitializer.CheckSeedIntegrityAsync(_db);
        }
        catch (Exception ex)
        {
            // 记录但不抛——避免 SysPermission 表缺失等极端情况让整页 500
            var logger = HttpContext?.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("SeedIntegrity");
            logger?.LogWarning(ex, "[v2.13.102] LoadRolePanelAsync 中 CheckSeedIntegrityAsync 异常");
            SeedIntegrity = null;
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
        // 验证模型绑定是否成功
        if (!ModelState.IsValid)
        {
            return new JsonResult(new { success = false, message = "数据格式验证失败，请检查输入内容" });
        }

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
        // v2.13.221 BUG 2 修复：同时处理「添加」和「取消」两种状态（原代码只处理添加，导致取消勾选无效）
        var privacyPerm = await _db.SysPermissions.FirstOrDefaultAsync(p => p.PermissionCode == "privacy:field:enable");
        if (privacyPerm != null)
        {
            var existingPrivacyRp = await _db.SysRolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == RoleId && rp.PermissionId == privacyPerm.Id);
            if (PrivacyFieldEnabled && existingPrivacyRp == null)
            {
                _db.SysRolePermissions.Add(new SysRolePermission
                {
                    RoleId = RoleId,
                    PermissionId = privacyPerm.Id,
                    CreatedAt = DateTime.Now
                });
            }
            else if (!PrivacyFieldEnabled && existingPrivacyRp != null)
            {
                // 取消勾选 + 已存在授权 → 删除（v2.13.221 BUG 2 修复）
                _db.SysRolePermissions.Remove(existingPrivacyRp);
            }
        }

        await _db.SaveChangesAsync();

        var msg = $"角色 {role.RoleName} 的权限已更新（{PermissionIds?.Length ?? 0} 项" + (PrivacyFieldEnabled ? " + 允许显示隐私字段" : "") + "）";
        return new JsonResult(new { success = true, message = msg });
    }

    // ====================== v2.13.102 seed 完整性 banner Handler ======================

    /// <summary>
    /// v2.13.102 新增：AJAX 重新检查（GET /Settings?handler=RoleSeedIntegrity）。
    /// 返回 success 让前端 reload，由 Razor 重渲染 banner（无需手动拼 HTML）。
    /// </summary>
    public IActionResult OnGetRoleSeedIntegrityAsync()
    {
        return new JsonResult(new { success = true, message = "请刷新页面查看最新状态" });
    }

    /// <summary>
    /// v2.13.102 新增：一键修复（POST /Settings?handler=RoleSeedRepair）。
    /// 调用 DatabaseInitializer.MigrateFieldPermissionAsync 执行 idempotent INSERT；
    /// 然后 CheckSeedIntegrityAsync 验证修复结果；返回 JSON 让前端 toast + reload。
    ///
    /// 设计原则：修复操作幂等可重复执行；不重启 Admin 立即生效；
    /// 仅依赖 antiforgery token 防止 CSRF；不依赖具体角色权限（admin 默认可调）。
    /// </summary>
    public async Task<IActionResult> OnPostRoleSeedRepairAsync()
    {
        try
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SeedRepair");
            // v2.13.103 改：返回 SeedMigrationResult 结构化结果
            var migResult = await DatabaseInitializer.MigrateFieldPermissionAsync(_db, logger, CancellationToken.None);
            var report = await DatabaseInitializer.CheckSeedIntegrityAsync(_db, CancellationToken.None);

            // v2.13.103 严格判定：只有迁移完全成功 + 完整性检查通过 才 success=true
            // 否则返回详细步骤让用户看清失败原因
            if (migResult.AllSucceeded && report.Ok)
            {
                return new JsonResult(new
                {
                    success = true,
                    message = $"seed 完整性修复成功（{report.Summary}）。步骤：{string.Join(" | ", migResult.PermSteps.Concat(migResult.RolePermSteps))}"
                });
            }
            // 修复有失败或完整性仍有缺失——返回详细错误
            var stepsDetail = string.Join(" | ", migResult.PermSteps.Concat(migResult.RolePermSteps));
            var failMsg = !string.IsNullOrEmpty(migResult.FatalError)
                ? $"致命异常：{migResult.FatalError}"
                : $"修复步骤：{stepsDetail}";
            var missingDetail = report.Ok
                ? ""
                : $" 仍有缺失：{string.Join("、", report.MissingPermissionLabels.Concat(report.MissingRolePermissionLabels))}";
            return new JsonResult(new
            {
                success = false,
                message = $"{failMsg}{missingDetail}。请检查「数据库连接」是否与托盘一致，或使用 scripts/seed_v2.13.103_personnel_add.sql 手动修复。"
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = $"修复异常：{ex.Message}" });
        }
    }
}