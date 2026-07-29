using System;

class Program
{
    static void Main()
    {
        // 假设: 注册码有效期 = 2026-07-26（从注册表读取的字符串 "2026-07-26"）
        // LicenseForm 中 RegDate = new DateTime(2026, 7, 26) → Kind=Unspecified
        // LicenseMonitor.ReadState(): ToUniversalTime() → 减去8h → 2026-07-25T16:00:00Z → SpecifyKind(Utc)
        // 但注意！Unspecified → ToUniversalTime() 假设 Unspecified = Local → 减8h！
        // 然而实际流程: RegisterSdk.CheckReg() 中日期是从注册表字符串解析的

        Console.WriteLine("=== v2.13.177 根因分析: RegDate 从注册表字符串 Parse 后的 DateTime Kind ===\n");

        // 注册表存的是字符串 "2026-07-26"，经 CheckReg() → GetDateByRegCDKey() 解析
        // → new DateTime(year, month, day) → Kind=Unspecified
        var regDateFromTable = new DateTime(2026, 7, 26);
        Console.WriteLine($"注册表 RegDate (字符串) '2026-07-26' 解析后 → Kind={regDateFromTable.Kind}");

        // LicenseMonitor.ReadState():
        // reg.RegDate.Value.ToUniversalTime() → Unspecified 被当作 Local → 减 8h → 2026-07-25T16:00:00
        var utcViaToUniversal = regDateFromTable.ToUniversalTime();
        Console.WriteLine($"  → ToUniversalTime() = {utcViaToUniversal} (Kind={utcViaToUniversal.Kind})");

        // 然后 SpecifyKind(Utc) → 2026-07-25T16:00:00Z (注意 Date 变成了 2026-07-25!)
        var finalUtc = DateTime.SpecifyKind(utcViaToUniversal, DateTimeKind.Utc);
        Console.WriteLine($"  → SpecifyKind(Utc) = {finalUtc}, Date={finalUtc.Date:yyyy-MM-dd}");

        // 当前服务器时间（北京时间服务器，假设是 2026-07-26 03:00 北京 = 2026-07-25 19:00 UTC）
        var serverUtcNow = new DateTime(2026, 7, 25, 19, 0, 0, DateTimeKind.Utc);
        Console.WriteLine($"\n当前服务器 UtcNow = {serverUtcNow}, Date={serverUtcNow.Date:yyyy-MM-dd}");

        bool expired = finalUtc.Date < serverUtcNow.Date;
        Console.WriteLine($"\n  RegDateUTC_Date ({finalUtc.Date:yyyy-MM-dd}) < UtcNow_Date ({serverUtcNow.Date:yyyy-MM-dd}) = {expired}");
        Console.WriteLine($"  IsReadOnly = {expired} → {(expired ? "🔴 BUG! 用户还在有效期内却被锁定!" : "✅ 正常")}");

        // 另一种可能: 服务器在注册当天（2026-07-26）北京时间中午 = UTC 04:00
        var serverUtcNow2 = new DateTime(2026, 7, 26, 4, 0, 0, DateTimeKind.Utc);
        Console.WriteLine($"\n--- 同一天情况: 北京 2026-07-26 12:00 = UTC 2026-07-26 04:00 ---");
        Console.WriteLine($"  UtcNow = {serverUtcNow2}, Date={serverUtcNow2.Date:yyyy-MM-dd}");
        bool expired2 = finalUtc.Date < serverUtcNow2.Date;
        Console.WriteLine($"  RegDateUTC_Date ({finalUtc.Date:yyyy-MM-dd}) < UtcNow_Date ({serverUtcNow2.Date:yyyy-MM-dd}) = {expired2}");
        Console.WriteLine($"  IsReadOnly = {expired2} → {(expired2 ? "🔴 BUG!" : "✅")}");

        Console.WriteLine("\n=== 关键发现 ===");
        Console.WriteLine("regDate New DateTime(y,m,d) → Kind=Unspecified → ToUniversalTime() 假定它是 Local → 减8h");
        Console.WriteLine("导致 '2026-07-26' 变成 UTC '2026-07-25T16:00:00' → .Date = 2026-07-25");
        Console.WriteLine("如果服务器 UTC 时间 ≥ 2026-07-26 → 误判为过期 → 只读!");
        Console.WriteLine("\n→ 这就是为什么 '主机时间 >= 有效期前2天' 时按钮全锁的原因!");
    }
}
