using System.Text.Json.Serialization;

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

    /// <summary>提供程序：SqlServer / Sqlite</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "SqlServer";

    /// <summary>Sqlite 文件路径（当 Provider=Sqlite 时使用）</summary>
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
