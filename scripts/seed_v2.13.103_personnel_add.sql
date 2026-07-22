-- ====================================================================
-- v2.13.103 手动 seed 修复脚本（personnel:add 缺失终极修复）
-- v2.13.109 重写：SQLite 语法 → SQL Server 语法（移除 PRAGMA / INSERT OR IGNORE；
--                   改用 SET IDENTITY_INSERT ON/OFF + IF NOT EXISTS）
--
-- 适用场景：
--   v2.13.102 一键修复后，权限矩阵 Modal「人员清单」分组仍不显示
--   「新增人员 (personnel:add)」复选框。
--
-- 用法：
--   1. 用 SQL Server Management Studio 或 sqlcmd 连接到生产 DB（默认 WaterMeterDB）
--   2. 修改脚本第 30 行的 USE [WaterMeterDB] 为实际库名
--   3. 整段脚本一次性执行（事务包裹，失败回滚）
--
-- 注意：
--   - IF NOT EXISTS 守卫幂等：已存在则跳过，重复执行安全
--   - SET IDENTITY_INSERT ON/OFF 配对包裹每条 INSERT（SQL Server IDENTITY 列要求）
--   - 完成后**必须 Ctrl+Shift+R 硬刷浏览器**（v2.13.102 banner 缓存）
-- ====================================================================

USE [WaterMeterDB];  -- 根据实际数据库名调整
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- ============ 段 1：诊断现状（只读，不会改数据）============
    SELECT '1.1 SysPermission 总数' AS Step, COUNT(*) AS Result FROM [dbo].[SysPermission];
    SELECT '1.2 SysPermission Id=40 personnel:add 存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysPermission] WHERE Id=40;
    SELECT '1.3 SysPermission Id=40 完整行' AS Step, * FROM [dbo].[SysPermission] WHERE Id=40;
    SELECT '1.4 SysRolePermission Id=61 (admin→40) 存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysRolePermission] WHERE Id=61;
    SELECT '1.5 SysRolePermission Id=61 完整行' AS Step, * FROM [dbo].[SysRolePermission] WHERE Id=61;

    -- ============ 段 2：完整 SysPermission 列结构（确认 Description 可空）============
    -- SQL Server 版本：INFORMATION_SCHEMA.COLUMNS
    SELECT
        c.COLUMN_NAME,
        c.DATA_TYPE,
        c.IS_NULLABLE,
        c.CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS c
    WHERE c.TABLE_SCHEMA = 'dbo'
      AND c.TABLE_NAME = 'SysPermission'
    ORDER BY c.ORDINAL_POSITION;

    -- ============ 段 3：强制 INSERT Id=40 + Id=61（幂等）============
    -- v2.13.108 P0 终极修复：SQL Server IDENTITY_INSERT 必须显式开启

    -- SysPermission Id=40 personnel:add
    SET IDENTITY_INSERT [dbo].[SysPermission] ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 40)
    BEGIN
        INSERT INTO [dbo].[SysPermission]
            ([Id], [PermissionCode], [PermissionName], [PermissionType], [ParentId], [Route], [Icon], [SortOrder], [IsActive], [IsSystem], [CreatedAt])
        VALUES
            (40, N'personnel:add', N'新增人员', 2, 9, N'/Personnel/Create', N'bi-plus-lg', 7, 1, 0, '2026-07-22');
    END;

    SET IDENTITY_INSERT [dbo].[SysPermission] OFF;

    -- SysRolePermission Id=61 admin → Id=40
    SET IDENTITY_INSERT [dbo].[SysRolePermission] ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 61)
    BEGIN
        INSERT INTO [dbo].[SysRolePermission]
            ([Id], [RoleId], [PermissionId], [CreatedAt])
        VALUES
            (61, 1, 40, '2026-07-22');
    END;

    SET IDENTITY_INSERT [dbo].[SysRolePermission] OFF;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============ 段 4：验证修复结果（必跑，必须看到 1 行）============
SELECT '4.1 SysPermission Id=40 现在存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysPermission] WHERE Id=40;
SELECT '4.2 SysRolePermission Id=61 现在存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysRolePermission] WHERE Id=61;
SELECT '4.3 admin 权限数（修复前 36，修复后 37）' AS Step, COUNT(*) AS Result FROM [dbo].[SysRolePermission] WHERE RoleId=1;

-- ============ 完成后必做 ============
-- 1. 重启 DormManage.Admin.exe（不要用 TrayApp 启，自己启验证）
-- 2. 浏览器访问 /Settings?tab=roles → Ctrl+Shift+R 硬刷
-- 3. 点 admin 行「权限矩阵」按钮
-- 4. 期望：banner 绿色「SysPermission 4/4 · SysRolePermission 4/4 · SysFieldPermission 5/5」
-- 5. 期望：「人员清单」分组下可见「└ 新增人员 (personnel:add)」复选框，默认勾选
-- 6. 访问 /Personnel → PageHeader「新增」按钮按权限控制显示