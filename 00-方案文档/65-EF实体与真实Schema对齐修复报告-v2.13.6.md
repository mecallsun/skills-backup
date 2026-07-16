# EF 实体与真实 SQL Server Schema 对齐修复报告（核心模块）— v2.13.6

> **版本**：v2.13.6
> **日期**：2026-07-16
> **作者**：Claude Opus 4.8 (1M context)
> **范围**：宿舍 / 办理登记 / 人员 / 抄表 四大核心业务模块 —— EF Core 实体映射与真实 SQL Server（192.168.1.237 / WaterMeterDB）schema 对齐，消除 HTTP 500
> **真理源**：`init_schema.sql`（v2.13.3，实时探测 DDL）
> **关联**：64-详情编辑页路由链接修复验证报告-v2.13.5、CLAUDE.md §8.1 冒烟测试

---

## 1. 背景与根因

冒烟测试发现：Api（默认 `Database:Provider=SqlServer`）多个核心端点返回 HTTP 500，而 Admin（SQLite）正常。根因是 **EF Core 实体模型与真实 SQL Server schema 系统级不一致**，此前仅在 SQLite 下被掩盖（EF 按实体约定自建 SQLite 表，列名自洽）：

| 不一致类型 | 示例 |
|-----------|------|
| **主键列名** | 实体统一 `Id`，真实表为 `DormId`/`BookingId`/`RecordId`/`EmployeeId` 等 |
| **普通列名** | DormBooking 实体 `Type` ↔ 真实 `BookingType` |
| **列类型** | DormBooking `BookingType`/`Status` 真实为 TINYINT，实体为 int |
| **多余属性** | SysEmployee 实体 `Team`(string)，真实表仅有 `TeamId`(int)，无 `Team` 列 |

## 2. 修复内容（DormManage.Shared/Data/DormDbContext.cs）

### 2.1 主键列名映射（HasColumnName）

| 实体 | 真实主键列 | 映射 |
|------|-----------|------|
| Dorm | DormId | `Property(e=>e.Id).HasColumnName("DormId")` |
| DormBooking | BookingId | `HasColumnName("BookingId")` |
| MeterRecord | RecordId(BIGINT) | `HasColumnName("RecordId")`（实体 int，取值在 int 范围内安全） |
| SysEmployee | EmployeeId | `HasColumnName("EmployeeId")` |

### 2.2 普通列名与类型（DormBooking）

- `Type` → `HasColumnName("BookingType").HasConversion<byte>()`（列名 + TINYINT）
- `Status` → `HasConversion<byte>()`（TINYINT）

### 2.3 多余属性忽略（SysEmployee）

- `Ignore(e => e.Team)`：真实表无 `Team` 字符串列（仅 `TeamId` FK）。

### 2.4 抄表路由澄清

抄表列表真实路由为 `/api/meter/records`（非 `/api/meter`），此前 404 系路径误用，非代码缺陷。MeterRecord 实体列名已全部对齐，`Status` 本就是 `byte`（匹配 TINYINT），仅受益于 2.1 主键映射。

## 3. 验证结果（连真实 SQL Server + SQLite 回归）

### 3.1 SQL Server 核心端点 7/7 通过

| 端点 | 修复前 | 修复后 |
|------|--------|--------|
| `/api/dorms` | 500 列名'Id' | ✅ 200 |
| `/api/v1/bookings` | 500 列名'Type' | ✅ 200 |
| `/api/v1/personnel` | 500 列名'Team' | ✅ 200 |
| `/api/meter/records` | 404(路由) | ✅ 200 |
| `/api/meter/months` | — | ✅ 200 |
| `/api/v1/system/dbhealth/quick` | 200 | ✅ 200 |
| `/api/v1/pda/coverage` | 200 | ✅ 200 |

后台服务 `DictionaryFallbackService.BatchNormalizeEmployeesAsync`（依赖 SysEmployee）修复前启动即抛"列名 'Id' 无效"，修复后无异常。

### 3.2 SQLite（Admin）回归

列名映射变更后旧 `dorm.db` 结构过时，删除由 `EnsureCreated` 重建。重建后：登录 302→成功，`/`、`/Dorms`、`/Personnel`、`/Booking` 均 200，无回退。

> ⚠️ 部署提示：升级到本版本后，SQLite 开发库需删除 `dorm.db` 让其按新映射重建（生产 SQL Server 无需变更，本次仅对齐 EF 映射到既有真实表）。

## 4. 遗留项（需产品决策，未纳入本次）

### 4.1 Sys*/RBAC/认证子系统与真实库结构性分裂
RBAC 为 v2.13.x 基于 SQLite 新增，与真实 WaterMeterDB 深度分裂：
- `/api/v1/auth/roles` → 500 `对象名 'SysRoles' 无效`（表名约定错配，需 ToTable("SysRole")）
- `/api/v1/auth/users` → 500 主键 `Id`→`UserId` + 列名分裂（实体 `UserName` vs 真实 `Username`；实体含 `EmployeeId`/`SortOrder` 等真实表无的列；真实表含 `Salt/Mobile/Email/IsLocked/FailedLoginCount/LastLoginAt/LastLoginIp` 实体无）
- **真实库缺表**：`SysPermission`、`SysRolePermission`、`SysUserFilterCache`、`SysSystemIntegration`、`AppVersion`、`SysIntegration`（实体有 DbSet 但真理源无对应表）→ RBAC 权限体系在 SQL Server 无法直接运行
- 结构性键差异：`SysUserRole` 真实复合主键 `(UserId,RoleId)`（实体用 `Id`）；`SysConfig` 真实自然主键 `ConfigKey`（实体用 `Id`）

**决策问题**：RBAC 是否需在 SQL Server 运行？若是 → 需将缺失表补入真理源 DDL 并重构 Sys* 实体/复合键；若否 → RBAC 仅 SQLite，需在架构文档明确 provider 边界。

### 4.2 班组（Team）显示/筛选
`SysEmployee.Team`(string) 已 Ignore；`PersonnelService` 的 `Where(e=>e.Team==team)` 班组筛选需改为按 `TeamId` 关联 Team 表 —— 属后续增强。

### 4.3 CLAUDE.md §8.1 冒烟清单订正
清单中的 `/health`、`/api/v1/xx/dictionaries` 端点实际不存在；真实健康检查为 `/api/v1/system/dbhealth/quick`。已在 CLAUDE.md 订正。
