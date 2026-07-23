-- ============================================================
-- v2.13.130 设备读数日志（EquipmentReading）迁移脚本
-- 日期：2026-07-23
-- 类型：DDL 新增 + SysPermission seed + admin 授权（v2.13.108 IDENTITY_INSERT 模式）
-- ============================================================

-- ------------------------------------------------------------
-- 1. 新建 EquipmentReading 表（与 DormMeter 配置层 + MeterRecord 聚合层构成三层数据模型）
-- 设计：不 FK 到 DormMeter（PDA 原始上传流水可能没经过设备档案配置），独立日志表
-- 索引：EquipmentId（查最新读数）、ReadTime（按时间段查询/批量删除）、(EquipmentType, ReadTime) 复合索引
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EquipmentReading')
BEGIN
    CREATE TABLE [dbo].[EquipmentReading] (
        [ReadingId]       INT             IDENTITY(1,1) NOT NULL,
        [EquipmentId]     NVARCHAR(64)    NOT NULL,            -- 设备 ID（电表/冷水/热水表编号）
        [EquipmentType]   TINYINT         NOT NULL,            -- 1=电表 2=冷水 3=热水
        [Reading]         DECIMAL(12,2)   NOT NULL DEFAULT 0,
        [ReadTime]        DATETIME        NOT NULL,            -- 读取时间（业务读取时刻）
        [Remark]          NVARCHAR(500)   NULL,
        [CreatedBy]       NVARCHAR(64)    NULL,                -- 记录创建人（审计）
        [CreatedAt]       DATETIME        NOT NULL DEFAULT GETDATE(),
        [UpdatedAt]       DATETIME        NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_EquipmentReading] PRIMARY KEY CLUSTERED ([ReadingId]),
        CONSTRAINT [CK_EquipmentReading_Type] CHECK ([EquipmentType] BETWEEN 1 AND 3)
    );
    CREATE NONCLUSTERED INDEX [IX_EquipmentReading_EquipmentId] ON [dbo].[EquipmentReading] ([EquipmentId]);
    CREATE NONCLUSTERED INDEX [IX_EquipmentReading_ReadTime]    ON [dbo].[EquipmentReading] ([ReadTime]);
    CREATE NONCLUSTERED INDEX [IX_EquipmentReading_Type_Time]   ON [dbo].[EquipmentReading] ([EquipmentType], [ReadTime]);
    PRINT '✓ EquipmentReading 表已创建';
END
ELSE
BEGIN
    PRINT '⊘ EquipmentReading 表已存在，跳过';
END
GO

-- ------------------------------------------------------------
-- 2. SysPermission seed（v2.13.130 新增 5 个权限码）
-- v2.13.108 修复：SQL Server IDENTITY 列必须 SET IDENTITY_INSERT ON/OFF 包裹（缺则被 try/catch 静默吞掉）
-- v2.13.114 改进：去掉 Id 硬编码，改用 (RoleId, PermissionCode) 唯一性判断（避免生产 DB Id 已占位）
-- ------------------------------------------------------------
SET IDENTITY_INSERT [dbo].[SysPermission] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:view')
    INSERT INTO [dbo].[SysPermission] ([PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (N'equipment-reading:view', N'查看设备记录', 1, 10, N'/Basics?tab=equipmentreading', N'bi-journal-text', 41, 1, 0, '2026-07-23');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:create')
    INSERT INTO [dbo].[SysPermission] ([PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (N'equipment-reading:create', N'新增设备记录', 2, (SELECT Id FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:view'), N'', N'', 42, 1, 0, '2026-07-23');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:edit')
    INSERT INTO [dbo].[SysPermission] ([PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (N'equipment-reading:edit', N'修改设备记录', 2, (SELECT Id FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:view'), N'', N'', 43, 1, 0, '2026-07-23');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:delete')
    INSERT INTO [dbo].[SysPermission] ([PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (N'equipment-reading:delete', N'删除设备记录', 2, (SELECT Id FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:view'), N'', N'', 44, 1, 0, '2026-07-23');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:batch-delete')
    INSERT INTO [dbo].[SysPermission] ([PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
    VALUES (N'equipment-reading:batch-delete', N'批量删除设备记录', 2, (SELECT Id FROM [dbo].[SysPermission] WHERE [PermissionCode] = N'equipment-reading:view'), N'', N'', 45, 1, 0, '2026-07-23');
SET IDENTITY_INSERT [dbo].[SysPermission] OFF;
PRINT '✓ SysPermission seed (equipment-reading:*) 5 个权限码已写入（幂等）';
GO

-- ------------------------------------------------------------
-- 3. SysRolePermission admin 授权（v2.13.114 幂等模式 — 不硬编码 RP Id，按 PermissionCode JOIN）
-- ------------------------------------------------------------
DECLARE @adminRoleId INT = 1;

;WITH code_to_perm AS (
    SELECT N'equipment-reading:view' AS [PermissionCode] UNION ALL
    SELECT N'equipment-reading:create' UNION ALL
    SELECT N'equipment-reading:edit' UNION ALL
    SELECT N'equipment-reading:delete' UNION ALL
    SELECT N'equipment-reading:batch-delete'
)
INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
SELECT @adminRoleId, sp.Id, '2026-07-23'
FROM [dbo].[SysPermission] sp
INNER JOIN code_to_perm c2p ON sp.[PermissionCode] = c2p.[PermissionCode]
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[SysRolePermission] rp
    WHERE rp.RoleId = @adminRoleId AND rp.PermissionId = sp.Id
);
PRINT '✓ SysRolePermission admin → equipment-reading:* 5 个授权已写入（幂等）';
GO

-- ------------------------------------------------------------
-- 4. 验证完整性
-- ------------------------------------------------------------
DECLARE @permCount INT = (
    SELECT COUNT(*) FROM [dbo].[SysPermission]
    WHERE [PermissionCode] IN (
        N'equipment-reading:view',
        N'equipment-reading:create',
        N'equipment-reading:edit',
        N'equipment-reading:delete',
        N'equipment-reading:batch-delete'
    )
);
DECLARE @rpCount INT = (
    SELECT COUNT(*) FROM [dbo].[SysRolePermission] rp
    INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
    WHERE rp.RoleId = 1 AND sp.[PermissionCode] LIKE N'equipment-reading:%'
);

PRINT N'=== v2.13.130 验证 ===';
PRINT N'SysPermission equipment-reading:* 期望 5 / 实际 ' + CAST(@permCount AS NVARCHAR(10));
PRINT N'SysRolePermission admin → equipment-reading:* 期望 5 / 实际 ' + CAST(@rpCount AS NVARCHAR(10));

IF @permCount = 5 AND @rpCount = 5
    PRINT N'✅ v2.13.130 设备读数日志迁移完整';
ELSE
    PRINT N'❌ v2.13.130 设备读数日志迁移不完整，请检查上述计数';
GO
