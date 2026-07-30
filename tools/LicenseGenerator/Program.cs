using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseGenerator;

/// <summary>
/// v2.13.94 注册机（部署给公司内部使用，不对外发布）
/// 用法：
///   LicenseGenerator.exe {SN} {LTDName} {ExpireDate:yyyy-MM-dd}
/// 示例：
///   LicenseGenerator.exe ABCDE-FGHIJ-KLMNO-PQRST-UVWXY "金智电子有限公司" 2027-12-31
///
/// 输出：CDKEY（5-5-5-5-5 = 29 位）
/// 算法：20 位验证段（MD5(SN|LTDName|SECRET_KEY) 前 20） + 5 位日期段（YYYYMMDD 转 HEX）
/// </summary>
class Program
{
    private const string SECRET_KEY = "JINGE-DORM-MANAGE-2026-LICENSE";

    static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法：LicenseGenerator {SN} {LTDName} {ExpireDate:yyyy-MM-dd}");
            Console.WriteLine("示例：LicenseGenerator.exe ABCDE-FGHIJ-KLMNO-PQRST-UVWXY \"金智电子有限公司\" 2027-12-31");
            return 1;
        }

        var sn = args[0].Replace("-", "").ToUpperInvariant();
        if (sn.Length != 25)
        {
            Console.WriteLine($"❌ SN 格式错误：应为 25 位（5-5-5-5-5），实际 {sn.Length} 位");
            return 2;
        }

        var ltdName = args[1];
        if (!DateTime.TryParseExact(args[2], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var expireDate))
        {
            Console.WriteLine($"❌ 日期格式错误：{args[2]}（应为 yyyy-MM-dd）");
            return 3;
        }

        // 计算验证段
        var verifyRaw = $"{sn}|{ltdName.ToUpperInvariant()}|{SECRET_KEY}";
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(verifyRaw));
        var verifyStr = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant().Substring(0, 20);

        // 计算日期段（YYYY → 2 hex，M-1 → 1 hex，DD → 2 hex）
        var yearHex = (expireDate.Year - 2000).ToString("X2");  // e.g. 2026 → 1A
        var monthHex = (expireDate.Month - 1).ToString("X1");   // e.g. 12 → B
        var dayHex = expireDate.Day.ToString("X2");              // e.g. 31 → 1F
        var dateStr = yearHex + monthHex + dayHex;  // 5 位

        // 拼接 CDKEY
        var raw25 = verifyStr + dateStr;  // 20 + 5 = 25
        var cdkey = $"{raw25.Substring(0, 5)}-{raw25.Substring(5, 5)}-{raw25.Substring(10, 5)}-{raw25.Substring(15, 5)}-{raw25.Substring(20, 5)}";

        Console.WriteLine();
        Console.WriteLine("===== v2.13.94 注册码生成 =====");
        Console.WriteLine($"SN:        {FormatSn(sn)}");
        Console.WriteLine($"公司名称:   {ltdName}");
        Console.WriteLine($"到期日期:   {expireDate:yyyy-MM-dd}");
        Console.WriteLine($"注册码:     {cdkey}");
        Console.WriteLine();
        Console.WriteLine("📌 提示：将 SN + 公司名称 + 注册码 三项发给客户，让客户在「系统设置 → 关于系统 → 软件注册」中录入。");

        return 0;
    }

    private static string FormatSn(string raw25)
        => $"{raw25.Substring(0, 5)}-{raw25.Substring(5, 5)}-{raw25.Substring(10, 5)}-{raw25.Substring(15, 5)}-{raw25.Substring(20, 5)}";
}