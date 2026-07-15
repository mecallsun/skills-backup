using System.Collections.Generic;

namespace DormManage.Admin.Services;

public static class AuthHelperExtensions
{
    public class MenuNode
    {
        public int Id { get; set; }
        public string PermissionCode { get; set; } = "";
        public string PermissionName { get; set; } = "";
        public string Route { get; set; } = "";
        public string Icon { get; set; } = "";
    }
}
