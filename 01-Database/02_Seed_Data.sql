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

-- ============================================================
-- v2.13.57 补充：人员清单演示数据（10 条）
-- 与 DormDbContext.cs line 647-657 HasData 种子数据 1:1 对齐
-- DepartmentId/EmployeeTypeId/EmploymentStatusId/ResidenceStatusId
-- 需与基础资料字典一致（Department=1..6, EmployeeType=1..5, EmploymentStatus=1..3, ResidenceStatus=1..3）
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM SysEmployee WHERE EmployeeCode='EMP-2026-001')
BEGIN
    SET IDENTITY_INSERT SysEmployee ON;
    INSERT INTO SysEmployee(EmployeeId, EmployeeCode, RealName, DepartmentId, Department, EmployeeTypeId, EmployeeType, TeamId, Gender, Phone, EmploymentStatusId, Status, HireDate, BedNo, DormCode, Team, ResidenceStatusId, AttendanceTypeId, IsActive, CreatedAt)
    VALUES
        (1,  'EMP-2026-001', N'张三', 1, N'生产部', 1, N'合同工', 1, 1, '13800000001', 1, 1, '2025-01-15', 1, 'D-301', N'A班',  1, 2, 1, GETDATE()),
        (2,  'EMP-2026-002', N'李四', 2, N'技术部', 1, N'合同工', 2, 1, '13800000002', 1, 1, '2025-02-20', 1, 'D-302', N'B班',  1, 4, 1, GETDATE()),
        (3,  'EMP-2026-003', N'王五', 3, N'行政部', 1, N'合同工', 3, 2, '13800000003', 1, 1, '2024-06-10', 1, 'D-303', N'C班',  1, 1, 1, GETDATE()),
        (4,  'EMP-2026-004', N'赵六', 2, N'技术部', 2, N'临时工', 4, 1, '13800000004', 1, 1, '2025-03-01', 1, 'D-401', N'D班',  1, 2, 1, GETDATE()),
        (5,  'EMP-2026-005', N'孙七', 1, N'生产部', 1, N'合同工', 1, 1, '13800000005', 1, 1, '2024-11-05', 2, 'D-301', N'A班',  1, 3, 1, GETDATE()),
        (6,  'EMP-2026-006', N'周八', 4, N'财务部', 1, N'合同工', 2, 2, '13800000006', 1, 1, '2025-04-15', 2, 'D-302', N'B班',  1, 1, 1, GETDATE()),
        (7,  'EMP-2026-007', N'吴九', 5, N'销售部', 3, N'外包',   3, 1, '13800000007', 1, 1, '2024-09-20', 1, 'D-303', N'C班',  1, 2, 1, GETDATE()),
        (8,  'EMP-2026-008', N'郑十', 2, N'技术部', 1, N'合同工', 4, 1, '13800000008', 1, 1, '2025-01-08', 2, 'D-401', N'D班',  1, 4, 1, GETDATE()),
        (9,  'EMP-2026-009', N'钱一', 6, N'后勤部', 1, N'合同工', 1, 1, '13800000009', 1, 1, '2024-12-01', 1, 'D-402', N'A班',  1, 1, 1, GETDATE()),
        (10, 'EMP-2026-010', N'陈二', 3, N'行政部', 4, N'实习生', 5, 2, '13800000010', 2, 2, '2026-08-01', NULL, NULL,    N'默认班', 2, NULL, 1, GETDATE());
    SET IDENTITY_INSERT SysEmployee OFF;
END
GO

-- ============================================================
-- v2.13.57 补充：办理登记演示数据（10 条）
-- 与 DormDbContext.cs line 661-671 HasData 种子数据 1:1 对齐
-- EmployeeId/DormCode 受 FK 约束（FK_DormBooking_Employee / FK_DormBooking_Dorm）
-- v2.13.58 修订：DormCode 必须与 02_Seed_Data.sql 演示宿舍档案 D-301/D-302/D-303/D-401/D-402 对齐
--   原 v2.13.57 误用 D-001~D-005，导致 FK_DormBooking_Dorm 约束失败
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM DormBooking WHERE BookingId=1)
BEGIN
    SET IDENTITY_INSERT DormBooking ON;
    INSERT INTO DormBooking(BookingId, EmployeeId, EmployeeCode, EmployeeName, Phone, Department, AttendanceTypeId, DormCode, BookingType, BookingDate, Status, Reason, RegistrationDate, Registrar, IsActive, CreatedAt)
    VALUES
        (1,  1, 'EMP-2026-001', N'张三', '13800000001', N'生产部', 2, 'D-301', 1, '2025-01-15', 2, N'入职', '2025-01-15 10:00:00', 'admin', 1, GETDATE()),
        (2,  2, 'EMP-2026-002', N'李四', '13800000002', N'技术部', 4, 'D-302', 1, '2025-02-20', 2, N'入职', '2025-02-20 14:30:00', 'admin', 1, GETDATE()),
        (3,  3, 'EMP-2026-003', N'王五', '13800000003', N'行政部', 1, 'D-303', 1, '2024-06-10', 2, N'入职', '2024-06-10 09:15:00', 'admin', 1, GETDATE()),
        (4,  4, 'EMP-2026-004', N'赵六', '13800000004', N'技术部', 2, 'D-401', 1, '2025-03-01', 2, N'入职', '2025-03-01 11:00:00', 'admin', 1, GETDATE()),
        (5,  5, 'EMP-2026-005', N'孙七', '13800000005', N'生产部', 3, 'D-301', 1, '2024-11-05', 2, N'调宿', '2024-11-05 16:20:00', 'admin', 1, GETDATE()),
        (6,  6, 'EMP-2026-006', N'周八', '13800000006', N'财务部', 1, 'D-302', 1, '2025-04-15', 2, N'入职', '2025-04-15 10:45:00', 'admin', 1, GETDATE()),
        (7,  7, 'EMP-2026-007', N'吴九', '13800000007', N'销售部', 2, 'D-303', 1, '2024-09-20', 2, N'入职', '2024-09-20 13:30:00', 'admin', 1, GETDATE()),
        (8,  8, 'EMP-2026-008', N'郑十', '13800000008', N'技术部', 4, 'D-401', 1, '2025-01-08', 2, N'入职', '2025-01-08 08:00:00', 'admin', 1, GETDATE()),
        (9,  9, 'EMP-2026-009', N'钱一', '13800000009', N'后勤部', 1, 'D-402', 1, '2024-12-01', 2, N'入职', '2024-12-01 15:00:00', 'admin', 1, GETDATE()),
        (10, 1, 'EMP-2026-001', N'张三', '13800000001', N'生产部', 2, 'D-301', 2, '2025-06-30', 3, N'离职', '2025-06-30 17:00:00', 'admin', 1, GETDATE());
    SET IDENTITY_INSERT DormBooking OFF;
END
GO

PRINT '✅ 种子数据插入完成';
PRINT '默认账号：';
PRINT '  管理员 admin / admin123';
PRINT '  PDA抄表员 pda001 / pda123';
PRINT '  查看员 viewer / view123';
GO