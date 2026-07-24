using System;
using DormManage.Shared.Register;

namespace DormManage.Shared.Security;

/// <summary>
/// v2.13.136 注册状态守卫：判定当前进程是否进入"全局只读"模式
///
/// 业务规则（与 v2.13.94 RegisterSdk 联动）：
/// - RegisterSdk.CheckReg().RegInt == 1 → 注册有效 → 允许所有读写操作
/// - RegisterSdk.CheckReg().RegInt == 0 → 注册已过期 → 全局只读
/// - RegisterSdk.CheckReg().RegInt == -1 → 未注册 → 全局只读
/// - RegisterSdk.CheckReg() 抛异常 → 按"未注册"处理（更安全的默认值）
///
/// 进程内缓存：首次调用查询注册表，结果在进程生命周期内缓存；
/// 调用 <see cref="ResetCache"/> 强制下次重新查询（用于托盘写入新 CDKEY 后立即生效）。
///
/// 跨进程同步：本类仅在 Admin/Api 自身进程内缓存，托盘端写入 CDKEY 后
/// 通过环境变量 / 共享文件 / IPC 通知下游进程（v2.13.32 数据源热加载模式）。
/// </summary>
public static class LicenseGuard
{
    /// <summary>
    /// 当前进程内注册状态缓存
    /// - null = 尚未检测
    /// - 0/-1/1 = RegInt 缓存值
    /// </summary>
    private static int? _cachedRegInt;

    /// <summary>最近一次检测时间（用于调试）</summary>
    private static DateTime _lastCheckUtc = DateTime.MinValue;

    private static readonly object _lock = new();

    /// <summary>
    /// 是否进入全局只读模式
    /// </summary>
    /// <returns>true = 注册失败/已过期（POST/PUT/DELETE 全部拦截）；false = 注册有效</returns>
    public static bool IsReadOnly()
    {
        lock (_lock)
        {
            if (!_cachedRegInt.HasValue)
            {
                try
                {
                    var reg = RegisterSdk.CheckReg();
                    _cachedRegInt = reg.RegInt;
                    _lastCheckUtc = DateTime.UtcNow;
                }
                catch (Exception)
                {
                    // 异常时按"未注册"处理（更安全的默认值）
                    _cachedRegInt = -1;
                    _lastCheckUtc = DateTime.UtcNow;
                }
            }
            return _cachedRegInt != 1;
        }
    }

    /// <summary>
    /// 获取当前缓存的 RegInt（仅供调试/UI 状态显示使用）
    /// </summary>
    public static int? CachedRegInt
    {
        get
        {
            lock (_lock) { return _cachedRegInt; }
        }
    }

    /// <summary>
    /// 重置缓存（用于托盘端 LicenseForm 写入新 CDKEY 后强制重读）
    ///
    /// 注意：这是进程内的单实例缓存重置，不跨进程。
    /// 跨进程场景（托盘 → Admin/Api）需配合：
    /// 1) WebAdmin 通过 127.0.0.1:5099 IPC 通知 Admin/Api 重启；或
    /// 2) Admin/Api 启动一个简易文件监听（修改 machine.dat 时间戳触发重读）；或
    /// 3) 用户重启 Web 服务（最简单但体验差）
    /// </summary>
    public static void ResetCache()
    {
        lock (_lock)
        {
            _cachedRegInt = null;
            _lastCheckUtc = DateTime.MinValue;
        }
    }

    /// <summary>
    /// 获取最近一次注册状态检测时间（UTC），用于 SysOpLog 审计
    /// </summary>
    public static DateTime LastCheckUtc
    {
        get
        {
            lock (_lock) { return _lastCheckUtc; }
        }
    }
}