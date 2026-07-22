# v2.13.96 智能抄表重命名 — 生产 DB 更新脚本
# 用途：将 SysPermission 中"抄表记录"相关菜单/权限名升级为"智能抄表"
#     将 SysRole PDA 操作员描述中的"抄表模块"改为"智能抄表模块"
#
# 执行方式：服务器管理员在生产 DB 上运行
# 安全：仅 UPDATE 文本字段，不动 Schema / 路由 / 权限码
#
# 已包含：v2.13.96 重命名 — 菜单种子 ID 14 + 子权限 ID 32/33/34 + 角色 3 描述

$server = "192.168.1.237"
$database = "WaterMeterDB"

Write-Host "=== v2.13.96 智能抄表重命名 SQL 脚本 ===" -ForegroundColor Cyan

$sql = @"
USE [$database];

-- 主菜单（Id=14）
UPDATE SysPermission SET PermissionName = '智能抄表' WHERE Id = 14 AND PermissionName = '抄表记录';
-- 按钮权限（Id=32/33/34）
UPDATE SysPermission SET PermissionName = '修正智能抄表' WHERE Id = 32;
UPDATE SysPermission SET PermissionName = '删除智能抄表' WHERE Id = 33;
UPDATE SysPermission SET PermissionName = '导出智能抄表' WHERE Id = 34;
-- PDA 操作员角色描述（Id=3）
UPDATE SysRole SET Description = REPLACE(Description, '抄表模块', '智能抄表模块') WHERE Id = 3;

SELECT 'v2.13.96 UPDATE DONE' AS Result;
"@

Write-Host $sql -ForegroundColor Yellow
Write-Host "`n请在 SQL Server Management Studio 中执行上述脚本，或使用 sqlcmd。" -ForegroundColor Green
