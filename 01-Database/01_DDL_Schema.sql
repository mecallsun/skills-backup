-- ============================================================
-- 金戈新材料 - PDA 水电抄表系统 数据库结构
-- 适用：SQL Server 2017 / 2019
-- 编码：UTF-8（保存时确保 SSMS 使用中文排序规则）
--
-- v2.13.145 默认参数更新
-- 数据库服务器：172.16.0.100
-- 数据库名：    WaterMeterDB
-- 数据库账号：  user / 1234（SQL 保留关键字，必须 [user] 转义）
--
-- 执行顺序：
--   1. 用 sa 登录 SSMS → 执行 00_创建数据库用户.sql（创建 [user]）
--   2. 重新连接 → 选 user 登录 → 执行本脚本
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

-- ============================================================
-- 9. 人员清单 SysEmployee（v2.13.57 补充：原脚本遗漏 DormBooking/SysEmployee 表）
-- ============================================================
IF OBJECT_ID('dbo.SysEmployee', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SysEmployee (
        EmployeeId         INT            IDENTITY(1,1) NOT NULL,
        EmployeeCode       NVARCHAR(20)   NOT NULL,           -- 工号（与 DormBooking.EmployeeCode 同源；人员清单为唯一真源）
        RealName           NVARCHAR(50)   NOT NULL,           -- 姓名
        DepartmentId       INT            NOT NULL DEFAULT 1,
        Department         NVARCHAR(50)   NULL,               -- 部门冗余
        EmployeeTypeId     INT            NOT NULL DEFAULT 1,
        EmployeeType       NVARCHAR(64)   NULL,               -- 员工类型冗余
        TeamId             INT            NOT NULL DEFAULT 1,
        Gender             TINYINT        NOT NULL DEFAULT 1, -- 1=男 2=女
        Phone              NVARCHAR(20)   NULL,
        EmploymentStatusId INT            NOT NULL DEFAULT 1, -- 1=在职 2=待入职 3=已离职
        Status             TINYINT        NOT NULL DEFAULT 1, -- 在职状态（冗余，已弃用）
        HireDate           DATE           NULL,
        LeaveDate          DATE           NULL,
        BedNo              INT            NULL,
        DormCode           NVARCHAR(20)   NULL,               -- 当前宿舍（入住人数单一数据源）
        Team               NVARCHAR(20)   NULL,
        ResidenceStatusId  INT            NOT NULL DEFAULT 2, -- 1=已住宿 2=未住宿 3=待入住
        AttendanceTypeId   INT            NULL,
        Remark             NVARCHAR(500)  NULL,
        IsActive           BIT            NOT NULL DEFAULT 1,
        CreatedAt          DATETIME       NOT NULL DEFAULT GETDATE(),
        UpdatedAt          DATETIME       NULL,
        CONSTRAINT PK_SysEmployee PRIMARY KEY (EmployeeId)
    );
    CREATE UNIQUE INDEX IX_SysEmployee_EmployeeCode ON dbo.SysEmployee(EmployeeCode);
    CREATE INDEX IX_SysEmployee_EmploymentStatusId ON dbo.SysEmployee(EmploymentStatusId);
    CREATE INDEX IX_SysEmployee_ResidenceStatusId ON dbo.SysEmployee(ResidenceStatusId);
    CREATE INDEX IX_SysEmployee_DormCode ON dbo.SysEmployee(DormCode);
END;
GO

-- ============================================================
-- 10. 办理登记表 DormBooking（v2.13.57 补充：核心业务表 + FK 关联人员清单）
-- ============================================================
IF OBJECT_ID('dbo.DormBooking', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DormBooking (
        BookingId           INT            IDENTITY(1,1) NOT NULL,
        EmployeeId          INT            NOT NULL,           -- FK → SysEmployee.EmployeeId（人员清单为唯一真源）
        EmployeeCode        NVARCHAR(64)   NOT NULL,           -- 冗余：登记时存档人员清单的工号
        EmployeeName        NVARCHAR(128)  NOT NULL,           -- 冗余：登记时存档人员清单的姓名
        Phone               NVARCHAR(32)   NULL,               -- 冗余：登记时存档人员清单的手机号
        Department          NVARCHAR(128)  NULL,               -- 冗余：登记时存档人员清单的部门
        AttendanceTypeId    TINYINT        NULL,               -- 冗余：登记时存档人员清单的考勤班次
        BedNo               INT            NULL,               -- v2.13.24 业务深度：床位号
        MoveFromDormCode    NVARCHAR(32)   NULL,               -- v2.13.24 业务深度：调宿来源房号
        ActualCheckInDate   DATE           NULL,               -- v2.13.24 业务深度：实际入住日期
        ActualCheckOutDate  DATE           NULL,               -- v2.13.24 业务深度：实际退房日期
        DormCode            NVARCHAR(64)   NOT NULL,           -- FK → Dorm.DormCode（宿舍代码）
        BookingType         TINYINT        NOT NULL,           -- 1=入住 2=退房
        BookingDate         DATE           NOT NULL,           -- 入退日期
        Status              TINYINT        NOT NULL,           -- 1=预约 2=在宿 3=已退房 4=已取消
        Reason              NVARCHAR(512)  NULL,
        CancellationReason  NVARCHAR(512)  NULL,               -- v2.13.24 业务深度：取消原因
        Remark              NVARCHAR(1024) NULL,
        RegistrationDate    DATETIME       NOT NULL DEFAULT GETDATE(),
        Registrar           NVARCHAR(64)   NOT NULL,
        CheckInOperator     NVARCHAR(64)   NULL,               -- v2.13.24 业务深度：入住确认操作人
        CheckOutOperator    NVARCHAR(64)   NULL,               -- v2.13.24 业务深度：退房确认操作人
        IsActive            BIT            NOT NULL DEFAULT 1,
        CreatedAt           DATETIME       NOT NULL DEFAULT GETDATE(),
        UpdatedAt           DATETIME       NULL,
        CONSTRAINT PK_DormBooking PRIMARY KEY (BookingId),
        -- v2.13.57 P0 修复：FK 关联约束确保 DormBooking.EmployeeId 必须在 SysEmployee 中存在
        CONSTRAINT FK_DormBooking_Employee FOREIGN KEY (EmployeeId)
            REFERENCES dbo.SysEmployee(EmployeeId),
        CONSTRAINT FK_DormBooking_Dorm FOREIGN KEY (DormCode)
            REFERENCES dbo.Dorm(DormCode)
    );
    CREATE INDEX IX_DormBooking_EmployeeId_BookingDate ON dbo.DormBooking(EmployeeId, BookingDate);
    CREATE INDEX IX_DormBooking_DormCode_BookingDate ON dbo.DormBooking(DormCode, BookingDate);
    CREATE INDEX IX_DormBooking_Status_BookingDate ON dbo.DormBooking(Status, BookingDate);
    CREATE INDEX IX_DormBooking_EmployeeId ON dbo.DormBooking(EmployeeId);
END;
GO

-- ============================================================
-- v2.13.120 设备档案（DormMeter）— 与 Dorm 1:1 关系
-- ============================================================
IF OBJECT_ID('dbo.DormMeter', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DormMeter (
        DormMeterId        INT            IDENTITY(1,1) NOT NULL,
        DormId             INT            NOT NULL,           -- 房号 FK → Dorm.DormId (UNIQUE 1:1)
        ElectricMeterId    NVARCHAR(64)   NULL,               -- 电表 ID/编号（现场标识）
        ColdWaterMeterId   NVARCHAR(64)   NULL,               -- 冷水表 ID/编号
        HotWaterMeterId    NVARCHAR(64)   NULL,               -- 热水表 ID/编号
        Remark             NVARCHAR(500)  NULL,
        IsActive           BIT            NOT NULL DEFAULT 1,
        CreatedAt          DATETIME       NOT NULL DEFAULT GETDATE(),
        UpdatedAt          DATETIME       NULL DEFAULT GETDATE(),
        CONSTRAINT PK_DormMeter PRIMARY KEY CLUSTERED (DormMeterId),
        CONSTRAINT FK_DormMeter_Dorm FOREIGN KEY (DormId)
            REFERENCES dbo.Dorm(DormId) ON DELETE CASCADE,
        CONSTRAINT UX_DormMeter_DormId UNIQUE (DormId)        -- 1:1 唯一约束
    );
    PRINT '✓ dbo.DormMeter 表已创建（v2.13.120）';
END;
GO

-- v2.13.168 设备ID 过滤唯一索引（3 列各建，仅约束非空值 —— 同列内唯一）
-- 跨列全局唯一由 Service 层 CheckDeviceIdUniqueAsync 保证；此处为「同列重复」DB 兜底
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DormMeter_ElectricMeterId' AND object_id = OBJECT_ID('dbo.DormMeter'))
    CREATE UNIQUE INDEX UX_DormMeter_ElectricMeterId  ON dbo.DormMeter(ElectricMeterId)  WHERE ElectricMeterId  IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DormMeter_ColdWaterMeterId' AND object_id = OBJECT_ID('dbo.DormMeter'))
    CREATE UNIQUE INDEX UX_DormMeter_ColdWaterMeterId ON dbo.DormMeter(ColdWaterMeterId) WHERE ColdWaterMeterId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DormMeter_HotWaterMeterId' AND object_id = OBJECT_ID('dbo.DormMeter'))
    CREATE UNIQUE INDEX UX_DormMeter_HotWaterMeterId  ON dbo.DormMeter(HotWaterMeterId)  WHERE HotWaterMeterId  IS NOT NULL;
PRINT '✓ dbo.DormMeter 设备ID 过滤唯一索引已创建（v2.13.168）';
GO

-- ============================================================
-- v2.13.130 设备读数日志（EquipmentReading）— 与 DormMeter 配置层 + MeterRecord 聚合层构成三层数据模型
-- 设计：不 FK 到 DormMeter（PDA 原始上传流水可能没经过设备档案配置），独立日志表
-- 索引：EquipmentId（查最新读数）、ReadTime（按时间段查询/批量删除）、(EquipmentType, ReadTime) 复合索引
-- ============================================================
IF OBJECT_ID('dbo.EquipmentReading', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EquipmentReading (
        ReadingId       INT            IDENTITY(1,1) NOT NULL,
        EquipmentId     NVARCHAR(64)   NOT NULL,             -- 设备 ID（电表/冷水/热水表编号）
        EquipmentType   TINYINT        NOT NULL,             -- 1=电表 2=冷水 3=热水
        Reading         DECIMAL(12,2)  NOT NULL DEFAULT 0,
        ReadTime        DATETIME       NOT NULL,             -- 读取时间（业务读取时刻）
        Remark          NVARCHAR(500)  NULL,
        CreatedBy       NVARCHAR(64)   NULL,                 -- 记录创建人（审计）
        CreatedAt       DATETIME       NOT NULL DEFAULT GETDATE(),
        UpdatedAt       DATETIME       NULL DEFAULT GETDATE(),
        CONSTRAINT PK_EquipmentReading PRIMARY KEY CLUSTERED (ReadingId),
        CONSTRAINT CK_EquipmentReading_Type CHECK (EquipmentType BETWEEN 1 AND 3)
    );
    CREATE NONCLUSTERED INDEX IX_EquipmentReading_EquipmentId ON dbo.EquipmentReading (EquipmentId);
    CREATE NONCLUSTERED INDEX IX_EquipmentReading_ReadTime    ON dbo.EquipmentReading (ReadTime);
    CREATE NONCLUSTERED INDEX IX_EquipmentReading_Type_Time   ON dbo.EquipmentReading (EquipmentType, ReadTime);
    PRINT '✓ dbo.EquipmentReading 表已创建（v2.13.130）';
END;
GO

PRINT '✅ 数据库结构创建完成';
GO