using Microsoft.Data.SqlClient;
var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

// 检查 DormBooking 中的 NOT NULL 字符串列的 NULL 情况
var checks = new[]
{
    "SELECT COUNT(*) FROM DormBooking WHERE EmployeeCode IS NULL OR EmployeeCode = ''",
    "SELECT COUNT(*) FROM DormBooking WHERE EmployeeName IS NULL OR EmployeeName = ''",
    "SELECT COUNT(*) FROM DormBooking WHERE DormCode IS NULL OR DormCode = ''",
    "SELECT COUNT(*) FROM DormBooking WHERE Registrar IS NULL OR Registrar = ''",
};
foreach (var sql in checks)
{
    using var cmd = new SqlCommand(sql, conn);
    var cnt = (int)cmd.ExecuteScalar();
    Console.WriteLine($"{sql.Substring(sql.IndexOf("WHERE"))}: {cnt} NULL");
}

// BillingStandard
Console.WriteLine();
var checks2 = new[]
{
    "SELECT COUNT(*) FROM BillingStandard WHERE StandardName IS NULL OR StandardName = ''",
    "SELECT COUNT(*) FROM BillingStandard WHERE ApplicableType IS NULL OR ApplicableType = ''",
};
foreach (var sql in checks2)
{
    using var cmd = new SqlCommand(sql, conn);
    var cnt = (int)cmd.ExecuteScalar();
    Console.WriteLine($"{sql.Substring(sql.IndexOf("WHERE"))}: {cnt} NULL");
}