-- ============================================================
-- 金戈新材料 - PDA 水电抄表系统
-- 数据库初始化：创建用户 + 建库 + 建表 + 种子数据
--
-- v2.13.145 默认参数更新
-- 数据库服务器: 172.16.0.100
-- 数据库账号:    user  (SQL 保留关键字，必须用方括号 [user] 转义)
-- 数据库密码:    1234
-- 数据库名:      WaterMeterDB
--
-- 执行方式：用 sa 登录 SSMS 连接到 172.16.0.100 执行
-- ============================================================

USE [master];
GO

-- ============================================================
-- 步骤 1: 创建数据库登录账号（如果不存在）
-- 注意：user 是 SQL Server 保留关键字，必须用方括号 [user] 转义
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'user')
BEGIN
    CREATE LOGIN [user]
    WITH PASSWORD = N'1234',
         DEFAULT_DATABASE = [WaterMeterDB],
         CHECK_EXPIRATION = OFF,
         CHECK_POLICY = OFF;   -- 关闭密码策略，避免过期
    PRINT '✓ 已创建登录账号 user';
END
ELSE
BEGIN
    PRINT '⚠ 登录账号 user 已存在，跳过创建';
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
-- 步骤 3: 创建数据库用户并授权（user 对 WaterMeterDB）
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'user')
BEGIN
    CREATE USER [user] FOR LOGIN [user];
    PRINT '✓ 已创建数据库用户 user';
END;

-- 授予 db_owner 权限（应用需要建表、增删改查等所有权限）
ALTER ROLE [db_owner] ADD MEMBER [user];
PRINT '✓ 已授予 user db_owner 角色';
GO

PRINT '';
PRINT '========================================';
PRINT '✅ 数据库用户创建完成！';
PRINT '   服务器: 172.16.0.100';
PRINT '   数据库: WaterMeterDB';
PRINT '   账号:   user';
PRINT '   密码:   1234';
PRINT '========================================';
PRINT '';
PRINT '下一步：在 SSMS 中以 user 登录，执行：';
PRINT '   1. 01_DDL_Schema.sql （建表）';
PRINT '   2. 02_Seed_Data.sql （种子数据）';
GO