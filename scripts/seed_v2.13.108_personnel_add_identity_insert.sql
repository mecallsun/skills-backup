-- ============================================================
-- v2.13.108 P0 BUG 修复：SysPermission + SysRolePermission IDENTITY_INSERT
-- ============================================================
-- 问题背景：
--   v2.13.92/97/99/100/101/102/106 多次尝试补齐 personnel:add (Id=40) +
--   admin → personnel:add (Id=61) seed，但 DatabaseInitializer.MigrateFieldPermissionAsync
--   迁移 SQL 缺少 SET IDENTITY_INSERT ON，导致 SQL Server 上报：
--     "Cannot insert explicit value for identity column in table 'SysPermission'
--      when IDENTITY_INSERT is set to OFF."
--   异常被 try/catch 静默吞掉，仅 WARNING 日志；UI 永远拿不到 personnel:add 权限码，
--   「人员清单」页头「新增」按钮 PageHeader PermissionCode 校验失败 → 按钮隐藏。
--
-- 修复（v2.13.108）：
--   1. DatabaseInitializer.MigrateFieldPermissionAsync SQL Server 版本
--      在 INSERT 前后加 SET IDENTITY_INSERT [table] ON / OFF
--   2. 提供本手动 SQL 脚本，作为迁移失败时的兜底方案
--
-- 使用方法：
--   1. 停止 TrayApp + Admin + Api 进程
--   2. 用 SQL Server Management Studio 或 sqlcmd 执行本脚本
--   3. 重启 Admin → DatabaseInitializer 会自动跳过已存在的 Id（WHERE NOT EXISTS 守卫）
--
-- 验证：
--   SELECT * FROM SysPermission WHERE Id IN (37, 38, 39, 40);
--   SELECT * FROM SysRolePermission WHERE Id IN (58, 59, 60, 61);
--   期望：8 行全部存在
-- ============================================================

USE [WaterMeterDB];  -- 根据实际数据库名调整（v2.13.22 起默认 WaterMeterDB）
GO

-- 1. SysPermission：v2.13.92 三个种子（Id 37/38/39）+ v2.13.97 一个补充（Id 40）
SET IDENTITY_INSERT [dbo].[SysPermission] ON;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 37)
INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
VALUES (37, N'settings:fields', N'字段权限', 1, 18, N'/Settings?tab=fields', N'bi-shield-check', 28, 1, 1, N'管理敏感字段清单', '2026-07-22');
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 38)
INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
VALUES (38, N'fieldpermission:edit', N'编辑字段权限', 2, 37, N'', N'', 29, 1, 1, N'勾选/取消勾选敏感字段', '2026-07-22');
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 39)
INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
VALUES (39, N'privacy:field:enable', N'启用隐私字段保护', 3, 0, N'', N'', 30, 1, 1, N'勾选此权限的角色将看不到所有 SysFieldPermission 清单中的字段', '2026-07-22');
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 40)
INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
VALUES (40, N'personnel:add', N'新增人员', 2, 9, N'/Personnel/Create', N'bi-plus-lg', 7, 1, 0, '2026-07-22');
GO

SET IDENTITY_INSERT [dbo].[SysPermission] OFF;
GO

-- 2. SysRolePermission：admin 角色关联（Id 58/59/60 + v2.13.97 Id 61）
SET IDENTITY_INSERT [dbo].[SysRolePermission] ON;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 58)
INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
VALUES (58, 1, 37, '2026-07-22');
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 59)
INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
VALUES (59, 1, 38, '2026-07-22');
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 60)
INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
VALUES (60, 1, 39, '2026-07-22');
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 61)
INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
VALUES (61, 1, 40, '2026-07-22');
GO

SET IDENTITY_INSERT [dbo].[SysRolePermission] OFF;
GO

-- 3. 验证
PRINT '=== SysPermission 验证 ===';
SELECT Id, PermissionCode, PermissionName, PermissionType, IsActive
FROM [dbo].[SysPermission]
WHERE Id IN (37, 38, 39, 40)
ORDER BY Id;

PRINT '=== SysRolePermission 验证 ===';
SELECT Id, RoleId, PermissionId
FROM [dbo].[SysRolePermission]
WHERE Id IN (58, 59, 60, 61)
ORDER BY Id;

PRINT '=== 期望：8 行全部存在 ===';
GO