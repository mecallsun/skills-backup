namespace DormManage.Shared.Models;

/// <summary>
/// 系统权限（RBAC）— v2.13.3 增强：补充 Description / IsSystem / UpdatedAt / CreatedBy
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

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否系统内置（内置权限不允许删除）
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// 权限描述（用途说明）
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreatedBy { get; set; }
}
