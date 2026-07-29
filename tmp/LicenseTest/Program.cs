using System;
using System.Text;
using DormManage.Shared.Register;
using DormManage.Shared.Security;
using DormManage.Shared.Services;

namespace LicenseTest;

/// <summary>
/// 本机注册授权完整测试 CLI 工具（无需 GUI 即可模拟 LicenseForm + TrayApp 完整流程）
///
/// 使用：
///   1. status        — 查看当前注册状态
///   2. register SN LTD CDKEY — 注册（SN/公司名/CDKEY）
///   3. validate SN LTD CDKEY — 仅校验，不写入
///   4. clear         — 清除注册信息
///   5. simulate      — 模拟「重启」后读取持久化数据并校验
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var cmd = args[0].ToLowerInvariant();
        try
        {
            switch (cmd)
            {
                case "status":
                    DoStatus();
                    return 0;
                case "register":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("用法：register SN LTDName CDKEY");
                        return 1;
                    }
                    return DoRegister(args[1], args[2], args[3]) ? 0 : 1;
                case "validate":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("用法：validate SN LTDName CDKEY");
                        return 1;
                    }
                    return DoValidate(args[1], args[2], args[3]) ? 0 : 1;
                case "clear":
                    return DoClear() ? 0 : 1;
                case "simulate":
                    return DoSimulate() ? 0 : 1;
                default:
                    PrintHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"异常：{ex.Message}");
            return 2;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("用法:");
        Console.WriteLine("  LicenseTest status                                  - 查看当前注册状态");
        Console.WriteLine("  LicenseTest register <SN> <LTDName> <CDKEY>         - 注册");
        Console.WriteLine("  LicenseTest validate <SN> <LTDName> <CDKEY>         - 仅校验");
        Console.WriteLine("  LicenseTest clear                                  - 清除注册信息");
        Console.WriteLine("  LicenseTest simulate                               - 模拟重启读取并校验");
    }

    private static void DoStatus()
    {
        Console.WriteLine("=== 当前注册状态 ===");
        var reg = RegisterSdk.CheckReg();
        Console.WriteLine($"机器码 (SN):     {reg.SN}");
        Console.WriteLine($"公司名 (LTD):    {reg.LTDName}");
        Console.WriteLine($"注册码 (CDKEY):  {reg.CDKEY}");
        Console.WriteLine($"有效期 (RegDate):{reg.RegDate?.ToString("yyyy-MM-dd") ?? "—"}");
        Console.WriteLine($"使用次数:        {reg.UseTimes} / {RegisterSdk.TRIAL_LIMIT}");
        Console.WriteLine($"状态 (RegInt):   {reg.RegInt} (1=已注册/0=已过期/-1=未注册/2=公司名不符)");
        Console.WriteLine();
        Console.WriteLine($"判定结果:        {(reg.RegInt == 1 && reg.RegDate >= DateTime.Today ? "✅ 注册有效" : "❌ 无效")}");

        // 模拟 LicenseGuard.IsReadOnly
        bool isReadOnly;
        string reason;
        if (reg.RegInt != 1) { isReadOnly = true; reason = $"RegInt={reg.RegInt}"; }
        else if (!reg.RegDate.HasValue) { isReadOnly = true; reason = "RegDate 缺失"; }
        else if (reg.RegDate.Value.Date < DateTime.Today) { isReadOnly = true; reason = $"已过期（{reg.RegDate:yyyy-MM-dd} < {DateTime.Today:yyyy-MM-dd}）"; }
        else { isReadOnly = false; reason = "RegInt=1 + RegDate >= today"; }

        Console.WriteLine($"全局只读模式:    {(isReadOnly ? "🔒 是" : "🔓 否")}");
        Console.WriteLine($"原因:            {reason}");
    }

    private static bool DoRegister(string sn, string ltd, string cdkey)
    {
        Console.WriteLine("=== 注册流程 ===");

        // Step 1: 验证 CDKEY
        Console.WriteLine($"[1/4] 验证 CDKEY...");
        var check = RegisterSdk.CheckRegCDKey(new RegItem { CDKEY = cdkey, SN = sn, LTDName = ltd });
        if (check.RegInt != 1)
        {
            Console.WriteLine($"  ❌ 验证失败 RegInt={check.RegInt}");
            Console.WriteLine($"  请检查：SN={sn.Length}位、公司名编码、CDKEY 是否与供应商一致");
            return false;
        }
        Console.WriteLine($"  ✅ RegInt={check.RegInt}, RegDate={check.RegDate:yyyy-MM-dd}");

        // Step 2: 写入持久化
        Console.WriteLine($"[2/4] 持久化注册信息 (HKLM/HKCU/DPAPI 文件)...");
        var reg = new RegItem
        {
            SN = sn,
            CDKEY = check.CDKEY,
            LTDName = ltd,
            RegDate = check.RegDate,
            RegInt = 1
        };
        if (!RegisterSdk.WriteRegItem(reg))
        {
            Console.WriteLine($"  ⚠️ 写入失败（HKLM 无权限 → 回退 HKCU → 文件兜底）");
        }
        else
        {
            Console.WriteLine($"  ✅ 已写入");
        }

        // Step 3: 重置缓存
        Console.WriteLine($"[3/4] 重置 LicenseGuard 缓存...");
        LicenseGuard.ResetCache();
        Console.WriteLine($"  ✅ 已重置");

        // Step 4: 立即重新读取
        Console.WriteLine($"[4/4] 重新读取校验...");
        var restored = RegisterSdk.CheckReg();
        if (restored.RegInt == 1 && restored.RegDate == check.RegDate)
        {
            Console.WriteLine($"  ✅ 重新读取成功");
            Console.WriteLine();
            Console.WriteLine($"🎉 注册成功！有效期至 {restored.RegDate:yyyy年MM月dd日}");
            return true;
        }
        Console.WriteLine($"  ❌ 重新读取失败 RegInt={restored.RegInt}");
        return false;
    }

    private static bool DoValidate(string sn, string ltd, string cdkey)
    {
        Console.WriteLine("=== CDKEY 校验 ===");
        Console.WriteLine($"输入 SN:    {sn}");
        Console.WriteLine($"输入 LTD:   {ltd}");
        Console.WriteLine($"输入 CDKEY: {cdkey}");
        Console.WriteLine();

        var check = RegisterSdk.CheckRegCDKey(new RegItem { CDKEY = cdkey, SN = sn, LTDName = ltd });
        Console.WriteLine($"结果:        RegInt={check.RegInt}");
        Console.WriteLine($"            RegDate={check.RegDate?.ToString("yyyy-MM-dd") ?? "—"}");
        Console.WriteLine($"            CDKEY(归一化)={check.CDKEY}");
        Console.WriteLine();
        Console.WriteLine(check.RegInt == 1 ? "✅ 校验通过：注册码有效" : "❌ 校验失败");
        return check.RegInt == 1;
    }

    private static bool DoClear()
    {
        Console.WriteLine("=== 清除注册信息 ===");
        RegisterSdk.DeleteRegAll();
        var reg = RegisterSdk.CheckReg();
        if (reg.RegInt == -1)
        {
            Console.WriteLine("✅ 已清除（RegInt=-1）");
            return true;
        }
        Console.WriteLine($"⚠️ 清除后仍 RegInt={reg.RegInt}");
        return false;
    }

    private static bool DoSimulate()
    {
        Console.WriteLine("=== 模拟「重启托盘」后读取持久化数据 ===");
        Console.WriteLine();

        // 模拟 TrayAppContext 启动流程
        Console.WriteLine("[阶段 1] TrayApp 启动 → 调用 RegisterSdk.CheckReg()");
        var reg = RegisterSdk.CheckReg();

        Console.WriteLine($"  SN:        {reg.SN}");
        Console.WriteLine($"  LTDName:   {reg.LTDName}");
        Console.WriteLine($"  CDKEY:     {reg.CDKEY}");
        Console.WriteLine($"  RegDate:   {reg.RegDate?.ToString("yyyy-MM-dd") ?? "—"}");
        Console.WriteLine($"  UseTimes:  {reg.UseTimes}");
        Console.WriteLine($"  RegInt:    {reg.RegInt}");
        Console.WriteLine();

        // 模拟启动校验日志输出
        Console.WriteLine("[阶段 2] 启动自动校验日志输出（参考 TrayAppContext.StartAutomaticValidationLogging）");
        string log;
        if (reg.RegInt == 1 && reg.RegDate.HasValue && reg.RegDate.Value.Date >= DateTime.Today)
        {
            log = $"✅ 启动自动校验通过：注册有效 至 {reg.RegDate:yyyy-MM-dd}（LTD={reg.LTDName}）";
        }
        else if (reg.RegInt == -1)
        {
            log = $"⚠️ 启动自动校验：未注册（试用第 {reg.UseTimes} 次 / {RegisterSdk.TRIAL_LIMIT}），启动 Api/Admin 仍可继续";
        }
        else
        {
            log = $"🚫 启动自动校验未通过：RegInt={reg.RegInt} → 全局只读模式";
        }
        Console.WriteLine($"  [AUTO-VALIDATE] {log}");
        Console.WriteLine();

        // 模拟全局只读模式判定
        Console.WriteLine("[阶段 3] LicenseGuard.IsReadOnly() 全局只读模式判定");
        bool readOnly;
        string reason;
        if (reg.RegInt != 1) { readOnly = true; reason = $"RegInt={reg.RegInt}"; }
        else if (!reg.RegDate.HasValue) { readOnly = true; reason = "RegDate 缺失"; }
        else if (reg.RegDate.Value.Date < DateTime.Today) { readOnly = true; reason = $"已过期（{reg.RegDate:yyyy-MM-dd} < {DateTime.Today:yyyy-MM-dd}）"; }
        else { readOnly = false; reason = "RegInt=1 + RegDate >= today"; }

        Console.WriteLine($"  IsReadOnly:  {readOnly}");
        Console.WriteLine($"  Reason:      {reason}");
        Console.WriteLine();

        // 模拟 LicenseMonitor 周期监控
        Console.WriteLine("[阶段 4] LicenseMonitor 5s 周期自动校验（模拟 1 次）");
        var monitor = new LicenseMonitor(
            checkRegFunc: () => RegisterSdk.CheckReg(),
            onChanged: state => Console.WriteLine($"  [LICENSE-PUSH] 状态变化: RegInt={state.RegInt}"),
            intervalSeconds: 5
        );
        monitor.Start();
        System.Threading.Thread.Sleep(1500);
        // LicenseMonitor 没有 Stop，依靠 GC + 内部 Timer.Dispose
        Console.WriteLine($"  ✅ LicenseMonitor 启动并完成 1 次校验（无错误即正常）");
        Console.WriteLine();

        Console.WriteLine("=== 模拟完成 ===");
        return !readOnly;
    }
}