using DormManage.TrayApp.Models;
using DormManage.TrayApp.Services;
using DormManage.Shared.Services;

namespace DormManage.TrayApp;

/// <summary>
/// 托盘应用上下文：管理 NotifyIcon + ProcessManager + HealthChecker + IPC Server 的生命周期。
///
/// 资源释放顺序（Dispose）：
/// 1. 停止 IPC Server（不再接收命令）
/// 2. 停止 HealthChecker 探测循环
/// 3. 释放 NotifyIcon
/// </summary>
public sealed class TrayAppContext : ApplicationContext, IDisposable
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly HealthChecker _health;
    private readonly ProcessManager _process;
    private readonly NotifyIconManager _notifyIcon;
    private readonly IpcServer? _ipcServer;

    public TrayAppContext(ConfigService config, LogService log)
    {
        _config = config;
        _log = log;
        _health = new HealthChecker(log, onCrashed: () => _process.HandleCrashAsync());
        _process = new ProcessManager(config, log, _health);

        _process.ServiceStateChanged += OnServiceStateChanged;
        _health.ServiceStateChanged += OnServiceStateChanged;

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

        // 启动 IPC Server（接收 Web Admin 命令：ping/status/start/stop/restart）
        _ipcServer = new IpcServer(ServiceIpc.DefaultPort, HandleIpcCommand);
        _ipcServer.Start();
        _log.Info($"IPC Server 已启动，监听 127.0.0.1:{ServiceIpc.DefaultPort}");
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

    #region IPC 命令处理（v2.13.3 新增）

    private void HandleIpcCommand(ServiceIpc.IpcCommand cmd, Action<ServiceIpc.IpcResponse> respond)
    {
        _log.Info($"IPC 收到命令：{cmd.Command} service={cmd.Service ?? "-"}");
        try
        {
            switch (cmd.Command?.ToLowerInvariant())
            {
                case "ping":
                    respond(new ServiceIpc.IpcResponse
                    {
                        Success = true,
                        Message = "pong",
                        Data = new { version = "v2.13.3" }
                    });
                    break;

                case "status":
                    respond(new ServiceIpc.IpcResponse
                    {
                        Success = true,
                        Message = "ok",
                        Data = new
                        {
                            api = new { state = _health.ApiState.ToString(), port = _config.Current.Tray.ApiPort },
                            admin = new { state = _health.AdminState.ToString(), port = _config.Current.Tray.AdminPort }
                        }
                    });
                    break;

                case "start":
                    _ = HandleStartCommandAsync(cmd.Service, respond);
                    break;

                case "stop":
                    _ = HandleStopCommandAsync(cmd.Service, respond);
                    break;

                case "restart":
                    _ = HandleRestartCommandAsync(cmd.Service, respond);
                    break;

                default:
                    respond(new ServiceIpc.IpcResponse { Success = false, Message = $"未知命令：{cmd.Command}" });
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"IPC 命令处理异常：{cmd.Command}", ex);
            respond(new ServiceIpc.IpcResponse { Success = false, Message = ex.Message });
        }
    }

    private async Task HandleStartCommandAsync(string? service, Action<ServiceIpc.IpcResponse> respond)
    {
        try
        {
            if (service is null || service == "all" || service == "api")
                await _process.StartApiAsync();
            if (service is null || service == "all" || service == "admin")
            {
                await Task.Delay(1000);
                await _process.StartAdminAsync();
            }
            respond(new ServiceIpc.IpcResponse { Success = true, Message = $"已启动 {service ?? "all"}" });
        }
        catch (Exception ex)
        {
            respond(new ServiceIpc.IpcResponse { Success = false, Message = ex.Message });
        }
    }

    private async Task HandleStopCommandAsync(string? service, Action<ServiceIpc.IpcResponse> respond)
    {
        try
        {
            if (service is null || service == "all" || service == "admin")
                await _process.StopAdminAsync();
            if (service is null || service == "all" || service == "api")
                await _process.StopApiAsync();
            respond(new ServiceIpc.IpcResponse { Success = true, Message = $"已停止 {service ?? "all"}" });
        }
        catch (Exception ex)
        {
            respond(new ServiceIpc.IpcResponse { Success = false, Message = ex.Message });
        }
    }

    private async Task HandleRestartCommandAsync(string? service, Action<ServiceIpc.IpcResponse> respond)
    {
        try
        {
            await _process.RestartAllAsync();
            respond(new ServiceIpc.IpcResponse { Success = true, Message = $"已重启 {service ?? "all"}" });
        }
        catch (Exception ex)
        {
            respond(new ServiceIpc.IpcResponse { Success = false, Message = ex.Message });
        }
    }

    #endregion

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
            _ipcServer?.Dispose();
            _health.Dispose();
            _notifyIcon.Hide();
            Application.Exit();
        }
    }

    public new void Dispose()
    {
        try { _ipcServer?.Dispose(); } catch { }
        try { _health.Dispose(); } catch { }
        try { _notifyIcon.Dispose(); } catch { }
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}