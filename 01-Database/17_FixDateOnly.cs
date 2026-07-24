using Microsoft.Data.SqlClient;
var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

// EF Core 8 在处理 DATE 类型字段上的 NULL 时会有 SqlNullValueException。
// 把 DormBooking 的 DateOnly? 字段从 DATE 改为 DATETIME 兼容 NULL。
// 这些字段在 EF 实体中是 DateOnly? 类型，SQL Server 用 DATETIME 2 类型存储会更安全
var alters = new[]
{
    "ALTER TABLE [dbo].[DormBooking] ALTER COLUMN [BookingDate] DATETIME NOT NULL;",
    "ALTER TABLE [dbo].[DormBooking] ALTER COLUMN [ActualCheckInDate] DATETIME NULL;",
    "ALTER TABLE [dbo].[DormBooking] ALTER COLUMN [ActualCheckOutDate] DATETIME NULL;",
    // BillingStandard 也改
    "ALTER TABLE [dbo].[BillingStandard] ALTER COLUMN [EffectiveFrom] DATETIME NULL;",
    "ALTER TABLE [dbo].[BillingStandard] ALTER COLUMN [EffectiveTo] DATETIME NULL;",
    // MeterRecord
    "ALTER TABLE [dbo].[MeterRecord] ALTER COLUMN [ReadDate] DATETIME NULL;",
};
int success = 0, failed = 0;
foreach (var sql in alters)
{
    try {
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✅ {sql.Substring(sql.IndexOf("ALTER COLUMN") + 13)}");
        success++;
    } catch (Exception ex) {
        Console.WriteLine($"❌ {ex.Message}");
        failed++;
    }
}
Console.WriteLine($"\n📊 完成: {success}/{success+failed}");