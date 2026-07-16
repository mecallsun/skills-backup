-- =========================================================================
-- 📌 金戈宿舍管理系统 — 绝对真理源 DDL
-- 来源：192.168.1.237 / WaterMeterDB 实时探测
-- 生成日期：2026-07-15
-- 版本：v2.13.3
--
-- 用途：
--   1. AI 编写后端代码时的唯一数据源依据
--   2. 禁止修改任何字段名、数据类型、约束
--   3. 所有 EF Core 模型必须与此 DDL 1:1 对齐
-- =========================================================================

-- 1. Address（地址）
CREATE TABLE [dbo].[Address] (
    [Id]           INT            IDENTITY(1,1) NOT NULL,
    [AddressText]  NVARCHAR(200)  NOT NULL,
    [Remark]       NVARCHAR(200)  NULL,
    [SortOrder]    INT            NOT NULL DEFAULT ((0)),
    [IsActive]     BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt]    DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]    DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Address] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Address_AddressText] UNIQUE ([AddressText])
);

-- 2. AttendanceType（考勤班次）
CREATE TABLE [dbo].[AttendanceType] (
    [Id]          INT            IDENTITY(1,1) NOT NULL,
    [Code]        NVARCHAR(20)   NOT NULL,
    [Name]        NVARCHAR(50)   NOT NULL,
    [WorkHours]   NVARCHAR(50)   NOT NULL,
    [Remark]      NVARCHAR(200)  NULL,
    [SortOrder]   INT            NOT NULL DEFAULT ((0)),
    [IsActive]    BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt]   DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]   DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_AttendanceType] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_AttendanceType_Code] UNIQUE ([Code])
);

-- 3. BillingStandard（费用标准）
CREATE TABLE [dbo].[BillingStandard] (
    [Id]              INT            IDENTITY(1,1) NOT NULL,
    [StandardName]    NVARCHAR(100)  NOT NULL,
    [ApplicableType]  NVARCHAR(40)   NOT NULL,
    [HotWaterPrice]   DECIMAL(10,2)  NOT NULL,
    [ColdWaterPrice]  DECIMAL(10,2)  NOT NULL,
    [ElectricityPrice] DECIMAL(10,2) NOT NULL,
    [EffectiveFrom]   DATE           NOT NULL,
    [EffectiveTo]     DATE           NOT NULL,
    [IsActive]        BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt]       DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]       DATETIME       NULL,
    CONSTRAINT [PK_BillingStandard] PRIMARY KEY ([Id])
);

-- 4. Building（楼栋）
CREATE TABLE [dbo].[Building] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Name]      NVARCHAR(50)   NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Building] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Building_Name] UNIQUE ([Name])
);

-- 5. Department（部门）
CREATE TABLE [dbo].[Department] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Code]      NVARCHAR(20)   NOT NULL,
    [Name]      NVARCHAR(50)   NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Department] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Department_Code] UNIQUE ([Code])
);

-- 6. Dorm（宿舍档案）
CREATE TABLE [dbo].[Dorm] (
    [DormId]         INT            IDENTITY(1,1) NOT NULL,
    [DormCode]       NVARCHAR(32)   NOT NULL,
    [Building]       NVARCHAR(32)   NOT NULL,
    [Floor]          NVARCHAR(16)   NOT NULL,
    [RoomNo]         NVARCHAR(16)   NOT NULL,
    [DormAddress]    NVARCHAR(128)  NOT NULL,
    [DormType]       NVARCHAR(16)   NOT NULL,
    [HasColdMeter]   BIT            NOT NULL DEFAULT ((1)),
    [HasHotMeter]    BIT            NOT NULL DEFAULT ((1)),
    [HasElectricMeter] BIT         NOT NULL DEFAULT ((1)),
    [Barcode]        NVARCHAR(64)   NOT NULL,
    [Remark]         NVARCHAR(256)  NULL,
    [IsActive]       BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt]      DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]      DATETIME       NOT NULL DEFAULT (GETDATE()),
    [BuildingId]     INT            NOT NULL,
    [BuildingName]   NVARCHAR(50)   NOT NULL,
    [FloorId]        INT            NOT NULL,
    [AddressId]      INT            NOT NULL,
    [AddressText]    NVARCHAR(200)  NOT NULL,
    [Capacity]       INT            NOT NULL DEFAULT ((2)),
    [Gender]         INT            NOT NULL DEFAULT ((1)),
    [BedNumbers]     NVARCHAR(1000) NOT NULL,
    [RoomCount]      INT            NOT NULL DEFAULT ((1)),
    CONSTRAINT [PK_Dorm] PRIMARY KEY ([DormId]),
    CONSTRAINT [UQ_Dorm_DormCode] UNIQUE ([DormCode]),
    CONSTRAINT [UQ_Dorm_Barcode] UNIQUE ([Barcode])
);

-- 7. DormBooking（办理登记）
CREATE TABLE [dbo].[DormBooking] (
    [BookingId]       INT            IDENTITY(1,1) NOT NULL,
    [EmployeeId]      INT            NOT NULL,
    [EmployeeCode]    NVARCHAR(64)   NOT NULL,
    [EmployeeName]    NVARCHAR(128)  NOT NULL,
    [Phone]           NVARCHAR(32)   NOT NULL,
    [Department]      NVARCHAR(128)  NOT NULL,
    [DormCode]        NVARCHAR(64)   NOT NULL,
    [BookingType]     TINYINT        NOT NULL,  -- 1=入住 2=退房
    [BookingDate]     DATE           NOT NULL,
    [Status]          TINYINT        NOT NULL,  -- 1=预约 2=在宿 3=已退房 4=已取消
    [Reason]          NVARCHAR(512)  NOT NULL,
    [Remark]          NVARCHAR(1024) NULL,
    [RegistrationDate] DATETIME      NOT NULL DEFAULT (GETDATE()),
    [Registrar]       NVARCHAR(64)   NOT NULL,
    [IsActive]        BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt]       DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]       DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_DormBooking] PRIMARY KEY ([BookingId])
);

-- 8. EmployeeType（员工类型）
CREATE TABLE [dbo].[EmployeeType] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Code]      NVARCHAR(20)   NOT NULL,
    [Name]      NVARCHAR(50)   NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_EmployeeType] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_EmployeeType_Code] UNIQUE ([Code])
);

-- 9. EmploymentStatus（在职状态）
CREATE TABLE [dbo].[EmploymentStatus] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Code]      NVARCHAR(20)   NOT NULL,
    [Name]      NVARCHAR(50)   NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_EmploymentStatus] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_EmploymentStatus_Code] UNIQUE ([Code])
);

-- 10. Floor（楼层）
CREATE TABLE [dbo].[Floor] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [FloorNo]   INT            NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Floor] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Floor_FloorNo] UNIQUE ([FloorNo])
);

-- 11. MeterImage（图片附件）
CREATE TABLE [dbo].[MeterImage] (
    [ImageId]       BIGINT         IDENTITY(1,1) NOT NULL,
    [RecordId]      BIGINT         NOT NULL,
    [MeterType]     NVARCHAR(16)   NOT NULL,  -- cold/hot/electric
    [RelativePath]  NVARCHAR(512)  NOT NULL,
    [AbsolutePath]  NVARCHAR(512)  NULL,
    [FileName]      NVARCHAR(128)  NOT NULL,
    [FileSize]      INT            NOT NULL DEFAULT ((0)),
    [FileHash]      NVARCHAR(64)   NOT NULL,
    [Width]         INT            NOT NULL,
    [Height]        INT            NOT NULL,
    [UploadedAt]    DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_MeterImage] PRIMARY KEY ([ImageId]),
    CONSTRAINT [FK_MeterImage_Record] FOREIGN KEY ([RecordId]) REFERENCES [dbo].[MeterRecord]([RecordId]) ON DELETE CASCADE
);
CREATE INDEX [IX_MeterImage_RecordId] ON [dbo].[MeterImage]([RecordId]);
CREATE INDEX [IX_MeterImage_Type] ON [dbo].[MeterImage]([MeterType]);

-- 12. MeterRecord（抄表记录）
CREATE TABLE [dbo].[MeterRecord] (
    [RecordId]        BIGINT         IDENTITY(1,1) NOT NULL,
    [DormId]          INT            NOT NULL,
    [DormCode]        NVARCHAR(32)   NOT NULL,
    [ReadMonth]       CHAR(7)        NOT NULL,  -- yyyy-MM
    [ColdMeter]       DECIMAL(12,2)  NOT NULL,
    [HotMeter]        DECIMAL(12,2)  NOT NULL,
    [ElectricMeter]   DECIMAL(12,2)  NOT NULL,
    [ColdUsage]       DECIMAL(12,2)  NOT NULL,
    [HotUsage]        DECIMAL(12,2)  NOT NULL,
    [ElectricUsage]   DECIMAL(12,2)  NOT NULL,
    [Operator]        NVARCHAR(64)   NOT NULL,
    [DeviceSn]        NVARCHAR(128)  NOT NULL,
    [ClientRecordId]  NVARCHAR(128)  NOT NULL,
    [ClientCreatedAt] DATETIME       NOT NULL,
    [ServerCreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [Status]          TINYINT        NOT NULL DEFAULT ((1)),  -- 0=未完成 1=正常 2=已修正 3=未完成(PDA) 4=已作废
    [Remark]          NVARCHAR(512)  NULL,
    [CreatedAt]       DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]       DATETIME       NULL,
    [IsActive]        BIT            NOT NULL DEFAULT ((1)),
    CONSTRAINT [PK_MeterRecord] PRIMARY KEY ([RecordId]),
    CONSTRAINT [FK_MeterRecord_Dorm] FOREIGN KEY ([DormId]) REFERENCES [dbo].[Dorm]([DormId])
);
CREATE UNIQUE INDEX [IX_MeterRecord_DormMonth] ON [dbo].[MeterRecord]([DormCode], [ReadMonth]);
CREATE UNIQUE INDEX [IX_MeterRecord_ClientId] ON [dbo].[MeterRecord]([DeviceSn], [ClientRecordId]);
CREATE INDEX [IX_MeterRecord_ServerCreatedAt] ON [dbo].[MeterRecord]([ServerCreatedAt] DESC);
CREATE INDEX [IX_MeterRecord_ReadMonth_Operator] ON [dbo].[MeterRecord]([ReadMonth], [Operator]);

-- 13. MeterUnit（计量单位）
CREATE TABLE [dbo].[MeterUnit] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Code]      NVARCHAR(20)   NOT NULL,
    [Name]      NVARCHAR(50)   NOT NULL,
    [Unit]      NVARCHAR(20)   NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_MeterUnit] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_MeterUnit_Code] UNIQUE ([Code])
);

-- 14. PdaDevice（PDA 设备）
CREATE TABLE [dbo].[PdaDevice] (
    [DeviceId]      INT            IDENTITY(1,1) NOT NULL,
    [DeviceSn]      NVARCHAR(64)   NOT NULL,
    [DeviceModel]   NVARCHAR(64)   NULL,
    [BoundUserId]   INT            NOT NULL,
    [LastLoginAt]   DATETIME       NOT NULL,
    [LastLoginIp]   NVARCHAR(128)  NOT NULL,
    [IsActive]      BIT            NOT NULL DEFAULT ((1)),
    [Remark]        NVARCHAR(512)  NULL,
    [CreatedAt]     DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_PdaDevice] PRIMARY KEY ([DeviceId]),
    CONSTRAINT [UQ_PdaDevice_Sn] UNIQUE ([DeviceSn])
);

-- 15. ResidenceStatus（住宿状态）
CREATE TABLE [dbo].[ResidenceStatus] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Code]      NVARCHAR(20)   NOT NULL,
    [Name]      NVARCHAR(50)   NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_ResidenceStatus] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_ResidenceStatus_Code] UNIQUE ([Code])
);

-- 16. SysConfig（系统配置）
CREATE TABLE [dbo].[SysConfig] (
    [ConfigKey]     NVARCHAR(64)   NOT NULL,
    [ConfigValue]   NVARCHAR(MAX)  NOT NULL,
    [ConfigGroup]   NVARCHAR(32)   NOT NULL,
    [Description]   NVARCHAR(512)  NOT NULL,
    [UpdatedAt]     DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedBy]     NVARCHAR(64)   NOT NULL,
    CONSTRAINT [PK_SysConfig] PRIMARY KEY ([ConfigKey])
);

-- 17. SysEmployee（员工档案）
CREATE TABLE [dbo].[SysEmployee] (
    [EmployeeId]        INT            IDENTITY(1,1) NOT NULL,
    [EmployeeCode]      NVARCHAR(64)   NOT NULL,
    [RealName]          NVARCHAR(128)  NOT NULL,
    [Department]        NVARCHAR(128)  NOT NULL,
    [DepartmentId]      INT            NOT NULL,
    [EmployeeType]      NVARCHAR(64)   NOT NULL,
    [EmployeeTypeId]    INT            NOT NULL,
    [TeamId]            INT            NOT NULL,
    [Phone]             NVARCHAR(32)   NOT NULL,
    [HireDate]          DATE           NOT NULL,
    [LeaveDate]         DATE           NULL,
    [Status]            INT            NOT NULL DEFAULT ((1)),  -- 1=在职 2=待入职 3=已离职
    [DormCode]          NVARCHAR(64)   NOT NULL,
    [BedNo]             INT            NOT NULL,
    [AttendanceTypeId]  INT            NOT NULL,
    [EmploymentStatusId] INT           NOT NULL DEFAULT ((1)),
    [ResidenceStatusId] INT            NOT NULL DEFAULT ((2)),
    [Remark]            NVARCHAR(1024) NULL,
    [IsActive]          BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt]         DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]         DATETIME       NOT NULL DEFAULT (GETDATE()),
    [Gender]            INT            NOT NULL DEFAULT ((1)),
    CONSTRAINT [PK_SysEmployee] PRIMARY KEY ([EmployeeId]),
    CONSTRAINT [UQ_SysEmployee_EmployeeCode] UNIQUE ([EmployeeCode])
);

-- 18. SysOpLog（操作日志）
CREATE TABLE [dbo].[SysOpLog] (
    [LogId]     BIGINT         IDENTITY(1,1) NOT NULL,
    [UserId]    INT            NOT NULL,
    [Username]  NVARCHAR(64)   NOT NULL,
    [Action]    NVARCHAR(128)  NOT NULL,
    [Target]    NVARCHAR(512)  NOT NULL,
    [Detail]    NVARCHAR(MAX)  NOT NULL,
    [Ip]        NVARCHAR(128)  NOT NULL,
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_SysOpLog] PRIMARY KEY ([LogId])
);
CREATE INDEX [IX_SysOpLog_CreatedAt] ON [dbo].[SysOpLog]([CreatedAt] DESC);

-- 19. SysRole（角色）
CREATE TABLE [dbo].[SysRole] (
    [RoleId]      INT            IDENTITY(1,1) NOT NULL,
    [RoleCode]    NVARCHAR(32)   NOT NULL,
    [RoleName]    NVARCHAR(64)   NOT NULL,
    [Description] NVARCHAR(256)  NULL,
    [IsActive]    BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt]   DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_SysRole] PRIMARY KEY ([RoleId]),
    CONSTRAINT [UQ_SysRole_RoleCode] UNIQUE ([RoleCode])
);

-- 20. SysUser（系统用户）
CREATE TABLE [dbo].[SysUser] (
    [UserId]         INT            IDENTITY(1,1) NOT NULL,
    [Username]       NVARCHAR(32)   NOT NULL,
    [PasswordHash]   NVARCHAR(256)  NOT NULL,
    [Salt]           NVARCHAR(32)   NOT NULL,
    [DisplayName]    NVARCHAR(64)   NULL,
    [Mobile]         NVARCHAR(16)   NULL,
    [Email]          NVARCHAR(64)   NULL,
    [IsActive]       BIT            NOT NULL DEFAULT ((1)),
    [IsLocked]       BIT            NOT NULL DEFAULT ((0)),
    [FailedLoginCount] INT          NOT NULL DEFAULT ((0)),
    [LastLoginAt]    DATETIME       NULL,
    [LastLoginIp]    NVARCHAR(64)   NULL,
    [CreatedAt]      DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_SysUser] PRIMARY KEY ([UserId]),
    CONSTRAINT [UQ_SysUser_Username] UNIQUE ([Username])
);

-- 21. SysUserRole（用户角色关联）
CREATE TABLE [dbo].[SysUserRole] (
    [UserId] INT NOT NULL,
    [RoleId] INT NOT NULL,
    CONSTRAINT [PK_SysUserRole] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_SysUserRole_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SysUser]([UserId]) ON DELETE CASCADE,
    CONSTRAINT [FK_SysUserRole_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SysRole]([RoleId]) ON DELETE CASCADE
);

-- 22. Team（班组）
CREATE TABLE [dbo].[Team] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Code]      NVARCHAR(20)   NOT NULL,
    [Name]      NVARCHAR(50)   NOT NULL,
    [Remark]    NVARCHAR(200)  NULL,
    [SortOrder] INT            NOT NULL DEFAULT ((0)),
    [IsActive]  BIT            NOT NULL DEFAULT ((1)),
    [CreatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Team] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Team_Code] UNIQUE ([Code])
);

-- 23. 视图 v_MeterRecordDetail
CREATE VIEW [dbo].[v_MeterRecordDetail] AS
SELECT
    r.[RecordId], r.[DormId], r.[DormCode], r.[ReadMonth],
    r.[ColdMeter], r.[HotMeter], r.[ElectricMeter],
    r.[ColdUsage], r.[HotUsage], r.[ElectricUsage],
    r.[Operator], r.[DeviceSn], r.[Status],
    r.[ClientCreatedAt], r.[ServerCreatedAt],
    d.[Building], d.[Floor], d.[RoomNo], d.[DormAddress],
    d.[HasColdMeter], d.[HasHotMeter], d.[HasElectricMeter]
FROM [dbo].[MeterRecord] r
INNER JOIN [dbo].[Dorm] d ON d.[DormId] = r.[DormId];

GO
PRINT N'✅ 数据库结构定义完成（绝对真理源 v2.13.3）';
