using Microsoft.Win32;

namespace DormManage.TrayApp.Services;

/// <summary>
/// Windows 自启动注册管理（v2.13.3）
///
/// 实现方式：写入 HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// （无需管理员权限，仅当前用户生效）
///
/// 注册表项：DormManage.TrayApp（仅托盘程序）
/// 值：DormManage.TrayApp.exe 的完整路径
///
/// v2.13.137 完全托管模式：
/// - 仅托盘可注册自启动（DormManage.TrayApp）
/// - Admin/Api 子进程禁止自启动（必须依附托盘）
/// - 启动时自动清理历史可能存在的 DormManage.Admin / DormManage.Api 自启项
/// </summary>
public class AutoStartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DormManage.TrayApp";

    /// <summary>v2.13.137 禁止自启动的子进程名称列表</summary>
    private static readonly string[] ForbiddenAutoStartNames = new[]
    {
        "DormManage.Admin",
        "DormManage.Api"
    };

    /// <summary>查询当前是否已注册自启动（仅托盘）</summary>
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

            // v2.13.137 注册前先清理子进程的历史自启项
            CleanupForbiddenAutoStart();

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

    /// <summary>
    /// v2.13.137 清理禁止自启项（DormManage.Admin / DormManage.Api）
    /// 防止历史部署或手动操作残留的子进程自启动
    /// </summary>
    public int CleanupForbiddenAutoStart()
    {
        var removed = 0;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return 0;

            foreach (var name in ForbiddenAutoStartNames)
            {
                if (key.GetValue(name) != null)
                {
                    key.DeleteValue(name, throwOnMissingValue: false);
                    removed++;
                }
            }
        }
        catch { /* 忽略 */ }
        return removed;
    }
}