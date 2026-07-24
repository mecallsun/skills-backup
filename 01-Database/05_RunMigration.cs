using Microsoft.Data.SqlClient;

var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";

var ddlStatements = new[]
{
// EmployeeBilling: 补充 EF 实体期望的列（与现有 Days 列共存）
@"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EmployeeBilling') AND name='ShareRatio')
    ALTER TABLE [dbo].[EmployeeBilling] ADD [ShareRatio] DECIMAL(5,4) NOT NULL DEFAULT ((0));
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EmployeeBilling') AND name='ResidentCount')
    ALTER TABLE [dbo].[EmployeeBilling] ADD [ResidentCount] INT NOT NULL DEFAULT ((0));
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EmployeeBilling') AND name='ColdShareAmount')
    ALTER TABLE [dbo].[EmployeeBilling] ADD [ColdShareAmount] DECIMAL(12,2) NOT NULL DEFAULT ((0));
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EmployeeBilling') AND name='HotShareAmount')
    ALTER TABLE [dbo].[EmployeeBilling] ADD [HotShareAmount] DECIMAL(12,2) NOT NULL DEFAULT ((0));
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EmployeeBilling') AND name='ElectricityShareAmount')
    ALTER TABLE [dbo].[EmployeeBilling] ADD [ElectricityShareAmount] DECIMAL(12,2) NOT NULL DEFAULT ((0));
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EmployeeBilling') AND name='Department')
    ALTER TABLE [dbo].[EmployeeBilling] ADD [Department] NVARCHAR(128) NULL;",

// DormBilling: 补充 Department 列（如果不存在）
@"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DormBilling') AND name='Department')
    ALTER TABLE [dbo].[DormBilling] ADD [Department] NVARCHAR(128) NULL;",

// SysUserSecurityQuestion: 已经是标准字段，跳过
@"SELECT 1",
};

using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine($"✅ 已连接到 {conn.DataSource}/{conn.Database}");

int success = 0, failed = 0;
foreach (var ddl in ddlStatements)
{
    try
    {
        using var cmd = new SqlCommand(ddl, conn);
        cmd.CommandTimeout = 30;
        cmd.ExecuteNonQuery();
        success++;
        Console.WriteLine($"✅ ALTER 执行成功");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"❌ 失败: {ex.Message}");
    }
}

Console.WriteLine($"\n📊 ALTER 完成: 成功 {success}, 失败 {failed}");

Console.WriteLine("\n=== EmployeeBilling 列清单 ===");
using var cmdCols = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeBilling' ORDER BY ORDINAL_POSITION", conn);
using var reader = cmdCols.ExecuteReader();
while (reader.Read()) Console.WriteLine($"  {reader.GetString(0)} ({reader.GetString(1)})");
reader.Close();

Console.WriteLine("\n=== DormBilling 列清单 ===");
using var cmdCols2 = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='DormBilling' ORDER BY ORDINAL_POSITION", conn);
using var reader2 = cmdCols2.ExecuteReader();
while (reader2.Read()) Console.WriteLine($"  {reader2.GetString(0)} ({reader2.GetString(1)})");
reader2.Close();

Console.WriteLine("\n=== SysUserSecurityQuestion 列清单 ===");
using var cmdCols3 = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SysUserSecurityQuestion' ORDER BY ORDINAL_POSITION", conn);
using var reader3 = cmdCols3.ExecuteReader();
while (reader3.Read()) Console.WriteLine($"  {reader3.GetString(0)} ({reader3.GetString(1)})");