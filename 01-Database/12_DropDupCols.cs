using Microsoft.Data.SqlClient;
var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

// 删除重复的 UnitPrice 列
var drops = new[] {
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [ColdWaterUnitPrice];",
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [HotWaterUnitPrice];",
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [ElectricUnitPrice];",
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [WaterPrice];",
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [HotWaterPrice];", // 等等，HotWaterPrice 是 EF 期望的，不能删
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [ColdWaterPrice];", // 等等，ColdWaterPrice 是 EF 期望的
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [ElectricityPrice];", // 等等，ElectricityPrice 是 EF 期望的
};

// 安全删除（只用 EF 实体不需要的列）
var safeDrops = new[] {
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [ColdWaterUnitPrice];",
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [HotWaterUnitPrice];",
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [ElectricUnitPrice];",
    "ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [WaterPrice];",
};

int success = 0, failed = 0;
foreach (var sql in safeDrops)
{
    try {
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✅ {sql.Substring(sql.IndexOf("DROP COLUMN") + 12)}");
        success++;
    } catch (Exception ex) {
        Console.WriteLine($"❌ {ex.Message}");
        failed++;
    }
}
Console.WriteLine($"\n📊 完成: {success}/{success+failed}");