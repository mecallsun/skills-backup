namespace DormManage.Shared.Models;

/// <summary>
/// 角色-权限关联表
/// </summary>
public class SysRolePermission
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }
}
