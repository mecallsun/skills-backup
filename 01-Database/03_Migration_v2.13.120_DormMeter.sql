-- ============================================================
-- v2.13.120 设备档案（DormMeter）迁移脚本
-- 日期：2026-07-23
-- 类型：DDL 新增 + SysPermission seed + admin 授权
-- ============================================================

-- ------------------------------------------------------------
-- 1. 新建 DormMeter 表（与 Dorm 1:1 关系）
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DormMeter')
BEGIN
    CREATE TABLE [dbo].[DormMeter] (
        [DormMeterId]        INT             IDENTITY(1,1) NOT NULL,
        [DormId]             INT             NOT NULL,
        [ElectricMeterId]    NVARCHAR(64)    NULL,
        [ColdWaterMeterId]   NVARCHAR(64)    NULL,
        [HotWaterMeterId]    NVARCHAR(64)    NULL,
        [Remark]             NVARCHAR(500)   NULL,
        [IsActive]           BIT             NOT NULL DEFAULT 1,
        [CreatedAt]          DATETIME        NOT NULL DEFAULT GETDATE(),
        [UpdatedAt]          DATETIME        NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_DormMeter] PRIMARY KEY CLUSTERED ([DormMeterId]),
        CONSTRAINT [FK_DormMeter_Dorm] FOREIGN KEY ([DormId])
            REFERENCES [dbo].[Dorm]([DormId]) ON DELETE CASCADE,
        CONSTRAINT [UX_DormMeter_DormId] UNIQUE ([DormId])
    );
    PRINT '✓ DormMeter 表已创建';
END
ELSE
BEGIN
    PRINT '⊘ DormMeter 表已存在，跳过';
END
GO

-- ------------------------------------------------------------
-- 2. SysPermission seed（v2.13.120 新增 4 个权限码）
-- ------------------------------------------------------------
SET IDENTITY_INSERT [dbo].[SysPermission] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 42)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (42, N'device:view', N'查看设备档案', 1, 10, N'/Basics?tab=device', N'bi-cpu', 31, 1, 0, '2026-07-23');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 43)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (43, N'device:create', N'新增设备档案', 2, 42, N'', N'', 32, 1, 0, '2026-07-23');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 44)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (44, N'device:edit', N'修改设备档案', 2, 42, N'', N'', 33, 1, 0, '2026-07-23');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 45)
    INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (45, N'device:delete', N'删除设备档案', 2, 42, N'', N'', 34, 1, 0, '2026-07-23');
SET IDENTITY_INSERT [dbo].[SysPermission] OFF;
PRINT '✓ SysPermission seed (42-45) 已写入';
GO

-- ------------------------------------------------------------
-- 3. SysRolePermission admin 授权（幂等）
-- ------------------------------------------------------------
DECLARE @adminRoleId INT = 1;

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[SysRolePermission] rp
    INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
    WHERE rp.RoleId = @adminRoleId AND sp.PermissionCode = N'device:view'
)
    INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
    SELECT @adminRoleId, Id, '2026-07-23' FROM [dbo].[SysPermission]
    WHERE PermissionCode = N'device:view';

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[SysRolePermission] rp
    INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
    WHERE rp.RoleId = @adminRoleId AND sp.PermissionCode = N'device:create'
)
    INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
    SELECT @adminRoleId, Id, '2026-07-23' FROM [dbo].[SysPermission]
    WHERE PermissionCode = N'device:create';

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[SysRolePermission] rp
    INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
    WHERE rp.RoleId = @adminRoleId AND sp.PermissionCode = N'device:edit'
)
    INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
    SELECT @adminRoleId, Id, '2026-07-23' FROM [dbo].[SysPermission]
    WHERE PermissionCode = N'device:edit';

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[SysRolePermission] rp
    INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
    WHERE rp.RoleId = @adminRoleId AND sp.PermissionCode = N'device:delete'
)
    INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
    SELECT @adminRoleId, Id, '2026-07-23' FROM [dbo].[SysPermission]
    WHERE PermissionCode = N'device:delete';
PRINT '✓ SysRolePermission admin → device 4 个权限码已写入（幂等）';
GO

-- ------------------------------------------------------------
-- 4. 验证完整性
-- ------------------------------------------------------------
DECLARE @permCount INT = (
    SELECT COUNT(*) FROM [dbo].[SysPermission]
    WHERE Id IN (42, 43, 44, 45)
);
DECLARE @rpCount INT = (
    SELECT COUNT(*) FROM [dbo].[SysRolePermission] rp
    INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
    WHERE rp.RoleId = 1 AND sp.PermissionCode LIKE N'device:%'
);

PRINT N'=== v2.13.120 验证 ===';
PRINT N'SysPermission device:* 期望 4 / 实际 ' + CAST(@permCount AS NVARCHAR(10));
PRINT N'SysRolePermission admin → device:* 期望 4 / 实际 ' + CAST(@rpCount AS NVARCHAR(10));

IF @permCount = 4 AND @rpCount = 4
    PRINT N'✅ v2.13.120 设备档案迁移完整';
ELSE
    PRINT N'❌ v2.13.120 设备档案迁移不完整，请检查上述计数';
GO