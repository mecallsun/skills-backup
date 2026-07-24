using System;
using System.Threading;
using System.Threading.Tasks;
using DormManage.Shared.Services;

namespace DormManage.Shared.Security;

/// <summary>
/// v2.13.137 注册状态守卫（依赖反转版）：
///
/// 业务规则：
/// - Admin/Api 中间件调用 IsReadOnly() 判断是否全局只读
/// - 数据源：**托盘进程 IPC**（不再读注册表、不再调用 RegisterSdk）
/// - 缓存策略：进程内缓存 30s（避免每个 HTTP 请求都调 IPC）
/// - 托盘未运行 → 默认只读（IsReadOnly = true，最安全的默认值）
///
/// 反向依赖设计：
/// - 旧版 v2.13.136 直接调用 RegisterSdk.CheckReg()（每个进程独立校验）
/// - 新版 v2.13.137 调用 IpcClient.GetRegStateAsync() → TCP :5099 → 托盘端 RegisterSdk
/// - 优势：所有注册校验集中在托盘进程，CDKEY/机器码/试用期不离开托盘
/// - 用户原话："所有 web 服务及 pda 服务 程序的运行，必须 依附于 托盘程序 的运行及注册校验"
///
/// 跨进程协作：
/// - 启动：Web/Api 子进程启动后立即 IPC getregstate 一次（同步阻塞 ~50ms）
/// - 运行期：30s 轮询 + 托盘注册状态变化时主动触发回调（LicenseMonitor）
/// - 托盘重启：30s 轮询检测托盘重启，新状态自动生效
/// </summary>
public static class LicenseGuard
{
    /// <summary>进程内缓存：最近一次注册状态（null = 尚未查询）</summary>
    private static ServiceIpc.RegStateDto? _cachedState;
    private static DateTime _cacheExpiresAtUtc = DateTime.MinValue;

    /// <summary>缓存有效期（30s 过期）</summary>
    private const int CacheTtlSeconds = 30;

    /// <summary>IPC 客户端（懒初始化）</summary>
    private static IpcClient? _ipcClient;

    /// <summary>轮询定时器（后台 30s 刷新缓存）</summary>
    private static Timer? _pollTimer;

    private static readonly object _lock = new();

    /// <summary>
    /// 是否进入全局只读模式
    /// v2.13.143 防御深度：显式判断 RegDate（不依赖 RegisterSdk 内嵌规则）
    /// </summary>
    /// <returns>true = 只读（注册失败/过期/RegDate 缺失/托盘未运行/异常）；false = 注册有效</returns>
    public static bool IsReadOnly()
    {
        var state = GetCachedState();
        if (state is null)
        {
            // 托盘未运行 / IPC 失败 → 默认只读（最安全的默认）
            return true;
        }

        // 第一道：注册状态枚举（v2.13.137 已具备）
        if (state.RegInt != 1)
        {
            return true;  // 未注册 / 已过期（RegisterSdk 内嵌规则）
        }

        // 第二道 v2.13.143：显式日期校验（防御深度）
        if (!state.RegDate.HasValue)
        {
            // RegInt=1 但 RegDate 缺失 → 数据异常 → 拒绝（默认只读更安全）
            return true;
        }
        if (state.RegDate.Value.Date < DateTime.Today)
        {
            // 在期内检测失败 → 已过期 → 只读
            return true;
        }

        return false;  // RegInt=1 且 RegDate >= today → 正常运行
    }

    /// <summary>
    /// 获取当前注册状态（带 30s 缓存）
    /// </summary>
    public static ServiceIpc.RegStateDto? GetCachedState()
    {
        lock (_lock)
        {
            EnsurePollingStarted();

            // 缓存有效 → 直接返回
            if (_cachedState is not null && DateTime.UtcNow < _cacheExpiresAtUtc)
            {
                return _cachedState;
            }

            // 缓存过期 → 同步刷新（首次启动阻塞 ~50ms，后续 30s 轮询不阻塞）
            try
            {
                _ipcClient ??= new IpcClient();
                var state = _ipcClient.GetRegStateAsync(timeoutMs: 2000).GetAwaiter().GetResult();
                _cachedState = state;
                _cacheExpiresAtUtc = DateTime.UtcNow.AddSeconds(CacheTtlSeconds);
                return state;
            }
            catch (IpcUnavailableException)
            {
                // 托盘未运行或 IPC 失败 → 返回旧缓存（可能为 null）
                return _cachedState;
            }
            catch (Exception)
            {
                return _cachedState;
            }
        }
    }

    /// <summary>
    /// 异步获取注册状态（中间件非阻塞场景）
    /// </summary>
    public static async Task<ServiceIpc.RegStateDto?> GetCachedStateAsync()
    {
        ServiceIpc.RegStateDto? state;
        lock (_lock)
        {
            EnsurePollingStarted();

            if (_cachedState is not null && DateTime.UtcNow < _cacheExpiresAtUtc)
            {
                return _cachedState;
            }
        }

        try
        {
            _ipcClient ??= new IpcClient();
            state = await _ipcClient.GetRegStateAsync(timeoutMs: 2000);
            lock (_lock)
            {
                _cachedState = state;
                _cacheExpiresAtUtc = DateTime.UtcNow.AddSeconds(CacheTtlSeconds);
            }
            return state;
        }
        catch (IpcUnavailableException)
        {
            return null;
        }
    }

    /// <summary>
    /// 重置缓存（强制下次 IsReadOnly 调用立即刷新）
    ///
    /// 注意：v2.13.137 此方法仅清空本地缓存，不再调用 RegisterSdk.ResetCache()
    /// 因为 Web/Api 端不应直接调用 RegisterSdk（注册校验必须在托盘端）
    /// 托盘端 LicenseForm 写入新 CDKEY 后通过 LicenseMonitor 周期推送（5s 内生效）
    /// 或子进程 30s 轮询触发自动刷新
    /// </summary>
    public static void ResetCache()
    {
        lock (_lock)
        {
            _cachedState = null;
            _cacheExpiresAtUtc = DateTime.MinValue;
        }
    }

    /// <summary>
    /// 启动后台轮询定时器（首次调用 IsReadOnly 时自动启动）
    /// </summary>
    private static void EnsurePollingStarted()
    {
        if (_pollTimer is not null) return;
        _pollTimer = new Timer(
            callback: _ => RefreshFromTray(),
            state: null,
            dueTime: TimeSpan.FromSeconds(CacheTtlSeconds),
            period: TimeSpan.FromSeconds(CacheTtlSeconds));
    }

    /// <summary>
    /// 定时器回调：刷新缓存（异步转同步等待避免阻塞定时器线程）
    /// </summary>
    private static void RefreshFromTray()
    {
        try
        {
            _ipcClient ??= new IpcClient();
            var state = _ipcClient.GetRegStateAsync(timeoutMs: 2000).GetAwaiter().GetResult();
            lock (_lock)
            {
                _cachedState = state;
                _cacheExpiresAtUtc = DateTime.UtcNow.AddSeconds(CacheTtlSeconds);
            }
        }
        catch
        {
            // 托盘暂时不可用 → 保持旧缓存或标记为 null
            // 不更新 _cacheExpiresAtUtc，下次调用会立即重试
        }
    }

    /// <summary>
    /// 获取最近一次注册状态检测时间（UTC）
    /// </summary>
    public static DateTime LastCheckUtc
    {
        get
        {
            lock (_lock)
            {
                return _cachedState?.DetectedAtUtc ?? DateTime.MinValue;
            }
        }
    }
}