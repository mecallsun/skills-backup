using Microsoft.Data.SqlClient;
var connStr = "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine($"✅ 已连接 {conn.DataSource}/{conn.Database}");

var alters = new[]
{
    "ALTER TABLE [dbo].[SysUser] ADD [PasswordResetFailedCount] INT NOT NULL DEFAULT ((0));",
    "ALTER TABLE [dbo].[SysUser] ADD [PasswordResetLockedUntil] DATETIME NULL;",
    "ALTER TABLE [dbo].[SysUser] ADD [PasswordResetToken] NVARCHAR(128) NULL;",
    "ALTER TABLE [dbo].[SysUser] ADD [PasswordResetTokenExpiry] DATETIME NULL;",
    "ALTER TABLE [dbo].[SysUser] ADD [WeChatBindAt] DATETIME NULL;",
    "ALTER TABLE [dbo].[SysUser] ADD [WeChatOpenId] NVARCHAR(64) NULL;",
};
int success = 0, fail = 0;
foreach (var sql in alters)
{
    try { using var cmd = new SqlCommand(sql, conn); cmd.ExecuteNonQuery(); success++; Console.WriteLine($"✅ {sql.Substring(0, sql.IndexOf(" ADD "))}"); }
    catch (Exception ex) { fail++; Console.WriteLine($"❌ {ex.Message}"); }
}
Console.WriteLine($"\n📊 完成: {success}/{success+fail}");

// 验证 SysUser 列
using var cmd2 = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SysUser' ORDER BY ORDINAL_POSITION", conn);
using var r = cmd2.ExecuteReader();
Console.WriteLine("\n=== SysUser 列清单 ===");
while (r.Read()) Console.WriteLine($"  {r.GetString(0)}");