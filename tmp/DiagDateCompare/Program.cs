using System;

class Program
{
    static void Main()
    {
        // 场景1: 主机日期 = 有效期（用户报告的 bug）
        Console.WriteLine("=== 场景1: 主机日期 == 有效期 ===");
        Test("2026-07-26 == 2026-07-26", new DateTime(2026, 7, 26), new DateTime(2026, 7, 26));

        // 场景2: 主机日期 = 有效期 - 1
        Console.WriteLine("\n=== 场景2: 主机日期 == 有效期 - 1 ===");
        Test("2026-07-25 < 2026-07-26", new DateTime(2026, 7, 26), new DateTime(2026, 7, 25));

        // 场景3: 主机日期 = 有效期 + 1
        Console.WriteLine("\n=== 场景3: 主机日期 == 有效期 + 1 ===");
        Test("2026-07-27 > 2026-07-26", new DateTime(2026, 7, 26), new DateTime(2026, 7, 27));

        // 场景4: 不同 Kind 类型
        Console.WriteLine("\n=== 场景4: 不同 DateTimeKind 的影响 ===");
        var regDateLocal = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Local);
        var regDateUtc = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
        var regDateUnspec = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Unspecified);
        var todayUtc = DateTime.UtcNow;
        var todayLocal = DateTime.Now;

        Console.WriteLine($"  regDate Kind=Local: {regDateLocal.Date}");
        Console.WriteLine($"  regDate Kind=Utc: {regDateUtc.Date}");
        Console.WriteLine($"  regDate Kind=Unspecified: {regDateUnspec.Date}");
        Console.WriteLine($"  DateTime.UtcNow.Date: {todayUtc.Date}");
        Console.WriteLine($"  DateTime.Now.Date: {todayLocal.Date}");

        Console.WriteLine("\n=== 结论 ===");
        Console.WriteLine("v2.13.179 的修复只比较 .Date (日期部分)，不含时间");
        Console.WriteLine("只要服务器日期 == RegDate → strict < 为 false → IsReadOnly=false → 可写");
        Console.WriteLine("唯一可能仍锁定的场景: DateTimeKind=Local 的 regDate 但服务器 UTC 还在昨天");
    }

    static void Test(string desc, DateTime regDate, DateTime hostDate)
    {
        bool isReadOnly_v179 = regDate.Date < hostDate.Date;
        Console.WriteLine($"  {desc}: regDate.Date ({regDate.Date:yyyy-MM-dd}) < hostDate.Date ({hostDate.Date:yyyy-MM-dd}) = {isReadOnly_v179}");
        Console.WriteLine($"    → IsReadOnly = {isReadOnly_v179} (strict <)");
    }
}