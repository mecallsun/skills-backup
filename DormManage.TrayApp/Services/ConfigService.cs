using System.Text.Json;
using DormManage.Shared.Models;
using DormManage.TrayApp.Models;
using Microsoft.Extensions.Configuration;

namespace DormManage.TrayApp.Services;

/// <summary>
/// 配置服务：负责 appsettings.json 的加载、内存缓存、原子写回。
///
/// 设计要点：
/// 1. 文件不存在时使用内置默认值，并写出一份新文件（首次启动）
/// 2. JSON 损坏时备份为 .bak 并重置为默认值（避免启动失败）
/// 3. 写回使用临时文件 + Replace 模式，保证原子性
/// </summary>
public class ConfigService
{
    private readonly string _configPath;
    private readonly LogService _log;
    private readonly object _lock = new();
    private AppConfig _current = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,  // 关键：允许 JSON 中用 PascalCase 也能匹配 C# 属性
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ConfigService(string configPath, LogService log)
    {
        _configPath = configPath;
        _log = log;
    }

    /// <summary>当前配置（线程安全快照）</summary>
    public AppConfig Current
    {
        get { lock (_lock) { return Clone(_current); } }
    }

    /// <summary>
    /// 加载配置；文件不存在则写出默认值；JSON 损坏则备份并重置。
    /// </summary>
    public AppConfig Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_configPath))
            {
                _log.Warn($"配置文件不存在：{_configPath}，写出默认值");
                _current = new AppConfig();
                SaveUnlocked(_current);
                return Clone(_current);
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (loaded is null)
                    throw new InvalidDataException("配置反序列化结果为 null");

                _current = loaded;
                _log.Info($"配置已加载：ApiPort={_current.Tray.ApiPort}, AdminPort={_current.Tray.AdminPort}, DbProvider={_current.Database.Provider}");
                _log.Info($"  ApiExecutable='{_current.Tray.ApiExecutable}'");
                _log.Info($"  AdminExecutable='{_current.Tray.AdminExecutable}'");
                _log.Info($"  DbConnStrLen={_current.Database.ConnectionString?.Length ?? 0}, SqlitePath='{_current.Database.SqlitePath}'");
                return Clone(_current);
            }
            catch (Exception ex)
            {
                var bakPath = _configPath + $".{DateTime.Now:yyyyMMddHHmmss}.bak";
                File.Copy(_configPath, bakPath, overwrite: true);
                _log.Error($"配置文件损坏，已备份至 {bakPath}，重置为默认值", ex);
                _current = new AppConfig();
                SaveUnlocked(_current);
                return Clone(_current);
            }
        }
    }

    /// <summary>更新配置并持久化（线程安全）</summary>
    public void Update(AppConfig newConfig)
    {
        lock (_lock)
        {
            _current = newConfig;
            SaveUnlocked(_current);
            _log.Info($"配置已更新：ApiPort={_current.Tray.ApiPort}, AdminPort={_current.Tray.AdminPort}, DbProvider={_current.Database.Provider}");
        }
    }

    private void SaveUnlocked(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOpts);
        var tmp = _configPath + ".tmp";
        File.WriteAllText(tmp, json, new System.Text.UTF8Encoding(false));
        // 原子替换
        File.Move(tmp, _configPath, overwrite: true);
    }

    private static AppConfig Clone(AppConfig src) => new()
    {
        Tray = new TraySection
        {
            ApiPort = src.Tray.ApiPort,
            AdminPort = src.Tray.AdminPort,
            ApiExecutable = src.Tray.ApiExecutable,
            AdminExecutable = src.Tray.AdminExecutable,
            AutoStartServices = src.Tray.AutoStartServices,
            AutoRestartOnCrash = src.Tray.AutoRestartOnCrash,
            HealthCheckIntervalSeconds = src.Tray.HealthCheckIntervalSeconds
        },
        Database = new DatabaseSection
        {
            Provider = src.Database.Provider,
            ConnectionString = src.Database.ConnectionString,
            SqlitePath = src.Database.SqlitePath
        },
        Storage = new StorageSection
        {
            ImageRoot = src.Storage.ImageRoot,
            LogRoot = src.Storage.LogRoot
        }
    };

    /// <summary>
    /// v2.13.19：根据 DatabaseConfigDto 更新 appsettings.json 中的 Database 段，
    /// 并生成对应的 ConnectionString，保证子进程环境变量来源一致。
    /// </summary>
    public void UpdateDatabaseSection(DatabaseConfigDto dto)
    {
        lock (_lock)
        {
            _current.Database.Provider = dto.Provider;
            _current.Database.SqlitePath = dto.SqlitePath ?? "";
            _current.Database.ConnectionString = dto.Provider == "Sqlite"
                ? (string.IsNullOrWhiteSpace(dto.SqlitePath)
                    ? ""
                    : $"Data Source={dto.SqlitePath}")
                : (string.IsNullOrWhiteSpace(dto.DbServer)
                    ? ""
                    : dto.BuildConnectionString());
            SaveUnlocked(_current);
        }
    }

    /// <summary>配置文件绝对路径</summary>
    public string ConfigPath => _configPath;
}