using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace DormManage.Shared.Register;

/// <summary>
/// v2.13.94 软件注册授权 — 等价层（替代原 WinForms 项目的 Public.Core.SDK.Register）
///
/// 算法（v2.13.94 修正版 — 完全对齐原 NPGS.Register Public.Core.SDK.dll）：
/// - 机器码 = Win32_Processor.ProcessorId (16 hex) + 磁盘卷序列号 VolumeSerialNumber (8 hex)
/// - 拼接成 24 字符大写 hex（如 "BFEBFBFF000A06A4AA2E3B0E"），**不经过 MD5**
/// - 注册码 = 20 位验证段（MD5(SN|LTDName|SECRET_KEY) 前 20 hex）+ 5 位日期段（HEX(YYYY-MM-DD)）= 25 位 hex
/// - 格式化 5-5-5-5-5 = 29 位 CDKEY
///
/// 跨平台兼容方案（Web + TrayApp）：
/// - TrayApp（WinForms net8.0-windows）：用 System.Management 取真实 CPUID + VolumeSerialNumber
/// - Web Admin/Api（跨平台 .NET 8）：System.Management 在 Linux 不可用 → 三层降级
///   ① 优先从 %ProgramData%\JINGE\DormManage\machine.dat 读取（TrayApp 启动时写入）
///   ② 环境变量 DORM_MACHINE_SN（TrayApp 启动子进程时注入）
///   ③ Web 端 fallback：MD5(MachineName + ProcessorCount + OSVersion) 取前 24 位（精度低但稳定）
///
/// 关键决策：
/// - 不直接依赖 Public.Core.SDK.dll（.NET Framework 4.8，无法在 .NET 8 Web 加载）
/// - TrayApp 启动时计算真实机器码并写入共享文件 + 环境变量，供 Web 进程读取
/// - Web 端 fallback 算法与真实算法位数保持一致（24 hex）以保证 CDKEY 长度兼容
/// </summary>
public static class RegisterSdk
{
    /// <summary>固定密钥（与"注册机"端共享；实际部署可改为公司专属密钥）</summary>
    private const string SECRET_KEY = "JINGE-DORM-MANAGE-2026-LICENSE";

    /// <summary>注册表路径（优先 HKLM，失败回退 HKCU）</summary>
    private const string REG_PATH = @"Software\JINGE\DormManage";

    /// <summary>机器码共享文件路径（无注册表权限时使用，由 TrayApp 启动时写入）</summary>
    private static readonly string LICENSE_FILE =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "JINGE", "DormManage", "license.dat");

    /// <summary>机器码共享文件路径（v2.13.94 新增：TrayApp → Web 通信）</summary>
    private static readonly string MACHINE_FILE =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "JINGE", "DormManage", "machine.dat");

    /// <summary>环境变量名（TrayApp 启动子进程时注入）</summary>
    public const string ENV_MACHINE_SN = "DORM_MACHINE_SN";

    /// <summary>试用次数上限（与原项目一致）</summary>
    public const int TRIAL_LIMIT = 30;

    /// <summary>
    /// 获取机器码（v2.13.94 修正版：完全对齐原 NPGS.Register 算法）
    /// 算法：Win32_Processor.ProcessorId (16 hex) + 磁盘卷序列号 VolumeSerialNumber (8 hex)
    ///       拼接成 24 字符大写 hex，**不经过 MD5**
    ///
    /// 跨平台调用顺序（Web 端）：
    ///   1) 环境变量 DORM_MACHINE_SN（TrayApp 注入）
    ///   2) 共享文件 %ProgramData%\JINGE\DormManage\machine.dat（TrayApp 写入）
    ///   3) Web 端 fallback：MD5(MachineName + ProcessorCount + OSVersion) 取前 24 位
    ///
    /// v2.13.142 修正：返回值为原始 24 位 hex（无任何分隔符）
    /// 历史 BUG（v2.13.137/v2.13.138）：旧版 GetSN 返回 28 字符带连字符的 display 形式（5-5-5-5-4）
    ///   导致 LicenseForm 二次格式化产生错位
    /// 修复后统一约定：**SN 字段恒为 raw 24 hex**（参考 NPGS.Register `textSN.Text = SN` 直接显示 raw）
    /// 严禁任何展示层格式化（用户原话 2026-07-24「机器码显示没有连接符」）
    /// </summary>
    public static string GetSN()
    {
        // 1) 环境变量（TrayApp → Web 进程最直接通道）
        try
        {
            var fromEnv = Environment.GetEnvironmentVariable(ENV_MACHINE_SN);
            if (!string.IsNullOrEmpty(fromEnv) && IsValidMachineSN(fromEnv))
            {
                return NormalizeRawSN(fromEnv);
            }
        }
        catch { }

        // 2) 共享机器码文件（TrayApp 启动时写入）
        try
        {
            if (File.Exists(MACHINE_FILE))
            {
                var fromFile = File.ReadAllText(MACHINE_FILE).Trim();
                if (IsValidMachineSN(fromFile))
                {
                    return NormalizeRawSN(fromFile);
                }
            }
        }
        catch { }

        // 3) Web 端 fallback（精度低但保证位数一致 — 24 hex）
        return NormalizeRawSN(ComputeFallbackSN());
    }

    /// <summary>
    /// v2.13.142：规范化 SN 为 raw 24 位大写 hex
    /// 历史 machine.dat 可能存 28 字符 display 形式（v2.13.138 之前 MachineCodeProvider 写入时
    /// 错误格式化），新版必须先去连字符再返回 raw。
    /// 新版 MachineCodeProvider.Initialize() 已直接返回 raw（v2.13.142），本函数保留用于兼容旧 machine.dat。
    /// </summary>
    private static string NormalizeRawSN(string input)
    {
        var raw = (input ?? "").Replace("-", "").Trim().ToUpperInvariant();
        if (raw.Length >= 24) return raw.Substring(0, 24);
        return raw.PadLeft(24, '0');
    }

    /// <summary>
    /// 写入机器码到共享文件（TrayApp 启动时调用）
    /// </summary>
    public static void WriteMachineSN(string sn)
    {
        try
        {
            var dir = Path.GetDirectoryName(MACHINE_FILE);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(MACHINE_FILE, sn?.Trim().ToUpperInvariant() ?? "");
            // 同步设置环境变量（影响当前进程及子进程）
            Environment.SetEnvironmentVariable(ENV_MACHINE_SN, sn?.Trim().ToUpperInvariant() ?? "");
        }
        catch { }
    }

    /// <summary>
    /// Web 端 fallback 算法（当 TrayApp 未运行时）
    /// MD5(MachineName + ProcessorCount + OSVersion + 固定常量) → 取前 24 hex
    /// </summary>
    private static string ComputeFallbackSN()
    {
        try
        {
            var raw = $"{Environment.MachineName}|{Environment.ProcessorCount}|{Environment.OSVersion.VersionString}|JINGE-DORM";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            return hex.Substring(0, 24);
        }
        catch
        {
            // 极端情况：返回基于 MachineName + ProcessorCount 的稳定字符串
            var fallback = Environment.MachineName + Environment.ProcessorCount + "JINGE";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(fallback));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            return hex.Substring(0, 24);
        }
    }

    /// <summary>
    /// 校验机器码格式：必须为 24 字符大写 hex
    /// </summary>
    private static bool IsValidMachineSN(string sn)
    {
        if (string.IsNullOrEmpty(sn) || sn.Length != 24) return false;
        foreach (var c in sn)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
        }
        return true;
    }

    /// <summary>
    /// 获取当前注册状态（等价 CheckReg）
    /// </summary>
    public static RegItem CheckReg()
    {
        var reg = new RegItem { SN = GetSN() };

        var cdkey = ReadRegValue("CDKEY") ?? "";
        var ltd = ReadRegValue("LTDName") ?? "";
        var dateStr = ReadRegValue("RegDate") ?? "";
        var useTimesStr = ReadRegValue("UseTimes") ?? "0";

        reg.CDKEY = cdkey;
        reg.LTDName = ltd;
        reg.UseTimes = int.TryParse(useTimesStr, out var t) ? t : 0;

        if (string.IsNullOrEmpty(cdkey))
        {
            reg.RegInt = -1;  // 未注册
            return reg;
        }

        // 校验 CDKEY 格式 + 解码日期
        if (cdkey.Length != 29)
        {
            reg.RegInt = 0;  // 已过期（无效）
            return reg;
        }

        var regDate = GetDateByRegCDKey(cdkey);
        reg.RegDate = regDate;

        // 校验 CDKEY 与 SN + LTDName 匹配
        var checkResult = CheckRegCDKey(new RegItem { CDKEY = cdkey, SN = reg.SN, LTDName = ltd });
        if (checkResult.RegInt != 1)
        {
            reg.RegInt = 0;
            return reg;
        }

        if (regDate < DateTime.Today)
        {
            reg.RegInt = 0;  // 已过期
            return reg;
        }

        reg.RegInt = 1;  // 已注册且有效
        return reg;
    }

    /// <summary>
    /// 校验 CDKEY（等价 CheckRegCDKey）
    /// 算法：取 CDKEY 末段（日期编码 5 位）+ 前 20 位验证段
    /// 验证段 = MD5(SN + LTDName + 密钥) 的前 20 位大写 hex
    /// v2.13.94 修正：SN 长度从 25 改为 24（与新机器码算法一致）
    /// </summary>
    public static RegItem CheckRegCDKey(RegItem input)
    {
        var result = new RegItem
        {
            SN = input.SN,
            CDKEY = input.CDKEY,
            LTDName = input.LTDName
        };

        if (string.IsNullOrEmpty(input.CDKEY) || input.CDKEY.Length != 29)
        {
            result.RegInt = 0;
            return result;
        }

        var cdkeyRaw = input.CDKEY.Replace("-", "").ToUpperInvariant();
        if (cdkeyRaw.Length != 25)
        {
            result.RegInt = 0;
            return result;
        }

        // 去除 SN 的连字符用于校验
        var snRaw = (input.SN ?? "").Replace("-", "").ToUpperInvariant();
        if (snRaw.Length != 24)
        {
            result.RegInt = 0;
            return result;
        }

        var dateStr = cdkeyRaw.Substring(20, 5);  // 末段 5 位（编码到期日）
        var verifyStr = cdkeyRaw.Substring(0, 20);  // 前 20 位（验证段）

        // 校验验证段（用纯 hex SN，不用 display 格式）
        var expected = ComputeVerifyString(snRaw, input.LTDName ?? "");
        if (verifyStr != expected)
        {
            result.RegInt = 0;
            return result;
        }

        // 解码日期
        try
        {
            var year = 2000 + Convert.ToInt32(dateStr.Substring(0, 2), 16);
            var month = Convert.ToInt32(dateStr.Substring(2, 1), 16) + 1;  // 0-based + 1
            var day = Convert.ToInt32(dateStr.Substring(3, 2), 16);
            result.RegDate = new DateTime(year, Math.Max(1, month), Math.Max(1, Math.Min(day, 28)));
        }
        catch
        {
            result.RegInt = 0;
            return result;
        }

        result.RegInt = 1;  // CDKEY 有效
        return result;
    }

    /// <summary>
    /// 解码 CDKEY 得到注册到期日（等价 GetDateByRegCDKey）
    /// </summary>
    public static DateTime GetDateByRegCDKey(string cdkey)
    {
        if (string.IsNullOrEmpty(cdkey)) return DateTime.MinValue;
        var raw = cdkey.Replace("-", "").ToUpperInvariant();
        if (raw.Length < 25) return DateTime.MinValue;

        try
        {
            var year = 2000 + Convert.ToInt32(raw.Substring(20, 2), 16);
            var month = Convert.ToInt32(raw.Substring(22, 1), 16) + 1;
            var day = Convert.ToInt32(raw.Substring(23, 2), 16);
            return new DateTime(year, Math.Max(1, month), Math.Max(1, Math.Min(day, 28)));
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// 写入注册信息（HKLM 优先 → HKCU 回退 → 文件兜底）
    /// </summary>
    public static bool WriteRegItem(RegItem reg)
    {
        try
        {
            WriteRegValue("CDKEY", reg.CDKEY);
            WriteRegValue("LTDName", reg.LTDName);
            WriteRegValue("RegDate", reg.RegDate?.ToString("yyyy-MM-dd") ?? "");
            WriteRegValue("SN", reg.SN);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>删除注册信息</summary>
    public static bool DeleteRegItem()
    {
        try
        {
            DeleteRegValue("CDKEY");
            DeleteRegValue("LTDName");
            DeleteRegValue("RegDate");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>清除所有注册信息</summary>
    public static void DeleteRegAll()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(REG_PATH, true);
            key?.DeleteSubKeyTree("License", false);
        }
        catch { }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REG_PATH, true);
            key?.DeleteSubKeyTree("License", false);
        }
        catch { }
        try
        {
            if (System.IO.File.Exists(LICENSE_FILE)) System.IO.File.Delete(LICENSE_FILE);
        }
        catch { }
    }

    /// <summary>试用次数 +1</summary>
    public static int IncrementUseTimes()
    {
        var current = 0;
        var s = ReadRegValue("UseTimes");
        if (int.TryParse(s, out var n)) current = n;
        current++;
        WriteRegValue("UseTimes", current.ToString());
        return current;
    }

    /// <summary>读取注册表值</summary>
    private static string? ReadRegValue(string name)
    {
        // 1) HKLM
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{REG_PATH}\License");
            if (key != null)
            {
                var v = key.GetValue(name)?.ToString();
                if (!string.IsNullOrEmpty(v)) return v;
            }
        }
        catch { }

        // 2) HKCU
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{REG_PATH}\License");
            if (key != null)
            {
                var v = key.GetValue(name)?.ToString();
                if (!string.IsNullOrEmpty(v)) return v;
            }
        }
        catch { }

        // 3) 文件
        try
        {
            if (System.IO.File.Exists(LICENSE_FILE))
            {
                var lines = System.IO.File.ReadAllLines(LICENSE_FILE);
                foreach (var line in lines)
                {
                    var idx = line.IndexOf('=');
                    if (idx > 0 && line.Substring(0, idx) == name)
                        return line.Substring(idx + 1);
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>写入注册表值</summary>
    private static void WriteRegValue(string name, string value)
    {
        if (value == null) value = "";

        // 1) HKLM
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey($@"{REG_PATH}\License", true);
            key.SetValue(name, value, RegistryValueKind.String);
            return;
        }
        catch { }

        // 2) HKCU
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"{REG_PATH}\License", true);
            key.SetValue(name, value, RegistryValueKind.String);
            return;
        }
        catch { }

        // 3) 文件
        try
        {
            var dir = System.IO.Path.GetDirectoryName(LICENSE_FILE);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            var existing = System.IO.File.Exists(LICENSE_FILE)
                ? System.IO.File.ReadAllLines(LICENSE_FILE).ToList()
                : new List<string>();
            var idx = existing.FindIndex(l => l.StartsWith(name + "="));
            var newLine = $"{name}={value}";
            if (idx >= 0) existing[idx] = newLine;
            else existing.Add(newLine);
            System.IO.File.WriteAllLines(LICENSE_FILE, existing);
        }
        catch { }
    }

    private static void DeleteRegValue(string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{REG_PATH}\License", true);
            key?.DeleteValue(name, false);
        }
        catch { }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{REG_PATH}\License", true);
            key?.DeleteValue(name, false);
        }
        catch { }
        try
        {
            if (System.IO.File.Exists(LICENSE_FILE))
            {
                var lines = System.IO.File.ReadAllLines(LICENSE_FILE).ToList();
                lines.RemoveAll(l => l.StartsWith(name + "="));
                System.IO.File.WriteAllLines(LICENSE_FILE, lines);
            }
        }
        catch { }
    }

    /// <summary>计算验证段（20 位 hex）</summary>
    private static string ComputeVerifyString(string sn, string ltdName)
    {
        var raw = (sn ?? "").Replace("-", "").ToUpperInvariant()
                + "|" + (ltdName ?? "").ToUpperInvariant()
                + "|" + SECRET_KEY;
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var hex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        // 取前 20 位
        return hex.Substring(0, 20);
    }

    }