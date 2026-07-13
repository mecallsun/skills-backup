-- ============================================================
-- 金戈新材料 - PDA 水电抄表系统 数据库结构
-- 适用：SQL Server 2017 / 2019
-- 编码：UTF-8（保存时确保 SSMS 使用中文排序规则）
--
-- 数据库服务器：192.168.1.237
-- 数据库名：    WaterMeterDB
-- 数据库账号：  __DB_USER__ / __DB_PASSWORD__
--
-- 执行顺序：
--   1. 用 sa 登录 SSMS → 执行 00_创建数据库用户.sql（创建 __DB_USER__）
--   2. 重新连接 → 选 __DB_USER__ 登录 → 执行本脚本
--   3. 继续执行 02_Seed_Data.sql
-- ============================================================

-- 1. 创建数据库（若不存在）
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'WaterMeterDB')
BEGIN
    CREATE DATABASE WaterMeterDB
    COLLATE Chinese_PRC_CI_AS;
END;
GO
USE WaterMeterDB;
GO

-- ============================================================
-- 1. 宿舍档案表 Dorm
-- ============================================================
IF OBJECT_ID('dbo.Dorm', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dorm (
        DormId           INT            IDENTITY(1,1) NOT NULL,
        DormCode         NVARCHAR(32)   NOT NULL,        -- 宿舍编码（如 D-301）
        Building         NVARCHAR(32)   NULL,            -- 楼栋
        Floor            NVARCHAR(16)   NULL,            -- 楼层
        RoomNo           NVARCHAR(16)   NULL,            -- 房号
        DormAddress      NVARCHAR(128)  NULL,            -- 完整地址
        DormType         NVARCHAR(16)   NULL,            -- 宿舍类型：单人间/双人间
        HasColdMeter     BIT            NOT NULL DEFAULT 1,
        HasHotMeter      BIT            NOT NULL DEFAULT 1,
        HasElectricMeter BIT            NOT NULL DEFAULT 1,
        Barcode          NVARCHAR(64)   NULL,            -- 宿舍对应的条码/二维码内容
        Remark           NVARCHAR(256)  NULL,
        IsActive         BIT            NOT NULL DEFAULT 1,
        CreatedAt        DATETIME       NOT NULL DEFAULT GETDATE(),
        UpdatedAt        DATETIME       NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_Dorm PRIMARY KEY (DormId)
    );
    CREATE UNIQUE INDEX IX_Dorm_DormCode ON dbo.Dorm(DormCode);
    CREATE INDEX IX_Dorm_Barcode ON dbo.Dorm(Barcode) WHERE Barcode IS NOT NULL;
END;
GO

-- ============================================================
-- 2. 抄表记录主表 MeterRecord
-- ============================================================
IF OBJECT_ID('dbo.MeterRecord', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MeterRecord (
        RecordId         BIGINT         IDENTITY(1,1) NOT NULL,
        DormId           INT            NOT NULL,
        DormCode         NVARCHAR(32)   NOT NULL,        -- 冗余字段便于查询
        ReadMonth        CHAR(7)        NOT NULL,        -- 抄表年月 2026-07
        ColdMeter        DECIMAL(12,2)  NULL,            -- 冷水表读数
        HotMeter         DECIMAL(12,2)  NULL,            -- 热水表读数
        ElectricMeter    DECIMAL(12,2)  NULL,            -- 电表读数
        ColdUsage        DECIMAL(12,2)  NULL,            -- 本月冷水用量（上期自动算）
        HotUsage         DECIMAL(12,2)  NULL,
        ElectricUsage    DECIMAL(12,2)  NULL,
        Operator         NVARCHAR(32)   NOT NULL,        -- 操作人
        DeviceSn         NVARCHAR(64)   NOT NULL,        -- PDA 设备号
        ClientRecordId   NVARCHAR(64)   NOT NULL,        -- PDA 端幂等 ID
        ClientCreatedAt  DATETIME       NULL,            -- PDA 端创建时间
        ServerCreatedAt  DATETIME       NOT NULL DEFAULT GETDATE(),
        Status           TINYINT        NOT NULL DEFAULT 1, -- 1=正常 0=作废 2=复核
        Remark           NVARCHAR(256)  NULL,
        CONSTRAINT PK_MeterRecord PRIMARY KEY (RecordId),
        CONSTRAINT FK_MeterRecord_Dorm FOREIGN KEY (DormId) REFERENCES dbo.Dorm(DormId)
    );

    -- 核心索引：每宿舍每月只能有一条记录（防重复录入）
    CREATE UNIQUE INDEX IX_MeterRecord_DormMonth
        ON dbo.MeterRecord(DormCode, ReadMonth);

    -- 幂等索引：PDA 端同一 clientRecordId 不可重复
    CREATE UNIQUE INDEX IX_MeterRecord_ClientId
        ON dbo.MeterRecord(DeviceSn, ClientRecordId);

    -- 时间倒序索引（后台列表）
    CREATE INDEX IX_MeterRecord_ServerCreatedAt
        ON dbo.MeterRecord(ServerCreatedAt DESC);

    -- 多条件筛选索引
    CREATE INDEX IX_MeterRecord_ReadMonth_Operator
        ON dbo.MeterRecord(ReadMonth, Operator);
END;
GO

-- ============================================================
-- 3. 图片附件关联表 MeterImage
-- ============================================================
IF OBJECT_ID('dbo.MeterImage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MeterImage (
        ImageId          BIGINT         IDENTITY(1,1) NOT NULL,
        RecordId         BIGINT         NOT NULL,
        MeterType        NVARCHAR(16)   NOT NULL,        -- cold/hot/electric
        RelativePath     NVARCHAR(512)  NOT NULL,        -- /uploads/202607/D-301/xxx.jpg
        AbsolutePath     NVARCHAR(512)  NULL,            -- D:\MeterImages\...
        FileName         NVARCHAR(128)  NOT NULL,        -- 纯文件名
        FileSize         INT            NOT NULL DEFAULT 0, -- 字节
        FileHash         NVARCHAR(64)   NULL,            -- SHA256（可选）
        Width            INT            NULL,
        Height           INT            NULL,
        UploadedAt       DATETIME       NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_MeterImage PRIMARY KEY (ImageId),
        CONSTRAINT FK_MeterImage_Record FOREIGN KEY (RecordId) REFERENCES dbo.MeterRecord(RecordId) ON DELETE CASCADE
    );
    CREATE INDEX IX_MeterImage_RecordId ON dbo.MeterImage(RecordId);
    CREATE INDEX IX_MeterImage_Type ON dbo.MeterImage(MeterType);
END;
GO

-- ============================================================
-- 4. 用户表 SysUser / 角色表 SysRole / 用户角色关联 SysUserRole
-- ============================================================
IF OBJECT_ID('dbo.SysRole', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SysRole (
        RoleId           INT            IDENTITY(1,1) NOT NULL,
        RoleCode         NVARCHAR(32)   NOT NULL,        -- Admin / Operator / Viewer
        RoleName         NVARCHAR(64)   NOT NULL,
        Description      NVARCHAR(256)  NULL,
        IsActive         BIT            NOT NULL DEFAULT 1,
        CreatedAt        DATETIME       NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_SysRole PRIMARY KEY (RoleId),
        CONSTRAINT UQ_SysRole_Code UNIQUE (RoleCode)
    );
END;
GO

IF OBJECT_ID('dbo.SysUser', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SysUser (
        UserId           INT            IDENTITY(1,1) NOT NULL,
        Username         NVARCHAR(32)   NOT NULL,
        PasswordHash     NVARCHAR(256)  NOT NULL,        -- SHA256(Salt + Password)
        Salt             NVARCHAR(32)   NOT NULL,
        DisplayName      NVARCHAR(64)   NULL,
        Mobile           NVARCHAR(16)   NULL,
        Email            NVARCHAR(64)   NULL,
        IsActive         BIT            NOT NULL DEFAULT 1,
        IsLocked         BIT            NOT NULL DEFAULT 0,
        FailedLoginCount INT            NOT NULL DEFAULT 0,
        LastLoginAt      DATETIME       NULL,
        LastLoginIp      NVARCHAR(64)   NULL,
        CreatedAt        DATETIME       NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_SysUser PRIMARY KEY (UserId),
        CONSTRAINT UQ_SysUser_Username UNIQUE (Username)
    );
END;
GO

IF OBJECT_ID('dbo.SysUserRole', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SysUserRole (
        UserId           INT            NOT NULL,
        RoleId           INT            NOT NULL,
        CONSTRAINT PK_SysUserRole PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_SysUserRole_User FOREIGN KEY (UserId) REFERENCES dbo.SysUser(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_SysUserRole_Role FOREIGN KEY (RoleId) REFERENCES dbo.SysRole(RoleId) ON DELETE CASCADE
    );
END;
GO

-- ============================================================
-- 5. PDA 设备表 PdaDevice
-- ============================================================
IF OBJECT_ID('dbo.PdaDevice', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PdaDevice (
        DeviceId         INT            IDENTITY(1,1) NOT NULL,
        DeviceSn         NVARCHAR(64)   NOT NULL,
        DeviceModel      NVARCHAR(64)   NULL,
        BoundUserId      INT            NULL,            -- 绑定操作员
        LastLoginAt      DATETIME       NULL,
        LastLoginIp      NVARCHAR(64)   NULL,
        IsActive         BIT            NOT NULL DEFAULT 1,
        Remark           NVARCHAR(256)  NULL,
        CreatedAt        DATETIME       NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_PdaDevice PRIMARY KEY (DeviceId),
        CONSTRAINT UQ_PdaDevice_Sn UNIQUE (DeviceSn)
    );
END;
GO

-- ============================================================
-- 6. 系统配置表 SysConfig（存储路径、是否允许重复等）
-- ============================================================
IF OBJECT_ID('dbo.SysConfig', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SysConfig (
        ConfigKey        NVARCHAR(64)   NOT NULL,
        ConfigValue      NVARCHAR(MAX)  NOT NULL,
        ConfigGroup      NVARCHAR(32)   NULL,
        Description      NVARCHAR(256)  NULL,
        UpdatedAt        DATETIME       NOT NULL DEFAULT GETDATE(),
        UpdatedBy        NVARCHAR(32)   NULL,
        CONSTRAINT PK_SysConfig PRIMARY KEY (ConfigKey)
    );
END;
GO

-- ============================================================
-- 7. 操作日志表 SysOpLog
-- ============================================================
IF OBJECT_ID('dbo.SysOpLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SysOpLog (
        LogId            BIGINT         IDENTITY(1,1) NOT NULL,
        UserId           INT            NULL,
        Username         NVARCHAR(32)   NULL,
        Action           NVARCHAR(64)   NOT NULL,
        Target           NVARCHAR(256)  NULL,
        Detail           NVARCHAR(MAX)  NULL,
        Ip               NVARCHAR(64)   NULL,
        CreatedAt        DATETIME       NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_SysOpLog PRIMARY KEY (LogId)
    );
    CREATE INDEX IX_SysOpLog_CreatedAt ON dbo.SysOpLog(CreatedAt DESC);
END;
GO

-- ============================================================
-- 8. 视图 v_MeterRecordDetail（Web 端列表常用）
-- ============================================================
IF OBJECT_ID('dbo.v_MeterRecordDetail', 'V') IS NULL
BEGIN
    EXEC('CREATE VIEW dbo.v_MeterRecordDetail AS
    SELECT
        r.RecordId, r.DormId, r.DormCode, r.ReadMonth,
        r.ColdMeter, r.HotMeter, r.ElectricMeter,
        r.ColdUsage, r.HotUsage, r.ElectricUsage,
        r.Operator, r.DeviceSn, r.Status,
        r.ClientCreatedAt, r.ServerCreatedAt,
        d.Building, d.Floor, d.RoomNo, d.DormAddress,
        d.HasColdMeter, d.HasHotMeter, d.HasElectricMeter
    FROM dbo.MeterRecord r
    INNER JOIN dbo.Dorm d ON d.DormId = r.DormId;');
END;
GO

PRINT '✅ 数据库结构创建完成';
GO