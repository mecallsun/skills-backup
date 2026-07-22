-- ====================================================================
-- v2.13.110 手动 seed 修复脚本（billingstandard:add 缺失终极修复）
-- 参照 v2.13.103_personnel_add.sql 模式，专为费用标准「新增标准」按钮权限
--
-- 适用场景：
--   v2.13.110 启动 DatabaseInitializer 后，权限矩阵 Modal「费用标准」分组
--   仍不显示「新增费用标准 (billingstandard:add)」复选框。
--
-- 用法：
--   1. 用 SQL Server Management Studio 或 sqlcmd 连接到生产 DB（默认 WaterMeterDB）
--   2. 修改脚本第 21 行的 USE [WaterMeterDB] 为实际库名
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
    SELECT '1.1 SysPermission Id=41 billingstandard:add 存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysPermission] WHERE Id=41;
    SELECT '1.2 SysPermission Id=41 完整行' AS Step, * FROM [dbo].[SysPermission] WHERE Id=41;
    SELECT '1.3 SysRolePermission Id=62 (admin→41) 存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysRolePermission] WHERE Id=62;
    SELECT '1.4 SysRolePermission Id=62 完整行' AS Step, * FROM [dbo].[SysRolePermission] WHERE Id=62;

    -- ============ 段 2：完整 SysPermission 列结构（确认 Description 可空）============
    SELECT
        c.COLUMN_NAME,
        c.DATA_TYPE,
        c.IS_NULLABLE,
        c.CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS c
    WHERE c.TABLE_SCHEMA = 'dbo'
      AND c.TABLE_NAME = 'SysPermission'
    ORDER BY c.ORDINAL_POSITION;

    -- ============ 段 3：强制 INSERT Id=41 + Id=62（幂等）============
    -- v2.13.108 复用：SQL Server IDENTITY_INSERT 必须显式开启

    -- SysPermission Id=41 billingstandard:add
    SET IDENTITY_INSERT [dbo].[SysPermission] ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 41)
    BEGIN
        INSERT INTO [dbo].[SysPermission]
            ([Id], [PermissionCode], [PermissionName], [PermissionType], [ParentId], [Route], [Icon], [SortOrder], [IsActive], [IsSystem], [CreatedAt])
        VALUES
            (41, N'billingstandard:add', N'新增费用标准', 2, 11, N'/BillingStandard/Create', N'bi-plus-lg', 5, 1, 0, '2026-07-22');
    END;

    SET IDENTITY_INSERT [dbo].[SysPermission] OFF;

    -- SysRolePermission Id=62 admin → Id=41
    SET IDENTITY_INSERT [dbo].[SysRolePermission] ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 62)
    BEGIN
        INSERT INTO [dbo].[SysRolePermission]
            ([Id], [RoleId], [PermissionId], [CreatedAt])
        VALUES
            (62, 1, 41, '2026-07-22');
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
SELECT '4.1 SysPermission Id=41 现在存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysPermission] WHERE Id=41;
SELECT '4.2 SysRolePermission Id=62 现在存在' AS Step, COUNT(*) AS Result FROM [dbo].[SysRolePermission] WHERE Id=62;
SELECT '4.3 admin 权限数（修复前 37，修复后 38）' AS Step, COUNT(*) AS Result FROM [dbo].[SysRolePermission] WHERE RoleId=1;

-- ============ 完成后必做 ============
-- 1. 重启 DormManage.Admin.exe（不要用 TrayApp 启，自己启验证）
-- 2. 浏览器访问 /Settings?tab=roles → Ctrl+Shift+R 硬刷
-- 3. 点 admin 行「权限矩阵」按钮
-- 4. 期望：banner 绿色「SysPermission 5/5 · SysRolePermission 5/5 · SysFieldPermission 5/5」
-- 5. 期望：「费用标准」分组下可见「└ 新增费用标准 (billingstandard:add)」复选框，默认勾选
-- 6. 访问 /BillingStandard → PageHeader「新增标准」按钮按权限控制显示