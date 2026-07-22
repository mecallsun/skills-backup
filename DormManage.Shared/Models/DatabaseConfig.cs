using System.Text.Json.Serialization;
// SQLite Provider 已于 v2.13.109 移除。SqlitePath 字段保留 [Obsolete] 仅为兼容旧配置反序列化。

namespace DormManage.Shared.Models;

/// <summary>
/// 数据库连接配置契约（v2.13.19 数据库连接双 UI 双向同步）
/// 字段与 SQL Server 连接字符串一一对应，前端按字段录入，后端用 SqlConnectionStringBuilder 组装
/// </summary>
public class DatabaseConfigDto
{
    /// <summary>数据库服务器（IP 或 域名）</summary>
    [JsonPropertyName("dbServer")]
    public string DbServer { get; set; } = "192.168.1.237";

    /// <summary>端口（默认 1433）</summary>
    [JsonPropertyName("dbPort")]
    public int DbPort { get; set; } = 1433;

    /// <summary>数据库名称</summary>
    [JsonPropertyName("dbName")]
    public string DbName { get; set; } = "WaterMeterDB";

    /// <summary>用户名</summary>
    [JsonPropertyName("dbUser")]
    public string DbUser { get; set; } = "__DB_USER__";

    /// <summary>密码（AES-256 加密后存储，前端展示时脱敏）</summary>
    [JsonPropertyName("dbPassword")]
    public string? DbPassword { get; set; } = "__DB_PASSWORD__";

    /// <summary>提供程序：固定为 SqlServer（v2.13.109 起移除 SQLite 双 provider）</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "SqlServer";

    /// <summary>
    /// 历史 SQLite 配置字段。SQLite Provider 已于 v2.13.109 移除，
    /// 仅用于兼容旧版 db_setting.json 反序列化。不再参与连接字符串构造、备份恢复、UI 编辑、进程环境变量。
    /// </summary>
    [Obsolete("SQLite provider has been removed; retained for legacy configuration deserialization.")]
    [JsonPropertyName("sqlitePath")]
    public string? SqlitePath { get; set; }

    /// <summary>使用 SqlConnectionStringBuilder 组装（防裸拼接）</summary>
    public string BuildConnectionString()
    {
        var builder = new System.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(DbServer) ? "localhost" : DbServer,
            InitialCatalog = DbName,
            UserID = DbUser,
            Password = DbPassword ?? string.Empty,
            // 默认安全选项
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 10
        };
        if (DbPort > 0 && DbPort != 1433)
        {
            builder.DataSource = $"{DbServer},{DbPort}";
        }
        return builder.ConnectionString;
    }
}

/// <summary>
/// 系统参数表（v2.13.19 数据库连接参数持久化表）
/// </summary>
public class SysParameter
{
    public int Id { get; set; }

    /// <summary>参数键（全局唯一，如 "db.server"、"db.port"、"db.password"）</summary>
    public string ParamKey { get; set; } = string.Empty;

    /// <summary>参数值（AES 加密后存储，读取时解密）</summary>
    public string? ParamValue { get; set; }

    /// <summary>参数分类</summary>
    public string Category { get; set; } = "Database";

    /// <summary>参数描述</summary>
    public string? Description { get; set; }

    /// <summary>是否加密</summary>
    public bool IsEncrypted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
