-- =========================================================================
-- 金戈宿舍管理系统 — 增量迁移脚本 v2.13.7（RBAC 上 SQL Server）
-- 日期：2026-07-16
-- 用途：对既有 SQL Server（192.168.1.237 / WaterMeterDB）执行，补齐 RBAC 缺失结构
-- 特性：幂等（可重复执行），仅新增（不删除/不改现有数据）
-- 关联：init_schema.sql v2.13.7、65/66 修复报告
-- =========================================================================

SET NOCOUNT ON;

-- 1. SysRole 补 SortOrder 列（Web 端角色列表 OrderBy 使用）
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[SysRole]') AND name = N'SortOrder')
BEGIN
    ALTER TABLE [dbo].[SysRole] ADD [SortOrder] INT NOT NULL DEFAULT ((0));
    PRINT N'✅ 已为 SysRole 添加 SortOrder 列';
END
ELSE PRINT N'ℹ️ SysRole.SortOrder 已存在，跳过';
GO

-- 2. SysPermission（权限/菜单节点）
IF OBJECT_ID(N'[dbo].[SysPermission]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SysPermission] (
        [Id]             INT            IDENTITY(1,1) NOT NULL,
        [PermissionCode] NVARCHAR(64)   NOT NULL,
        [PermissionName] NVARCHAR(64)   NOT NULL,
        [PermissionType] TINYINT        NOT NULL DEFAULT ((1)),
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
    PRINT N'✅ 已创建 SysPermission 表';
END
ELSE PRINT N'ℹ️ SysPermission 已存在，跳过';
GO

-- 3. SysRolePermission（角色权限关联）
IF OBJECT_ID(N'[dbo].[SysRolePermission]', N'U') IS NULL
BEGIN
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
    PRINT N'✅ 已创建 SysRolePermission 表';
END
ELSE PRINT N'ℹ️ SysRolePermission 已存在，跳过';
GO

PRINT N'✅ v2.13.7 RBAC 增量迁移完成';
