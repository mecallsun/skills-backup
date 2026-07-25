using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace DormManage.Shared.Security;

/// <summary>
/// v2.13.155 托盘托管守卫（禁止独立启动）
///
/// 需求：所有 Web/Api 服务程序必须由 DormManage.TrayApp 托盘程序拉起，禁止独立双击运行。
///
/// 机制（两层防御，仅在 Release 生产构建中强制，Debug 开发不拦截）：
///   L1 签名握手令牌：托盘启动子进程前，用内嵌密钥对「childKey|托盘PID」做 HMAC-SHA256 签名，
///      通过环境变量 <see cref="HandshakeEnvVar"/> 注入；子进程用同一密钥重算签名比对。
///      → 双击 Admin.exe / Api.exe（无令牌）直接拒绝。
///   L2 父进程校验：子进程通过 NtQueryInformationProcess 取父进程，必须为 DormManage.TrayApp。
///      → 手工设置环境变量伪造令牌（但父进程非托盘）仍被拒绝。
///
/// 说明：这是「防误用 / 防独立运行」的托管边界，非高强度密码学边界；密钥在 Release 下
/// 由 Obfuscar HideStrings 加密，配合父进程校验，足以满足「禁止独立使用」的业务诉求。
/// </summary>
public static class TrayLaunchGuard
{
    /// <summary>握手令牌环境变量名（托盘注入 → 子进程读取校验）</summary>
    public const string HandshakeEnvVar = "DormManage_TRAY_HANDSHAKE";

    /// <summary>合法父进程名（不含 .exe）</summary>
    private const string TrayProcessName = "DormManage.TrayApp";

    /// <summary>子进程键：Admin / Api</summary>
    public const string ChildAdmin = "Admin";
    public const string ChildApi = "Api";

    // 内嵌握手密钥（Release 下经 Obfuscar HideStrings 加密）。
    private static readonly byte[] SecretKey =
        Encoding.UTF8.GetBytes("Jinge#Dorm@2026$Tray^Handshake&Key!v1");

    /// <summary>
    /// 托盘端调用：为指定子进程生成签名握手令牌，注入其环境变量。
    /// 令牌格式：childKey|托盘PID|HMAC(childKey|托盘PID)
    /// </summary>
    public static string CreateHandshakeToken(string childKey)
    {
        var trayPid = Environment.ProcessId;
        var payload = $"{childKey}|{trayPid}";
        return $"{payload}|{Sign(payload)}";
    }

    /// <summary>
    /// 子进程（Admin/Api）启动时调用：校验是否由托盘拉起。
    /// </summary>
    /// <param name="childKey">本程序标识（<see cref="ChildAdmin"/> / <see cref="ChildApi"/>）</param>
    /// <param name="reason">失败原因（用于日志/控制台提示）</param>
    /// <returns>true=合法（由托盘拉起）；false=非法（独立启动/伪造）</returns>
    public static bool VerifyLaunchedByTray(string childKey, out string reason)
    {
        // L1：握手令牌
        var token = Environment.GetEnvironmentVariable(HandshakeEnvVar);
        if (string.IsNullOrEmpty(token))
        {
            reason = "缺少托盘启动握手令牌（未通过托盘程序启动）";
            return false;
        }

        var parts = token.Split('|');
        if (parts.Length != 3)
        {
            reason = "握手令牌格式非法";
            return false;
        }
        if (!string.Equals(parts[0], childKey, StringComparison.Ordinal))
        {
            reason = $"握手令牌归属程序不匹配（令牌={parts[0]}，本程序={childKey}）";
            return false;
        }

        var expectSig = Sign($"{parts[0]}|{parts[1]}");
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(parts[2]),
                Encoding.ASCII.GetBytes(expectSig)))
        {
            reason = "握手令牌签名校验失败";
            return false;
        }

        // L2：父进程校验（尽力而为；无法判定父进程时依赖已通过的 L1 令牌）
        try
        {
            if (OperatingSystem.IsWindows() && TryGetParentProcess(out var parentName, out var parentPid))
            {
                if (!string.Equals(parentName, TrayProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"父进程非托盘程序（实际：{parentName}）";
                    return false;
                }
                if (int.TryParse(parts[1], out var tokenPid) && parentPid != tokenPid)
                {
                    reason = $"父进程 PID({parentPid}) 与令牌 PID({tokenPid}) 不一致";
                    return false;
                }
            }
        }
        catch
        {
            // 无法判定父进程（权限/平台差异）→ 不加严，依赖 L1 令牌结果
        }

        reason = "OK";
        return true;
    }

    private static string Sign(string payload)
    {
        using var h = new HMACSHA256(SecretKey);
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    // ── 父进程查询（Windows NtQueryInformationProcess）─────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    [SupportedOSPlatform("windows")]
    private static bool TryGetParentProcess(out string name, out int pid)
    {
        name = string.Empty;
        pid = 0;
        var pbi = new PROCESS_BASIC_INFORMATION();
        int status = NtQueryInformationProcess(
            Process.GetCurrentProcess().Handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
        if (status != 0) return false;

        pid = pbi.InheritedFromUniqueProcessId.ToInt32();
        if (pid <= 0) return false;

        try
        {
            using var p = Process.GetProcessById(pid);
            name = p.ProcessName; // 不含 .exe
            return true;
        }
        catch
        {
            // 父进程已退出/无法访问
            return false;
        }
    }
}
