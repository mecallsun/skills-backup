using Microsoft.Data.SqlClient;
var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine($"✅ 已连接 {conn.DataSource}/{conn.Database}");

// 重命名 BillingStandard 列
var renames = new[]
{
    "EXEC sp_rename '[dbo].[BillingStandard].[ColdWaterUnitPrice]', 'ColdWaterPrice', 'COLUMN';",
    "EXEC sp_rename '[dbo].[BillingStandard].[HotWaterUnitPrice]', 'HotWaterPrice', 'COLUMN';",
    "EXEC sp_rename '[dbo].[BillingStandard].[ElectricUnitPrice]', 'ElectricityPrice', 'COLUMN';",
};

int success = 0, failed = 0;
foreach (var sql in renames)
{
    try {
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✅ {sql.Substring(sql.IndexOf("'dbo")+1, sql.LastIndexOf("'") - sql.IndexOf("'dbo") - 1)}");
        success++;
    } catch (Exception ex) {
        Console.WriteLine($"❌ {ex.Message}");
        failed++;
    }
}
Console.WriteLine($"\n📊 完成: {success}/{success+failed}");

// 验证
Console.WriteLine("\n=== BillingStandard 列 ===");
using var cmd2 = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='BillingStandard' ORDER BY ORDINAL_POSITION", conn);
using var r = cmd2.ExecuteReader();
while (r.Read()) Console.WriteLine($"  {r.GetString(0)}");