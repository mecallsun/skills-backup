using System;
using System.Data.SqlClient;
class Program
{
    static void Main()
    {
        const string cs = "Server=172.16.0.100;Database=WaterMeterDB;User Id=user;Password=1234;Encrypt=false;TrustServerCertificate=true;";
        using var c = new SqlConnection(cs); c.Open();
        using var cmd = new SqlCommand(@"SELECT COUNT(*) FROM SysRolePermission WHERE RoleId = 1", c);
        Console.WriteLine($"admin (RoleId=1) 总权限数: {Convert.ToInt32(cmd.ExecuteScalar())}");
        using var cmd2 = new SqlCommand(@"SELECT COUNT(*) FROM SysRolePermission rp INNER JOIN SysPermission p ON rp.PermissionId = p.Id WHERE rp.RoleId = 1 AND p.PermissionCode = 'privacy:field:enable'", c);
        Console.WriteLine($"admin 含 privacy:field:enable 授权数: {Convert.ToInt32(cmd2.ExecuteScalar())}");
    }
}
