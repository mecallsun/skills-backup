using Microsoft.Data.SqlClient;
var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

// 检查 DormBooking 当前列
using var cmd1 = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='DormBooking' ORDER BY ORDINAL_POSITION", conn);
using var r = cmd1.ExecuteReader();
Console.WriteLine("=== DormBooking ===");
while (r.Read()) Console.WriteLine($"  {r.GetString(0),-25} {r.GetString(1),-15} {r.GetString(2)}");
r.Close();

// 检查 BillingStandard
using var cmd2 = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='BillingStandard' ORDER BY ORDINAL_POSITION", conn);
using var r2 = cmd2.ExecuteReader();
Console.WriteLine("\n=== BillingStandard ===");
while (r2.Read()) Console.WriteLine($"  {r2.GetString(0),-25} {r2.GetString(1)}");
r2.Close();

// 给 DormBooking 添加 BookingType 列（如果不存在）
using var cmd3 = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='DormBooking' AND COLUMN_NAME='BookingType'", conn);
if ((int)cmd3.ExecuteScalar() == 0)
{
    using var cmd4 = new SqlCommand("ALTER TABLE [dbo].[DormBooking] ADD [BookingType] TINYINT NOT NULL DEFAULT ((1));", conn);
    cmd4.ExecuteNonQuery();
    Console.WriteLine("\n✅ DormBooking.BookingType 已添加");
}
else
{
    Console.WriteLine("\nDormBooking.BookingType 已存在");
}