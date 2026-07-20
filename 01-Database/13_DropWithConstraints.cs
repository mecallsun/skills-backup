using Microsoft.Data.SqlClient;
var connStr = "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

string[] cols = { "ColdWaterUnitPrice", "HotWaterUnitPrice", "ElectricUnitPrice" };
foreach (var col in cols)
{
    // 找 default 约束
    using var cmd1 = new SqlCommand($@"
        SELECT df.name FROM sys.default_constraints df
        JOIN sys.columns c ON df.parent_object_id = c.object_id AND df.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID('dbo.BillingStandard') AND c.name = @col", conn);
    cmd1.Parameters.AddWithValue("@col", col);
    using var r = cmd1.ExecuteReader();
    string dfName = "";
    while (r.Read()) dfName = r.GetString(0);
    r.Close();

    if (!string.IsNullOrEmpty(dfName))
    {
        using var cmd2 = new SqlCommand($"ALTER TABLE [dbo].[BillingStandard] DROP CONSTRAINT [{dfName}];", conn);
        cmd2.ExecuteNonQuery();
        Console.WriteLine($"✅ Dropped constraint {dfName}");
    }

    using var cmd3 = new SqlCommand($"ALTER TABLE [dbo].[BillingStandard] DROP COLUMN [{col}];", conn);
    try { cmd3.ExecuteNonQuery(); Console.WriteLine($"✅ Dropped {col}"); }
    catch (Exception ex) { Console.WriteLine($"❌ {col}: {ex.Message}"); }
}