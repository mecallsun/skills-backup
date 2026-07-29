using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using DormManage.Shared.Data;
using DormManage.Shared.Services;

const string CONN = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";

var opts = new DbContextOptionsBuilder<DormDbContext>().UseSqlServer(CONN).Options;
int pass = 0, fail = 0;
void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "✅ PASS" : "❌ FAIL")} | {name} | {detail}");
    if (ok) pass++; else fail++;
}

using (var db = new DormDbContext(opts))
{
    // ---- DB 层：确认 3 个过滤唯一索引存在 ----
    var idx = db.Database.SqlQueryRaw<string>(
        "SELECT name AS Value FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.DormMeter') AND name LIKE 'UX_DormMeter_%MeterId'")
        .ToList();
    Check("DB 索引 UX_DormMeter_ElectricMeterId 存在", idx.Contains("UX_DormMeter_ElectricMeterId"), string.Join(",", idx));
    Check("DB 索引 UX_DormMeter_ColdWaterMeterId 存在", idx.Contains("UX_DormMeter_ColdWaterMeterId"), string.Join(",", idx));
    Check("DB 索引 UX_DormMeter_HotWaterMeterId 存在", idx.Contains("UX_DormMeter_HotWaterMeterId"), string.Join(",", idx));

    var svc = new BasicsService(db);
    int id1 = 0, id2 = 0;

    // ---- 用例1：新增 A102(dormId=2) 电表=TESTUNIQ-E001 → 成功 ----
    var r1 = await svc.CreateDeviceMeterAsync(new DormMeterDto { DormId = 2, ElectricMeterId = "TESTUNIQ-E001" });
    Check("用例1 新增合法记录成功", r1.Success, $"code={r1.Code} msg={r1.Message}");
    if (r1.Success) id1 = r1.Data!.Id;

    // ---- 用例2：新增 A103(dormId=3) 电表=TESTUNIQ-E001（跨记录同列重复）→ DEVICE_ID_DUPLICATE ----
    var r2 = await svc.CreateDeviceMeterAsync(new DormMeterDto { DormId = 3, ElectricMeterId = "TESTUNIQ-E001" });
    Check("用例2 跨记录同列重复被拒", !r2.Success && r2.Code == "DEVICE_ID_DUPLICATE", $"code={r2.Code} msg={r2.Message}");

    // ---- 用例4：新增 A103 电表=冷水=TESTUNIQ-E009（同记录内重复）→ 同一记录内... ----
    var r4 = await svc.CreateDeviceMeterAsync(new DormMeterDto { DormId = 3, ElectricMeterId = "TESTUNIQ-E009", ColdWaterMeterId = "TESTUNIQ-E009" });
    Check("用例4 同一记录内重复被拒", !r4.Success && r4.Code == "DEVICE_ID_DUPLICATE" && r4.Message.Contains("同一记录内"), $"code={r4.Code} msg={r4.Message}");

    // ---- 用例3准备：新增 A103 电表=TESTUNIQ-E002 → 成功 ----
    var r3p = await svc.CreateDeviceMeterAsync(new DormMeterDto { DormId = 3, ElectricMeterId = "TESTUNIQ-E002" });
    Check("用例3准备 新增 R2 成功", r3p.Success, $"code={r3p.Code} msg={r3p.Message}");
    if (r3p.Success) id2 = r3p.Data!.Id;

    // ---- 用例3：编辑 R2 冷水=TESTUNIQ-E001（跨列全局，与 R1 电表冲突）→ DEVICE_ID_DUPLICATE ----
    if (id2 > 0)
    {
        var r3 = await svc.UpdateDeviceMeterAsync(id2, new DormMeterDto { Id = id2, DormId = 3, ElectricMeterId = "TESTUNIQ-E002", ColdWaterMeterId = "TESTUNIQ-E001" });
        Check("用例3 跨列全局重复被拒（Service 层）", !r3.Success && r3.Code == "DEVICE_ID_DUPLICATE", $"code={r3.Code} msg={r3.Message}");
    }

    // ---- 用例6-真实数据：新增 A105(dormId=5) 电表=f85b1b599074（与现有 A101 冲突）→ DEVICE_ID_DUPLICATE ----
    var r6 = await svc.CreateDeviceMeterAsync(new DormMeterDto { DormId = 5, ElectricMeterId = "f85b1b599074" });
    Check("用例6 与真实现有数据冲突被拒", !r6.Success && r6.Code == "DEVICE_ID_DUPLICATE", $"code={r6.Code} msg={r6.Message}");

    // ---- DB 层拦截：原生 SQL 直插重复电表 → 期望唯一索引冲突 SqlException ----
    // 先留下 R1(TESTUNIQ-E001) 用于制造重复；用 dormId=6(A106) 直插同电表
    bool dbRejected = false; string dbDetail = "";
    if (id1 > 0)
    {
        try
        {
            var maxId = db.DormMeters.Max(m => (int?)m.Id) ?? 0;
            await db.Database.ExecuteSqlRawAsync(
                $"INSERT INTO dbo.DormMeter (DormMeterId, DormId, ElectricMeterId, IsActive, CreatedAt) VALUES ({maxId + 1}, 6, 'TESTUNIQ-E001', 1, GETDATE())");
            dbDetail = "INSERT 未报错（索引未生效！）";
            // 若竟然插入成功，清理掉
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM dbo.DormMeter WHERE DormMeterId = {maxId + 1}");
        }
        catch (Exception ex) when (ex is SqlException || ex.InnerException is SqlException)
        {
            dbRejected = true;
            var se = (ex as SqlException) ?? (SqlException)ex.InnerException!;
            dbDetail = $"SqlException #{se.Number}: {se.Message.Split('\n')[0]}";
        }
    }
    Check("DB 层原生 INSERT 同列重复被唯一索引拒绝", dbRejected, dbDetail);

    // ---- 清理测试数据 ----
    if (id1 > 0) await svc.DeleteDeviceMeterAsync(id1);
    if (id2 > 0) await svc.DeleteDeviceMeterAsync(id2);
    var remain = db.DormMeters.Count();
    Check("清理后 DormMeter 记录数回到 1（仅剩 A101）", remain == 1, $"remain={remain}");
}

Console.WriteLine($"\n==== 结果：{pass} PASS / {fail} FAIL ====");
Environment.Exit(fail == 0 ? 0 : 1);
