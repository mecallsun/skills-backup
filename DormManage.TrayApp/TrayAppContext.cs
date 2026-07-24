using System.Reflection;
using System.Text.Json;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.TrayApp.Models;
using DormManage.TrayApp.Services;

namespace DormManage.TrayApp;

/// <summary>
/// 托盘应用上下文：管理 NotifyIcon + ProcessManager + HealthChecker + IPC Server 的生命周期。
///
/// 【v2.13.4 修复】右键 → 设置 报 "UI异常，创建窗口出错"
/// 根因：原版继承 <see cref="ApplicationContext"/>，无主 Form；
///       <see cref="Form.ShowDialog()"/> 在无 Owner 时 WinForms 内部尝试创建隐式 Owner 窗口，
///       在部分 DPI / 主题 / 启动时序下 CreateWindowEx 失败。
/// 修复：内嵌一个不可见 <see cref="OwnerForm"/>（Opacity=0, ShowInTaskbar=false, FormBorderStyle=None, Size=0,0），
///       并设为 <see cref="ApplicationContext.MainForm"/>；
///       所有 ShowDialog 均传入 _ownerForm 作为 Owner；
///       并给所有弹窗路径加 try-catch 保护，避免一次失败拖死右键菜单。
///
/// 资源释放顺序（Dispose）：
/// 1. 停止 IPC Server（不再接收命令）
/// 2. 停止 HealthChecker 探测循环
/// 3. 释放 NotifyIcon
/// 4. 销毁 OwnerForm（释放窗口句柄）
/// </summary>
public sealed class TrayAppContext : ApplicationContext, IDisposable
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly HealthChecker _health;
    private readonly ProcessManager _process;
    private readonly NotifyIconManager _notifyIcon;
    private readonly IpcServer? _ipcServer;
    private readonly LicenseMonitor _licenseMonitor;

    /// <summary>v2.13.4 新增：不可见主窗体，作为所有弹窗的 Owner</summary>
    private readonly Form _ownerForm;

    public TrayAppContext(ConfigService config, LogService log)
    {
        _config = config;
        _log = log;

        // 1) 关键：先创建不可见 OwnerForm（不 Show，仅作为窗口句柄宿主）
        _ownerForm = CreateOwnerForm();
        // 设为主 Form，使 ApplicationContext 知道存在一个窗口宿主
        MainForm = _ownerForm;

        // 2) 创建健康检查与进程管理器（依赖 ConfigService / LogService）
        // 注：ProcessManager ↔ HealthChecker 是循环依赖，通过 lambda 闭包延迟解析
#pragma warning disable CS8602 // lambda 调用时 _process 已赋值
        _health = new HealthChecker(log, onCrashed: () => _process.HandleCrashAsync());
        _process = new ProcessManager(config, log, _health);
#pragma warning restore CS8602

        _process.ServiceStateChanged += OnServiceStateChanged;
        _health.ServiceStateChanged += OnServiceStateChanged;

        // v2.13.137 注册状态监控：托盘端是注册校验唯一权威
        // 周期 5s 探测注册状态变化 → 触发 IPC Push 给所有子进程
        _licenseMonitor = new LicenseMonitor(
            checkRegFunc: () => DormManage.Shared.Register.RegisterSdk.CheckReg(),
            onChanged: BroadcastRegStateChanged,
            intervalSeconds: 5);
        _licenseMonitor.OnChanged += state =>
        {
            _log.Info($"[LICENSE] 注册状态变化: RegInt={state.RegInt} LTDName={state.LTDName}");
        };
        _licenseMonitor.Start();

        // v2.13.19：订阅数据库配置变更事件，刷新 appsettings.json 中的 Database 段
        AppConfigManager.Instance.OnDatabaseConfigUpdated += OnDatabaseConfigUpdated;

        _health.Start(
            config.Current.Tray.HealthCheckIntervalSeconds,
            GetCurrentPorts);

        // v2.13.137 完全托管模式：启动时清理历史可能存在的子进程自启项
        // 仅托盘程序可自启；Admin/Api 必须由托盘启动
        try
        {
            var removed = new Services.AutoStartManager().CleanupForbiddenAutoStart();
            if (removed > 0)
            {
                _log.Warn($"[AUTO-START] 已清理 {removed} 个禁止自启项（DormManage.Admin / DormManage.Api）");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"[AUTO-START] 清理禁止自启项异常：{ex.Message}");
        }

        // 3) 创建托盘图标 + 右键菜单（关联 _ownerForm 作为 ContextMenuStrip 宿主）
        _notifyIcon = new NotifyIconManager(
            owner: _ownerForm,
            onOpenAdmin: OpenAdminBrowser,
            onOpenApi: OpenApiBrowser,
            onSettings: ShowSettings,
            onRestartAll: async () => await _process.RestartAllAsync(),
            onViewLogs: OpenLogsFolder,
            onAbout: ShowAbout,
            onLicense: ShowLicense,
            onExit: async () =>
            {
                _log.Info("用户请求退出");
                var ok = SafeShow(
                    () => MessageBox.Show(
                        _ownerForm,
                        "确定要停止所有服务并退出托盘吗？",
                        "退出确认",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question),
                    fallbackText: "确定要停止所有服务并退出托盘吗？");
                if (ok != DialogResult.Yes) return;
                await ExitAsync();
            },
            onToggleAutoStart: ToggleAutoStart);

        // 4) 配置驱动：托盘启动后自动拉起 Api + Admin
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

        // 5) 启动 IPC Server（接收 Web Admin 命令：ping/status/start/stop/restart）
        _ipcServer = new IpcServer(ServiceIpc.DefaultPort, HandleIpcCommand);
        _ipcServer.Start();
        _log.Info($"IPC Server 已启动，监听 127.0.0.1:{ServiceIpc.DefaultPort}");

        _log.Info("托盘上下文初始化完成");
    }

    /// <summary>
    /// 创建不可见 OwnerForm —— 仅作为窗口句柄宿主，不显示、不接收输入、不在任务栏显示。
    /// </summary>
    private static Form CreateOwnerForm()
    {
        var f = new Form
        {
            Name = "TrayAppOwnerForm",
            Text = "DormManage.TrayApp",
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            Opacity = 0d,
            Size = new Size(0, 0),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000), // 屏幕外
            WindowState = FormWindowState.Normal,
            MinimizeBox = false,
            MaximizeBox = false,
            ControlBox = false,
            Enabled = false   // 禁用所有输入，避免被意外聚焦
        };
        // 关键：必须创建窗口句柄（ShowInTaskbar=false 不会自动创建 Handle）
        // 通过访问 Handle 强制创建，但不 Show
        _ = f.Handle;
        return f;
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

    /// <summary>
    /// v2.13.19：数据库配置变更事件回调，将最新字段式配置同步到 appsettings.json。
    /// </summary>
    private void OnDatabaseConfigUpdated(object? sender, DatabaseConfigDto e)
    {
        try
        {
            _config.UpdateDatabaseSection(e);
            _log.Info($"收到数据库配置更新事件，已同步到 appsettings.json：Provider={e.Provider}, Server={e.DbServer}, Db={e.DbName}");
        }
        catch (Exception ex)
        {
            _log.Error("同步数据库配置到 appsettings.json 失败", ex);
        }
    }

    #region IPC 命令处理（v2.13.3 新增 / v2.13.19 扩展数据库配置同步）

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
                        Data = new { version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.13.4" }
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

                case "getdbconfig":
                    _ = HandleGetDbConfigAsync(respond);
                    break;

                case "setdbconfig":
                case "dbconfig.updated":
                    _ = HandleSetDbConfigAsync(cmd.Payload, respond);
                    break;

                // v2.13.137 注册状态查询（Web/Api 子进程 → TrayApp）
                // 数据源：RegisterSdk.CheckReg()（托盘端 WMI/注册表读取）
                // 返回：RegStateDto（RegInt/SN/CDKEY/LTDName/RegDate/UseTimes）
                case "getregstate":
                    HandleGetRegState(respond);
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

    /// <summary>
    /// v2.13.19：处理 Web 端查询数据库配置请求。
    /// </summary>
    private async Task HandleGetDbConfigAsync(Action<ServiceIpc.IpcResponse> respond)
    {
        try
        {
            var cfg = await AppConfigManager.Instance.LoadAsync();
            if (cfg is null)
            {
                respond(new ServiceIpc.IpcResponse { Success = false, Message = "数据库配置不存在" });
                return;
            }

            // 密码脱敏返回
            if (!string.IsNullOrEmpty(cfg.DbPassword))
                cfg.DbPassword = "******";

            respond(new ServiceIpc.IpcResponse
            {
                Success = true,
                Message = "ok",
                Data = cfg
            });
        }
        catch (Exception ex)
        {
            _log.Error("IPC getdbconfig 处理失败", ex);
            respond(new ServiceIpc.IpcResponse { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// v2.13.19：处理 Web 端保存/推送数据库配置请求。
    /// </summary>
    private async Task HandleSetDbConfigAsync(Dictionary<string, object?>? payload, Action<ServiceIpc.IpcResponse> respond)
    {
        try
        {
            if (payload is null)
            {
                respond(new ServiceIpc.IpcResponse { Success = false, Message = "缺少 payload" });
                return;
            }

            var dto = JsonSerializer.Deserialize<DatabaseConfigDto>(JsonSerializer.Serialize(payload), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto is null)
            {
                respond(new ServiceIpc.IpcResponse { Success = false, Message = "无法解析数据库配置" });
                return;
            }

            var (ok, msg) = await AppConfigManager.Instance.SaveConfigurationAsync(dto);
            if (ok)
            {
                var refreshed = await AppConfigManager.Instance.LoadAsync();
                if (refreshed is not null)
                    _config.UpdateDatabaseSection(refreshed);

                // v2.13.32：保存成功后 AppConfigManager 已自动触发 AppConfigRuntime.ApplyExternalConfiguration
                // 所有 Api/Admin 进程的下次 HTTP 请求自动走新连接，无需重启
                // 数据库配置变化对子进程（Api/Admin）通过 db_setting.json FileSystemWatcher 自动同步
                _log.Info($"IPC setdbconfig/dbconfig.updated 成功并已热加载：Provider={dto.Provider}, Server={dto.DbServer}, Db={dto.DbName}（Api/Admin 下次请求自动切换）");
            }

            respond(new ServiceIpc.IpcResponse { Success = ok, Message = msg });
        }
        catch (Exception ex)
        {
            _log.Error("IPC setdbconfig/dbconfig.updated 处理失败", ex);
            respond(new ServiceIpc.IpcResponse { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// v2.13.137 注册状态查询（同步）
    /// 数据源：RegisterSdk.CheckReg()（托盘端 WMI 取真实硬件特征）
    /// 用途：Web/Api 中间件 LicenseGuard 调用此方法获取注册状态
    /// </summary>
    private void HandleGetRegState(Action<ServiceIpc.IpcResponse> respond)
    {
        try
        {
            var reg = DormManage.Shared.Register.RegisterSdk.CheckReg();
            var state = new ServiceIpc.RegStateDto
            {
                RegInt = reg.RegInt,
                SN = reg.SN,
                CDKEY = reg.CDKEY ?? "",
                LTDName = reg.LTDName ?? "",
                RegDate = reg.RegDate,
                UseTimes = reg.UseTimes,
                DetectedAtUtc = DateTime.UtcNow
            };

            respond(new ServiceIpc.IpcResponse
            {
                Success = true,
                Message = "ok",
                Data = state
            });
        }
        catch (Exception ex)
        {
            _log.Error("IPC getregstate 处理失败", ex);
            respond(new ServiceIpc.IpcResponse
            {
                Success = false,
                Message = $"注册状态查询失败：{ex.Message}"
            });
        }
    }

    /// <summary>
    /// v2.13.137 推送注册状态变化（LicenseMonitor 触发）
    /// 当前实现：通过 IPC Push（Web/Api 端反向建立 TCP 连接接收推送）
    /// 因 TrayAppContext 单向 IPC 协议限制，本方法仅日志记录；
    /// 实际推送由子进程 30s 轮询触发（详见 Admin/Api LicenseGuard）
    /// </summary>
    private void BroadcastRegStateChanged(ServiceIpc.RegStateDto state)
    {
        _log.Info($"[LICENSE-PUSH] 注册状态变化: RegInt={state.RegInt} LTDName={state.LTDName ?? "-"} CDKEY={(string.IsNullOrEmpty(state.CDKEY) ? "空" : "已设置")}");
        // 子进程通过 30s 轮询 getregstate 自动同步最新状态
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

    /// <summary>
    /// 打开系统设置窗口（按需求 57 §3.2）
    /// v2.13.4 修复：用 _ownerForm 作 Owner + 整体 try-catch 保护
    /// </summary>
    private void ShowSettings()
    {
        try
        {
            _log.Info("用户请求打开系统设置");
            using var form = new Forms.SettingsForm(_config, _log, _process, _health);
            // 关键修复：传入 _ownerForm 作 Owner，避免 WinForms 内部隐式创建失败
            var result = form.ShowDialog(_ownerForm);

            if (result == DialogResult.OK)
            {
                var c = _config.Current;
                _log.Info($"配置已保存：ApiPort={c.Tray.ApiPort}, AdminPort={c.Tray.AdminPort}");
            }
        }
        catch (Exception ex)
        {
            _log.Error("打开系统设置窗口失败", ex);
            MessageBox.Show(_ownerForm,
                $"打开系统设置失败：{ex.Message}\n\n请查看日志 logs/tray-{DateTime.Now:yyyyMMdd}.log",
                "系统设置",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowAbout()
    {
        try
        {
            using var form = new Forms.AboutForm();
            form.ShowDialog(_ownerForm);
        }
        catch (Exception ex)
        {
            _log.Error("打开关于窗口失败", ex);
            MessageBox.Show(_ownerForm, $"打开关于窗口失败：{ex.Message}", "关于", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>v2.13.94 软件注册授权弹窗（托盘右键菜单入口）</summary>
    private void ShowLicense()
    {
        try
        {
            using var form = new Forms.LicenseForm();
            form.ShowDialog(_ownerForm);
        }
        catch (Exception ex)
        {
            _log.Error("打开软件注册窗口失败", ex);
            MessageBox.Show(_ownerForm, $"打开软件注册窗口失败：{ex.Message}", "软件注册", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
            _log.Error("打开日志目录失败", ex);
            MessageBox.Show(_ownerForm, $"无法打开日志目录：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            Dispose();
            Application.Exit();
        }
    }

    /// <summary>
    /// 切换 Windows 开机自启动（v2.13.3）
    /// </summary>
    private void ToggleAutoStart()
    {
        var mgr = new Services.AutoStartManager();
        try
        {
            if (mgr.IsEnabled())
            {
                if (mgr.Disable())
                {
                    _notifyIcon.RefreshAutoStartStatus(false);
                    MessageBox.Show(_ownerForm, "已取消开机自启动", "托盘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                if (mgr.Enable())
                {
                    _notifyIcon.RefreshAutoStartStatus(true);
                    MessageBox.Show(_ownerForm, "已设置开机自启动", "托盘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(_ownerForm, "设置失败：请以管理员权限运行或检查注册表权限", "托盘", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("切换自启动异常", ex);
            MessageBox.Show(_ownerForm, $"操作失败：{ex.Message}", "托盘", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// 包装 MessageBox.Show 调用，避免 CreateWindowEx 失败导致整个回调挂掉。
    /// 优先尝试带 Owner 的 Show；若失败则回退到无 Owner 的 Show。
    /// </summary>
    private DialogResult SafeShow(Func<DialogResult> showWithOwner, string fallbackText)
    {
        try
        {
            return showWithOwner();
        }
        catch
        {
            try
            {
                return MessageBox.Show(fallbackText, "退出确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            }
            catch
            {
                // 兜底：默认 No（保守，不退出）
                return DialogResult.No;
            }
        }
    }

    public new void Dispose()
    {
        try { _ipcServer?.Dispose(); } catch (Exception ex) { _log.Warn($"IPC 释放异常：{ex.Message}"); }
        try { _health.Dispose(); } catch (Exception ex) { _log.Warn($"Health 释放异常：{ex.Message}"); }
        try { _notifyIcon.Dispose(); } catch (Exception ex) { _log.Warn($"NotifyIcon 释放异常：{ex.Message}"); }
        try
        {
            if (!_ownerForm.IsDisposed)
            {
                _ownerForm.Hide();
                _ownerForm.Dispose();
            }
        }
        catch (Exception ex) { _log.Warn($"OwnerForm 释放异常：{ex.Message}"); }
        GC.SuppressFinalize(this);
    }
}