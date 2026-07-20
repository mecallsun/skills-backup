using Microsoft.Data.SqlClient;
var connStr = "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

Console.WriteLine("=== BillingStandard data ===");
using var cmd1 = new SqlCommand("SELECT Id, StandardName, EffectiveFrom, EffectiveTo FROM BillingStandard", conn);
using var r = cmd1.ExecuteReader();
while (r.Read())
{
    Console.WriteLine($"  Id={r.GetInt32(0)}, Name={r.GetString(1)}, EffectiveFrom={r.GetValue(2)}, EffectiveTo={r.GetValue(3)}");
}
r.Close();

Console.WriteLine("\n=== DormBooking sample (first row all cols) ===");
using var cmd2 = new SqlCommand("SELECT TOP 1 * FROM DormBooking", conn);
using var r2 = cmd2.ExecuteReader();
int n = r2.FieldCount;
for (int i = 0; i < n; i++) Console.Write($"{r2.GetName(i),-25}");
Console.WriteLine();
while (r2.Read())
{
    for (int i = 0; i < n; i++)
    {
        var v = r2.IsDBNull(i) ? "NULL" : r2.GetValue(i).ToString();
        if (v.Length > 25) v = v.Substring(0, 22) + "...";
        Console.Write($"{v,-25}");
    }
    Console.WriteLine();
}