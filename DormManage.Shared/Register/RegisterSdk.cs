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
    /// <summary>
    /// v2.13.146 关键修复：.NET 8 默认不带 GBK codepage provider（CodePage 936）
    /// NPGS 算法 GetLtdSerialNum 用 Encoding.Default（中文 Windows = GBK）字节转 hex
    /// 没注册会抛 NotSupportedException，导致 RegisterSdk.CheckRegCDKey 算法 A 全部失败
    /// </summary>
    static RegisterSdk()
    {
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
        catch { /* 已注册过 */ }
    }

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
    /// v2.13.143 license 文件二进制格式（DPAPI 加密）：
    /// 结构 = magic(4) "JLDL" + version(1) 0x01 + DPAPI(LocalMachine) 加密密文
    /// 普通用户用 type / 文本编辑器查看只能看到乱码（二进制）
    /// </summary>
    private static readonly byte[] LICENSE_FILE_MAGIC = { 0x4A, 0x4C, 0x44, 0x4C }; // "JLDL"
    private const byte LICENSE_FILE_VERSION = 0x01;

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
    /// v2.13.146 修复：移除了硬性 `Length != 29` 检查，归一化逻辑下沉到 CheckRegCDKey。
    /// 兼容旧注册表里存了 25 字符 raw CDKEY 的场景（如 v2.13.142-v2.13.145 时期）。
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

        // v2.13.146：长度检查放宽到 25 字符（raw）或 29 字符（dashed），归一化由 CheckRegCDKey 内部完成
        if (cdkey.Replace("-", "").Length != 25)
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

        // v2.13.146：归一化回写（如果读出 25 字符 raw，自动升级到 29 字符 dashed 存储）
        if (checkResult.CDKEY != cdkey && !string.IsNullOrEmpty(checkResult.CDKEY))
        {
            reg.CDKEY = checkResult.CDKEY;
            try { WriteRegValue("CDKEY", checkResult.CDKEY); } catch { }
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
    /// 校验 CDKEY（v2.13.146 双算法兼容）
    ///
    /// 算法 A（NPGS.Register 原始算法 — Kingdee.NPGS.Core.Register.cs 1:1 等价层）：
    ///   - 公司名编码：Encoding.Default（GBK）字节 → 大写 hex 拼接
    ///   - 验证段生成：MD5(SN) 或 MD5(LTDName_GBK_hex) → 25 hex uppercase 5-5-5-5-5
    ///   - 日期嵌入：在 base CDKEY 的第 [2,8,14,20] 位插入 ConvertInt10To36(yyMMdd)
    ///   - 末 5 位：MD5(yyyyMMdd).ToUpper().Substring(0, 5)
    ///   - CDKEY 校验：GetRegCDKey(GetSnCDKey(SN), VDate) == 用户CDKEY
    ///               或 GetRegCDKey(GetLtdCDKey(LTDName), VDate) == 用户CDKEY
    ///   - 字符集：[0-9A-Z]（A-Z 合法，不止 hex 的 a-f）
    ///
    /// 算法 B（v2.13.94 本项目原生算法 — 向后兼容）：
    ///   - 验证段 = MD5(SN + "|" + LTDName + "|" + SECRET_KEY).Substring(0, 20).ToUpper()
    ///   - 末 5 位 = HEX(YYYY-MM-DD)
    ///
    /// v2.13.146 修复：原 v2.13.94 仅算法 B，导致 NPGS 风格 CDKEY 验证失败。
    /// 改为先试算法 A（SN 路径 + LTDName 路径），失败再试算法 B，任一通过 RegInt=1。
    ///
    /// v2.13.146 二次修复：硬性 `Length != 29` 检查导致 25 字符 raw 输入直接拒绝。
    /// LicenseForm.TryNormalizeCDKey 剥离连字符后传 25 字符，验证窗口立刻返回 RegInt=0。
    /// 改为统一归一化：接受 25 字符 raw 或 29 字符 dashed，内部统一转 dashed 后比较。
    /// </summary>
    public static RegItem CheckRegCDKey(RegItem input)
    {
        var result = new RegItem
        {
            SN = input.SN,
            CDKEY = input.CDKEY,
            LTDName = input.LTDName
        };

        if (string.IsNullOrEmpty(input.CDKEY))
        {
            result.RegInt = 0;
            return result;
        }

        // v2.13.146 二次修复：归一化为「29 字符 dashed 形式」，与 GetRegCDKey 输出严格对齐
        var cdkeyDashed = NormalizeCDKeyToDashed(input.CDKEY);
        if (cdkeyDashed == null)
        {
            result.RegInt = 0;
            return result;
        }
        var cdkeyRaw = cdkeyDashed.Replace("-", "").ToUpperInvariant();

        var snRaw = (input.SN ?? "").Replace("-", "").ToUpperInvariant();
        var ltdRaw = input.LTDName ?? "";

        // === 算法 A：NPGS.Register 原始算法 ===
        // Step 1: 先用 NPGS 算法解日期（GetDateByRegCDKey）— 已支持 25/29 字符两种形式
        var npgsExpDate = GetDateByRegCDKey(cdkeyDashed);
        if (npgsExpDate > DateTime.MinValue)
        {
            // Step 2: SN 路径
            if (snRaw.Length == 24)
            {
                var snBase = GetCDKey(snRaw);  // MD5(SN) → 29 字符串 (含连字符)
                var snFull = GetRegCDKey(snBase, npgsExpDate);
                if (string.Equals(snFull, cdkeyDashed, StringComparison.OrdinalIgnoreCase))
                {
                    result.RegInt = 1;
                    result.RegDate = npgsExpDate;
                    result.CDKEY = cdkeyDashed;  // 回写归一化形式
                    return result;
                }
            }

            // Step 3: LTDName 路径（GBK 编码字节 → hex 拼接 → MD5）
            if (!string.IsNullOrEmpty(ltdRaw))
            {
                var ltdBase = GetCDKey(GetLtdSerialNum(ltdRaw));  // 29 字符串
                var ltdFull = GetRegCDKey(ltdBase, npgsExpDate);
                if (string.Equals(ltdFull, cdkeyDashed, StringComparison.OrdinalIgnoreCase))
                {
                    result.RegInt = 1;
                    result.RegDate = npgsExpDate;
                    result.CDKEY = cdkeyDashed;
                    return result;
                }
            }
        }

        // === 算法 B：v2.13.94 本项目原生算法（向后兼容） ===
        if (snRaw.Length != 24)
        {
            result.RegInt = 0;
            return result;
        }

        var dateStr = cdkeyRaw.Substring(20, 5);
        var verifyStr = cdkeyRaw.Substring(0, 20);
        var expected = ComputeVerifyString(snRaw, ltdRaw);
        if (verifyStr == expected)
        {
            try
            {
                var year = 2000 + Convert.ToInt32(dateStr.Substring(0, 2), 16);
                var month = Convert.ToInt32(dateStr.Substring(2, 1), 16) + 1;
                var day = Convert.ToInt32(dateStr.Substring(3, 2), 16);
                result.RegDate = new DateTime(year, Math.Max(1, month), Math.Max(1, Math.Min(day, 28)));
                result.RegInt = 1;
                result.CDKEY = cdkeyDashed;
                return result;
            }
            catch { }
        }

        result.RegInt = 0;
        return result;
    }

    /// <summary>
    /// v2.13.146 二次修复：CDKEY 归一化为 29 字符 dashed 形式 (5-5-5-5-5)
    /// 接受两种输入：25 字符 raw（LicenseForm TryNormalizeCDKey 剥离连字符后）
    ///             或 29 字符 dashed（用户手动输入或 ReadRegValue 读取）
    /// 任何不合规输入返回 null（上层判定为 RegInt=0）
    /// </summary>
    private static string? NormalizeCDKeyToDashed(string? input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        var cleaned = input.Replace("-", "").Trim().ToUpperInvariant();
        if (cleaned.Length != 25) return null;

        // 字符集校验：[0-9A-Z]（NPGS 36 进制，A-Z 全大写字母合法）
        foreach (var c in cleaned)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'))) return null;
        }

        // 重新插入连字符：5-5-5-5-5 = 29 字符
        return $"{cleaned.Substring(0, 5)}-{cleaned.Substring(5, 5)}-{cleaned.Substring(10, 5)}-{cleaned.Substring(15, 5)}-{cleaned.Substring(20, 5)}";
    }

    /// <summary>
    /// 解码 CDKEY 得到注册到期日（v2.13.146 双算法兼容）
    /// 算法 A：NPGS 36 进制日期编码（位置 [2,8,14,20] 在**含连字符的 CDKEY** 中）+ 末 5 位 MD5(yyyyMMdd) 校验
    ///        关键：NPGS 用的是 RegCDKey.Substring(2,1) 等 — 即原始 29 字符串上的索引，不是 raw 去连字符后的索引
    /// 算法 B：v2.13.94 末 5 位 HEX(YYYY-MM-DD)
    /// 任一解析成功即返回日期；都失败返回 DateTime.MinValue
    /// </summary>
    public static DateTime GetDateByRegCDKey(string cdkey)
    {
        if (string.IsNullOrEmpty(cdkey)) return DateTime.MinValue;
        var raw = cdkey.Replace("-", "").ToUpperInvariant();
        if (raw.Length < 25) return DateTime.MinValue;

        // === 算法 A：NPGS 36 进制日期解码 ===
        // 关键：必须在**含连字符**的 CDKEY（29 位）上取索引，不是 raw
        // 1) 如果用户传入 29 位（含连字符）→ 直接用
        // 2) 如果用户传入 25 位（raw）→ 重新插入连字符恢复原 29 位
        var original = cdkey.Length == 29 ? cdkey : (raw.Substring(0, 5) + "-" + raw.Substring(5, 5) + "-" + raw.Substring(10, 5) + "-" + raw.Substring(15, 5) + "-" + raw.Substring(20, 5));
        try
        {
            // NPGS 原始代码: RegCDKey.Substring(2, 1) + RegCDKey.Substring(8, 1) + RegCDKey.Substring(14, 1) + RegCDKey.Substring(20, 1)
            var text36 = original.Substring(2, 1) + original.Substring(8, 1) + original.Substring(14, 1) + original.Substring(20, 1);
            var decimalVal = Convert36ToInt10(text36);
            var decimalStr = decimalVal.ToString().PadLeft(6, '0');
            if (decimalStr.Length >= 6)
            {
                var dateStr = "20" + decimalStr.Substring(0, 2) + "-" + decimalStr.Substring(2, 2) + "-" + decimalStr.Substring(4, 2);
                var expDate = DateTime.ParseExact(dateStr, "yyyy-MM-dd", null);

                // 验证末 5 位 = MD5(yyyyMMdd).ToUpper().Substring(0, 5)
                var desDate = ComputeMD5AsciiUpper(expDate.ToString("yyyyMMdd")).Substring(0, 5);
                if (desDate == raw.Substring(20, 5))
                {
                    return expDate;
                }
            }
        }
        catch { }

        // === 算法 B：v2.13.94 末 5 位 HEX 日期（向后兼容） ===
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

    // ============================================================
    // v2.13.146 NPGS 算法 1:1 等价层（Public.Core.SDK.Register）
    // ============================================================

    /// <summary>
    /// NPGS 公司名序列号：Encoding.Default.GetBytes(ltdName) → 每个字节大写 hex 拼接
    /// Encoding.Default 在中文 Windows = GBK 编码（关键！与 UTF-8 不同）
    /// </summary>
    private static string GetLtdSerialNum(string ltdName)
    {
        // NPGS 原始：C# Encoding.Default.GetBytes(LtdName) → each byte.ToString("X")
        // 中文 Windows 上 Encoding.Default = GBK
        try
        {
            var gbk = Encoding.GetEncoding(936);  // GBK = code page 936
            var bytes = gbk.GetBytes(ltdName ?? "");
            return string.Concat(bytes.Select(b => b.ToString("X")));
        }
        catch
        {
            // 兜底用系统默认编码
            var bytes = Encoding.Default.GetBytes(ltdName ?? "");
            return string.Concat(bytes.Select(b => b.ToString("X")));
        }
    }

    /// <summary>
    /// NPGS CDKEY base 生成：MD5(isNum) → 25 hex uppercase → 5-5-5-5-5 格式
    /// 与原 NPGS `GetCDKey` 等价（不含日期嵌入）
    /// </summary>
    private static string GetCDKey(string isNum)
    {
        var md5Hash = ComputeMD5Ascii(isNum ?? "");  // 32 hex lowercase
        var raw25 = md5Hash.Substring(0, 25).ToUpperInvariant();
        return raw25.Substring(0, 5) + "-" + raw25.Substring(5, 5) + "-" +
               raw25.Substring(10, 5) + "-" + raw25.Substring(15, 5) + "-" +
               raw25.Substring(20, 5);
    }

    /// <summary>
    /// NPGS GetRegCDKey：在 base CDKEY 第 [2,8,14,20] 位嵌入日期字符 + 末尾追加 MD5(yyyyMMdd) 前 5 位
    /// </summary>
    private static string GetRegCDKey(string baseKey, DateTime vdate)
    {
        try
        {
            // Text = vdate.ToString("yyMMdd")（6 位数字字符串）
            var text = vdate.ToString("yyMMdd");
            var text36 = ConvertInt10To36(text);  // 36 进制 → [0-9A-Z]

            if (text36.Length < 4) return baseKey;  // 安全兜底

            var key = baseKey;
            key = ChangeByChar(key, 2, text36[0].ToString());
            key = ChangeByChar(key, 8, text36[1].ToString());
            key = ChangeByChar(key, 14, text36[2].ToString());
            key = ChangeByChar(key, 20, text36[3].ToString());

            var desDate = ComputeMD5AsciiUpper(vdate.ToString("yyyyMMdd")).Substring(0, 5);
            return key.Substring(0, 24) + desDate;
        }
        catch
        {
            return baseKey;
        }
    }

    /// <summary>
    /// NPGS ChangeByChar：把 key[where] 替换成 word
    /// </summary>
    private static string ChangeByChar(string key, int where, string word)
    {
        if (string.IsNullOrEmpty(key)) return word;
        if (where == 0) return word + key.Substring(1);
        if (where >= key.Length - 1) return key.Substring(0, key.Length - 1) + word;
        var a = key.Substring(0, where);
        var b = key.Substring(where + 1);
        return a + word + b;
    }

    /// <summary>
    /// NPGS ConvertInt10To36：10 进制数字字符串 → 36 进制字符 [0-9A-Z]
    /// </summary>
    private static string ConvertInt10To36(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        if (!long.TryParse(input, out var i)) return "";

        var sb = new StringBuilder();
        while (i > 35)
        {
            var j = i % 36;
            sb.Append(j <= 9 ? (char)('0' + j) : (char)('A' + j - 10));
            i = i / 36;
        }
        sb.Append(i <= 9 ? (char)('0' + i) : (char)('A' + i - 10));

        var chars = sb.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// NPGS Convert36ToInt10：36 进制字符 [0-9A-Z] → 10 进制整数
    /// </summary>
    private static int Convert36ToInt10(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;
        int result = 0;
        int baseValue = 1;
        for (int i = input.Length - 1; i >= 0; i--)
        {
            var c = input[i];
            int digit;
            if (c >= '0' && c <= '9') digit = c - '0';
            else if (c >= 'A' && c <= 'Z') digit = c - 'A' + 10;
            else if (c >= 'a' && c <= 'z') digit = c - 'a' + 10;  // 容错
            else return 0;
            result += digit * baseValue;
            baseValue *= 36;
        }
        return result;
    }

    /// <summary>
    /// NPGS MD5：Encoding.ASCII.GetBytes + MD5 + hex lowercase
    /// </summary>
    private static string ComputeMD5Ascii(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input ?? "");
        var hash = MD5.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// NPGS MD5：Encoding.ASCII.GetBytes + MD5 + hex UPPERCASE
    /// </summary>
    private static string ComputeMD5AsciiUpper(string input)
    {
        return ComputeMD5Ascii(input).ToUpperInvariant();
    }

    /// <summary>
    /// 写入注册信息（HKLM 优先 → HKCU 回退 → 文件兜底）
    /// v2.13.146 修复：CDKEY 统一归一化为 29 字符 dashed 形式，避免下次 CheckReg() 读出
    /// 25 字符 raw 又被「Length != 29」检查拒绝。
    /// </summary>
    public static bool WriteRegItem(RegItem reg)
    {
        try
        {
            // v2.13.146：归一化为 dashed 形式，确保后续 CheckReg() 读回时长度/格式正确
            var cdkeyDashed = NormalizeCDKeyToDashed(reg.CDKEY) ?? reg.CDKEY;
            WriteRegValue("CDKEY", cdkeyDashed);
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

        // 3) 文件（v2.13.143 DPAPI 加密，向前兼容 v2.13.142 明文）
        try
        {
            if (System.IO.File.Exists(LICENSE_FILE))
            {
                var kv = ReadLicenseFile();
                if (kv != null && kv.TryGetValue(name, out var v)) return v;
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

        // 3) 文件（v2.13.143 DPAPI 加密）
        try
        {
            // 先读出已有 KV（自动解密或兼容明文）
            var kv = ReadLicenseFile() ?? new Dictionary<string, string>();
            kv[name] = value;
            WriteLicenseFile(kv);
        }
        catch { }
    }

    /// <summary>
    /// v2.13.143：读取 license 文件
    /// 1) 检测 magic "JLDL" + version=0x01 → 新 DPAPI 加密格式
    /// 2) 否则 → 向后兼容 v2.13.142 之前的明文 NAME=VALUE 行
    /// 失败时返回 null（让上层走 HKLM/HKCU fallback）
    /// </summary>
    private static Dictionary<string, string>? ReadLicenseFile()
    {
        if (!System.IO.File.Exists(LICENSE_FILE)) return null;
        try
        {
            var allBytes = System.IO.File.ReadAllBytes(LICENSE_FILE);
            if (allBytes.Length < 5) return null;

            // 检测 magic + version（新格式）
            bool isNewFormat =
                allBytes[0] == LICENSE_FILE_MAGIC[0] &&
                allBytes[1] == LICENSE_FILE_MAGIC[1] &&
                allBytes[2] == LICENSE_FILE_MAGIC[2] &&
                allBytes[3] == LICENSE_FILE_MAGIC[3] &&
                allBytes[4] == LICENSE_FILE_VERSION;

            string[] lines;
            if (isNewFormat)
            {
                // DPAPI 解密
                var cipher = new byte[allBytes.Length - 5];
                System.Buffer.BlockCopy(allBytes, 5, cipher, 0, cipher.Length);
                var plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.LocalMachine);
                lines = Encoding.UTF8.GetString(plain).Split('\n');
            }
            else
            {
                // 旧明文格式（v2.13.142 及之前），直接按行解析
                lines = System.IO.File.ReadAllLines(LICENSE_FILE);
            }

            var dict = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var idx = line.IndexOf('=');
                if (idx > 0)
                {
                    var key = line.Substring(0, idx);
                    var val = line.Substring(idx + 1);
                    if (!string.IsNullOrEmpty(key)) dict[key] = val;
                }
            }
            return dict.Count > 0 ? dict : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// v2.13.143：写入 license 文件（DPAPI 加密格式 magic=JLDL + version=0x01 + cipher）
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void WriteLicenseFile(Dictionary<string, string> kv)
    {
        var dir = System.IO.Path.GetDirectoryName(LICENSE_FILE);
        if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

        // 序列化 NAME=VALUE 行
        var plain = string.Join("\n", kv
            .Where(p => !string.IsNullOrEmpty(p.Key))
            .Select(p => $"{p.Key}={p.Value}"));
        var plainBytes = Encoding.UTF8.GetBytes(plain);

        // DPAPI LocalMachine 加密（同一台机器任何进程都可解密）
        var cipher = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

        // 写 magic + version + cipher
        using var fs = System.IO.File.Create(LICENSE_FILE);
        fs.Write(LICENSE_FILE_MAGIC, 0, LICENSE_FILE_MAGIC.Length);
        fs.WriteByte(LICENSE_FILE_VERSION);
        fs.Write(cipher, 0, cipher.Length);
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