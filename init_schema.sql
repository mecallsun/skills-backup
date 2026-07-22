-- =========================================================================
-- 📌 金戈宿舍管理系统 — 绝对真理源 DDL
-- 来源：192.168.1.237 / WaterMeterDB 实时探测
-- 生成日期：2026-07-15
-- 版本：v2.13.7（2026-07-16 RBAC 补表：SysRole.SortOrder 列 + SysPermission/SysRolePermission 表）
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
    [SortOrder]   INT            NOT NULL DEFAULT ((0)),  -- v2.13.7 RBAC 补列：角色排序（Web 端 OrderBy 使用）
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
    -- v2.13.26 个人中心与账号安全
    [WeChatOpenId]               NVARCHAR(64)  NULL,
    [WeChatBindAt]               DATETIME      NULL,
    [PasswordResetToken]         NVARCHAR(128) NULL,
    [PasswordResetTokenExpiry]   DATETIME      NULL,
    [PasswordResetFailedCount]   INT           NOT NULL DEFAULT ((0)),
    [PasswordResetLockedUntil]   DATETIME      NULL,
    CONSTRAINT [PK_SysUser] PRIMARY KEY ([UserId]),
    CONSTRAINT [UQ_SysUser_Username] UNIQUE ([Username])
);
CREATE UNIQUE INDEX [IX_SysUser_WeChatOpenId]
    ON [dbo].[SysUser]([WeChatOpenId]) WHERE [WeChatOpenId] IS NOT NULL;

-- 20.1 SysUserSecurityQuestion（v2.13.26 密码找回 - 安全问题）
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
GO

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

-- =========================================================================
-- v2.13.7 RBAC 补表：权限表与角色权限关联表（原真理源缺失，RBAC 上 SQL Server 补齐）
-- 列名与 EF 实体属性 1:1 对齐，主键 [Id]（与 SysPermission/SysRolePermission 实体一致）
-- =========================================================================

-- 24. SysPermission（权限/菜单节点）
CREATE TABLE [dbo].[SysPermission] (
    [Id]             INT            IDENTITY(1,1) NOT NULL,
    [PermissionCode] NVARCHAR(64)   NOT NULL,
    [PermissionName] NVARCHAR(64)   NOT NULL,
    [PermissionType] TINYINT        NOT NULL DEFAULT ((1)),  -- 1=菜单 2=操作
    [ParentId]       INT            NOT NULL DEFAULT ((0)),
    [Route]          NVARCHAR(256)  NULL,
    [Icon]           NVARCHAR(64)   NULL,
    [SortOrder]      INT            NOT NULL DEFAULT ((0)),
    [IsActive]       BIT            NOT NULL DEFAULT ((1)),
    [IsSystem]       BIT            NOT NULL DEFAULT ((0)),
    [Description]    NVARCHAR(256)  NULL,
    [CreatedAt]      DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]      DATETIME       NULL,
    [CreatedBy]      NVARCHAR(64)   NULL,
    CONSTRAINT [PK_SysPermission] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_SysPermission_Code] UNIQUE ([PermissionCode])
);
CREATE INDEX [IX_SysPermission_ParentId] ON [dbo].[SysPermission]([ParentId]);

-- 25. SysRolePermission（角色权限关联）
CREATE TABLE [dbo].[SysRolePermission] (
    [Id]           INT            IDENTITY(1,1) NOT NULL,
    [RoleId]       INT            NOT NULL,
    [PermissionId] INT            NOT NULL,
    [CreatedAt]    DATETIME       NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_SysRolePermission] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_SysRolePermission_RolePerm] UNIQUE ([RoleId], [PermissionId]),
    CONSTRAINT [FK_SysRolePermission_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SysRole]([RoleId]) ON DELETE CASCADE,
    CONSTRAINT [FK_SysRolePermission_Perm] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[SysPermission]([Id]) ON DELETE CASCADE
);

-- 26. 视图 v_MeterRecordDetail
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

-- =========================================================================
-- v2.13.24 业务深度补表：DormBilling/EmployeeBilling/SysUserFilterCache/
--   AppVersion/SysIntegration/SysParameter/SysSystemIntegration
--   原 EF Migration 自动创建，DDL 现统一到 init_schema.sql 作为权威
-- =========================================================================

-- 27. DormBilling（宿舍账单）
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

-- 28. EmployeeBilling（员工分摊账单）
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

-- 29. SysUserFilterCache（用户筛选条件云端缓存 — v2.13.12）
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

-- 30. AppVersion（PDA 版本管理）
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

-- 31. SysIntegration（系统集成 HR/K3ERP 配置）
CREATE TABLE [dbo].[SysIntegration] (
    [Id]            INT            IDENTITY(1,1) NOT NULL,
    [Code]          NVARCHAR(64)   NOT NULL,
    [Name]          NVARCHAR(128)  NOT NULL,
    [IntegrationType] NVARCHAR(32) NOT NULL,  -- HR / K3ERP / Other
    [Endpoint]      NVARCHAR(512)  NOT NULL,
    [AuthJson]      NVARCHAR(MAX)  NULL,
    [IsActive]      BIT            NOT NULL DEFAULT ((1)),
    [LastSyncAt]    DATETIME       NULL,
    [LastSyncStatus] NVARCHAR(32)  NULL,
    [Remark]        NVARCHAR(500)  NULL,
    [CreatedAt]     DATETIME       NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt]     DATETIME       NULL,
    CONSTRAINT [PK_SysIntegration] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_SysIntegration_Code] UNIQUE ([Code])
);

-- 32. SysParameter（数据库连接持久化 — v2.13.19 双 UI 同步）
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

-- 33. SysSystemIntegration（系统集成 — 占位兼容表）
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

GO
PRINT N'✅ v2.13.24 业务深度 7 张表 DDL 已补充完成';

-- =========================================================================
-- v2.13.99 隐私字段权限表（SysFieldPermission）
--   背景：v2.13.92 引入但 SQL Server 生产库从未落地，导致
--         Html.IsFieldHiddenAsync 链路短路返回 false，隐私字段始终可见。
--   修复：DatabaseInitializer 启动迁移（运行时自动补建），
--         本节为手工 DDL（运维补漏 / 新部署一次性脚本）。
-- =========================================================================

-- 34. SysFieldPermission（字段权限清单）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SysFieldPermission]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SysFieldPermission] (
        [Id]                INT             IDENTITY(1,1) NOT NULL,
        [FieldKey]          NVARCHAR(64)    NOT NULL,
        [Module]            NVARCHAR(32)    NOT NULL,
        [FieldName]         NVARCHAR(64)    NOT NULL,
        [FieldType]         NVARCHAR(16)    NULL,
        [SensitivityLevel]  TINYINT         NOT NULL DEFAULT ((1)),
        [SortOrder]         INT             NOT NULL DEFAULT ((0)),
        [IsActive]          BIT             NOT NULL DEFAULT ((0)),
        [Description]       NVARCHAR(200)   NULL,
        [CreatedAt]         DATETIME2       NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt]         DATETIME2       NULL,
        [UpdatedBy]         NVARCHAR(64)    NULL,
        CONSTRAINT [PK_SysFieldPermission] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_SysFieldPermission_FieldKey] UNIQUE ([FieldKey])
    );
END;
GO

-- 35. v2.13.92 3 个权限码种子（settings:fields / fieldpermission:edit / privacy:field:enable）
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 37)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
    VALUES (37, N'settings:fields', N'字段权限', 1, 18, N'/Settings?tab=fields', N'bi-shield-check', 28, 1, 1, N'管理敏感字段清单', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 38)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
    VALUES (38, N'fieldpermission:edit', N'编辑字段权限', 2, 37, N'', N'', 29, 1, 1, N'勾选/取消勾选敏感字段', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 39)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
    VALUES (39, N'privacy:field:enable', N'启用隐私字段保护', 3, 0, N'', N'', 30, 1, 1, N'勾选此权限的角色将看不到所有 SysFieldPermission 清单中的字段', '2026-07-22');
GO

-- 36. v2.13.92 admin 角色关联（SysRolePermission Id 58/59/60）
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 58)
    INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
    VALUES (58, 1, 37, '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 59)
    INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
    VALUES (59, 1, 38, '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 60)
    INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
    VALUES (60, 1, 39, '2026-07-22');
GO

-- 37. v2.13.92 SysFieldPermission 5 字段种子
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 1)
    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
    VALUES (1, N'employee.realname', N'Personnel', N'姓名', N'string', 1, 1, 1, N'员工真实姓名（高 PII）', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 2)
    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
    VALUES (2, N'employee.phone', N'Personnel', N'手机号', N'string', 1, 2, 1, N'联系电话（高 PII）', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 3)
    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
    VALUES (3, N'employee.employeecode', N'Personnel', N'工号', N'string', 2, 3, 1, N'公司内唯一标识', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 4)
    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
    VALUES (4, N'employee.dormcode', N'Personnel', N'宿舍房号', N'string', 2, 4, 1, N'当前入住房号（隐私住址）', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 5)
    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
    VALUES (5, N'employee.remark', N'Personnel', N'备注', N'string', 2, 5, 1, N'自由文本备注（可能含敏感信息）', '2026-07-22');
GO

PRINT N'✅ v2.13.99 SysFieldPermission 表 + 隐私字段权限种子 DDL 已补充';

-- =========================================================================
-- v2.13.97 补充：personnel:add 权限码（用户反馈 P0：缺少「新增人员」按钮权限）
--   v2.13.100 修订：v2.13.99 MigrateFieldPermissionAsync 漏写本 seed，
--                   现同步追加到 init_schema.sql（SQL Server 真相源）
-- =========================================================================

-- 38. v2.13.97 SysPermission Id=40 (personnel:add)
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 40)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (40, N'personnel:add', N'新增人员', 2, 9, N'/Personnel/Create', N'bi-plus-lg', 7, 1, 0, '2026-07-22');
GO

-- 39. v2.13.97 SysRolePermission Id=61 (admin → personnel:add)
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 61)
    INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
    VALUES (61, 1, 40, '2026-07-22');
GO

PRINT N'✅ v2.13.97 personnel:add (Id=40) + admin 关联 (Id=61) DDL 已补充';
