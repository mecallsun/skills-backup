$connString = "Server=192.168.1.237;Database=WaterMeterDB;User Id=__DB_USER__;Password=__DB_PASSWORD__;TrustServerCertificate=True;"
$sql = @"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SysFieldPermission' AND xtype='U')
BEGIN
    CREATE TABLE dbo.SysFieldPermission (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FieldKey NVARCHAR(64) NOT NULL UNIQUE,
        Module NVARCHAR(32) NOT NULL,
        FieldName NVARCHAR(64) NOT NULL,
        FieldType NVARCHAR(16) NULL,
        SensitivityLevel TINYINT NOT NULL DEFAULT 2,
        SortOrder INT NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        Description NVARCHAR(200) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NULL,
        UpdatedBy NVARCHAR(64) NULL
    );
END

IF NOT EXISTS (SELECT * FROM SysPermission WHERE PermissionCode='privacy:field:enable')
    INSERT INTO SysPermission (PermissionCode, PermissionName, PermissionType, ParentId, IsActive, IsSystem, Description, SortOrder, CreatedAt)
    VALUES ('privacy:field:enable', '启用隐私字段保护', 3, 0, 1, 1, '勾选此权限的角色将看不到所有 SysFieldPermission 清单中的字段', 30, GETDATE());

IF NOT EXISTS (SELECT * FROM SysPermission WHERE PermissionCode='settings:fields')
    INSERT INTO SysPermission (PermissionCode, PermissionName, PermissionType, ParentId, Route, Icon, IsActive, IsSystem, Description, SortOrder, CreatedAt)
    VALUES ('settings:fields', '字段权限', 1, 18, '/Settings?tab=fields', 'bi-shield-check', 1, 1, '管理敏感字段清单', 28, GETDATE());

IF NOT EXISTS (SELECT * FROM SysPermission WHERE PermissionCode='fieldpermission:edit')
    INSERT INTO SysPermission (PermissionCode, PermissionName, PermissionType, ParentId, IsActive, IsSystem, Description, SortOrder, CreatedAt)
    VALUES ('fieldpermission:edit', '编辑字段权限', 2, 37, 1, 1, '勾选/取消勾选敏感字段', 29, GETDATE());

-- admin 角色 (Id=1) 关联新权限
IF NOT EXISTS (SELECT * FROM SysRolePermission WHERE RoleId=1 AND PermissionId=(SELECT Id FROM SysPermission WHERE PermissionCode='settings:fields'))
    INSERT INTO SysRolePermission (RoleId, PermissionId, CreatedAt)
    SELECT 1, Id, GETDATE() FROM SysPermission WHERE PermissionCode IN ('settings:fields','fieldpermission:edit','privacy:field:enable')
    AND NOT EXISTS (SELECT 1 FROM SysRolePermission WHERE RoleId=1 AND SysRolePermission.PermissionId=SysPermission.Id);

-- seed 5 字段
IF NOT EXISTS (SELECT * FROM SysFieldPermission WHERE FieldKey='employee.realname')
    INSERT INTO SysFieldPermission (FieldKey, Module, FieldName, SensitivityLevel, SortOrder, IsActive, Description, CreatedAt) VALUES
    ('employee.realname',   'Personnel', '姓名',     1, 1, 1, '员工真实姓名（高 PII）',               GETDATE()),
    ('employee.phone',      'Personnel', '手机号',   1, 2, 1, '联系电话（高 PII）',                   GETDATE()),
    ('employee.employeecode','Personnel', '工号',     2, 3, 1, '公司内唯一标识',                      GETDATE()),
    ('employee.dormcode',   'Personnel', '宿舍房号', 2, 4, 1, '当前入住房号（隐私住址）',             GETDATE()),
    ('employee.remark',     'Personnel', '备注',     2, 5, 1, '自由文本备注（可能含敏感信息）',       GETDATE());

PRINT 'v2.13.92 SysFieldPermission + 3 权限码 + 5 seed 字段已就绪';
"@

try {
    Add-Type -AssemblyName System.Data.SqlClient -ErrorAction Stop
} catch {
    Add-Type -AssemblyName Microsoft.Data.SqlClient -ErrorAction Stop
}

$conn = New-Object System.Data.SqlClient.SqlConnection $connString
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$result = $cmd.ExecuteScalar()
Write-Host $result
$conn.Close()