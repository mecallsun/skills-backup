using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.Settings;

/// <summary>
/// v2.13.92 字段权限子 Tab partial — SysFieldPermission 加载 + 隐私字段保护总开关渲染。
/// 嵌入到 IndexModel（同 partial class），共享 ActiveTab / PageModel 上下文。
/// </summary>
public partial class IndexModel
{
    /// <summary>所有字段权限记录（按 SortOrder 排序）— 渲染 /Settings?tab=fields 表格</summary>
    public List<SysFieldPermission> FieldPermissions { get; set; } = new();

    /// <summary>当前编辑角色是否启用了隐私字段保护（渲染「启用隐私字段保护」checkbox 时用）</summary>
    public bool RolePrivacyFieldEnabled { get; set; }

    /// <summary>权限扁平列表（含 Id/Code/Type）— 用于 JS 渲染 perm_privacy_field checkbox 时按 Code 查 Id</summary>
    public List<PermissionLiteViewModel> Permissions { get; set; } = new();

    /// <summary>轻量权限 DTO（只暴露前端需要的字段）</summary>
    public class PermissionLiteViewModel
    {
        public int Id { get; set; }
        public string PermissionCode { get; set; } = "";
        public string PermissionName { get; set; } = "";
        public byte PermissionType { get; set; }
    }

    /// <summary>加载字段权限数据</summary>
    public async Task LoadFieldPermissionPanelAsync()
    {
        var svc = HttpContext.RequestServices.GetService(typeof(ISysFieldPermissionService))
                  as ISysFieldPermissionService;
        if (svc != null)
        {
            FieldPermissions = await svc.GetAllAsync();
        }

        // 同步加载权限列表（前端 JS 需要 privacy:field:enable 的 Id 来判断角色是否已启用）
        Permissions = await _db.SysPermissions
            .OrderBy(p => p.SortOrder)
            .Select(p => new PermissionLiteViewModel
            {
                Id = p.Id,
                PermissionCode = p.PermissionCode,
                PermissionName = p.PermissionName,
                PermissionType = p.PermissionType
            })
            .ToListAsync();
    }
}