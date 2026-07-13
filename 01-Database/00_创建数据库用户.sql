-- ============================================================
-- 金戈新材料 - PDA 水电抄表系统
-- 数据库初始化：创建用户 + 建库 + 建表 + 种子数据
--
-- 数据库服务器: 192.168.1.237
-- 数据库账号:    __DB_USER__
-- 数据库密码:    __DB_PASSWORD__
-- 数据库名:      WaterMeterDB
--
-- 执行方式：用 sa 登录 SSMS 连接到 192.168.1.237 执行
-- ============================================================

USE [master];
GO

-- ============================================================
-- 步骤 1: 创建数据库登录账号（如果不存在）
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'__DB_USER__')
BEGIN
    CREATE LOGIN [__DB_USER__]
    WITH PASSWORD = N'__DB_PASSWORD__',
         DEFAULT_DATABASE = [WaterMeterDB],
         CHECK_EXPIRATION = OFF,
         CHECK_POLICY = OFF;   -- 关闭密码策略，避免过期
    PRINT '✓ 已创建登录账号 __DB_USER__';
END
ELSE
BEGIN
    PRINT '⚠ 登录账号 __DB_USER__ 已存在，跳过创建';
END;
GO

-- ============================================================
-- 步骤 2: 创建 WaterMeterDB 数据库（如果不存在）
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'WaterMeterDB')
BEGIN
    CREATE DATABASE [WaterMeterDB]
    COLLATE Chinese_PRC_CI_AS;
    PRINT '✓ 已创建数据库 WaterMeterDB';
END
ELSE
BEGIN
    PRINT '⚠ 数据库 WaterMeterDB 已存在，跳过创建';
END;
GO

USE [WaterMeterDB];
GO

-- ============================================================
-- 步骤 3: 创建数据库用户并授权（__DB_USER__ 对 WaterMeterDB）
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'__DB_USER__')
BEGIN
    CREATE USER [__DB_USER__] FOR LOGIN [__DB_USER__];
    PRINT '✓ 已创建数据库用户 __DB_USER__';
END;

-- 授予 db_owner 权限（应用需要建表、增删改查等所有权限）
ALTER ROLE [db_owner] ADD MEMBER [__DB_USER__];
PRINT '✓ 已授予 __DB_USER__ db_owner 角色';
GO

PRINT '';
PRINT '========================================';
PRINT '✅ 数据库用户创建完成！';
PRINT '   服务器: 192.168.1.237';
PRINT '   数据库: WaterMeterDB';
PRINT '   账号:   __DB_USER__';
PRINT '   密码:   __DB_PASSWORD__';
PRINT '========================================';
PRINT '';
PRINT '下一步：在 SSMS 中以 __DB_USER__ 登录，执行：';
PRINT '   1. 01_DDL_Schema.sql （建表）';
PRINT '   2. 02_Seed_Data.sql （种子数据）';
GO