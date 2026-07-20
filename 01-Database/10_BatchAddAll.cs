using Microsoft.Data.SqlClient;
var connStr = "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;";

// 列规格：(表, 列, 类型, nullable)
var cols = new (string Table, string Col, string SqlType, bool Nullable)[]
{
    // DormBooking 缺失列
    ("DormBooking", "AttendanceTypeId", "INT", true),
    ("DormBooking", "BedNo", "INT", true),
    ("DormBooking", "MoveFromDormCode", "NVARCHAR(64)", true),
    ("DormBooking", "ActualCheckInDate", "DATE", true),
    ("DormBooking", "ActualCheckOutDate", "DATE", true),
    ("DormBooking", "CancellationReason", "NVARCHAR(500)", true),
    ("DormBooking", "CheckInOperator", "NVARCHAR(64)", true),
    ("DormBooking", "CheckOutOperator", "NVARCHAR(64)", true),

    // BillingStandard 缺失列
    ("BillingStandard", "StandardName", "NVARCHAR(128)", false),
    ("BillingStandard", "ApplicableType", "NVARCHAR(32)", false),
    ("BillingStandard", "HotWaterUnitPrice", "DECIMAL(12,4)", false),
    ("BillingStandard", "ColdWaterUnitPrice", "DECIMAL(12,4)", false),
    ("BillingStandard", "ElectricUnitPrice", "DECIMAL(12,4)", false),
    ("BillingStandard", "EffectiveFrom", "DATE", true),
    ("BillingStandard", "EffectiveTo", "DATE", true),

    // SysUserFilterCache: EF 期望 ModuleName（不是 Module）
    ("SysUserFilterCache", "ModuleName", "NVARCHAR(64)", false),
    ("SysUserFilterCache", "FilterJson", "NVARCHAR(MAX)", false),
    ("SysUserFilterCache", "CreatedAt", "DATETIME", false),

    // SysIntegration: EF 期望 SystemCode/SystemName（不是 Code/Name）
    ("SysIntegration", "SystemCode", "NVARCHAR(64)", false),
    ("SysIntegration", "SystemName", "NVARCHAR(128)", false),
    ("SysIntegration", "ServerAddress", "NVARCHAR(512)", true),
    ("SysIntegration", "Account", "NVARCHAR(128)", true),
    ("SysIntegration", "Password", "NVARCHAR(256)", true),
    ("SysIntegration", "ApiKey", "NVARCHAR(256)", true),
    ("SysIntegration", "IsEnabled", "BIT", false),
    ("SysIntegration", "SyncIntervalMinutes", "INT", false),
    ("SysIntegration", "LastSyncTime", "DATETIME", true),
    ("SysIntegration", "LastSyncResult", "BIT", true),
    ("SysIntegration", "LastSyncMessage", "NVARCHAR(500)", true),
    ("SysIntegration", "LastTestTime", "DATETIME", true),
    ("SysIntegration", "LastTestResult", "BIT", true),
    ("SysIntegration", "ExtraConfigJson", "NVARCHAR(MAX)", true),

    // SysOpLog: EF 期望 Action/Target/Detail/Ip（不是 EntityName/EntityId）
    ("SysOpLog", "Action", "NVARCHAR(64)", false),
    ("SysOpLog", "Target", "NVARCHAR(128)", true),
    ("SysOpLog", "Detail", "NVARCHAR(MAX)", true),
    ("SysOpLog", "Ip", "NVARCHAR(64)", true),
};

using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine($"✅ 已连接 {conn.DataSource}/{conn.Database}");

int success = 0, skipped = 0, failed = 0;
foreach (var (table, col, sqlType, nullable) in cols)
{
    using var cmdCheck = new SqlCommand($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@t AND COLUMN_NAME=@c", conn);
    cmdCheck.Parameters.AddWithValue("@t", table);
    cmdCheck.Parameters.AddWithValue("@c", col);
    if ((int)cmdCheck.ExecuteScalar() > 0) { skipped++; continue; }

    var def = nullable ? "NULL" : "NOT NULL DEFAULT (0)";
    // nvarchar 用 ''
    if (sqlType.StartsWith("NVARCHAR") || sqlType.StartsWith("VARCHAR")) {
        def = nullable ? "NULL" : "NOT NULL DEFAULT ('')";
    }
    if (sqlType == "BIT") {
        def = nullable ? "NULL DEFAULT ((0))" : "NOT NULL DEFAULT ((0))";
    }
    var sql = $"ALTER TABLE [dbo].[{table}] ADD [{col}] {sqlType} {def}";
    try {
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✅ {table}.{col}");
        success++;
    }
    catch (Exception ex) {
        Console.WriteLine($"❌ {table}.{col}: {ex.Message}");
        failed++;
    }
}

Console.WriteLine($"\n📊 完成: 新增 {success}, 跳过 {skipped}, 失败 {failed}");