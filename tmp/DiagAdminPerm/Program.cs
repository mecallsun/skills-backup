using System;
using System.Data.SqlClient;
class Program
{
    static void Main()
    {
        const string cs = "Server=172.16.0.100;Database=WaterMeterDB;User Id=user;Password=1234;Encrypt=false;TrustServerCertificate=true;";
        using var c = new SqlConnection(cs); c.Open();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM SysUserRole WHERE UserId = 1 AND RoleId = 1", c);
        Console.WriteLine($"admin 角色关联: {cmd.ExecuteScalar()}");
        using var cmd2 = new SqlCommand("SELECT COUNT(*) FROM SysRolePermission WHERE RoleId = 1", c);
        Console.WriteLine($"admin 权限数: {cmd2.ExecuteScalar()}");
        using var cmd3 = new SqlCommand("SELECT COUNT(*) FROM SysPermission WHERE IsActive = 1", c);
        Console.WriteLine($"SysPermission active: {cmd3.ExecuteScalar()}");
    }
}
