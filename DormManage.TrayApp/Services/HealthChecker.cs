using DormManage.TrayApp.Models;

namespace DormManage.TrayApp.Services;

/// <summary>
/// HTTP 健康检查器：周期探测 Api / Admin 端点，更新内部状态。
///
/// 探测策略：
/// - 单次探测：3s 超时
/// - 连续 3 次失败 → 标记 Crashed 并触发 RestartCallback
/// - 任一次成功 → 立即复位为 Running
/// </summary>
public class HealthChecker : IDisposable
{
    private readonly HttpClient _http;
    private readonly LogService _log;
    private readonly Func<Task>? _onCrashed;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private int _apiConsecutiveFailures = 0;
    private int _adminConsecutiveFailures = 0;
    private const int FailureThreshold = 3;

    public ServiceState ApiState { get; private set; } = ServiceState.Stopped;
    public ServiceState AdminState { get; private set; } = ServiceState.Stopped;
    public ServiceHealth? LastApiHealth { get; private set; }
    public ServiceHealth? LastAdminHealth { get; private set; }

    public event Action<string, ServiceState>? ServiceStateChanged;

    public HealthChecker(LogService log, Func<Task>? onCrashed = null)
    {
        _log = log;
        _onCrashed = onCrashed;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        _http.DefaultRequestHeaders.Add("User-Agent", "DormManage.TrayApp/2.13.2");
    }

    /// <summary>外部强制标记状态（如进程刚启动后立即设为 Starting）</summary>
    public void MarkApiState(ServiceState state)
    {
        if (ApiState == state) return;
        ApiState = state;
        if (state == ServiceState.Running) _apiConsecutiveFailures = 0;
        ServiceStateChanged?.Invoke("Api", state);
    }

    public void MarkAdminState(ServiceState state)
    {
        if (AdminState == state) return;
        AdminState = state;
        if (state == ServiceState.Running) _adminConsecutiveFailures = 0;
        ServiceStateChanged?.Invoke("Admin", state);
    }

    /// <summary>启动周期探测循环</summary>
    public void Start(int intervalSeconds, Func<(int ApiPort, int AdminPort)> configProvider)
    {
        if (_loopTask is not null) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(intervalSeconds, configProvider, _cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }

    private async Task LoopAsync(int intervalSeconds, Func<(int ApiPort, int AdminPort)> configProvider, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, intervalSeconds));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var (apiPort, adminPort) = configProvider();
                var apiHealth = await CheckAsync("Api", $"http://127.0.0.1:{apiPort}/swagger/index.html");
                UpdateApiState(apiHealth);

                var adminHealth = await CheckAsync("Admin", $"http://127.0.0.1:{adminPort}/");
                UpdateAdminState(adminHealth);
            }
            catch (Exception ex)
            {
                _log.Error("健康检查循环异常", ex);
            }

            try { await Task.Delay(interval, ct); } catch { /* canceled */ break; }
        }
    }

    private async Task<ServiceHealth> CheckAsync(string serviceName, string url)
    {
        try
        {
            var resp = await _http.GetAsync(url);
            var ok = serviceName == "Admin"
                ? resp.IsSuccessStatusCode || (int)resp.StatusCode == 302
                : resp.IsSuccessStatusCode;
            return new ServiceHealth(serviceName, ok, 0, $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return new ServiceHealth(serviceName, false, 0, ex.Message);
        }
    }

    private void UpdateApiState(ServiceHealth health)
    {
        LastApiHealth = health;
        if (health.IsHealthy)
        {
            _apiConsecutiveFailures = 0;
            if (ApiState != ServiceState.Running)
            {
                ApiState = ServiceState.Running;
                ServiceStateChanged?.Invoke("Api", ServiceState.Running);
                _log.Info($"Api 健康检查通过：{health.Detail}");
            }
        }
        else
        {
            _apiConsecutiveFailures++;
            if (_apiConsecutiveFailures >= FailureThreshold && ApiState != ServiceState.Crashed)
            {
                ApiState = ServiceState.Crashed;
                ServiceStateChanged?.Invoke("Api", ServiceState.Crashed);
                _log.Error($"Api 连续 {_apiConsecutiveFailures} 次健康检查失败：{health.Detail}");
                TriggerRestart();
            }
            else if (ApiState == ServiceState.Starting || ApiState == ServiceState.Running)
            {
                _log.Warn($"Api 健康检查失败 ({_apiConsecutiveFailures}/{FailureThreshold})：{health.Detail}");
            }
        }
    }

    private void UpdateAdminState(ServiceHealth health)
    {
        LastAdminHealth = health;
        if (health.IsHealthy)
        {
            _adminConsecutiveFailures = 0;
            if (AdminState != ServiceState.Running)
            {
                AdminState = ServiceState.Running;
                ServiceStateChanged?.Invoke("Admin", ServiceState.Running);
                _log.Info($"Admin 健康检查通过：{health.Detail}");
            }
        }
        else
        {
            _adminConsecutiveFailures++;
            if (_adminConsecutiveFailures >= FailureThreshold && AdminState != ServiceState.Crashed)
            {
                AdminState = ServiceState.Crashed;
                ServiceStateChanged?.Invoke("Admin", ServiceState.Crashed);
                _log.Error($"Admin 连续 {_adminConsecutiveFailures} 次健康检查失败：{health.Detail}");
                TriggerRestart();
            }
            else if (AdminState == ServiceState.Starting || AdminState == ServiceState.Running)
            {
                _log.Warn($"Admin 健康检查失败 ({_adminConsecutiveFailures}/{FailureThreshold})：{health.Detail}");
            }
        }
    }

    private int _restartLock = 0;
    private void TriggerRestart()
    {
        // 防止两个服务同时崩溃时重复触发
        if (Interlocked.Exchange(ref _restartLock, 1) != 0) return;
        Task.Run(async () =>
        {
            try
            {
                if (_onCrashed is not null) await _onCrashed();
            }
            finally
            {
                await Task.Delay(5000);
                Interlocked.Exchange(ref _restartLock, 0);
            }
        });
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }
}