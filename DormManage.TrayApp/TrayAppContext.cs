using DormManage.TrayApp.Models;
using DormManage.TrayApp.Services;

namespace DormManage.TrayApp;

/// <summary>
/// 托盘应用上下文：管理 NotifyIcon + ProcessManager + HealthChecker 的生命周期。
///
/// 资源释放顺序（Dispose）：
/// 1. 停止 HealthChecker 探测循环
/// 2. 停止子进程（ProcessManager.StopAllAsync）
/// 3. 释放 NotifyIcon
/// </summary>
public sealed class TrayAppContext : ApplicationContext, IDisposable
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly HealthChecker _health;
    private readonly ProcessManager _process;
    private readonly NotifyIconManager _notifyIcon;

    public TrayAppContext(ConfigService config, LogService log)
    {
        _config = config;
        _log = log;
        _health = new HealthChecker(log, onCrashed: () => _process.HandleCrashAsync());
        _process = new ProcessManager(config, log, _health);

        // 把 ProcessManager 状态变更转发给 NotifyIcon
        _process.ServiceStateChanged += OnServiceStateChanged;
        _health.ServiceStateChanged += OnServiceStateChanged;

        // 健康检查器需要获取最新端口（配置变更时也会刷新）
        _health.Start(
            config.Current.Tray.HealthCheckIntervalSeconds,
            GetCurrentPorts);

        _notifyIcon = new NotifyIconManager(
            onOpenAdmin: OpenAdminBrowser,
            onOpenApi: OpenApiBrowser,
            onSettings: ShowSettings,
            onRestartAll: async () => await _process.RestartAllAsync(),
            onViewLogs: OpenLogsFolder,
            onAbout: ShowAbout,
            onExit: async () =>
            {
                _log.Info("用户请求退出");
                var ok = MessageBox.Show(
                    "确定要停止所有服务并退出托盘吗？",
                    "退出确认",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (ok != DialogResult.Yes) return;
                await ExitAsync();
            });

        // 初次启动：根据配置自动启动服务
        if (config.Current.Tray.AutoStartServices)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _process.StartAllAsync();
                }
                catch (Exception ex)
                {
                    _log.Error("自动启动失败", ex);
                }
            });
        }

        Application.ApplicationExit += (_, _) =>
        {
            // 同步等待停止（避免子进程成孤儿）
            try
            {
                _process.StopAllAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _log.Error("退出时停止服务异常", ex);
            }
        };
    }

    private (int ApiPort, int AdminPort) GetCurrentPorts()
    {
        var c = _config.Current;
        return (c.Tray.ApiPort, c.Tray.AdminPort);
    }

    private void OnServiceStateChanged(string name, ServiceState state)
    {
        _notifyIcon.UpdateServiceState(name, state);
    }

    private void OpenAdminBrowser()
    {
        var port = _config.Current.Tray.AdminPort;
        OpenBrowser($"http://localhost:{port}/");
    }

    private void OpenApiBrowser()
    {
        var port = _config.Current.Tray.ApiPort;
        OpenBrowser($"http://localhost:{port}/swagger/index.html");
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开浏览器：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowSettings()
    {
        using var form = new Forms.SettingsForm(_config, _log, _process, _health);
        form.ShowDialog();
        // 配置可能变更，重新读取端口
        var c = _config.Current;
        _log.Info($"配置刷新：ApiPort={c.Tray.ApiPort}, AdminPort={c.Tray.AdminPort}");
    }

    private void ShowAbout()
    {
        using var form = new Forms.AboutForm();
        form.ShowDialog();
    }

    private void OpenLogsFolder()
    {
        try
        {
            var logDir = string.IsNullOrWhiteSpace(_config.Current.Storage.LogRoot)
                ? Path.Combine(AppContext.BaseDirectory, "logs")
                : (Path.IsPathRooted(_config.Current.Storage.LogRoot)
                    ? _config.Current.Storage.LogRoot
                    : Path.Combine(AppContext.BaseDirectory, _config.Current.Storage.LogRoot));
            Directory.CreateDirectory(logDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开日志目录：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ExitAsync()
    {
        try
        {
            await _process.StopAllAsync();
        }
        catch (Exception ex)
        {
            _log.Error("退出时停止服务异常", ex);
        }
        finally
        {
            _health.Dispose();
            _notifyIcon.Hide();
            Application.Exit();
        }
    }

    public new void Dispose()
    {
        try { _health.Dispose(); } catch { }
        try { _notifyIcon.Dispose(); } catch { }
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}