using System.Text.Json;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Api.HostedServices;

/// <summary>
/// 数据库配置文件监视器（v2.13.32 热加载核心组件）
///
/// 用途：监听 db_setting.json 变更，跨进程同步数据库连接配置。
/// - 当 Web 端保存新连接 → 写文件 → 本进程立即收到变更 → ApplyExternalConfiguration
/// - 当 Api/Admin 独立启动（无托盘）时，手动修改 db_setting.json 也能触发热加载
///
/// 防抖策略：200ms 内多次 Changed 事件合并为一次应用（FileSystemWatcher 经常触发多次）
///
/// 不监控 SysParameter 表变更（SQL Server 数据库变更无内置事件触发机制，
/// 跨进程的 SysParameter 表更新由 TrayApp IPC 触发 OnDatabaseConfigUpdated 事件完成）
/// </summary>
public sealed class DatabaseConfigFileWatcher : IHostedService, IDisposable
{
    private readonly ILogger<DatabaseConfigFileWatcher> _logger;
    private FileSystemWatcher? _watcher;
    private readonly string _configFilePath;
    private System.Timers.Timer? _debounceTimer;
    private readonly object _debounceLock = new();

    public DatabaseConfigFileWatcher(ILogger<DatabaseConfigFileWatcher> logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "db_setting.json");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configFilePath);
            var fileName = Path.GetFileName(_configFilePath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                _logger.LogWarning("[DB-CONFIG-WATCHER] 监控目录不存在: {Dir}", directory);
                return Task.CompletedTask;
            }

            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("[DB-CONFIG-WATCHER] db_setting.json 不存在，跳过文件监控（仅 SysParameter 路径生效）");
                return Task.CompletedTask;
            }

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Renamed += OnRenamed;

            _debounceTimer = new System.Timers.Timer(200) { AutoReset = false };
            _debounceTimer.Elapsed += OnDebounceElapsed;

            _logger.LogInformation("[DB-CONFIG-WATCHER] 已启动监控: {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DB-CONFIG-WATCHER] 启动失败");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
            if (_debounceTimer is not null)
            {
                _debounceTimer.Dispose();
            }
            _logger.LogInformation("[DB-CONFIG-WATCHER] 已停止");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DB-CONFIG-WATCHER] 停止时异常");
        }
        return Task.CompletedTask;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        // 防抖：200ms 内多次事件只触发一次应用
        lock (_debounceLock)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        lock (_debounceLock)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }
    }

    private void OnDebounceElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            // 等待文件释放（防止读取时还被占用）
            if (!File.Exists(_configFilePath)) return;

            // 重试 3 次（文件可能被另一个进程锁定）
            string json = string.Empty;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    json = File.ReadAllText(_configFilePath);
                    break;
                }
                catch (IOException) when (i < 2)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("[DB-CONFIG-WATCHER] 读取文件失败（重试 3 次后仍锁定）");
                return;
            }

            var config = JsonSerializer.Deserialize<DatabaseConfigDto>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            if (config is null)
            {
                _logger.LogWarning("[DB-CONFIG-WATCHER] 反序列化配置为空");
                return;
            }

            // 解密密码字段（db_setting.json 中存的是 AES-256 密文）
            if (!string.IsNullOrEmpty(config.DbPassword))
            {
                try
                {
                    config.DbPassword = AesEncryptor.Decrypt(config.DbPassword);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DB-CONFIG-WATCHER] 密码解密失败（可能已是明文）");
                }
            }

            // 触发热加载（写入运行时单例 + 通知所有订阅者）
            AppConfigManager.Instance.ApplyExternalConfiguration(config);
            _logger.LogInformation("[DB-CONFIG] 检测到 db_setting.json 变更，已热加载：Provider={Provider}, Server={Server}, Db={Db}",
                config.Provider, config.DbServer, config.DbName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DB-CONFIG-WATCHER] 应用配置变更失败");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}
