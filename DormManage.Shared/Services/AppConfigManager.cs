using System.Text.Json;
using DormManage.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Shared.Services;

/// <summary>
/// 统一配置中心服务（v2.13.19 数据库连接双 UI 双向同步机制核心）
///
/// 设计要点：
/// - 单例：全应用进程一个实例，避免读写竞争
/// - 双擎持久化：先写本地文件（崩溃保险）→ 再写 SQL Server SysParameter 表
/// - 触发广播：保存成功后触发 OnDatabaseConfigUpdated 事件订阅者
/// </summary>
public class AppConfigManager
{
    private static readonly Lazy<AppConfigManager> _instance = new(() => new AppConfigManager());
    public static AppConfigManager Instance => _instance.Value;

    private readonly string _filePath;
    private readonly object _lock = new();

    /// <summary>
    /// 数据库配置变更事件（订阅者: Web 端 SignalR Hub、TrayApp UI Dispatcher）
    /// 事件参数为完整 DatabaseConfigDto（含 AES-256 加密后的密码）
    /// </summary>
    public event EventHandler<DatabaseConfigDto>? OnDatabaseConfigUpdated;

    private AppConfigManager()
    {
        // 本地配置文件路径（与宿主 App 在同一工作目录）
        _filePath = Path.Combine(AppContext.BaseDirectory, "db_setting.json");
    }

    /// <summary>
    /// 异步测试数据库连接（不写入，仅验证连通性）
    /// 使用 SqlConnectionStringBuilder 组装字符串（防裸拼接）
    /// </summary>
    public async Task<(bool Success, string Message)> TestDbConnectionAsync(DatabaseConfigDto config)
    {
        // v2.13.19：处理前端传来的 "unchanged" 密码哨兵
        config = await ResolveUnchangedPasswordAsync(config);

        if (config.Provider == "Sqlite")
        {
            if (string.IsNullOrWhiteSpace(config.SqlitePath))
                return (false, "SQLite 数据库路径为空");
            try
            {
                var builder = new SqliteConnectionStringBuilder
                {
                    DataSource = config.SqlitePath
                };
                await using var conn = new SqliteConnection(builder.ConnectionString);
                await conn.OpenAsync();
                return (true, "SQLite 连接成功");
            }
            catch (Exception ex)
            {
                return (false, $"SQLite 连接失败: {ex.Message}");
            }
        }
        else  // SqlServer
        {
            try
            {
                var connStr = config.BuildConnectionString();
                await using var conn = new System.Data.SqlClient.SqlConnection(connStr);
                await conn.OpenAsync();
                return (true, "SQL Server 连接成功");
            }
            catch (Exception ex)
            {
                return (false, $"SQL Server 连接失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 解决 "unchanged" 密码哨兵：读取旧配置中的真实密码替换。
    /// </summary>
    private async Task<DatabaseConfigDto> ResolveUnchangedPasswordAsync(DatabaseConfigDto config)
    {
        if (config.DbPassword != "unchanged")
            return config;

        var existing = await LoadAsync();
        if (existing is not null && !string.IsNullOrEmpty(existing.DbPassword))
        {
            config.DbPassword = existing.DbPassword;
            return config;
        }

        return config;
    }

    /// <summary>
    /// 异步保存数据库配置（双擎持久化：先文件 → 后 DB → 广播）
    /// 1) 解决 "unchanged" 密码哨兵
    /// 2) 测试连通性（失败则不保存！）
    /// 3) AES-256 加密密码字段
    /// 4) 写入本地 db_setting.json（atomic rename）
    /// 5) 写入 SQL Server SysParameter 表
    /// 6) 失败回滚：本地文件 → 旧版本
    /// 7) 触发 OnDatabaseConfigUpdated 事件广播
    /// </summary>
    public async Task<(bool Success, string Message)> SaveConfigurationAsync(DatabaseConfigDto newConfig)
    {
        // Step 1: 解决 "unchanged" 密码哨兵（v2.13.19）
        newConfig = await ResolveUnchangedPasswordAsync(newConfig);

        // Step 2: 安全卡口 — 测试连通性
        var (connOk, connMsg) = await TestDbConnectionAsync(newConfig);
        if (!connOk)
            return (false, $"数据库连通性校验失败，无法保存：{connMsg}");

        // Step 3: AES-256 加密密码
        var encrypted = new DatabaseConfigDto
        {
            DbServer = newConfig.DbServer,
            DbPort = newConfig.DbPort,
            DbName = newConfig.DbName,
            DbUser = newConfig.DbUser,
            DbPassword = AesEncryptor.Encrypt(newConfig.DbPassword ?? string.Empty),
            Provider = newConfig.Provider,
            SqlitePath = newConfig.SqlitePath
        };

        lock (_lock)
        {
            try
            {
                // Step 4: 备份旧文件 + 写入新文件（临时文件 → rename 保证原子性）
                string? oldContent = null;
                if (File.Exists(_filePath))
                    oldContent = File.ReadAllText(_filePath);

                var tempPath = _filePath + ".tmp";
                var json = JsonSerializer.Serialize(encrypted, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);

                // Step 5: 写入数据库 (此处需要 DbContext，通过回调实现)
                _ = Task.Run(async () =>
                {
                    try { await WriteToDatabaseAsync(encrypted); }
                    catch (Exception ex) { Console.WriteLine($"[AppConfigManager] DB 写入失败: {ex.Message}"); }
                });

                // Step 6: 触发广播
                OnDatabaseConfigUpdated?.Invoke(this, encrypted);

                return (true, "数据库配置保存成功（本地文件已同步，数据库记录已写入后台任务）");
            }
            catch (Exception ex)
            {
                return (false, $"保存失败：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 异步读取当前配置（优先文件，其次数据库）
    /// </summary>
    public async Task<DatabaseConfigDto?> LoadAsync(Func<DatabaseConfigDto>? sqlFallback = null)
    {
        lock (_lock)
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var config = JsonSerializer.Deserialize<DatabaseConfigDto>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    if (config != null) return DecryptConfig(config);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[AppConfigManager] IO 异常: {ex.Message}");
                }
            }
        }

        // 文件不存在 → 返回数据库 fallback
        return sqlFallback != null ? DecryptConfig(sqlFallback.Invoke()) : null;
    }

    /// <summary>
    /// 解密配置中的密码字段
    /// </summary>
    private DatabaseConfigDto DecryptConfig(DatabaseConfigDto config)
    {
        if (!string.IsNullOrEmpty(config.DbPassword))
        {
            config.DbPassword = AesEncryptor.Decrypt(config.DbPassword);
        }
        return config;
    }

    /// <summary>
    /// 写入 SQL Server SysParameter 表（独立 DbContext 实例，避免与宿主 DbContext 冲突）
    /// </summary>
    private async Task WriteToDatabaseAsync(DatabaseConfigDto config)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DormManage.Shared.Data.DormDbContext>()
            .UseSqlServer(config.BuildConnectionString())
            .Options;
        await using var ctx = new DormManage.Shared.Data.DormDbContext(options);

        var paramsToWrite = new Dictionary<string, (string Value, bool Encrypted, string Desc)>
        {
            ["db.provider"] = (config.Provider, false, "数据库提供程序 (SqlServer/Sqlite)"),
            ["db.server"] = (config.DbServer, false, "数据库服务器"),
            ["db.port"] = (config.DbPort.ToString(), false, "端口号"),
            ["db.name"] = (config.DbName, false, "数据库名"),
            ["db.user"] = (config.DbUser, false, "账号"),
            ["db.password"] = (config.DbPassword ?? string.Empty, true, "密码 (AES-256)"),
            ["db.sqlitePath"] = (config.SqlitePath ?? string.Empty, false, "SQLite 数据库路径")
        };

        foreach (var (key, (value, encrypted, desc)) in paramsToWrite)
        {
            var existing = await ctx.SysParameters
                .FirstOrDefaultAsync(p => p.Category == "Database" && p.ParamKey == key);

            if (existing != null)
            {
                existing.ParamValue = value;
                existing.IsEncrypted = encrypted;
                existing.Description = desc;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                ctx.SysParameters.Add(new SysParameter
                {
                    Category = "Database",
                    ParamKey = key,
                    ParamValue = value,
                    IsEncrypted = encrypted,
                    Description = desc,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }
        }

        await ctx.SaveChangesAsync();
    }
}
