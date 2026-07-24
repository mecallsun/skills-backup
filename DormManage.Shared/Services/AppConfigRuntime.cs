using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 运行时数据库配置中心（v2.13.32 热加载核心）
///
/// 设计要点：
/// - 单例（与 AppConfigManager.Instance 配合）：进程级唯一配置缓存
/// - 数据源：优先 SysParameter 表 → db_setting.json → appsettings.json → 默认值
/// - 线程安全：volatile + lock 双保险
/// - 热切换机制：
///   1. Web/托盘保存 → AppConfigManager.SaveConfigurationAsync
///   2. SaveConfigurationAsync 末尾触发 Runtime.Reload()
///   3. Reload 读取最新配置（4 级优先级） → 更新 _current → 触发 OnChanged 事件
///   4. 下一个 HTTP 请求通过 IDbContextFactory 拿到新配置
///   5. 不需要重启 Api/Admin 子进程
///
/// 调用方：
/// - IDbContextFactory lambda: 每次 CreateDbContext 调用 GetCurrent()
/// - DbConfigController.RuntimeInfo: HTTP 端点显示当前连接
/// - DbConfigController.Reload: HTTP 端点主动重载
/// </summary>
public interface IAppConfigRuntime
{
    /// <summary>同步获取当前配置（线程安全）</summary>
    DatabaseConfigDto GetCurrent();

    /// <summary>最后重载时间（UTC）</summary>
    DateTime LastReloadedAt { get; }

    /// <summary>配置变更事件（订阅者：Interceptor / FileWatcher / Web UI）</summary>
    event EventHandler<DatabaseConfigDto>? OnChanged;

    /// <summary>主动重载配置（从 4 级优先级源读取最新）</summary>
    void Reload();

    /// <summary>从外部（如 FileWatcher）直接注入新配置（不读文件）</summary>
    void ApplyExternalConfiguration(DatabaseConfigDto config);
}

public sealed class AppConfigRuntime : IAppConfigRuntime
{
    private static readonly Lazy<AppConfigRuntime> _instance = new(() => new AppConfigRuntime());
    public static AppConfigRuntime Instance => _instance.Value;

    // 双重保险：volatile 保证多线程可见性，lock 保证原子更新
    private volatile DatabaseConfigDto? _current;
    private readonly object _lock = new();
    // 注意：volatile 不支持 DateTime 结构，改用 long 存 ticks（始终原子）
    private long _lastReloadedAtTicks = DateTime.UtcNow.Ticks;

    public event EventHandler<DatabaseConfigDto>? OnChanged;

    private AppConfigRuntime() { }

    /// <summary>
    /// 同步获取当前配置（线程安全）
    /// 首次访问触发 lazy load（从 AppConfigManager.LoadAsync）
    /// </summary>
    public DatabaseConfigDto GetCurrent()
    {
        if (_current is not null) return _current;

        lock (_lock)
        {
            if (_current is null)
            {
                _current = LoadInitial();
                _lastReloadedAtTicks = DateTime.UtcNow.Ticks;
            }
        }
        return _current!;
    }

    public DateTime LastReloadedAt => new DateTime(Interlocked.Read(ref _lastReloadedAtTicks), DateTimeKind.Utc);

    /// <summary>
    /// 主动重载配置（4 级优先级回退读取）
    /// 1. SysParameter 表（最高优先级，运行时真源）
    /// 2. db_setting.json（AES-256 加密字段式配置）
    /// 3. appsettings.json ConnectionStrings.Default（兜底默认）
    /// 4. 硬编码默认 172.16.0.100/WaterMeterDB/user/1234（v2.13.145 灾难场景）
    /// </summary>
    public void Reload()
    {
        DatabaseConfigDto fresh;
        try
        {
            // 从 AppConfigManager 读取（已实现 4 级回退）
            var loaded = AppConfigManager.Instance.LoadAsync().GetAwaiter().GetResult();
            fresh = loaded ?? BuildFallback();
        }
        catch
        {
            // 任何 IO 异常 → 兜底默认
            fresh = BuildFallback();
        }

        lock (_lock)
        {
            _current = fresh;
            Interlocked.Exchange(ref _lastReloadedAtTicks, DateTime.UtcNow.Ticks);
        }

        try
        {
            OnChanged?.Invoke(this, fresh);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppConfigRuntime] OnChanged 订阅者异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 从外部注入配置（FileSystemWatcher / IPC / 测试场景）
    /// 不读文件，直接更新内存缓存 + 触发事件
    /// </summary>
    public void ApplyExternalConfiguration(DatabaseConfigDto config)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));

        lock (_lock)
        {
            _current = config;
            Interlocked.Exchange(ref _lastReloadedAtTicks, DateTime.UtcNow.Ticks);
        }

        try
        {
            OnChanged?.Invoke(this, config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppConfigRuntime] ApplyExternalConfiguration 订阅者异常: {ex.Message}");
        }
    }

    private DatabaseConfigDto LoadInitial()
    {
        try
        {
            var loaded = AppConfigManager.Instance.LoadAsync().GetAwaiter().GetResult();
            return loaded ?? BuildFallback();
        }
        catch
        {
            return BuildFallback();
        }
    }

    /// <summary>
    /// 硬编码默认配置（灾难场景最终兜底）
    /// v2.13.145 默认值更新：172.16.0.100 / user / 1234
    /// </summary>
    private static DatabaseConfigDto BuildFallback()
    {
        return new DatabaseConfigDto
        {
            Provider = "SqlServer",
            DbServer = "172.16.0.100",
            DbPort = 1433,
            DbName = "WaterMeterDB",
            DbUser = "user",            // SQL 保留关键字 - 连接串 UID=user 无需方括号
            DbPassword = "1234"
        };
    }
}
