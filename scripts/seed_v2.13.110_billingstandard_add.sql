-- ====================================================================
-- v2.13.110 手动 seed 修复脚本（billingstandard:add 缺失终极修复）
-- v2.13.114 重大修订：原硬编码 Id=41/62 + IDENTITY_INSERT 模式生产 DB 不可靠
--   （SysRolePermission.Id 是 IDENTITY(1,1) 列，生产已累积到 Id=184+，
--    Id=62 已被 RoleId=9 占位 → INSERT (62,1,41) 因 PK 冲突被 try/catch 静默吞掉，
--    admin 永远拿不到 billingstandard:add 权限）
-- 修订后按 (RoleId, PermissionCode) 唯一性判断幂等插入，不指定 Id
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
--   - IF NOT EXISTS (RoleId=1, PermissionCode='X') 守卫幂等：已存在则跳过，重复执行安全
--   - 不指定 Id（IDENTITY 列自动分配），不再需要 SET IDENTITY_INSERT
--   - 完成后**必须 Ctrl+Shift+R 硬刷浏览器**（v2.13.102 banner 缓存）
-- ====================================================================

USE [WaterMeterDB];  -- 根据实际数据库名调整
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- ============ 段 1：诊断现状（只读，不会改数据）============
    SELECT '1.1 SysPermission billingstandard:add 存在' AS Step, COUNT(*) AS Result
        FROM [dbo].[SysPermission] WHERE PermissionCode = N'billingstandard:add';
    SELECT '1.2 admin (UserId=1) 是否拥有 billingstandard:add 权限' AS Step, COUNT(*) AS Result
        FROM [dbo].[SysUserRole] ur
        INNER JOIN [dbo].[SysRolePermission] rp ON ur.RoleId = rp.RoleId
        INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
        WHERE ur.UserId = 1 AND sp.PermissionCode = N'billingstandard:add';

    -- ============ 段 2：缺失项补救（幂等）============
    -- 段 2.1：SysPermission billingstandard:add 不存在则插入
    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE PermissionCode = N'billingstandard:add')
    BEGIN
        SET IDENTITY_INSERT [dbo].[SysPermission] ON;
        INSERT INTO [dbo].[SysPermission]
            ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
        VALUES
            (41, N'billingstandard:add', N'新增费用标准', 2, 11, N'/BillingStandard/Create', N'bi-plus-lg', 5, 1, 0, '2026-07-22');
        SET IDENTITY_INSERT [dbo].[SysPermission] OFF;
    END;

    -- 段 2.2：admin (UserId=1, RoleId=1) → billingstandard:add 不存在则插入（不指定 Id）
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[SysUserRole] ur
        INNER JOIN [dbo].[SysRolePermission] rp ON ur.RoleId = rp.RoleId
        INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
        WHERE ur.UserId = 1 AND sp.PermissionCode = N'billingstandard:add'
    )
    BEGIN
        INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
        SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
        WHERE PermissionCode = N'billingstandard:add';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============ 段 3：验证修复结果（必跑，必须看到 1 行）============
SELECT '3.1 SysPermission billingstandard:add 现在存在' AS Step, COUNT(*) AS Result
    FROM [dbo].[SysPermission] WHERE PermissionCode = N'billingstandard:add';
SELECT '3.2 admin (UserId=1) 现在拥有 billingstandard:add 权限' AS Step, COUNT(*) AS Result
    FROM [dbo].[SysUserRole] ur
    INNER JOIN [dbo].[SysRolePermission] rp ON ur.RoleId = rp.RoleId
    INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
    WHERE ur.UserId = 1 AND sp.PermissionCode = N'billingstandard:add';

-- ============ 完成后必做 ============
-- 1. 重启 DormManage.Admin.exe（不要用 TrayApp 启，自己启验证）
-- 2. 浏览器访问 /Settings?tab=roles → Ctrl+Shift+R 硬刷
-- 3. 点 admin 行「权限矩阵」按钮
-- 4. 期望：banner 绿色「SysPermission 5/5 · SysRolePermission(admin) 5/5 · SysFieldPermission 5/5」
-- 5. 期望：「费用标准」分组下可见「└ 新增费用标准 (billingstandard:add)」复选框，默认勾选
-- 6. 访问 /BillingStandard → PageHeader「新增标准」按钮按权限控制显示