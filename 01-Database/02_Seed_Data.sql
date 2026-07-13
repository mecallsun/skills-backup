-- ============================================================
-- 初始化数据 / 种子数据
--
-- 执行前提：
-- 1. 已执行 00_创建数据库用户.sql （创建 __DB_USER__ 登录账号）
-- 2. 已执行 01_DDL_Schema.sql （创建表结构）
-- 3. 执行本脚本使用 __DB_USER__ 登录（SSMS → 192.168.1.237 → __DB_USER__）
--
-- 默认账号（密码原文）：
--   admin / admin123     系统管理员
--   pda001 / pda123      PDA抄表员
--   viewer / view123     查看员
-- ============================================================
USE WaterMeterDB;
GO

-- 1. 默认角色
IF NOT EXISTS (SELECT 1 FROM SysRole WHERE RoleCode='Admin')
    INSERT INTO SysRole(RoleCode, RoleName, Description) VALUES
        ('Admin',    '系统管理员', '拥有全部权限，可管理用户、宿舍、系统配置'),
        ('Operator', 'PDA操作员', '可通过PDA上传抄表数据，不可管理后台'),
        ('Viewer',   '查看员',     '可查看数据与图片，不可修改');
GO

-- 2. 默认管理员账号（密码：admin123，加盐后SHA256）
-- 密码原文：admin123
-- Salt: WaterMeter2026
-- SHA256(Salt+Password) = SHA256('WaterMeter2026admin123')
-- = 8f5b8a3d2e1f4c6b9a7d0e3f5c8b1a4d7e0f2c5b8a3d6e9f1c4b7a0d3e6f9c2b (示例)
IF NOT EXISTS (SELECT 1 FROM SysUser WHERE Username='admin')
BEGIN
    DECLARE @Salt NVARCHAR(32) = 'WaterMeter2026';
    DECLARE @Pwd NVARCHAR(64) = 'admin123';
    DECLARE @Hash NVARCHAR(256) = CONVERT(NVARCHAR(256),
        HASHBYTES('SHA2_256', CAST(@Salt + @Pwd AS VARBINARY(MAX))), 2);

    INSERT INTO SysUser(Username, PasswordHash, Salt, DisplayName, Mobile, IsActive)
    VALUES('admin', @Hash, @Salt, '系统管理员', '13800000000', 1);

    DECLARE @Uid INT = SCOPE_IDENTITY();
    DECLARE @Rid INT = (SELECT RoleId FROM SysRole WHERE RoleCode='Admin');
    INSERT INTO SysUserRole(UserId, RoleId) VALUES(@Uid, @Rid);
END
GO

-- 默认 PDA 演示账号（密码：pda123）
IF NOT EXISTS (SELECT 1 FROM SysUser WHERE Username='pda001')
BEGIN
    DECLARE @Salt2 NVARCHAR(32) = 'WaterMeter2026';
    DECLARE @Pwd2 NVARCHAR(64) = 'pda123';
    DECLARE @Hash2 NVARCHAR(256) = CONVERT(NVARCHAR(256),
        HASHBYTES('SHA2_256', CAST(@Salt2 + @Pwd2 AS VARBINARY(MAX))), 2);

    INSERT INTO SysUser(Username, PasswordHash, Salt, DisplayName, Mobile, IsActive)
    VALUES('pda001', @Hash2, @Salt2, '抄表员A', '13900000001', 1);

    DECLARE @Uid2 INT = SCOPE_IDENTITY();
    DECLARE @Rid2 INT = (SELECT RoleId FROM SysRole WHERE RoleCode='Operator');
    INSERT INTO SysUserRole(UserId, RoleId) VALUES(@Uid2, @Rid2);
END
GO

-- 3. 默认查看员（密码：view123）
IF NOT EXISTS (SELECT 1 FROM SysUser WHERE Username='viewer')
BEGIN
    DECLARE @Salt3 NVARCHAR(32) = 'WaterMeter2026';
    DECLARE @Pwd3 NVARCHAR(64) = 'view123';
    DECLARE @Hash3 NVARCHAR(256) = CONVERT(NVARCHAR(256),
        HASHBYTES('SHA2_256', CAST(@Salt3 + @Pwd3 AS VARBINARY(MAX))), 2);

    INSERT INTO SysUser(Username, PasswordHash, Salt, DisplayName, IsActive)
    VALUES('viewer', @Hash3, @Salt3, '查看员', 1);

    DECLARE @Uid3 INT = SCOPE_IDENTITY();
    DECLARE @Rid3 INT = (SELECT RoleId FROM SysRole WHERE RoleCode='Viewer');
    INSERT INTO SysUserRole(UserId, RoleId) VALUES(@Uid3, @Rid3);
END
GO

-- 4. 系统配置
IF NOT EXISTS (SELECT 1 FROM SysConfig WHERE ConfigKey='ImageBasePath')
    INSERT INTO SysConfig(ConfigKey, ConfigValue, ConfigGroup, Description)
    VALUES('ImageBasePath', 'D:\MeterImages', 'Storage', '图片物理存储根路径');
GO
IF NOT EXISTS (SELECT 1 FROM SysConfig WHERE ConfigKey='ImageUrlPrefix')
    INSERT INTO SysConfig(ConfigKey, ConfigValue, ConfigGroup, Description)
    VALUES('ImageUrlPrefix', '/uploads', 'Storage', '图片URL前缀（IIS虚拟目录）');
GO
IF NOT EXISTS (SELECT 1 FROM SysConfig WHERE ConfigKey='AllowDuplicateMonth')
    INSERT INTO SysConfig(ConfigKey, ConfigValue, ConfigGroup, Description)
    VALUES('AllowDuplicateMonth', 'false', 'Business', '是否允许同宿舍同月重复录入');
GO
IF NOT EXISTS (SELECT 1 FROM SysConfig WHERE ConfigKey='MaxImageSizeKB')
    INSERT INTO SysConfig(ConfigKey, ConfigValue, ConfigGroup, Description)
    VALUES('MaxImageSizeKB', '500', 'Business', '单张图片最大KB（超过则拒绝）');
GO
IF NOT EXISTS (SELECT 1 FROM SysConfig WHERE ConfigKey='CurrentReadMonth')
    INSERT INTO SysConfig(ConfigKey, ConfigValue, ConfigGroup, Description)
    VALUES('CurrentReadMonth', CONVERT(NVARCHAR(7), GETDATE(), 120), 'Business', '当前抄表月份 YYYY-MM');
GO

-- 5. 演示宿舍档案
IF NOT EXISTS (SELECT 1 FROM Dorm WHERE DormCode='D-301')
    INSERT INTO Dorm(DormCode, Building, Floor, RoomNo, DormAddress, DormType, Barcode)
    VALUES('D-301', '1号楼', '3F', '301', '金戈新材料1号楼3层301室', '单人间', 'JG-D-301');
GO
IF NOT EXISTS (SELECT 1 FROM Dorm WHERE DormCode='D-302')
    INSERT INTO Dorm(DormCode, Building, Floor, RoomNo, DormAddress, DormType, Barcode)
    VALUES('D-302', '1号楼', '3F', '302', '金戈新材料1号楼3层302室', '双人间', 'JG-D-302');
GO
IF NOT EXISTS (SELECT 1 FROM Dorm WHERE DormCode='D-303')
    INSERT INTO Dorm(DormCode, Building, Floor, RoomNo, DormAddress, DormType, Barcode)
    VALUES('D-303', '1号楼', '3F', '303', '金戈新材料1号楼3层303室', '单人间', 'JG-D-303');
GO
IF NOT EXISTS (SELECT 1 FROM Dorm WHERE DormCode='D-401')
    INSERT INTO Dorm(DormCode, Building, Floor, RoomNo, DormAddress, DormType, Barcode)
    VALUES('D-401', '1号楼', '4F', '401', '金戈新材料1号楼4层401室', '单人间', 'JG-D-401');
GO
IF NOT EXISTS (SELECT 1 FROM Dorm WHERE DormCode='D-402')
    INSERT INTO Dorm(DormCode, Building, Floor, RoomNo, DormAddress, DormType, Barcode)
    VALUES('D-402', '1号楼', '4F', '402', '金戈新材料1号楼4层402室', '双人间', 'JG-D-402');
GO

PRINT '✅ 种子数据插入完成';
PRINT '默认账号：';
PRINT '  管理员 admin / admin123';
PRINT '  PDA抄表员 pda001 / pda123';
PRINT '  查看员 viewer / view123';
GO