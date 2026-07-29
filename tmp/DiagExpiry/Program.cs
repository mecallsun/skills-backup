using System;

class Program
{
    static void Main()
    {
        Console.WriteLine($"DateTime.Today = {DateTime.Today}");
        Console.WriteLine($"DateTime.Today.Date = {DateTime.Today.Date:yyyy-MM-dd}");
        Console.WriteLine($"DateTime.Today.Ticks = {DateTime.Today.Ticks}");
        Console.WriteLine();

        var regDate = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Unspecified);
        Console.WriteLine($"regDate = {regDate}");
        Console.WriteLine($"regDate.Date = {regDate.Date:yyyy-MM-dd}");
        Console.WriteLine();

        // BUG 版本
        bool b1 = regDate < DateTime.Today;
        Console.WriteLine($"[BUG]   regDate < DateTime.Today: {b1}");
        Console.WriteLine($"        compare={regDate.Ticks} < {DateTime.Today.Ticks}");

        // 修复版本
        bool b2 = regDate.Date < DateTime.Today.Date;
        Console.WriteLine($"[FIX]   regDate.Date < DateTime.Today.Date: {b2}");

        // 等价测试
        var reg1 = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Unspecified);
        var reg2 = new DateTime(2026, 7, 26, 17, 17, 36, DateTimeKind.Local);
        var reg3 = new DateTime(2026, 7, 26, 23, 59, 59, DateTimeKind.Unspecified);
        Console.WriteLine();
        Console.WriteLine($"reg1 (00:00) < Today (17:17) = {reg1 < DateTime.Today}");
        Console.WriteLine($"reg2 (17:17) < Today (17:17) = {reg2 < DateTime.Today}");
        Console.WriteLine($"reg3 (23:59) < Today (17:17) = {reg3 < DateTime.Today}");
    }
}