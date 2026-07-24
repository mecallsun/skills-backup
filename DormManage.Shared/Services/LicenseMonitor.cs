using System;
using System.Threading;
using System.Threading.Tasks;

namespace DormManage.Shared.Services;

/// <summary>
/// v2.13.137 注册状态监控器（共享库 TrayApp 使用）
///
/// 设计原理：
/// - TrayApp 是注册校验的唯一权威，所有 Web/Api 子进程通过 IPC 查询此服务
/// - 本类在 TrayApp 进程内启动周期探测线程，检测注册状态变化时触发事件
/// - TrayAppContext 订阅 OnChanged → 通过 IPC Push `regstate.changed` 给所有子进程
///
/// 触发 OnChanged 的场景：
/// 1. 用户在 LicenseForm 写入新 CDKEY（LicenseGuard.ResetCache 之外）
/// 2. 用户取消注册（DeleteRegItem）
/// 3. 试用次数 +1（IncrementUseTimes）跨过 TRIAL_LIMIT 阈值
/// 4. 注册表被外部工具修改（恶意攻击/手动维护）
/// 5. 进程重启后首次启动（OnStart 触发一次）
/// </summary>
public class LicenseMonitor : IDisposable
{
    private readonly Func<DormManage.Shared.Register.RegItem> _checkRegFunc;
    private readonly int _intervalSeconds;
    private readonly Action<ServiceIpc.RegStateDto> _onChanged;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private ServiceIpc.RegStateDto? _lastState;

    /// <summary>事件：注册状态变化（外部订阅做 Push）</summary>
    public event Action<ServiceIpc.RegStateDto>? OnChanged;

    public LicenseMonitor(
        Func<DormManage.Shared.Register.RegItem> checkRegFunc,
        Action<ServiceIpc.RegStateDto> onChanged,
        int intervalSeconds = 5)
    {
        _checkRegFunc = checkRegFunc ?? throw new ArgumentNullException(nameof(checkRegFunc));
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _intervalSeconds = intervalSeconds;
    }

    /// <summary>
    /// 启动周期监控（仅 TrayApp 进程调用）
    /// </summary>
    public void Start()
    {
        if (_loopTask is not null) return;

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));

        // 启动时立即触发一次（让子进程立刻拿到当前状态）
        _ = Task.Run(() =>
        {
            try
            {
                var current = ReadState();
                _lastState = current;
                _onChanged(current);
            }
            catch { /* 启动失败不致命 */ }
        });
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), ct);

                var current = ReadState();
                if (HasChanged(_lastState, current))
                {
                    _lastState = current;
                    OnChanged?.Invoke(current);
                    _onChanged(current);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* 单次失败不中断 */ }
        }
    }

    /// <summary>
    /// 读取当前注册状态并转换为 RegStateDto
    /// </summary>
    private ServiceIpc.RegStateDto ReadState()
    {
        var reg = _checkRegFunc();
        return new ServiceIpc.RegStateDto
        {
            RegInt = reg.RegInt,
            SN = reg.SN,
            CDKEY = reg.CDKEY ?? "",
            LTDName = reg.LTDName ?? "",
            RegDate = reg.RegDate,
            UseTimes = reg.UseTimes,
            DetectedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 状态变化判定（RegInt 或 RegDate 变化视为变化）
    /// </summary>
    private static bool HasChanged(ServiceIpc.RegStateDto? prev, ServiceIpc.RegStateDto curr)
    {
        if (prev is null) return true;
        if (prev.RegInt != curr.RegInt) return true;
        if (prev.RegDate != curr.RegDate) return true;
        if (prev.CDKEY != curr.CDKEY) return true;
        return false;
    }

    /// <summary>
    /// 外部强制推送（如用户主动注册/取消注册）
    /// </summary>
    public void ForceNotify()
    {
        try
        {
            var current = ReadState();
            _lastState = current;
            OnChanged?.Invoke(current);
            _onChanged(current);
        }
        catch { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }
}