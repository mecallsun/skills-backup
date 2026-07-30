using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

class Program
{
    static void Main()
    {
        Console.WriteLine("===== LicenseGuard 诊断工具 =====\n");

        // 1) 读取注册表
        Console.WriteLine("【1】注册表 / 文件读取:");
        const string REG_PATH = @"Software\JINGE\DormManage\License";
        string? cdkey = null, ltd = null, regDateStr = null, sn = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(REG_PATH);
            if (key != null)
            {
                cdkey = key.GetValue("CDKEY")?.ToString();
                ltd = key.GetValue("LTDName")?.ToString();
                regDateStr = key.GetValue("RegDate")?.ToString();
                sn = key.GetValue("SN")?.ToString();
            }
        }
        catch (Exception ex) { Console.WriteLine($"  HKLM 读取失败：{ex.Message}"); }
        if (string.IsNullOrEmpty(cdkey))
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REG_PATH);
                if (key != null)
                {
                    cdkey = key.GetValue("CDKEY")?.ToString();
                    ltd = key.GetValue("LTDName")?.ToString();
                    regDateStr = key.GetValue("RegDate")?.ToString();
                    sn = key.GetValue("SN")?.ToString();
                }
            }
            catch { }
        }
        Console.WriteLine($"  CDKEY = '{cdkey ?? "<NULL>"}'");
        Console.WriteLine($"  LTDName = '{ltd ?? "<NULL>"}'");
        Console.WriteLine($"  RegDate (str) = '{regDateStr ?? "<NULL>"}'");
        Console.WriteLine($"  SN = '{sn ?? "<NULL>"}'");

        // 2) 当前机器码
        Console.WriteLine("\n【2】当前机器码 (GetSN via fallback):");
        try
        {
            var raw = $"{Environment.MachineName}|{Environment.ProcessorCount}|{Environment.OSVersion.VersionString}|JINGE-DORM";
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            Console.WriteLine($"  当前机器 fallback SN = {hex.Substring(0, 24)}");
        }
        catch (Exception ex) { Console.WriteLine($"  {ex.Message}"); }

        // 3) 主机日期
        Console.WriteLine("\n【3】主机日期:");
        Console.WriteLine($"  DateTime.Now (local) = {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  DateTime.UtcNow = {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  DateTime.Today = {DateTime.Today:yyyy-MM-dd}");

        // 4) IPC 探测
        Console.WriteLine("\n【4】IPC 端口 5099 探测 (TrayApp):");
        try
        {
            using var tcp = new TcpClient();
            var task = tcp.ConnectAsync("127.0.0.1", 5099);
            if (task.Wait(2000))
            {
                Console.WriteLine("  ✓ TrayApp 端口可连接");
                // 发送 getregstate 命令
                var cmd = new { Command = "getregstate" };
                var cmdJson = JsonSerializer.Serialize(cmd) + "\n";
                var bytes = Encoding.UTF8.GetBytes(cmdJson);
                var stream = tcp.GetStream();
                stream.Write(bytes);
                var buf = new byte[4096];
                using var ms = new MemoryStream();
                int n;
                while ((n = stream.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > 0 && Encoding.UTF8.GetString(ms.ToArray()).Contains("}")) break;
                }
                var resp = Encoding.UTF8.GetString(ms.ToArray()).Trim();
                Console.WriteLine($"  响应: {resp}");
            }
            else
            {
                Console.WriteLine("  ✗ 连接超时 — TrayApp 未运行或 IPC 失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ IPC 异常：{ex.Message}");
        }

        // 5) LicenseGuard 诊断结论
        Console.WriteLine("\n【5】IsReadOnly 推断:");
        if (string.IsNullOrEmpty(cdkey))
        {
            Console.WriteLine("  → cdkey 为空 → 视为未注册（RegInt=-1, RegStatus=Unregistered）");
            Console.WriteLine("  → IsReadOnly 应返回 false（试用模式可写）");
            Console.WriteLine("  → 试用模式记录数限制：住宿登记 500 / 住宿档案 5 / 人员清单 5");
        }
        else if (!string.IsNullOrEmpty(regDateStr) && DateTime.TryParse(regDateStr, out var rd))
        {
            if (rd.Date < DateTime.Today)
            {
                Console.WriteLine($"  → cdkey 存在但 RegDate {rd:yyyy-MM-dd} < 今天 → RegStatus=Expired → IsReadOnly=true");
            }
            else
            {
                Console.WriteLine($"  → cdkey 存在且 RegDate {rd:yyyy-MM-dd} >= 今天 → 应 RegStatus=Valid");
                Console.WriteLine("  → 但若 IsReadOnly=true 则可能是 SN/LTDName 与 CDKEY 不匹配 → RegStatus=Invalid");
            }
        }
        else
        {
            Console.WriteLine("  → RegDate 解析失败 → 视为 Invalid → IsReadOnly=true");
        }

        Console.WriteLine("\n===== 诊断完成 =====");
    }
}