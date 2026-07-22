-- ====================================================================
-- v2.13.103 手动 seed 修复脚本（personnel:add 缺失终极修复）
--
-- 适用场景：
--   v2.13.102 一键修复后，权限矩阵 Modal「人员清单」分组仍不显示
--   「新增人员 (personnel:add)」复选框。
--
-- 用法：
--   1. 找到生产 SQLite DB 文件（默认：publish-final/Admin/dorm.db，
--      或 Settings → 数据库连接 中的 SQLitePath）
--   2. 用 DB Browser for SQLite / sqlite3.exe 打开
--   3. 切到「Execute SQL」标签，依次执行本脚本 4 段
--
-- 注意：
--   - INSERT OR IGNORE 幂等：已存在则跳过，重复执行安全
--   - 完成后**必须 Ctrl+Shift+R 硬刷浏览器**（v2.13.102 banner 缓存）
-- ====================================================================

-- ============ 段 1：诊断现状（只读，不会改数据）============
SELECT '1.1 SysPermission 总数' AS Step, COUNT(*) AS Result FROM SysPermission;
SELECT '1.2 SysPermission Id=40 personnel:add 存在' AS Step, COUNT(*) AS Result FROM SysPermission WHERE Id=40;
SELECT '1.3 SysPermission Id=40 完整行' AS Step, * FROM SysPermission WHERE Id=40;
SELECT '1.4 SysRolePermission Id=61 (admin→40) 存在' AS Step, COUNT(*) AS Result FROM SysRolePermission WHERE Id=61;
SELECT '1.5 SysRolePermission Id=61 完整行' AS Step, * FROM SysRolePermission WHERE Id=61;

-- ============ 段 2：完整 SysPermission 列结构（确认 Description 可空）============
PRAGMA table_info('SysPermission');

-- ============ 段 3：强制 INSERT Id=40 + Id=61（幂等）============
-- SysPermission Id=40 personnel:add
INSERT OR IGNORE INTO SysPermission
    (Id, PermissionCode, PermissionName, PermissionType, ParentId, Route, Icon, SortOrder, IsActive, IsSystem, CreatedAt)
VALUES
    (40, 'personnel:add', '新增人员', 2, 9, '/Personnel/Create', 'bi-plus-lg', 7, 1, 0, '2026-07-22 00:00:00');

-- SysRolePermission Id=61 admin → Id=40
INSERT OR IGNORE INTO SysRolePermission
    (Id, RoleId, PermissionId, CreatedAt)
VALUES
    (61, 1, 40, '2026-07-22 00:00:00');

-- ============ 段 4：验证修复结果（必跑，必须看到 1 行）============
SELECT '4.1 SysPermission Id=40 现在存在' AS Step, COUNT(*) AS Result FROM SysPermission WHERE Id=40;
SELECT '4.2 SysRolePermission Id=61 现在存在' AS Step, COUNT(*) AS Result FROM SysRolePermission WHERE Id=61;
SELECT '4.3 admin 权限数（修复前 36，修复后 37）' AS Step, COUNT(*) AS Result FROM SysRolePermission WHERE RoleId=1;

-- ============ 完成后必做 ============
-- 1. 关闭 SQLite 客户端
-- 2. 重启 DormManage.Admin.exe（不要用 TrayApp 启，自己启验证）
-- 3. 浏览器访问 /Settings?tab=roles → Ctrl+Shift+R 硬刷
-- 4. 点 admin 行「权限矩阵」按钮
-- 5. 期望：banner 绿色「SysPermission 4/4 · SysRolePermission 4/4 · SysFieldPermission 5/5」
-- 6. 期望：「人员清单」分组下可见「└ 新增人员 (personnel:add)」复选框，默认勾选
-- 7. 访问 /Personnel → PageHeader「新增」按钮按权限控制显示