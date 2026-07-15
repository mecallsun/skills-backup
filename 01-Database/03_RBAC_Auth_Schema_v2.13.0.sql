-- ============================================================
-- 金戈宿舍管理系统 - 数据库迁移脚本 v2.13.0
-- 主题：认证权限体系（用户/角色/权限 RBAC）
-- 日期：2026-07-14
-- ============================================================

-- 1. 系统用户表
CREATE TABLE SysUser (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserName NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    DisplayName NVARCHAR(50) NOT NULL,
    EmployeeId INTEGER,
    Email NVARCHAR(100),
    Phone NVARCHAR(20),
    IsActive INTEGER DEFAULT 1,
    LastLoginTime DATETIME,
    LastLoginIp NVARCHAR(45),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (EmployeeId) REFERENCES SysEmployee(EmployeeId) ON DELETE SET NULL
);

-- 2. 系统角色表
CREATE TABLE SysRole (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RoleCode NVARCHAR(50) NOT NULL UNIQUE,
    RoleName NVARCHAR(50) NOT NULL,
    Description NVARCHAR(200),
    SortOrder INTEGER DEFAULT 0,
    IsActive INTEGER DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 3. 系统权限表
CREATE TABLE SysPermission (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PermissionCode NVARCHAR(100) NOT NULL UNIQUE,
    PermissionName NVARCHAR(100) NOT NULL,
    PermissionType INTEGER DEFAULT 1,
    ParentId INTEGER DEFAULT 0,
    Route NVARCHAR(200),
    Icon NVARCHAR(50),
    SortOrder INTEGER DEFAULT 0,
    IsActive INTEGER DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ParentId) REFERENCES SysPermission(Id) ON DELETE RESTRICT
);

-- 4. 用户-角色关联表
CREATE TABLE SysUserRole (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    RoleId INTEGER NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES SysUser(Id) ON DELETE CASCADE,
    FOREIGN KEY (RoleId) REFERENCES SysRole(Id) ON DELETE CASCADE
);

-- 5. 角色-权限关联表
CREATE TABLE SysRolePermission (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RoleId INTEGER NOT NULL,
    PermissionId INTEGER NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(RoleId, PermissionId),
    FOREIGN KEY (RoleId) REFERENCES SysRole(Id) ON DELETE CASCADE,
    FOREIGN KEY (PermissionId) REFERENCES SysPermission(Id) ON DELETE CASCADE
);

-- ============================================================
-- 索引
-- ============================================================
CREATE INDEX IX_SysUser_UserActive ON SysUser(UserName, IsActive);
CREATE INDEX IX_SysUserRole_UserId ON SysUserRole(UserId);
CREATE INDEX IX_SysRolePermission_RoleId ON SysRolePermission(RoleId);
CREATE INDEX IX_SysPermission_Type ON SysPermission(PermissionType, IsActive);

-- ============================================================
-- 种子数据
-- ============================================================

-- 预置角色
INSERT INTO SysRole (Id, RoleCode, RoleName, Description, SortOrder, IsActive, CreatedAt) VALUES
(1, 'admin', '管理员', '系统超级管理员，拥有全部权限', 0, 1, '2026-07-14 00:00:00'),
(2, 'finance', '财务', '财务管理角色，可查看费用标准和账单', 1, 1, '2026-07-14 00:00:00'),
(3, 'pda_operator', 'PDA 操作员', 'PDA 抄表操作员，仅可访问抄表模块', 2, 1, '2026-07-14 00:00:00'),
(4, 'viewer', '访客', '只读角色，仅可查看首页数据看板', 3, 1, '2026-07-14 00:00:00');

-- 预置权限
INSERT INTO SysPermission (Id, PermissionCode, PermissionName, PermissionType, ParentId, Route, Icon, SortOrder, IsActive, CreatedAt) VALUES
(1, 'home:view', '首页看板', 1, 0, '/', 'bi-speedometer2', 0, 1, '2026-07-14 00:00:00'),
(2, 'booking:view', '办理登记', 1, 0, '/Booking', 'bi-clipboard-check', 1, 1, '2026-07-14 00:00:00'),
(3, 'booking:checkin', '入住办理', 2, 2, '/Booking/CheckIn', 'bi-box-arrow-in-right', 2, 1, '2026-07-14 00:00:00'),
(4, 'booking:checkout', '退房办理', 2, 2, '/Booking/CheckOut', 'bi-box-arrow-right', 3, 1, '2026-07-14 00:00:00'),
(5, 'dorm:view', '宿舍管理', 1, 0, '/Dorms', 'bi-building', 2, 1, '2026-07-14 00:00:00'),
(6, 'dorm:create', '新增宿舍', 2, 5, '', '', 4, 1, '2026-07-14 00:00:00'),
(7, 'dorm:edit', '编辑宿舍', 2, 5, '', '', 5, 1, '2026-07-14 00:00:00'),
(8, 'dorm:delete', '删除宿舍', 2, 5, '', '', 6, 1, '2026-07-14 00:00:00'),
(9, 'personnel:view', '人员清单', 1, 0, '/Personnel', 'bi-people', 3, 1, '2026-07-14 00:00:00'),
(10, 'personnel:import', '导入员工', 2, 9, '/Personnel/Import', 'bi-upload', 7, 1, '2026-07-14 00:00:00'),
(11, 'billing:view', '费用标准', 1, 0, '/BillingStandard', 'bi-currency-dollar', 4, 1, '2026-07-14 00:00:00'),
(12, 'dormbilling:view', '宿舍账单', 1, 0, '/DormBilling', 'bi-receipt', 5, 1, '2026-07-14 00:00:00'),
(13, 'employeebilling:view', '员工账单', 1, 0, '/EmployeeBilling', 'bi-wallet2', 6, 1, '2026-07-14 00:00:00'),
(14, 'meter:view', '抄表记录', 1, 0, '/Meter', 'bi-gauge', 7, 1, '2026-07-14 00:00:00'),
(15, 'meter:entry', '手动录入', 2, 14, '/Meter/Entry', 'bi-pencil', 8, 1, '2026-07-14 00:00:00'),
(16, 'meter:import', '批量导入', 2, 14, '/Meter/Import', 'bi-upload', 9, 1, '2026-07-14 00:00:00'),
(17, 'basics:view', '基础资料', 1, 0, '/Basics', 'bi-database', 8, 1, '2026-07-14 00:00:00'),
(18, 'settings:view', '系统设置', 1, 0, '/Settings', 'bi-gear', 9, 1, '2026-07-14 00:00:00');

-- 角色-权限关联（管理员：全部权限）
INSERT INTO SysRolePermission (RoleId, PermissionId, CreatedAt) VALUES
(1, 1, '2026-07-14 00:00:00'), (1, 2, '2026-07-14 00:00:00'),
(1, 3, '2026-07-14 00:00:00'), (1, 4, '2026-07-14 00:00:00'),
(1, 5, '2026-07-14 00:00:00'), (1, 6, '2026-07-14 00:00:00'),
(1, 7, '2026-07-14 00:00:00'), (1, 8, '2026-07-14 00:00:00'),
(1, 9, '2026-07-14 00:00:00'), (1, 10, '2026-07-14 00:00:00'),
(1, 11, '2026-07-14 00:00:00'), (1, 12, '2026-07-14 00:00:00'),
(1, 13, '2026-07-14 00:00:00'), (1, 14, '2026-07-14 00:00:00'),
(1, 15, '2026-07-14 00:00:00'), (1, 16, '2026-07-14 00:00:00'),
(1, 17, '2026-07-14 00:00:00'), (1, 18, '2026-07-14 00:00:00');

-- 财务角色
INSERT INTO SysRolePermission (RoleId, PermissionId, CreatedAt) VALUES
(2, 1, '2026-07-14 00:00:00'), (2, 11, '2026-07-14 00:00:00'),
(2, 12, '2026-07-14 00:00:00'), (2, 13, '2026-07-14 00:00:00'),
(2, 17, '2026-07-14 00:00:00'), (2, 18, '2026-07-14 00:00:00');

-- PDA 操作员
INSERT INTO SysRolePermission (RoleId, PermissionId, CreatedAt) VALUES
(3, 1, '2026-07-14 00:00:00'), (3, 14, '2026-07-14 00:00:00'),
(3, 15, '2026-07-14 00:00:00'), (3, 17, '2026-07-14 00:00:00');

-- 访客
INSERT INTO SysRolePermission (RoleId, PermissionId, CreatedAt) VALUES
(4, 1, '2026-07-14 00:00:00');

-- ============================================================
-- 密码说明
-- ============================================================
-- 密码使用 BCrypt 加密存储，不在 SQL 中直接写入明文。
-- 首次运行时，AuthService 会在登录时自动创建默认管理员账户。
-- 默认凭据：admin / admin123
-- ============================================================
