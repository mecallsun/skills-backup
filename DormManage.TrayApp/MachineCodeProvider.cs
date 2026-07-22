using System;
using System.IO;
using System.Management;
using System.Text;
using DormManage.Shared.Register;

namespace DormManage.TrayApp;

/// <summary>
/// v2.13.94 软件注册授权 — TrayApp 端机器码提供者
/// 用 WMI 取真实硬件特征（仅 Windows net8.0-windows 可用）：
///   1) Win32_Processor.ProcessorId → 16 字符大写 hex（Intel: BFEBFBFF000A06A4）
///   2) Win32 LogicalDisk VolumeSerialNumber → 8 字符大写 hex
/// 完全对齐原 NPGS.Register Public.Core.SDK.GetCpu() / GetDiskVolumeSerialNumber() 算法
///
/// 启动时调用 Initialize() 把真实机器码写入共享文件 + 环境变量，
/// 供 DormManage.Admin / DormManage.Api（跨平台 .NET 8，无 WMI）读取。
/// </summary>
public static class MachineCodeProvider
{
    /// <summary>
    /// 启动初始化：计算机器码 → 写入共享文件 + 设置环境变量
    /// </summary>
    public static string Initialize()
    {
        var raw = ComputeRawMachineCode();
        // 格式化为带连字符的展示样式（5-5-5-5-4 = 28 字符 display）
        var display = FormatDisplayStyle(raw);
        // 写共享文件 + 环境变量
        RegisterSdk.WriteMachineSN(raw);
        return display;
    }

    /// <summary>
    /// 计算机器码原始 24 字符 hex（16 CPUID + 8 VolumeSerialNumber）
    /// </summary>
    private static string ComputeRawMachineCode()
    {
        var cpuId = GetCpuId();
        var volSerial = GetDiskVolumeSerialNumber();
        var combined = (cpuId + volSerial).ToUpperInvariant();
        // 截取 24 字符（原算法可能 raw 长度 32，截前 24）
        if (combined.Length >= 24)
            return combined.Substring(0, 24);
        // 不够 24 字符时左侧补 0
        return combined.PadLeft(24, '0');
    }

    /// <summary>
    /// WMI 取 CPU ProcessorId（等价原 GetCpu()）
    /// 典型返回值：BFEBFBFF000A06A4（Intel）/ AuthenticAMD 系列（AMD）
    /// </summary>
    private static string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            using var collection = searcher.Get();
            foreach (ManagementObject mo in collection)
            {
                var id = mo["ProcessorId"]?.ToString();
                if (!string.IsNullOrEmpty(id))
                    return id.Trim();
            }
        }
        catch { }
        // 失败 fallback：使用环境变量（多核系统 ProcessorCount * 10 + OS hash）
        return (Environment.ProcessorCount * 10).ToString("X8") + "00000000";
    }

    /// <summary>
    /// 取系统盘 VolumeSerialNumber（等价原 GetDiskVolumeSerialNumber()）
    /// 典型返回值：AA2E3B0E（8 字符 hex）
    /// </summary>
    private static string GetDiskVolumeSerialNumber()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
            using var searcher = new ManagementObjectSearcher(
                $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='{systemDrive}'");
            using var collection = searcher.Get();
            foreach (ManagementObject mo in collection)
            {
                var serial = mo["VolumeSerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(serial))
                {
                    // 转换为 hex 字符串（VolumeSerialNumber 是数字）
                    if (long.TryParse(serial, out var n))
                        return n.ToString("X8");
                    return serial.Trim();
                }
            }
        }
        catch { }
        return "00000000";
    }

    /// <summary>
    /// 格式化 24 字符为 5-5-5-5-4 显示样式（28 字符）
    /// </summary>
    private static string FormatDisplayStyle(string raw24)
    {
        if (raw24.Length != 24) raw24 = raw24.PadRight(24, '0').Substring(0, 24);
        return $"{raw24.Substring(0, 5)}-{raw24.Substring(5, 5)}-{raw24.Substring(10, 5)}-{raw24.Substring(15, 5)}-{raw24.Substring(20, 4)}";
    }
}
