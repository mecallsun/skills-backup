using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";

// C# 类型 → SQL Server 类型映射
var typeMap = new Dictionary<string, string>
{
    ["int"] = "INT",
    ["long"] = "BIGINT",
    ["bool"] = "BIT",
    ["DateTime"] = "DATETIME",
    ["DateOnly"] = "DATE",
    ["decimal"] = "DECIMAL(18,4)",
    ["double"] = "FLOAT",
    ["float"] = "REAL",
    ["Guid"] = "UNIQUEIDENTIFIER",
    ["string"] = "NVARCHAR(255)",
    ["byte"] = "TINYINT",
    ["short"] = "SMALLINT",
};

// EF 实体与 SQL 表的对比清单
// 列出每个表缺失的列（手工维护）
var addColumns = new (string Table, string Col, string SqlType, bool Nullable)[]
{
    // DormBilling: EF 期望 ColdUsage/HotUsage/ElectricityUsage/DormId/ElectricityAmount
    ("DormBilling", "ColdUsage", "DECIMAL(12,2)", false),
    ("DormBilling", "HotUsage", "DECIMAL(12,2)", false),
    ("DormBilling", "ElectricityUsage", "DECIMAL(12,2)", false),
    ("DormBilling", "DormId", "INT", false),
    ("DormBilling", "ElectricityAmount", "DECIMAL(12,2)", false),

    // MeterRecord: 大量字段
    ("MeterRecord", "PreviousColdReading", "DECIMAL(12,2)", true),
    ("MeterRecord", "PreviousHotReading", "DECIMAL(12,2)", true),
    ("MeterRecord", "PreviousElectricReading", "DECIMAL(12,2)", true),
    ("MeterRecord", "ColdUsage", "DECIMAL(12,2)", true),
    ("MeterRecord", "HotUsage", "DECIMAL(12,2)", true),
    ("MeterRecord", "ElectricUsage", "DECIMAL(12,2)", true),
    ("MeterRecord", "Operator", "NVARCHAR(64)", true),
    ("MeterRecord", "DeviceSn", "NVARCHAR(128)", true),
    ("MeterRecord", "ClientRecordId", "NVARCHAR(128)", true),
    ("MeterRecord", "ClientCreatedAt", "DATETIME", true),
    ("MeterRecord", "ServerCreatedAt", "DATETIME", true),
    ("MeterRecord", "Status", "TINYINT", false),
    ("MeterRecord", "ReadDate", "DATE", true),
    ("MeterRecord", "ReadMode", "TINYINT", false),
    ("MeterRecord", "CorrectionReason", "NVARCHAR(500)", true),
    ("MeterRecord", "CorrectedBy", "NVARCHAR(64)", true),
    ("MeterRecord", "CorrectedAt", "DATETIME", true),
    ("MeterRecord", "ConfirmedAt", "DATETIME", true),

    // SysEmployee: EmployeeCode 可能不存在
    ("SysEmployee", "EmployeeCode", "NVARCHAR(64)", true),

    // SysUserFilterCache: 可能缺 UpdatedAt
    ("SysUserFilterCache", "UpdatedAt", "DATETIME", false),

    // SysOpLog: 可能缺字段
    ("SysOpLog", "Action", "NVARCHAR(64)", true),
    ("SysOpLog", "EntityName", "NVARCHAR(128)", true),
    ("SysOpLog", "EntityId", "NVARCHAR(64)", true),

    // PdaDevice: 检查
    ("PdaDevice", "DeviceCode", "NVARCHAR(64)", true),
    ("PdaDevice", "LastHeartbeatAt", "DATETIME", true),

    // BillingStandard: 检查
    ("BillingStandard", "StandardName", "NVARCHAR(128)", true),
    ("BillingStandard", "WaterPrice", "DECIMAL(12,4)", true),
    ("BillingStandard", "HotWaterPrice", "DECIMAL(12,4)", true),
    ("BillingStandard", "ElectricityPrice", "DECIMAL(12,4)", true),

    // SysIntegration: EF 期望的字段
    ("SysIntegration", "DisplayName", "NVARCHAR(128)", true),
    ("SysIntegration", "ConfigJson", "NVARCHAR(MAX)", true),
    ("SysIntegration", "Description", "NVARCHAR(500)", true),

    // MeterImage: 检查
    ("MeterImage", "ImagePath", "NVARCHAR(512)", true),
    ("MeterImage", "CapturedAt", "DATETIME", true),

    // SysConfig: 可能有 Key
    ("SysConfig", "ConfigKey", "NVARCHAR(64)", true),
    ("SysConfig", "ConfigValue", "NVARCHAR(MAX)", true),
    ("SysConfig", "Description", "NVARCHAR(500)", true),
};

using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine($"✅ 已连接 {conn.DataSource}/{conn.Database}");

int success = 0, skipped = 0, failed = 0;
foreach (var (table, col, sqlType, nullable) in addColumns)
{
    // 检查列是否存在
    using var cmdCheck = new SqlCommand($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@t AND COLUMN_NAME=@c", conn);
    cmdCheck.Parameters.AddWithValue("@t", table);
    cmdCheck.Parameters.AddWithValue("@c", col);
    var exists = (int)cmdCheck.ExecuteScalar() > 0;
    if (exists)
    {
        skipped++;
        continue;
    }

    var nullClause = nullable ? "NULL" : "NOT NULL DEFAULT (0)";
    var sql = $"ALTER TABLE [dbo].[{table}] ADD [{col}] {sqlType} {nullClause}";
    try
    {
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✅ {table}.{col} ({sqlType})");
        success++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {table}.{col}: {ex.Message}");
        failed++;
    }
}

Console.WriteLine($"\n📊 同步完成: 新增 {success}, 已存在 {skipped}, 失败 {failed}");