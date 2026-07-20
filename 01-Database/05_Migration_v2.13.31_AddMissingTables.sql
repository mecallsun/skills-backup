-- ============================================================
-- 金戈宿舍管理系统 - 数据库补表迁移脚本 (v2.13.31)
--
-- 用途：补充 SQL Server 缺失的 8 张业务表
--   - DormBilling（宿舍账单）
--   - EmployeeBilling（员工分摊账单）
--   - SysUserFilterCache（用户筛选条件云端缓存）
--   - SysUserSecurityQuestion（密码找回安全问题）
--   - AppVersion（PDA 版本管理）
--   - SysIntegration（系统集成 HR/K3ERP 配置）
--   - SysParameter（数据库连接持久化）
--   - SysSystemIntegration（系统集成占位兼容）
--
-- 来源：init_schema.sql（v2.13.24 业务深度补表段）
--
-- 执行方式：
--   1. 用 sa 登录 SSMS → 选 WaterMeterDB → 执行本脚本
--   2. 重复执行幂等（IF OBJECT_ID IS NULL 保护）
--
-- 风险：低（仅添加表，不修改现有数据）
-- ============================================================

USE WaterMeterDB;
GO

PRINT N'🚀 开始补表迁移 (v2.13.31)...';

-- ============================================================
-- 1. SysUserSecurityQuestion（密码找回安全问题 — v2.13.26）
-- ============================================================
IF OBJECT_ID('dbo.SysUserSecurityQuestion', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SysUserSecurityQuestion] (
        [Id]            INT            IDENTITY(1,1) NOT NULL,
        [UserId]        INT            NOT NULL,
        [QuestionIndex] INT            NOT NULL,
        [Question]      NVARCHAR(200)  NOT NULL,
        [AnswerHash]    NVARCHAR(500)  NOT NULL,    -- AES-256 加密存储
        [CreatedAt]     DATETIME       NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt]     DATETIME       NULL,
        CONSTRAINT [PK_SysUserSecurityQuestion] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_SQ_User_Index] UNIQUE ([UserId], [QuestionIndex]),
        CONSTRAINT [FK_SQ_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SysUser]([UserId]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_SQ_User] ON [dbo].[SysUserSecurityQuestion]([UserId]);
    PRINT N'✅ 已创建 SysUserSecurityQuestion';
END
ELSE
    PRINT N'⏭ SysUserSecurityQuestion 已存在，跳过';
GO

-- ============================================================
-- 2. DormBilling（宿舍账单）
-- ============================================================
IF OBJECT_ID('dbo.DormBilling', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DormBilling] (
        [Id]               INT            IDENTITY(1,1) NOT NULL,
        [BillingMonth]     CHAR(7)        NOT NULL,  -- yyyy-MM
        [DormCode]         NVARCHAR(64)   NOT NULL,
        [BuildingName]     NVARCHAR(50)   NULL,
        [AddressText]      NVARCHAR(200)  NULL,
        [ResidentCount]    INT            NOT NULL DEFAULT ((0)),
        [ColdAmount]       DECIMAL(12,2)  NOT NULL DEFAULT ((0)),
        [HotAmount]        DECIMAL(12,2)  NOT NULL DEFAULT ((0)),
        [ElectricAmount]   DECIMAL(12,2)  NOT NULL DEFAULT ((0)),
        [TotalAmount]      DECIMAL(12,2)  NOT NULL DEFAULT ((0)),
        [BillingStandardId] INT           NULL,
        [IsPublished]      BIT            NOT NULL DEFAULT ((0)),
        [GeneratedBy]      NVARCHAR(64)   NULL,
        [GeneratedAt]      DATETIME       NOT NULL DEFAULT (GETDATE()),
        [Remark]           NVARCHAR(500)  NULL,
        [CreatedAt]        DATETIME       NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt]        DATETIME       NULL,
        CONSTRAINT [PK_DormBilling] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_DormBilling_MonthDorm] UNIQUE ([BillingMonth], [DormCode])
    );
    CREATE INDEX [IX_DormBilling_Month] ON [dbo].[DormBilling]([BillingMonth] DESC);
    CREATE INDEX [IX_DormBilling_Dorm] ON [dbo].[DormBilling]([DormCode]);
    PRINT N'✅ 已创建 DormBilling';
END
ELSE
    PRINT N'⏭ DormBilling 已存在，跳过';
GO

-- ============================================================
-- 3. EmployeeBilling（员工分摊账单）
-- ============================================================
IF OBJECT_ID('dbo.EmployeeBilling', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EmployeeBilling] (
        [Id]               INT            IDENTITY(1,1) NOT NULL,
        [BillingMonth]     CHAR(7)        NOT NULL,
        [EmployeeId]       INT            NOT NULL,
        [EmployeeCode]     NVARCHAR(64)   NOT NULL,
        [EmployeeName]     NVARCHAR(128)  NOT NULL,
        [Department]       NVARCHAR(128)  NULL,
        [DormBillId]       INT            NULL,
        [DormCode]         NVARCHAR(64)   NOT NULL,
        [Days]             INT            NOT NULL DEFAULT ((0)),
        [TotalShareAmount] DECIMAL(12,2)  NOT NULL DEFAULT ((0)),
        [IsPublished]      BIT            NOT NULL DEFAULT ((0)),
        [GeneratedAt]      DATETIME       NOT NULL DEFAULT (GETDATE()),
        [Remark]           NVARCHAR(500)  NULL,
        [CreatedAt]        DATETIME       NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt]        DATETIME       NULL,
        CONSTRAINT [PK_EmployeeBilling] PRIMARY KEY ([Id]),
        CONSTRAINT [IX_EmployeeBilling_MonthEmp] UNIQUE ([BillingMonth], [EmployeeId])
    );
    CREATE INDEX [IX_EmployeeBilling_DormBillId] ON [dbo].[EmployeeBilling]([DormBillId]);
    CREATE INDEX [IX_EmployeeBilling_Emp] ON [dbo].[EmployeeBilling]([EmployeeId]);
    PRINT N'✅ 已创建 EmployeeBilling';
END
ELSE
    PRINT N'⏭ EmployeeBilling 已存在，跳过';
GO

-- ============================================================
-- 4. SysUserFilterCache（用户筛选条件云端缓存 — v2.13.12）
-- ============================================================
IF OBJECT_ID('dbo.SysUserFilterCache', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SysUserFilterCache] (
        [Id]         INT            IDENTITY(1,1) NOT NULL,
        [UserId]     INT            NOT NULL,
        [Module]     NVARCHAR(64)   NOT NULL,
        [FilterJson] NVARCHAR(MAX)  NOT NULL,
        [UpdatedAt]  DATETIME       NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_SysUserFilterCache] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_SysUserFilterCache_UserModule] UNIQUE ([UserId], [Module])
    );
    CREATE INDEX [IX_SysUserFilterCache_UserId] ON [dbo].[SysUserFilterCache]([UserId]);
    PRINT N'✅ 已创建 SysUserFilterCache';
END
ELSE
    PRINT N'⏭ SysUserFilterCache 已存在，跳过';
GO

-- ============================================================
-- 5. AppVersion（PDA 版本管理）
-- ============================================================
IF OBJECT_ID('dbo.AppVersion', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppVersion] (
        [Id]            INT            IDENTITY(1,1) NOT NULL,
        [VersionCode]   NVARCHAR(32)   NOT NULL,
        [VersionName]   NVARCHAR(64)   NOT NULL,
        [Platform]      NVARCHAR(32)   NOT NULL DEFAULT (N'PDA'),
        [DownloadUrl]   NVARCHAR(512)  NOT NULL,
        [ReleaseNotes]  NVARCHAR(MAX)  NULL,
        [IsMandatory]   BIT            NOT NULL DEFAULT ((0)),
        [IsActive]      BIT            NOT NULL DEFAULT ((1)),
        [PublishedAt]   DATETIME       NOT NULL DEFAULT (GETDATE()),
        [PublishedBy]   NVARCHAR(64)   NULL,
        [CreatedAt]     DATETIME       NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt]     DATETIME       NULL,
        CONSTRAINT [PK_AppVersion] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_AppVersion_Code] UNIQUE ([VersionCode])
    );
    PRINT N'✅ 已创建 AppVersion';
END
ELSE
    PRINT N'⏭ AppVersion 已存在，跳过';
GO

-- ============================================================
-- 6. SysIntegration（系统集成 HR/K3ERP 配置）
-- ============================================================
IF OBJECT_ID('dbo.SysIntegration', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SysIntegration] (
        [Id]              INT            IDENTITY(1,1) NOT NULL,
        [Code]            NVARCHAR(64)   NOT NULL,
        [Name]            NVARCHAR(128)  NOT NULL,
        [IntegrationType] NVARCHAR(32)   NOT NULL,
        [Endpoint]        NVARCHAR(512)  NOT NULL,
        [AuthJson]        NVARCHAR(MAX)  NULL,
        [IsActive]        BIT            NOT NULL DEFAULT ((1)),
        [LastSyncAt]      DATETIME       NULL,
        [LastSyncStatus]  NVARCHAR(32)   NULL,
        [Remark]          NVARCHAR(500)  NULL,
        [CreatedAt]       DATETIME       NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt]       DATETIME       NULL,
        CONSTRAINT [PK_SysIntegration] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_SysIntegration_Code] UNIQUE ([Code])
    );
    PRINT N'✅ 已创建 SysIntegration';
END
ELSE
    PRINT N'⏭ SysIntegration 已存在，跳过';
GO

-- ============================================================
-- 7. SysParameter（数据库连接持久化 — v2.13.19 双 UI 同步）
-- ============================================================
IF OBJECT_ID('dbo.SysParameter', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SysParameter] (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [ParamKey]    NVARCHAR(64)   NOT NULL,
        [ParamValue]  NVARCHAR(MAX)  NULL,
        [Category]    NVARCHAR(64)   NOT NULL DEFAULT (N'DB'),
        [Description] NVARCHAR(500)  NULL,
        [IsEncrypted] BIT            NOT NULL DEFAULT ((0)),
        [UpdatedBy]   NVARCHAR(64)   NULL,
        [UpdatedAt]   DATETIME       NOT NULL DEFAULT (GETDATE()),
        [CreatedAt]   DATETIME       NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_SysParameter] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_SysParameter_Key] UNIQUE ([ParamKey])
    );
    CREATE INDEX [IX_SysParameter_Category] ON [dbo].[SysParameter]([Category]);
    PRINT N'✅ 已创建 SysParameter';
END
ELSE
    PRINT N'⏭ SysParameter 已存在，跳过';
GO

-- ============================================================
-- 8. SysSystemIntegration（系统集成占位兼容表）
-- ============================================================
IF OBJECT_ID('dbo.SysSystemIntegration', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SysSystemIntegration] (
        [Id]         INT            IDENTITY(1,1) NOT NULL,
        [Code]       NVARCHAR(64)   NOT NULL,
        [Name]       NVARCHAR(128)  NOT NULL,
        [IsActive]   BIT            NOT NULL DEFAULT ((1)),
        [CreatedAt]  DATETIME       NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt]  DATETIME       NULL,
        CONSTRAINT [PK_SysSystemIntegration] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_SysSystemIntegration_Code] UNIQUE ([Code])
    );
    PRINT N'✅ 已创建 SysSystemIntegration';
END
ELSE
    PRINT N'⏭ SysSystemIntegration 已存在，跳过';
GO

-- ============================================================
-- 验证补表结果
-- ============================================================
PRINT N'';
PRINT N'📊 补表后数据库表清单：';
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;

PRINT N'';
PRINT N'✅ v2.13.31 补表迁移完成！';
PRINT N'   已补齐：SysUserSecurityQuestion / DormBilling / EmployeeBilling /';
PRINT N'          SysUserFilterCache / AppVersion / SysIntegration / SysParameter / SysSystemIntegration';
GO