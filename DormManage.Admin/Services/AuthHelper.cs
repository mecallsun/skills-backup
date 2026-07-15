using System.Collections.Generic;

namespace DormManage.Admin.Services;

public static class AuthHelperExtensions
{
    /// <summary>
    /// 菜单节点（用于导航栏渲染）
    /// </summary>
    public class MenuNode
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string PermissionCode { get; set; } = "";
        public string PermissionName { get; set; } = "";
        public string Route { get; set; } = "";
        public string Icon { get; set; } = "";
        public int SortOrder { get; set; }
        public byte PermissionType { get; set; } = 1;
    }
}
