using Microsoft.Data.SqlClient;

var connStr = "Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;";

// 把 SQL 表重建成 EF 实体期望的样子
var syncScripts = new[]
{
// AppVersion: EF 期望 Version/ReleaseDate/FileName/FileSize/Md5/IsForceUpdate/IsLatest/MinCompatibleVersion/IsEnabled + BaseEntity(Id/CreatedAt/UpdatedAt/IsActive)
@"IF OBJECT_ID('dbo.AppVersion', 'U') IS NOT NULL
BEGIN
    -- 保存可能存在的现有数据
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AppVersion') AND name='VersionCode')
    BEGIN
        SELECT * INTO #tmp_AppVersion FROM dbo.AppVersion;
        DROP TABLE dbo.AppVersion;
    END
    ELSE
        DROP TABLE dbo.AppVersion;
END
IF OBJECT_ID('dbo.AppVersion', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppVersion] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Version] NVARCHAR(20) NOT NULL,
        [FileName] NVARCHAR(200) NULL,
        [FileSize] BIGINT NOT NULL DEFAULT ((0)),
        [ReleaseNotes] NVARCHAR(1000) NULL,
        [IsLatest] BIT NOT NULL DEFAULT ((0)),
        [IsEnabled] BIT NOT NULL DEFAULT ((1)),
        [IsForceUpdate] BIT NOT NULL DEFAULT ((0)),
        [MinCompatibleVersion] NVARCHAR(20) NULL,
        [Md5] NVARCHAR(64) NULL,
        [ReleaseDate] DATETIME NOT NULL DEFAULT (GETDATE()),
        [IsActive] BIT NOT NULL DEFAULT ((1)),
        [CreatedAt] DATETIME NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] DATETIME NULL,
        CONSTRAINT [PK_AppVersion] PRIMARY KEY ([Id])
    );
END
-- 如果之前有 #tmp 表则迁移数据
IF OBJECT_ID('tempdb..#tmp_AppVersion') IS NOT NULL
BEGIN
    SET IDENTITY_INSERT dbo.AppVersion ON;
    INSERT INTO dbo.AppVersion (Id, Version, FileName, FileSize, ReleaseNotes, IsLatest, IsEnabled, IsForceUpdate, MinCompatibleVersion, Md5, ReleaseDate, IsActive, CreatedAt, UpdatedAt)
    SELECT Id, VersionCode, NULL, 0, ReleaseNotes, IsActive, IsActive, IsMandatory, NULL, NULL, PublishedAt, IsActive, CreatedAt, UpdatedAt
    FROM #tmp_AppVersion;
    SET IDENTITY_INSERT dbo.AppVersion OFF;
    DROP TABLE #tmp_AppVersion;
END",

// SysIntegration: 删除重建（让 EF 实体列对齐）
// 先看 EF 期望列
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
        Console.WriteLine($"✅ ALTER/DROP/CREATE 块成功");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"❌ 失败: {ex.Message}");
    }
}

Console.WriteLine($"\n📊 重建完成: 成功 {success}, 失败 {failed}");

// 验证
Console.WriteLine("\n=== AppVersion 新结构 ===");
using var cmd1 = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='AppVersion' ORDER BY ORDINAL_POSITION", conn);
using var r1 = cmd1.ExecuteReader();
while (r1.Read()) Console.WriteLine($"  {r1.GetString(0),-25} {r1.GetString(1),-15} {r1.GetString(2)}");
r1.Close();