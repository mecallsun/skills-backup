using System;
using System.Data.SqlClient;
using Microsoft.Win32;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== 触发 'warning' 黄色底的具体条件分析 ===\n");

        // 1. 从注册表读 CDKEY / RegDate
        const string regPath = @"Software\JINGE\DormManage\License";
        string cdkey = null, ltd = null, regDateStr = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(regPath);
            if (key != null)
            {
                cdkey = key.GetValue("CDKEY")?.ToString();
                ltd = key.GetValue("LTDName")?.ToString();
                regDateStr = key.GetValue("RegDate")?.ToString();
            }
        }
        catch (Exception ex) { Console.WriteLine($"HKLM 读取失败：{ex.Message}"); }

        Console.WriteLine($"当前注册信息:");
        Console.WriteLine($"  CDKEY = {cdkey}");
        Console.WriteLine($"  LTDName = {ltd}");
        Console.WriteLine($"  RegDate (注册表) = {regDateStr}");
        Console.WriteLine();

        // 2. 模拟 CheckRegCDKey 解码 RegDate
        if (!string.IsNullOrEmpty(cdkey) && DateTime.TryParse(regDateStr, out var regDate))
        {
            // 3. 实际 GetDateByRegCDKey (CDKEY 解码) 返回的日期
            // 算法 A: 第 36 进制位置解码 + MD5 校验
            // 算法 B: 末 5 位 HEX 日期
            // 这里我们用注册表存的 RegDate 模拟（实际解码结果应一致）
            var cdkeyDecodedDate = regDate;
            Console.WriteLine($"CDKEY 解码的 RegDate: {cdkeyDecodedDate:yyyy-MM-dd} (Kind={cdkeyDecodedDate.Kind})");
            Console.WriteLine();

            // 4. v2.13.180 修复后的判定
            Console.WriteLine("[v2.13.180 修复后] CheckReg 判定:");
            bool expired_new = cdkeyDecodedDate.Date < DateTime.Today.Date;
            Console.WriteLine($"  {cdkeyDecodedDate.Date:yyyy-MM-dd} < {DateTime.Today.Date:yyyy-MM-dd}");
            Console.WriteLine($"  = {expired_new}");
            Console.WriteLine($"  → {(expired_new ? "❌ 触发 Expired → warning 黄色" : "✅ 走 Valid → success 绿色")}");

            // 5. 模拟 GetLicenseBanner 输出
            string bannerLevel = expired_new ? "warning" : "success";
            string bannerText = expired_new
                ? "⚠ 许可已过期"  // 黄色
                : "✅ 软件已注册";  // 绿色
            string bannerClass = expired_new ? "alert-warning" : "alert-success";
            string bannerIcon = expired_new ? "bi-exclamation-triangle-fill" : "bi-check-circle-fill";

            Console.WriteLine();
            Console.WriteLine($"[模拟 LicenseBanner 输出]");
            Console.WriteLine($"  Level = {bannerLevel}");
            Console.WriteLine($"  Label = {bannerText}");
            Console.WriteLine($"  CSS Class = alert-{bannerLevel}");
            Console.WriteLine($"  Icon = {bannerIcon}");

            // 6. 模拟最终 HTML
            Console.WriteLine();
            Console.WriteLine($"[Settings/Index.cshtml 渲染的 HTML]");
            Console.WriteLine($"  <div class=\"alert {bannerClass}\">");
            Console.WriteLine($"    <i class=\"bi {bannerIcon}\"></i>");
            Console.WriteLine($"    <strong>{bannerText}</strong>");
            Console.WriteLine($"  </div>");
        }
        else
        {
            Console.WriteLine("CDKEY 或 RegDate 解析失败");
        }
    }
}