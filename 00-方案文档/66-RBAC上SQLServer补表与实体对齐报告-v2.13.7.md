# RBAC 上 SQL Server — 补表与实体对齐修复报告 — v2.13.7

> **版本**：v2.13.7
> **日期**：2026-07-16
> **作者**：Claude Opus 4.8 (1M context)
> **范围**：Sys*/RBAC 认证授权子系统 —— 补齐真理源缺失表 + 重构 Sys* 实体，使 RBAC 在 SQL Server 正常运行
> **真理源**：`init_schema.sql`（升级 v2.13.7）
> **迁移脚本**：`01-Database/migrations/v2.13.7-rbac-tables.sql`
> **前置**：65-EF实体与真实Schema对齐修复报告-v2.13.6（核心业务模块）

---

## 1. 背景

v2.13.6 修复了核心业务模块（宿舍/办理/人员/抄表）在 SQL Server 的映射，但遗留 Sys*/RBAC 子系统与真实 WaterMeterDB 的结构性分裂：
- `/api/v1/auth/roles` → 500 `对象名 'SysRoles' 无效`（表名约定错配）
- `/api/v1/auth/users` → 500 `列名 'Id'/'CreatedAt' 无效`（主键 + 列名分裂）
- 真实库缺 `SysPermission`、`SysRolePermission` 表（RBAC 权限体系为 v2.13.x 基于 SQLite 新增）

用户决策：**RBAC 上 SQL Server（补表 + 重构实体）**。

## 2. 真理源补表（init_schema.sql v2.13.7 + 增量迁移脚本）

| 变更 | 内容 |
|------|------|
| SysRole 补列 | `SortOrder INT NOT NULL DEFAULT 0`（Web 端角色列表 `OrderBy(r=>r.SortOrder)` SQL 排序依赖，不能忽略） |
| 新增 SysPermission | 权限/菜单节点表，PK `Id`，列名与 EF 实体 1:1，UQ(PermissionCode)，IX(ParentId) |
| 新增 SysRolePermission | 角色权限关联表，PK `Id`，UQ(RoleId,PermissionId)，FK→SysRole/SysPermission ON DELETE CASCADE |

增量迁移脚本 `v2.13.7-rbac-tables.sql`：**幂等**（`IF NOT EXISTS` 守卫）、**仅新增**（无删除/改数据），已对现网 192.168.1.237/WaterMeterDB 成功执行（SortOrder 列 + 2 表创建确认）。

## 3. 实体与映射重构（DormManage.Shared/Data/DormDbContext.cs）

| 实体 | 对齐动作 |
|------|---------|
| **SysUser** | ToTable("SysUser")；`Id`→`UserId`；`UserName`→`Username`；`Phone`→`Mobile`；`LastLoginTime`→`LastLoginAt`；`Ignore(EmployeeId)`、`Ignore(UpdatedAt)`（真实表无列，代码仅赋值不查询） |
| **SysRole** | ToTable("SysRole")；`Id`→`RoleId`；`SortOrder` 保留（对应新增列） |
| **SysUserRole** | ToTable("SysUserRole")；`Ignore(Id)`；复合主键 `HasKey(UserId,RoleId)`；种子数据去除 `Id` |
| **SysPermission** | ToTable("SysPermission")；PK `Id`（列名与实体 1:1，无需 HasColumnName） |
| **SysRolePermission** | ToTable("SysRolePermission")；PK `Id` |

> 关键原则：`SysUser.Phone/UserName/LastLoginTime` 等属性被业务代码大量引用，**保留 CLR 属性名，仅改列映射**（HasColumnName），避免改动控制器/页面。

## 4. 验证结果（双 provider）

### 4.1 SQL Server（真实 WaterMeterDB，迁移后）

| 端点 | 修复前 | 修复后 |
|------|--------|--------|
| `/api/v1/auth/users` | 500 列名'Id' | ✅ 200（返回真实用户 viewer 等） |
| `/api/v1/auth/roles` | 500 对象名'SysRoles' | ✅ 200（返回真实角色 Admin 等） |
| 核心回归 dorms/bookings/personnel/meter | 200 | ✅ 200（无回归） |

### 4.2 SQLite（Admin，删除 dorm.db 重建）

| 项 | 结果 |
|----|------|
| 登录 admin/admin123（种子含复合键 SysUserRole） | ✅ 302→成功 |
| `/Settings/User`、`/Settings/Role` | ✅ 200 |
| `/`、`/Dorms`、`/Personnel` | ✅ 200 |

## 5. 部署说明

1. **SQL Server 现网库**：执行 `01-Database/migrations/v2.13.7-rbac-tables.sql`（幂等，可安全重复）。
2. **SQLite 开发库**：删除 `dorm.db` 由 `EnsureCreated` 按新映射重建。
3. 升级后 RBAC 权限数据在 SQL Server 为空（现网未跑种子），如需初始权限，另需数据初始化脚本（SysPermission/SysRolePermission 种子）——列为后续。

## 6. 遗留项（非 RBAC，后续）

以下实体仍为 stub 或缺真实表，未在本次 RBAC 范围内，待专项处理：
- **stub 实体**（仅 Id+CreatedAt）：SysConfig（真实自然键 ConfigKey）、SysOpLog（LogId）、PdaDevice（DeviceId）、MeterImage（ImageId）— 需按真实表补全属性与映射
- **缺表 + 完整实体**：AppVersion、SysIntegration（有控制器，真实库无表，需补 DDL）
- **缺表 + stub**：SysUserFilterCache、SysSystemIntegration（筛选缓存/系统集成，需先明确需求再建模）
- RBAC 权限种子数据在 SQL Server 的初始化脚本

## v2.13.109 备注：SQLite Provider 彻底移除

自 v2.13.109 起，DormManage 运行时仅支持 SQL Server。SQLite 代码路径、EF Core SQLite Provider、SQLite 备份恢复逻辑均已移除。历史配置中的 SqlitePath 字段仅为旧配置反序列化兼容，不代表运行时继续支持 SQLite。生产数据库初始化以 init_schema.sql 和 SQL Server 运维脚本为准，不使用 EnsureCreated()。
