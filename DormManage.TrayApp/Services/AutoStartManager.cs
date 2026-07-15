using Microsoft.Win32;

namespace DormManage.TrayApp.Services;

/// <summary>
/// Windows 自启动注册管理（v2.13.3）
///
/// 实现方式：写入 HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// （无需管理员权限，仅当前用户生效）
///
/// 注册表项：DormManage.TrayApp
/// 值：DormManage.TrayApp.exe 的完整路径
/// </summary>
public class AutoStartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DormManage.TrayApp";

    /// <summary>查询当前是否已注册自启动</summary>
    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>获取当前注册的实际 EXE 路径</summary>
    public string? GetRegisteredPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>注册自启动（写入注册表）</summary>
    public bool Enable(string? exePath = null)
    {
        try
        {
            exePath ??= Environment.ProcessPath ?? AppContext.BaseDirectory;
            if (string.IsNullOrEmpty(exePath) || !global::System.IO.File.Exists(exePath))
                return false;

            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            // 完整路径 + 引号（处理路径中空格）
            key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>取消自启动</summary>
    public bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}