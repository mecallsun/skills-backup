using Microsoft.Data.SqlClient;

var connStr = "Server=172.16.0.100;Database=WaterMeterDB;UID=user;PWD=1234;TrustServerCertificate=True;";

// 同步脚本：让 SQL 表的列与 EF 实体一致
// EF 实体 AppVersion 期望：FileName/FileSize/IsEnabled/IsForceUpdate/IsLatest/Md5/MinCompatibleVersion/ReleaseDate/Version
// SQL 表当前：VersionCode/VersionName/Platform/DownloadUrl/IsMandatory/PublishedAt/PublishedBy
var syncScripts = new[]
{
// AppVersion: EF 期望字段（添加 Version, ReleaseDate, FileName, FileSize, Md5, IsForceUpdate, IsLatest, MinCompatibleVersion, IsEnabled）
@"-- AppVersion 添加 EF 期望的列
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='Version')
    ALTER TABLE [dbo].[AppVersion] ADD [Version] NVARCHAR(32) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='ReleaseDate')
    ALTER TABLE [dbo].[AppVersion] ADD [ReleaseDate] DATETIME NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='FileName')
    ALTER TABLE [dbo].[AppVersion] ADD [FileName] NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='FileSize')
    ALTER TABLE [dbo].[AppVersion] ADD [FileSize] BIGINT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='Md5')
    ALTER TABLE [dbo].[AppVersion] ADD [Md5] NVARCHAR(64) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='IsForceUpdate')
    ALTER TABLE [dbo].[AppVersion] ADD [IsForceUpdate] BIT NOT NULL DEFAULT ((0));
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='IsLatest')
    ALTER TABLE [dbo].[AppVersion] ADD [IsLatest] BIT NOT NULL DEFAULT ((0));
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='MinCompatibleVersion')
    ALTER TABLE [dbo].[AppVersion] ADD [MinCompatibleVersion] NVARCHAR(32) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='IsEnabled')
    ALTER TABLE [dbo].[AppVersion] ADD [IsEnabled] BIT NOT NULL DEFAULT ((1));",

// SysUserSecurityQuestion: EF 期望 IsActive 列
@"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.SysUserSecurityQuestion') AND name='IsActive')
    ALTER TABLE [dbo].[SysUserSecurityQuestion] ADD [IsActive] BIT NOT NULL DEFAULT ((1));",

// SysIntegration: EF 实体可能期望的字段（查看）
@"SELECT 1",

// SysParameter: EF 期望字段（保持现状）
@"SELECT 1",

// SysSystemIntegration: 同上
@"SELECT 1",
};

using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine($"✅ 已连接 {conn.DataSource}/{conn.Database}");

int success = 0, failed = 0;
foreach (var ddl in syncScripts)
{
    try
    {
        using var cmd = new SqlCommand(ddl, conn);
        cmd.CommandTimeout = 30;
        cmd.ExecuteNonQuery();
        success++;
        Console.WriteLine($"✅ ALTER 块成功");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"❌ 失败: {ex.Message}");
    }
}

Console.WriteLine($"\n📊 同步完成: 成功 {success}, 失败 {failed}");

// 验证表结构
Console.WriteLine("\n=== AppVersion 列清单（EF 期望）===");
using var cmd1 = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='AppVersion' ORDER BY ORDINAL_POSITION", conn);
using var r1 = cmd1.ExecuteReader();
while (r1.Read()) Console.WriteLine($"  {r1.GetString(0)}");
r1.Close();

Console.WriteLine("\n=== SysUserSecurityQuestion 列清单（EF 期望）===");
using var cmd2 = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SysUserSecurityQuestion' ORDER BY ORDINAL_POSITION", conn);
using var r2 = cmd2.ExecuteReader();
while (r2.Read()) Console.WriteLine($"  {r2.GetString(0)}");