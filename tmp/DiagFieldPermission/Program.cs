using System;
using System.Data.SqlClient;
class Program
{
    static void Main()
    {
        const string cs = "Server=172.16.0.100;Database=WaterMeterDB;User Id=user;Password=1234;Encrypt=false;TrustServerCertificate=true;";
        using var c = new SqlConnection(cs); c.Open();
        Console.WriteLine("=== 修复 SysRolePermission.Id 为 IDENTITY ===");
        try
        {
            // 步骤 1: 创建临时表保存数据
            using (var cmd = new SqlCommand(@"
                IF OBJECT_ID('tempdb..#SysRolePermissionBackup', 'U') IS NOT NULL DROP TABLE #SysRolePermissionBackup;
                SELECT * INTO #SysRolePermissionBackup FROM SysRolePermission;
                SELECT COUNT(*) AS BackupCount FROM #SysRolePermissionBackup", c))
            {
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read()) Console.WriteLine($"  备份行数: {rdr.GetInt32(0)}");
            }

            // 步骤 2: 删除并重建表（带 IDENTITY）
            using (var cmd = new SqlCommand(@"
                BEGIN TRANSACTION;
                DROP TABLE SysRolePermission;
                CREATE TABLE dbo.SysRolePermission (
                    Id              INT IDENTITY(1,1) NOT NULL,
                    RoleId          INT NOT NULL,
                    PermissionId    INT NOT NULL,
                    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT PK_SysRolePermission PRIMARY KEY (Id)
                );
                SET IDENTITY_INSERT dbo.SysRolePermission ON;
                INSERT INTO SysRolePermission (Id, RoleId, PermissionId, CreatedAt)
                SELECT Id, RoleId, PermissionId, CreatedAt FROM #SysRolePermissionBackup ORDER BY Id;
                SET IDENTITY_INSERT dbo.SysRolePermission OFF;
                COMMIT;
                SELECT COUNT(*) AS RestoredCount FROM SysRolePermission", c))
            {
                cmd.ExecuteNonQuery();
            }
            
            // 步骤 3: 验证
            using var cmd2 = new SqlCommand("SELECT COUNT(*) FROM SysRolePermission", c);
            Console.WriteLine($"  ✓ 修复后总行数: {cmd2.ExecuteScalar()}");
            
            // 步骤 4: 测试 INSERT
            using var cmd3 = new SqlCommand("INSERT INTO SysRolePermission (RoleId, PermissionId) VALUES (1, 100)", c);
            int n = cmd3.ExecuteNonQuery();
            Console.WriteLine($"  ✓ 测试 INSERT (无 Id): 影响行数 = {n}");
            using var cmd4 = new SqlCommand("DELETE FROM SysRolePermission WHERE RoleId = 1 AND PermissionId = 100", c);
            cmd4.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 错误: {ex.Message}");
        }
    }
}
