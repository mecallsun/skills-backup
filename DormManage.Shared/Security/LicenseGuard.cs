using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DormManage.Shared.Register;
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
///
/// v2.13.150：试用模式分模块记录上限（替代 v2.13.149 统一 5 条）
/// - 住宿登记（Booking）：最多 500 条
/// - 住宿档案（Dorms）：最多 5 条
/// - 人员清单（Personnel）：最多 5 条
/// - 当 RegInt=-1（未注册）+ UseTimes < TRIAL_LIMIT（试用中）→ 进入试用模式
/// - 当前记录数 ≥ 模块上限时拦截 POST，提示「试用功能受限请联系信息科！」
/// - 注册有效（RegInt=1）或全局只读时不进入此流程（已由 LicenseReadOnlyMiddleware 拦截）
/// </summary>
public static class LicenseGuard
{

    /// <summary>v2.13.150：试用受限错误码（统一供 Api 返回 + Razor Page 显示）</summary>
    public const string TrialLimitErrorCode = "TRIAL_LIMIT_EXCEEDED";

    /// <summary>v2.13.150：试用受限标准提示（用户原话，区别于 v2.13.149）</summary>
    public const string TrialLimitMessage = "试用功能受限请联系信息科！";

    /// <summary>v2.13.150：试用模式下各模块最大记录数（住宿登记/住宿档案/人员清单）</summary>
    public static readonly IReadOnlyDictionary<string, int> TrialMaxRecordsByModule = new Dictionary<string, int>
    {
        ["住宿登记"] = 500,
        ["住宿档案"] = 5,
        ["人员清单"] = 5
    };

    /// <summary>v2.13.169 注册状态枚举常量（与 RegStateDto.RegStatus 对应）</summary>
    public static class RegStatusEnum
    {
        public const int Unregistered = -1;  // 未注册（试用模式）
        public const int Valid = 1;          // 注册有效
        public const int Expired = 2;         // 已过期
        public const int Invalid = 3;         // 校验失败（机器码/公司名不匹配等）
    }

    /// <summary>v2.13.149：旧版统一 5 条上限（保留兼容，已被 TrialMaxRecordsByModule 取代）</summary>
    [Obsolete("v2.13.149 单一 5 条上限已被 v2.13.150 分模块上限取代，请使用 TrialMaxRecordsByModule")]
    public const int TrialMaxRecords = 5;

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
    /// v2.13.149：是否处于试用模式
    /// 业务规则：未注册（RegInt=-1）+ UseTimes < TRIAL_LIMIT → 试用模式
    /// 与 IsReadOnly 区别：IsReadOnly=true 时请求被中间件拦截 → 不会到此检查
    /// IsTrialMode=true 时只针对 3 模块的 Create 限制 5 条记录
    /// </summary>
    /// <returns>true = 试用模式（需检查 3 模块记录数）</returns>
    public static bool IsTrialMode()
    {
        var state = GetCachedState();
        if (state is null)
        {
            // 托盘未运行 → 默认只读（不放行任何试用检查）
            return false;
        }

        // v2.13.196：明确三种模式的判定
        // - 已注册（RegInt=1）→ 不是试用模式
        // - 未注册（RegInt=-1）→ 试用模式（包括次数未满 + 超试用次数强制模式）
        // - 其他（RegInt=0 旧格式）→ 不视为试用模式
        if (state.RegInt == 1)
        {
            return false;
        }

        if (state.RegInt == -1)
        {
            // 试用模式（强制）：包括超试用次数时也进入试用模式
            // 配合 CheckTrialRecordLimit 实现"3 模块记录数限制"
            return true;
        }

        // RegInt=0（旧格式过期或其他未知状态）→ 不视为试用模式
        return false;
    }

    /// <summary>
    /// v2.13.150：试用模式下检查指定模块的当前记录数，分模块上限
    /// 用于 Api Controller / Razor Page POST handler 在 Create 前调用
    /// 设计：每个调用方传入当前记录数（避免在此方法内做 DB 查询，职责单一）
    /// </summary>
    /// <param name="moduleName">模块显示名（必须为「住宿登记」「住宿档案」「人员清单」之一）</param>
    /// <param name="currentCount">当前记录数</param>
    /// <returns>(isAllowed, message) → isAllowed=true 放行；false 拦截并返回详细提示</returns>
    public static (bool IsAllowed, string Message) CheckTrialRecordLimit(string moduleName, int currentCount)
    {
        if (!IsTrialMode())
        {
            // 已注册或非试用模式 → 不限制
            return (true, "");
        }

        // v2.13.150：分模块上限（住宿登记 500 / 住宿档案 5 / 人员清单 5）
        if (!TrialMaxRecordsByModule.TryGetValue(moduleName, out var maxRecords))
        {
            // 未知模块名 → 默认按 5 条限制（保守安全）
            maxRecords = 5;
        }

        if (currentCount >= maxRecords)
        {
            // 超过分模块上限 → 拦截（v2.13.150 用户原话新提示）
            return (false, $"试用功能受限请联系信息科！\n\n当前『{moduleName}』已有 {currentCount} 条记录，超出试用上限 {maxRecords} 条。\n\n请联系信息科进行正式注册后即可继续使用。");
        }

        return (true, $"试用模式：当前『{moduleName}』{currentCount}/{maxRecords} 条");
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

    /// <summary>
    /// v2.13.169 注册状态徽章四态信息（供前端 license-status-badge.js 30s 轮询）
    ///
    /// 返回值：
    /// - code   : "已注册" / "试用模式" / "已过期" / "校验失败" / "授权不可用"
    /// - message: 详细提示（含操作建议）
    /// - level  : "success" / "info" / "warning" / "danger" / "info"
    ///
    /// 调用时机：LicenseStatusController.GetLicenseStatus 在每次请求时调用
    /// 与 IsReadOnly/IsTrialMode 区别：本方法返回「展示用」字符串而非布尔判定
    ///
    /// v2.13.179 全面梳理（用户原话：保证 3 种注册状态与 3 种运行模式的功能一致性）：
    ///   - Unregistered(-1) → 试用模式（中间件放行 GET + 3 模块 POST 限记录数）
    ///   - Valid(1)        → 注册模式（全功能）
    ///   - Expired(2)      → 只读模式（中间件拦截全部 POST）
    ///   - Invalid(3)      → 只读模式（同上）
    /// </summary>
    public static (string Code, string Message, string Level) GetLicenseBanner()
    {
        var state = GetCachedState();

        // 托盘未运行 / IPC 失败 → 不可用
        if (state is null)
        {
            return ("授权不可用", "无法连接到托盘程序的 IPC 服务（127.0.0.1:5099）。请检查托盘程序是否运行。", "danger");
        }

        // v2.13.179 详细化：5 case 覆盖所有场景
        return state.RegStatus switch
        {
            RegStatusEnum.Valid =>
                ("已注册",
                 $"✅ 软件已正式注册：{state.LTDName}，有效期至 {state.RegDate:yyyy-MM-dd}，所有功能正常使用。",
                 "success"),

            RegStatusEnum.Unregistered =>
                ("试用模式",
                 state.UseTimes >= RegisterSdk.TRIAL_LIMIT
                    ? "⚠ 试用次数已用尽，请联系信息科完成正式注册。"
                    : $"🟦 试用模式：剩余 {RegisterSdk.TRIAL_LIMIT - state.UseTimes} 次使用机会，正式注册请联系信息科。",
                 "info"),

            RegStatusEnum.Expired =>
                ("已过期",
                 $"⚠ 注册码已过期（{state.RegDate:yyyy-MM-dd}），软件进入只读模式。请联系信息科进行续期。",
                 "warning"),

            RegStatusEnum.Invalid =>
                ("校验失败",
                 "⚠ 注册码校验失败（机器码/公司名不匹配）。软件进入只读模式，请联系信息科。",
                 "danger"),

            _ =>
                ("未知状态",
                 $"未识别的注册状态码：{state.RegStatus}。请检查托盘程序日志。",
                 "info"),
        };
    }

    /// <summary>
    /// v2.13.170：将 RegInt 转换为 RegStatus 枚举值
    /// 转换规则：
    ///   RegInt=1 → Valid(1)
    ///   RegInt=0 → Expired(2)（已过期/无效）
    ///   RegInt=-1 → Unregistered(-1)（未注册/试用）
    ///
    /// 在 HandleGetRegState 调用，构造 RegStateDto 时使用
    /// </summary>
    public static int ConvertRegIntToRegStatus(int regInt)
    {
        return regInt switch
        {
            1 => RegStatusEnum.Valid,
            0 => RegStatusEnum.Expired,
            -1 => RegStatusEnum.Unregistered,
            _ => RegStatusEnum.Invalid,
        };
    }
}