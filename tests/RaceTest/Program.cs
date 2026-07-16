using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

// 选床/入住并发竞态测试（针对 SQL Server 验证 SERIALIZABLE 隔离防超容）
// 用法: dotnet run -- "<connectionString>" [并发数N] [容量Cap]
// 数据全部使用 __RACE__ 前缀，测试后自动清理（finally）。
var conn = args.Length > 0 ? args[0]
    : "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;";
int n = args.Length > 1 ? int.Parse(args[1]) : 8;   // 并发请求数
int cap = args.Length > 2 ? int.Parse(args[2]) : 1; // 宿舍容量

DbContextOptions<DormDbContext> BuildOptions() =>
    new DbContextOptionsBuilder<DormDbContext>()
        .UseSqlServer(conn, o => { o.UseCompatibilityLevel(120); o.EnableRetryOnFailure(2, TimeSpan.FromSeconds(3), null); })
        .Options;

const string DORM = "__RACE__";
var today = DateOnly.FromDateTime(DateTime.Today);
var empIds = new List<int>();

async Task CleanupAsync()
{
    await using var db = new DormDbContext(BuildOptions());
    await db.Database.ExecuteSqlRawAsync("DELETE FROM DormBooking WHERE DormCode = {0}", DORM);
    await db.Database.ExecuteSqlRawAsync("DELETE FROM SysEmployee WHERE EmployeeCode LIKE '__RACE_%'");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Dorm WHERE DormCode = {0}", DORM);
}

try
{
    // ---- Setup（raw SQL，绕过 EmployeeType 等 NOT NULL 分歧）----
    await CleanupAsync(); // 清理上次残留
    await using (var db = new DormDbContext(BuildOptions()))
    {
        await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO Dorm (DormCode,Building,Floor,RoomNo,DormAddress,DormType,Barcode,BuildingId,BuildingName,FloorId,AddressId,AddressText,Capacity,BedNumbers)
VALUES ({0},'T','1','999','race','M','__RACE_BC__',1,'T',1,1,'race',{1},'1')", DORM, cap);

        for (int i = 1; i <= n; i++)
        {
            await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO SysEmployee (EmployeeCode,RealName,Department,DepartmentId,EmployeeType,EmployeeTypeId,TeamId,Phone,HireDate,DormCode,BedNo,AttendanceTypeId)
VALUES ({0},{1},'T',1,'普通',1,1,'0','2020-01-01','',0,1)", $"__RACE_E{i}__", $"race{i}");
        }
        empIds = await db.Set<DormManage.Shared.Models.SysEmployee>()
            .Where(e => e.EmployeeCode.StartsWith("__RACE_E"))
            .Select(e => e.Id).ToListAsync();
    }
    Console.WriteLine($"Setup: dorm {DORM} cap={cap}, {empIds.Count} 测试员工");

    // ---- 并发 CheckIn（每任务独立 DbContext + BookingService）----
    var tasks = empIds.Select(empId => Task.Run(async () =>
    {
        await using var db = new DormDbContext(BuildOptions());
        var svc = new BookingService(db);
        var req = new BookingCheckInRequest { EmployeeId = empId, DormCode = DORM, BookingDate = today };
        var r = await svc.CheckInAsync(req, "racetest");
        return r.Success;
    })).ToArray();

    var results = await Task.WhenAll(tasks);
    int ok = results.Count(x => x);

    // ---- 断言 ----
    await using (var db = new DormDbContext(BuildOptions()))
    {
        var staying = await db.DormBookings.CountAsync(b =>
            b.DormCode == DORM && b.Status == BookingStatus.Staying);
        Console.WriteLine($"结果: 并发={n}, 容量={cap}, 成功={ok}, 在宿计数={staying}");
        bool pass = ok == cap && staying == cap;
        Console.WriteLine(pass
            ? $"✅ PASS：成功数({ok})==容量({cap})，无超容/双分配"
            : $"❌ FAIL：期望成功={cap} 在宿={cap}，实际 成功={ok} 在宿={staying}（存在竞态超容！）");
        Environment.ExitCode = pass ? 0 : 1;
    }
}
finally
{
    await CleanupAsync();
    Console.WriteLine("已清理测试数据");
}
