-- v2.13.180 启动期迁移 SQL（一次性执行）
-- 1. SysEmployee 表新增 IdNumber 列（身份证号）
IF COL_LENGTH('SysEmployee', 'IdNumber') IS NULL
BEGIN
    ALTER TABLE [dbo].[SysEmployee] ADD [IdNumber] NVARCHAR(18) NULL;
    PRINT '[v2.13.180] SysEmployee.IdNumber 列已新增';
END
GO

-- 2. SysFieldPermission 完整 19 项 seed (IF NOT EXISTS 幂等)
-- 注：当前生产 DB 的 SysFieldPermission.Id 列不是 IDENTITY 列，无需 SET IDENTITY_INSERT

-- Personnel 8 字段
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.realname')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (1, 'employee.realname', 'Personnel', '姓名', 'string', 1, 1, 1, '员工真实姓名（高 PII）', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.phone')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (2, 'employee.phone', 'Personnel', '手机号', 'string', 1, 2, 1, '联系电话（高 PII）', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.employeecode')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (3, 'employee.employeecode', 'Personnel', '工号', 'string', 2, 3, 1, '公司内唯一标识', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.dormcode')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (4, 'employee.dormcode', 'Personnel', '宿舍房号', 'string', 2, 4, 1, '当前入住房号（隐私住址）', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.remark')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (5, 'employee.remark', 'Personnel', '备注', 'string', 2, 5, 1, '自由文本备注（可能含敏感信息）', '2026-07-22');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.idnumber')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (16, 'employee.idnumber', 'Personnel', '身份证号', 'string', 1, 16, 1, '身份证号码（极高 PII）', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.hiredate')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (17, 'employee.hiredate', 'Personnel', '入职日期', 'date', 3, 17, 1, '入职日期（可推断入职时间）', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.leavedate')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (18, 'employee.leavedate', 'Personnel', '离职日期', 'date', 3, 18, 1, '离职日期（可推断在职状态）', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.employeetype')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (19, 'employee.employeetype', 'Personnel', '员工类型', 'string', 2, 19, 1, '员工类型（合同/外包/实习）', '2026-07-26');
-- v2.13.215 新增：班组、班次
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.team')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (20, 'employee.team', 'Personnel', '班组', 'string', 2, 20, 1, '所属班组（员工基础组织信息，可推断工作小组成员关系）', '2026-07-28');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'employee.attendance_type')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (21, 'employee.attendance_type', 'Personnel', '班次', 'string', 2, 21, 1, '考勤班次（员工排班信息，可推断作息规律）', '2026-07-28');

-- Booking 5 字段
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'booking.realname')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (6, 'booking.realname', 'Booking', '姓名', 'string', 1, 6, 1, '住宿人员真实姓名', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'booking.employeecode')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (7, 'booking.employeecode', 'Booking', '工号', 'string', 2, 7, 1, '住宿人员工号', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'booking.dormcode')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (8, 'booking.dormcode', 'Booking', '房号', 'string', 2, 8, 1, '住宿登记房号', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'booking.department')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (9, 'booking.department', 'Booking', '部门', 'string', 2, 9, 1, '住宿部门', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'booking.operator')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (10, 'booking.operator', 'Booking', '登记人', 'string', 2, 10, 1, '登记操作员', '2026-07-26');

-- Dorms 3 字段（v2.13.180 新增容量+在住人数）
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'dorm.address')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (11, 'dorm.address', 'Dorms', '地址', 'string', 2, 11, 1, '宿舍地址（隐私住址）', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'dorm.capacity')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (20, 'dorm.capacity', 'Dorms', '容量', 'int', 3, 20, 1, '宿舍最大入住人数（可推断房型）', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'dorm.currentcount')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (21, 'dorm.currentcount', 'Dorms', '在住人数', 'int', 3, 21, 1, '当前入住人数（可推断房型）', '2026-07-26');

-- Meter 1 字段
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'meter.operator')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (12, 'meter.operator', 'Meter', '抄表员', 'string', 2, 12, 1, '抄表员工号', '2026-07-26');

-- EmployeeBilling 3 字段
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'billing.realname')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (13, 'billing.realname', 'EmployeeBilling', '姓名', 'string', 1, 13, 1, '账单员工真实姓名', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'billing.employeecode')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (14, 'billing.employeecode', 'EmployeeBilling', '工号', 'string', 2, 14, 1, '账单员工工号', '2026-07-26');
IF NOT EXISTS (SELECT 1 FROM SysFieldPermission WHERE FieldKey = 'billing.dormcode')
    INSERT INTO SysFieldPermission (Id, FieldKey, Module, FieldName, FieldType, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt)
    VALUES (15, 'billing.dormcode', 'EmployeeBilling', '房号', 'string', 2, 15, 1, '账单房号', '2026-07-26');
GO

-- 验证
SELECT COUNT(*) AS TotalSysFieldPermissions FROM SysFieldPermission;
SELECT Id, FieldKey, FieldName, Module, SensitivityLevel FROM SysFieldPermission ORDER BY SortOrder;
GO