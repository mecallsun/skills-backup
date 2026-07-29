
-- 修复：添加缺失的字段权限记录 - 班组 (team) 和 班次 (attendance_type)
-- 用于解决 Settings → 字段权限界面不显示 '班组' 和 '班次' 字段的问题

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE FieldKey = 'employee.team')
BEGIN
    INSERT INTO [dbo].[SysFieldPermission] (
        [Id], [FieldKey], [Module], [FieldName], [FieldType], [SensitivityLevel], [SortOrder], 
        [IsActive], [Description], [CreatedAt], [UpdatedAt], [UpdatedBy]
    ) VALUES (
        9, N'employee.team', N'Personnel', N'班组', N'string', 2, 9, 1, 
        N'所属班组（员工基础组织信息，可推断工作小组成员关系）', GETDATE(), GETDATE(), 'system'
    );
    PRINT '✅ 已添加 班组 (employee.team) 字段权限记录';
END
ELSE
BEGIN
    PRINT 'ℹ️ 班组 字段权限记录已存在';
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE FieldKey = 'employee.attendance_type')
BEGIN
    INSERT INTO [dbo].[SysFieldPermission] (
        [Id], [FieldKey], [Module], [FieldName], [FieldType], [SensitivityLevel], [SortOrder], 
        [IsActive], [Description], [CreatedAt], [UpdatedAt], [UpdatedBy]
    ) VALUES (
        10, N'employee.attendance_type', N'Personnel', N'班次', N'string', 2, 10, 1, 
        N'考勤班次（员工排班信息，可推断作息规律）', GETDATE(), GETDATE(), 'system'
    );
    PRINT '✅ 已添加 班次 (employee.attendance_type) 字段权限记录';
END
ELSE
BEGIN
    PRINT 'ℹ️ 班次 字段权限记录已存在';
END;

