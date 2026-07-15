namespace DormManage.Shared.Models;

/// <summary>
/// 系统权限（RBAC）
/// </summary>
public class SysPermission
{
    public int Id { get; set; }

    /// <summary>
    /// 权限代码（唯一标识，如 home:view）
    /// </summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>
    /// 权限名称
    /// </summary>
    public string PermissionName { get; set; } = string.Empty;

    /// <summary>
    /// 权限类型：1=菜单 2=按钮 3=数据
    /// </summary>
    public byte PermissionType { get; set; } = 1;

    /// <summary>
    /// 父级权限 ID（0 表示顶级）
    /// </summary>
    public int ParentId { get; set; }

    /// <summary>
    /// 路由地址
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// 图标（Bootstrap Icons 类名）
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 排序权重
    /// </summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
