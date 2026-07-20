using Microsoft.Data.SqlClient;
var connStr = "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

// 改回 DATE 类型（EF Core 8 对 DateOnly 有内置转换，DATETIME 反而不行）
var alters = new[]
{
    "ALTER TABLE [dbo].[DormBooking] ALTER COLUMN [BookingDate] DATE NOT NULL;",
    "ALTER TABLE [dbo].[DormBooking] ALTER COLUMN [ActualCheckInDate] DATE NULL;",
    "ALTER TABLE [dbo].[DormBooking] ALTER COLUMN [ActualCheckOutDate] DATE NULL;",
    "ALTER TABLE [dbo].[BillingStandard] ALTER COLUMN [EffectiveFrom] DATE NULL;",
    "ALTER TABLE [dbo].[BillingStandard] ALTER COLUMN [EffectiveTo] DATE NULL;",
    "ALTER TABLE [dbo].[MeterRecord] ALTER COLUMN [ReadDate] DATE NULL;",
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