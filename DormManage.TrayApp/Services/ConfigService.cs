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
                _log.Info($"  DbConnStrLen={_current.Database.ConnectionString?.Length ?? 0}");  // v2.13.109: SqlitePath 已移除

                // v2.13.157 自愈：配置中的路径若失效，自动用 BaseDirectory 派生默认并写回（无需用户干预）
                AutoHealPathsIfInvalid();
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

    /// <summary>
    /// v2.13.157 自愈：检查配置中的 Api/Admin 可执行文件路径，
    /// 若指向不存在的位置（典型：用户拷贝到不同机器、目录结构差异），
    /// 自动用 BaseDirectory 派生合理路径并写回配置文件，用户无需人工干预。
    /// 探测顺序与 ProcessManager.TryResolveExePath 保持一致（单源真相）。
    /// </summary>
    private void AutoHealPathsIfInvalid()
    {
        var changed = false;
        foreach (var field in new[] { nameof(TraySection.ApiExecutable), nameof(TraySection.AdminExecutable) })
        {
            var val = field == nameof(TraySection.ApiExecutable) ? _current.Tray.ApiExecutable : _current.Tray.AdminExecutable;
            if (string.IsNullOrWhiteSpace(val) || !Path.Exists(Path.IsPathRooted(val) ? val : Path.Combine(AppContext.BaseDirectory, val)))
            {
                var childName = field == nameof(TraySection.ApiExecutable) ? "Api" : "Admin";
                var exeName = $"DormManage.{childName}.exe";
                var healed = TryFindExeUnderBase(exeName);
                if (healed != null)
                {
                    _log.Warn($"[AUTO-HEAL] {field}='{val}' 路径失效 → 自动修复为 '{healed}'（无需用户操作）");
                    if (field == nameof(TraySection.ApiExecutable))
                        _current.Tray.ApiExecutable = healed;
                    else
                        _current.Tray.AdminExecutable = healed;
                    changed = true;
                }
                else
                {
                    _log.Error($"[AUTO-HEAL] {field}='{val}' 路径失效且未找到 {exeName}（请确认 Api/ 或 Admin/ 子目录存在）");
                }
            }
        }
        if (changed) SaveUnlocked(_current);
    }

    /// <summary>与 ProcessManager.TryResolveExePath 候选路径一致；返回首个存在的绝对路径
/// v2.13.197：移除 v2.13.193 之前的"兄弟目录"陷阱（baseDir/Api/, baseDir/Admin/），统一使用主目录路径
/// </summary>
    public static string? TryFindExeUnderBase(string exeName)
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var baseParent = Path.GetDirectoryName(baseDir) ?? baseDir;
        var candidates = new[]
        {
            Path.Combine(baseDir, "..", "Api", exeName),                 // v2.13.197 默认: 主目录 Api/
            Path.Combine(baseDir, "..", "Admin", exeName),               // v2.13.197 默认: 主目录 Admin/
            Path.Combine(baseDir, exeName),                              // 兜底: 自身目录
            Path.Combine(baseParent, "Api", exeName),                    // 父目录 dev 模式
            Path.Combine(baseParent, "Admin", exeName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Api", exeName),  // 工作目录父目录
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Admin", exeName)
        };
        foreach (var c in candidates)
        {
            try
            {
                var normalized = Path.GetFullPath(c);
                if (File.Exists(normalized)) return normalized;
            }
            catch { }
        }
        return null;
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
    /// v2.13.109：仅支持 SqlServer；硬拒绝其他 Provider。
    /// </summary>
    public void UpdateDatabaseSection(DatabaseConfigDto dto)
    {
        // v2.13.109: 硬拒绝非 SqlServer Provider
        if (!string.Equals(dto.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            _log.Error($"UpdateDatabaseSection 拒绝：Provider={dto.Provider}（v2.13.109 起仅支持 SqlServer）");
            throw new InvalidOperationException("当前版本仅支持 SQL Server（SQLite 已于 v2.13.109 移除）");
        }

        lock (_lock)
        {
            _current.Database.Provider = "SqlServer";
            _current.Database.ConnectionString = string.IsNullOrWhiteSpace(dto.DbServer)
                ? ""
                : dto.BuildConnectionString();
            SaveUnlocked(_current);
        }
    }

    /// <summary>配置文件绝对路径</summary>
    public string ConfigPath => _configPath;
}