using Microsoft.Data.SqlClient;
var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";
using var conn = new SqlConnection(connStr);
conn.Open();

// 列出 DormBooking 所有列的 NULL 计数
var cols = new[] {
    "BookingId","EmployeeId","EmployeeCode","EmployeeName","Phone","Department",
    "DormCode","BookingType","BookingDate","Status","Reason","Remark",
    "RegistrationDate","Registrar","AttendanceTypeId","BedNo","MoveFromDormCode",
    "ActualCheckInDate","ActualCheckOutDate","CancellationReason","CheckInOperator","CheckOutOperator"
};
foreach (var c in cols)
{
    using var cmd = new SqlCommand($"SELECT COUNT(*) FROM DormBooking WHERE [{c}] IS NULL", conn);
    var cnt = (int)cmd.ExecuteScalar();
    if (cnt > 0)
    {
        Console.WriteLine($"  ❌ {c}: {cnt} NULL");
    }
}

// 检查 BookingType
using var cmd2 = new SqlCommand("SELECT TOP 5 BookingId, BookingType, Status FROM DormBooking", conn);
using var r = cmd2.ExecuteReader();
Console.WriteLine("\n=== Sample DormBooking ===");
while (r.Read()) Console.WriteLine($"  Id={r.GetInt32(0)}, BookingType={r.GetByte(1)}, Status={r.GetByte(2)}");

// 检查 BillingStandard 数据
using var cmd3 = new SqlCommand("SELECT TOP 3 * FROM BillingStandard", conn);
using var r3 = cmd3.ExecuteReader();
Console.WriteLine("\n=== Sample BillingStandard ===");
int fieldCount = r3.FieldCount;
for (int i = 0; i < fieldCount; i++) Console.Write($"{r3.GetName(i),-25}");
Console.WriteLine();
while (r3.Read())
{
    for (int i = 0; i < fieldCount; i++)
    {
        var v = r3.IsDBNull(i) ? "NULL" : r3.GetValue(i).ToString();
        Console.Write($"{v,-25}");
    }
    Console.WriteLine();
}