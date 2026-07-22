# v2.13.97 新增人员权限 — 生产 DB 修复脚本
# 用途：补全 personnel:add 按钮权限项，并授予 admin 角色
#     SysPermission 是否已存在由 HasData 自动处理（生产 DB 已经 DDL 过的表不会重新 seed）

$server = "192.168.1.237"
$database = "WaterMeterDB"

Write-Host "=== v2.13.97 personnel:add 修复 SQL 脚本 ===" -ForegroundColor Cyan

$sql = @"
USE [$database];

-- 1) 检查并插入 personnel:add 权限（若已存在则跳过）
IF NOT EXISTS (SELECT 1 FROM SysPermission WHERE PermissionCode = 'personnel:add')
BEGIN
    INSERT INTO SysPermission (PermissionCode, PermissionName, PermissionType, ParentId, Route, Icon, SortOrder, IsActive, CreatedAt)
    VALUES ('personnel:add', N'新增人员', 2, 9, '/Personnel/Create', 'bi-plus-lg', 7, 1, GETDATE());
    PRINT '[v2.13.97] personnel:add 权限已创建';
END
ELSE PRINT '[v2.13.97] personnel:add 已存在';

-- 2) 授权 admin（RoleId=1）
DECLARE @addId INT = (SELECT Id FROM SysPermission WHERE PermissionCode = 'personnel:add');
IF NOT EXISTS (SELECT 1 FROM SysRolePermission WHERE RoleId = 1 AND PermissionId = @addId)
BEGIN
    INSERT INTO SysRolePermission (RoleId, PermissionId, CreatedAt)
    VALUES (1, @addId, GETDATE());
    PRINT '[v2.13.97] admin 已被授权 personnel:add';
END
ELSE PRINT '[v2.13.97] admin 已拥有 personnel:add';

SELECT 'v2.13.97 personnel:add FIX DONE' AS Result;
"@

Write-Host $sql -ForegroundColor Yellow
Write-Host "`n请在 SQL Server Management Studio 中执行上述脚本。" -ForegroundColor Green
