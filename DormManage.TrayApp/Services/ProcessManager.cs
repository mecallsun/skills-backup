using System.Diagnostics;
using DormManage.TrayApp.Models;

namespace DormManage.TrayApp.Services;

/// <summary>
/// 进程管理器：负责 Admin / Api 子进程的启动、停止、重启、状态追踪。
///
/// 环境变量注入（与 Admin/Api Program.cs 对齐）：
/// - DormManage_KESTREL_PORT（Api/Admin 端口）
/// - DormManage_DB_CONN（SQL Server 连接串，Provider=SqlServer）
/// - DormManage_DB_CONN（SQL Server 连接串；v2.13.109 起仅 SqlServer 单 provider，DormManage_DB_PATH 已下线）
/// </summary>
public class ProcessManager
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly Func<AppConfig> _configProvider;
    private readonly HealthChecker _health;

    private Process? _apiProcess;
    private Process? _adminProcess;

    private bool _isStopping = false;   // 用户主动停止时为 true，抑制自动重启
    private DateTime _lastRestartAt = DateTime.MinValue;
    private int _restartCountInWindow = 0;
    private const int RestartWindowMinutes = 5;
    private const int MaxRestartInWindow = 3;

    public event Action<string, ServiceState>? ServiceStateChanged;

    public ProcessManager(ConfigService config, LogService log, HealthChecker health)
    {
        _config = config;
        _log = log;
        _health = health;
        _configProvider = () => _config.Current;
    }

    public ServiceState ApiState => _apiProcess is null ? ServiceState.Stopped :
        _apiProcess.HasExited ? ServiceState.Crashed : ServiceState.Running;
    public ServiceState AdminState => _adminProcess is null ? ServiceState.Stopped :
        _adminProcess.HasExited ? ServiceState.Crashed : ServiceState.Running;

    /// <summary>启动所有服务（按顺序：先 Api 再 Admin）</summary>
    public async Task StartAllAsync()
    {
        // v2.13.196 修正：托盘启动不再拒绝任何情况
        // 即使是超试用次数也允许启动（通过 TrialExceeded 状态标记强制窗口弹窗）
        IsLicenseValid();

        // v2.13.196：超试用次数强制弹窗确认（必须点击确认才能继续）
        // 如果用户取消确认，进程退出，强制要求正式注册后才能再次启动
        if (TrialExceeded)
        {
            var reg = DormManage.Shared.Register.RegisterSdk.CheckReg();
            var useTimes = reg.UseTimes;
            var trialLimit = DormManage.Shared.Register.RegisterSdk.TRIAL_LIMIT;
            _log.Warn($"[LICENSE] 试用次数已达上限 {useTimes}/{trialLimit}，强制弹窗确认");
            bool confirmed = ShowTrialExceedPrompt(useTimes, trialLimit);
            if (!confirmed)
            {
                _log.Error("[LICENSE] 用户未确认试用模式，进程退出");
                throw new InvalidOperationException("试用次数超出，用户未确认 - 请联系信息科完成正式注册");
            }
            _log.Info("[LICENSE] 用户已确认进入强制试用模式，继续启动");
        }

        _isStopping = false;
        await StartApiAsync();
        await Task.Delay(1000); // 错开启动避免端口瞬时竞争
        await StartAdminAsync();
    }

    /// <summary>
    /// v2.13.196：标记当前是否处于超试用次数状态
    /// 如果为 true，启动后必须先显示 TrialExceedPrompt 才能继续
    /// </summary>
    public bool TrialExceeded { get; private set; }

    /// <summary>
    /// v2.13.196：显示试用次数超出强制确认窗口
    /// 调用 TrialExceedPrompt.Show 在 UI 线程上弹窗
    /// </summary>
    private bool ShowTrialExceedPrompt(int useTimes, int trialLimit)
    {
        try
        {
            // 如果在非 UI 线程上，使用 invoke 切到 UI 线程
            bool result = false;
            var thread = System.Threading.Thread.CurrentThread;
            if (thread.GetApartmentState() == System.Threading.ApartmentState.STA)
            {
                result = DormManage.TrayApp.Forms.TrialExceedPrompt.Show(useTimes, trialLimit);
            }
            else
            {
                // 非 UI 线程，启动临时窗口循环
                var thread2 = new System.Threading.Thread(() =>
                {
                    result = DormManage.TrayApp.Forms.TrialExceedPrompt.Show(useTimes, trialLimit);
                });
                thread2.SetApartmentState(System.Threading.ApartmentState.STA);
                thread2.Start();
                thread2.Join();
            }
            return result;
        }
        catch (Exception ex)
        {
            _log.Error("[LICENSE] 弹窗失败", ex);
            return false;
        }
    }

    /// <summary>
    /// v2.13.196 修正：校验当前注册状态，决定是否需要弹出强制确认窗口
    /// 业务规则（修订）：
    /// - 永远允许启动（不再拒绝任何情况）
    /// - 如果是超试用次数（RegInt=-1 且 UseTimes ≥ TRIAL_LIMIT）→ 返回 TrialExceedPrompt 必须显示的标志
    /// - 其他所有情况（注册有效、注册过期、未注册次数未满）→ 都允许启动
    /// - 注册过期仍由运行时 LicenseGuard 拦截写入
    /// </summary>
    /// <returns>
    /// true = 正常状态（已注册或未试用完），可以正常启动
    /// 取决于调用方对 trial-exceeded 标记的处理
    /// </returns>
    private bool IsLicenseValid()
    {
        try
        {
            var reg = DormManage.Shared.Register.RegisterSdk.CheckReg();

            // 唯一特殊处理的情况：超试用次数（必须弹窗确认后进入试用模式）
            if (reg.RegInt == -1 && reg.UseTimes >= DormManage.Shared.Register.RegisterSdk.TRIAL_LIMIT)
            {
                _log.Warn($"[LICENSE] 试用次数已达上限 {reg.UseTimes}/{DormManage.Shared.Register.RegisterSdk.TRIAL_LIMIT}，启动后必须强制弹窗确认后进入试用模式");
                TrialExceeded = true;
            }
            else
            {
                TrialExceeded = false;
            }

            // 已注册（且未过期）
            if (reg.RegInt == 1)
            {
                if (reg.RegDate.HasValue)
                {
                    if (reg.RegDate.Value.Date < DateTime.Today)
                    {
                        _log.Info($"[LICENSE] 注册已过期（RegDate={reg.RegDate:yyyy-MM-dd} < Today），但允许启动（进入只读模式）");
                    }
                    else
                    {
                        _log.Info($"[LICENSE] 注册有效：LTD={reg.LTDName}，有效期至 {reg.RegDate:yyyy-MM-dd}");
                    }
                }
                else
                {
                    _log.Warn($"[LICENSE] RegInt=1 但 RegDate 缺失，允许启动但可能存在风险");
                }
                return true;
            }

            if (reg.RegInt == -1 && reg.UseTimes < DormManage.Shared.Register.RegisterSdk.TRIAL_LIMIT)
            {
                _log.Info($"[LICENSE] 试用模式：UseTimes={reg.UseTimes}/{DormManage.Shared.Register.RegisterSdk.TRIAL_LIMIT}");
                return true;
            }

            // RegInt == 0（旧格式过期）或其他未知状态 → 允许启动，由运行时决定只读
            _log.Info($"[LICENSE] 注册状态={reg.RegInt}，允许启动（运行时将通过 LicenseGuard 判定只读）");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("[LICENSE] 注册校验异常，按允许启动处理（安全优先）", ex);
            return true;  // 异常时允许启动，避免系统完全不可用
        }
    }

    public async Task StartApiAsync()
    {
        if (_apiProcess is { HasExited: false })
        {
            _log.Warn("Api 进程已在运行，跳过启动");
            return;
        }

        var cfg = _configProvider();
        if (!TryResolveExePath(cfg.Tray.ApiExecutable, out var exePath))
        {
            _log.Error($"Api 可执行文件不存在：{cfg.Tray.ApiExecutable}");
            throw new FileNotFoundException("Api 可执行文件不存在", cfg.Tray.ApiExecutable);
        }

        if (await IsPortInUseAsync(cfg.Tray.ApiPort))
        {
            _log.Error($"Api 端口 {cfg.Tray.ApiPort} 已被占用");
            throw new InvalidOperationException($"Api 端口 {cfg.Tray.ApiPort} 已被占用");
        }

        _log.Info($"启动 Api：{exePath} (port={cfg.Tray.ApiPort})");
        _log.Info($"  DB Provider={cfg.Database.Provider}, ConnStrLen={cfg.Database.ConnectionString?.Length ?? 0}");
        _health.MarkApiState(ServiceState.Starting);
        ServiceStateChanged?.Invoke("Api", ServiceState.Starting);

        var psi = BuildStartInfo(exePath, cfg, isApi: true);
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Exited += (_, _) => OnApiExited(process);

        try
        {
            process.Start();
            _apiProcess = process;
            _log.Info($"Api 进程已启动 PID={process.Id}");
        }
        catch (Exception ex)
        {
            _log.Error("Api 启动失败", ex);
            _health.MarkApiState(ServiceState.Crashed);
            ServiceStateChanged?.Invoke("Api", ServiceState.Crashed);
            throw;
        }
    }

    public async Task StartAdminAsync()
    {
        if (_adminProcess is { HasExited: false })
        {
            _log.Warn("Admin 进程已在运行，跳过启动");
            return;
        }

        var cfg = _configProvider();
        if (!TryResolveExePath(cfg.Tray.AdminExecutable, out var exePath))
        {
            _log.Error($"Admin 可执行文件不存在：{cfg.Tray.AdminExecutable}");
            throw new FileNotFoundException("Admin 可执行文件不存在", cfg.Tray.AdminExecutable);
        }

        if (await IsPortInUseAsync(cfg.Tray.AdminPort))
        {
            _log.Error($"Admin 端口 {cfg.Tray.AdminPort} 已被占用");
            throw new InvalidOperationException($"Admin 端口 {cfg.Tray.AdminPort} 已被占用");
        }

        _log.Info($"启动 Admin：{exePath} (port={cfg.Tray.AdminPort})");
        _log.Info($"  DB Provider={cfg.Database.Provider}, ConnStrLen={cfg.Database.ConnectionString?.Length ?? 0}");
        _health.MarkAdminState(ServiceState.Starting);
        ServiceStateChanged?.Invoke("Admin", ServiceState.Starting);

        var psi = BuildStartInfo(exePath, cfg, isApi: false);
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Exited += (_, _) => OnAdminExited(process);

        try
        {
            process.Start();
            _adminProcess = process;
            _log.Info($"Admin 进程已启动 PID={process.Id}");
        }
        catch (Exception ex)
        {
            _log.Error("Admin 启动失败", ex);
            _health.MarkAdminState(ServiceState.Crashed);
            ServiceStateChanged?.Invoke("Admin", ServiceState.Crashed);
            throw;
        }
    }

    public async Task StopAllAsync()
    {
        _isStopping = true;
        await StopAdminAsync();
        await Task.Delay(500);
        await StopApiAsync();
    }

    public async Task StopApiAsync()
    {
        if (_apiProcess is null or { HasExited: true }) return;
        _log.Info("停止 Api 进程...");
        _health.MarkApiState(ServiceState.Stopping);
        try
        {
            _apiProcess.CloseMainWindow();
            if (!_apiProcess.WaitForExit(5000))
            {
                _log.Warn("Api 5s 内未退出，强制结束");
                _apiProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Api 优雅停止异常：{ex.Message}");
        }
        _apiProcess = null;
        _health.MarkApiState(ServiceState.Stopped);
        ServiceStateChanged?.Invoke("Api", ServiceState.Stopped);
        await Task.CompletedTask;
    }

    public async Task StopAdminAsync()
    {
        if (_adminProcess is null or { HasExited: true }) return;
        _log.Info("停止 Admin 进程...");
        _health.MarkAdminState(ServiceState.Stopping);
        try
        {
            _adminProcess.CloseMainWindow();
            if (!_adminProcess.WaitForExit(5000))
            {
                _log.Warn("Admin 5s 内未退出，强制结束");
                _adminProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Admin 优雅停止异常：{ex.Message}");
        }
        _adminProcess = null;
        _health.MarkAdminState(ServiceState.Stopped);
        ServiceStateChanged?.Invoke("Admin", ServiceState.Stopped);
        await Task.CompletedTask;
    }

    public async Task RestartAllAsync()
    {
        _isStopping = true; // 抑制自动重启
        await StopAllAsync();
        await Task.Delay(2000);
        _isStopping = false;
        await StartAllAsync();
    }

    /// <summary>进程异常退出回调（来自 HealthChecker 触发）</summary>
    public async Task HandleCrashAsync()
    {
        if (_isStopping) return;
        if (!CanAutoRestart())
        {
            _log.Error("服务在 5 分钟内已重启超过 3 次，停止自愈，请检查配置或日志");
            return;
        }

        _log.Warn("检测到服务异常，5s 后自动重启...");
        await Task.Delay(5000);
        if (_isStopping) return;

        // v2.13.157 自愈：若上次启动失败为「配置路径无效」，先重新加载配置触发 AutoHeal
        // （每次重启都会触发 ConfigService.Load(), Load() 内部自动修复失效路径）
        try
        {
            // 重新加载配置以便触发 AutoHealPathsIfInvalid
            // 注：ConfigService.Load 在 TrayAppContext 启动时已执行一次；
            // 若用户外部编辑了配置但未重启托盘，这里手动 reload 以保证最新值
            _config.Load();

            if (_apiProcess is null or { HasExited: true })
                await StartApiAsync();
            if (_adminProcess is null or { HasExited: true })
                await StartAdminAsync();
        }
        catch (Exception ex)
        {
            _log.Error("自愈重启失败", ex);
        }
    }

    private void OnApiExited(Process p)
    {
        var code = p.ExitCode;
        _log.Warn($"Api 进程退出 code={code}, _isStopping={_isStopping}");
        ServiceStateChanged?.Invoke("Api", ServiceState.Stopped);
        _apiProcess = null;

        if (_isStopping) return;

        // 自动重启（异步，避免阻塞 Exited 事件线程）
        if (_config.Current.Tray.AutoRestartOnCrash)
        {
            _ = Task.Run(HandleCrashAsync);
        }
    }

    private void OnAdminExited(Process p)
    {
        var code = p.ExitCode;
        _log.Warn($"Admin 进程退出 code={code}, _isStopping={_isStopping}");
        ServiceStateChanged?.Invoke("Admin", ServiceState.Stopped);
        _adminProcess = null;

        if (_isStopping) return;
        if (_config.Current.Tray.AutoRestartOnCrash)
        {
            _ = Task.Run(HandleCrashAsync);
        }
    }

    private bool CanAutoRestart()
    {
        var now = DateTime.Now;
        if ((now - _lastRestartAt).TotalMinutes > RestartWindowMinutes)
        {
            _lastRestartAt = now;
            _restartCountInWindow = 1;
            return true;
        }
        _restartCountInWindow++;
        return _restartCountInWindow <= MaxRestartInWindow;
    }

    /// <summary>
    /// v2.13.157 多候选路径解析（与 ConfigService.TryFindExeUnderBase 单源真相）：
    /// 1. 配置原始值（相对 BaseDirectory 或绝对路径）
    /// 2. 相对 BaseDirectory 的候选路径：Api\xxx.exe、..\Api\xxx.exe（v2.13.142 旧默认）
    /// 3. 相对 BaseDirectory 父目录的候选（developer 模式把 Api/Admin 放在 publish-final 根）
    /// 4. 兜底：当前工作目录下的 Api\xxx.exe
    /// 优先返回最先匹配成功的路径。
    /// </summary>
    private static bool TryResolveExePath(string relativeOrAbsolute, out string fullPath)
    {
        var exeName = Path.GetFileName(string.IsNullOrWhiteSpace(relativeOrAbsolute)
            ? "DormManage.Unknown.exe"
            : relativeOrAbsolute);

        // 用 ConfigService 的共享候选探测（单源真相）
        var resolved = ConfigService.TryFindExeUnderBase(exeName);
        if (resolved != null)
        {
            // 如果用户配置是相对路径且已被覆盖为绝对路径，正常返回（下游用绝对路径即可）
            // 反哺逻辑在 ConfigService.AutoHealPathsIfInvalid 完成（启动时已自动修复到配置文件）
            fullPath = resolved;
            return true;
        }

        // 配置指向的绝对/相对路径作为最后的诚实返回（让错误日志能看到真实配置值）
        if (!string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            var direct = Path.IsPathRooted(relativeOrAbsolute)
                ? relativeOrAbsolute
                : Path.Combine(AppContext.BaseDirectory, relativeOrAbsolute);
            if (File.Exists(direct))
            {
                fullPath = direct;
                return true;
            }
        }

        fullPath = relativeOrAbsolute;
        return false;
    }

    private static ProcessStartInfo BuildStartInfo(string exePath, AppConfig cfg, bool isApi)
    {
        var workingDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            // v2.13.199: 隐藏命令行窗口（in-memory silent launch）
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        var port = isApi ? cfg.Tray.ApiPort : cfg.Tray.AdminPort;
        psi.EnvironmentVariables["DormManage_KESTREL_PORT"] = port.ToString();

        // v2.13.155 托盘托管守卫：注入签名握手令牌，子进程 (Admin/Api) 启动时校验，
        // 未由托盘拉起（无令牌 / 令牌非法 / 父进程非托盘）则拒绝启动，实现「禁止独立使用」。
        var childKey = isApi
            ? DormManage.Shared.Security.TrayLaunchGuard.ChildApi
            : DormManage.Shared.Security.TrayLaunchGuard.ChildAdmin;
        psi.EnvironmentVariables[DormManage.Shared.Security.TrayLaunchGuard.HandshakeEnvVar] =
            DormManage.Shared.Security.TrayLaunchGuard.CreateHandshakeToken(childKey);

        // v2.13.28: 数据库连接环境变量注入（优先级最高，覆盖 appsettings.json）
        // v2.13.109: SQLite 已移除，仅注入 DormManage_DB_CONN
        if (string.Equals(cfg.Database.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(cfg.Database.ConnectionString))
                psi.EnvironmentVariables["DormManage_DB_CONN"] = cfg.Database.ConnectionString;
            // 注意：此方法是 static，日志由调用方记录
        }
        // v2.13.109: 移除 DormManage_DB_PATH 分支（SQLite 已下线）

        if (!string.IsNullOrWhiteSpace(cfg.Storage.ImageRoot))
        {
            psi.EnvironmentVariables["DormManage_IMAGE_ROOT"] = Path.IsPathRooted(cfg.Storage.ImageRoot)
                ? cfg.Storage.ImageRoot
                : Path.Combine(AppContext.BaseDirectory, cfg.Storage.ImageRoot);
        }

        return psi;
    }

    private static async Task<bool> IsPortInUseAsync(int port)
    {
        try
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return await Task.FromResult(false);
        }
        catch
        {
            return await Task.FromResult(true);
        }
    }
}