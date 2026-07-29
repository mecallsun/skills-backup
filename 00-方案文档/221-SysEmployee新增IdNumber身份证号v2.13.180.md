# v2.13.180 — SysEmployee 新增 IdNumber 字段（身份证号）

**日期**：2026-07-26
**核心变更**：人员清单 SysEmployee 表增加 IdNumber 列（NVARCHAR(18)），字段权限清单扩展到 19 项

---

## 一、需求背景

用户原话：
> 在隐私字段权限清单中，增加:员工类型、入职日期、离职日期、身份证号（新增人员清单表字段）

**关键决策**：身份证号（IdNumber）在原 SysEmployee 表中**不存在**，需要：
1. EF Core 实体 `SysEmployee.cs` 新增 `IdNumber` 属性
2. DB schema `01_DDL_Schema.sql` 新增 `IdNumber NVARCHAR(18) NULL` 列
3. 启动期 ALTER TABLE 迁移（idempotent IF NOT EXISTS）

---

## 二、新增字段清单（共 4 项）

| ID | FieldKey | 字段名 | 实体属性 | DB 列 | 敏感等级 |
|----|----------|--------|----------|-------|---------|
| 16 | employee.idnumber | 身份证号 | `IdNumber` | `IdNumber NVARCHAR(18)` | 1（极高 PII） |
| 17 | employee.hiredate | 入职日期 | `HireDate`（已有） | `HireDate DATE`（已有） | 3 |
| 18 | employee.leavedate | 离职日期 | `LeaveDate`（已有） | `LeaveDate DATE`（已有） | 3 |
| 19 | employee.employeetype | 员工类型 | `EmployeeType`（已有） | `EmployeeType NVARCHAR(50)`（已有） | 2 |

> HireDate/LeaveDate/EmployeeType 在原 SysEmployee 已存在；只有 **IdNumber** 是真正新增字段。
> FieldKey 命名约定：`employee.{DB 列小写}`

---

## 三、核心改动

### 3.1 SysEmployee.cs（实体新增）

```csharp
/// <summary>
/// v2.13.180 新增：身份证号码（NVARCHAR(18)，极高 PII 字段）
/// 加入字段权限清单（employee.idnumber，敏感等级=1）
/// </summary>
[MaxLength(18)]
public string? IdNumber { get; set; }
```

### 3.2 01_DDL_Schema.sql（DB schema 新增列）

```sql
HireDate           DATE           NULL,
LeaveDate          DATE           NULL,
IdNumber           NVARCHAR(18)   NULL,               -- v2.13.180 新增：身份证号（极高 PII）
BedNo              INT            NULL,
```

### 3.3 DatabaseInitializer.cs（启动期 ALTER TABLE 迁移）

**新方法 `MigrateEmployeeIdNumberColumnAsync`**：

```csharp
const string sqlServerCheckAndAlter = @"
    IF COL_LENGTH('SysEmployee', 'IdNumber') IS NULL
    BEGIN
        ALTER TABLE [dbo].[SysEmployee] ADD [IdNumber] NVARCHAR(18) NULL;
        PRINT '[v2.13.180] SysEmployee.IdNumber 列已新增';
    END";

var affected = await db.Database.ExecuteSqlRawAsync(sqlServerCheckAndAlter, ct);
```

**集成到 InitializeAsync 启动流程**：第 4 步（IdNumber 列迁移）→ 第 5 步（种子字典）→ 第 6 步（admin）→ 第 7 步（字段权限）→ 第 8 步（AppVersion）

### 3.4 字段权限清单扩展（v2.13.180）

- `DormDbContext.HasData` 增加 Id=16-19 共 4 条 SysFieldPermission seed
- `DatabaseInitializer.MigrateFieldPermissionAsync` 启动 SQL 增加 4 行 IF NOT EXISTS
- `expectedFieldPermCount = 19`（15 → 19）
- UI 徽章「RBAC 四级权限 · 19 字段」+ 说明文字更新

---

## 四、改造前后对比

| 维度 | v2.13.92-179 | **v2.13.180** |
|------|-------------|--------------|
| SysEmployee 表字段 | 18 个核心字段（无 IdNumber） | **19 个**（新增 IdNumber） |
| SysFieldPermission 字段清单 | 5 → 15 | **19**（+4：idnumber/hiredate/leavedate/employeetype） |
| 字段权限可见字段数 | 15 | **19**（含身份证号） |
| 启动迁移步骤 | 1 步（FieldPermission） | **2 步**（IdNumber 列 + FieldPermission） |

---

## 五、改动文件清单

| 文件 | 改动 |
|------|------|
| `DormManage.Shared/Models/SysEmployee.cs` | 新增 `IdNumber` 属性（[MaxLength(18)] nullable string） |
| `01-Database/01_DDL_Schema.sql` | 新增 `IdNumber NVARCHAR(18) NULL` 列 |
| `DormManage.Shared/Services/DatabaseInitializer.cs` | 新增 `MigrateEmployeeIdNumberColumnAsync` 方法 + `StartupReport.IdNumberColumnMigrated` 字段 + 第 4 步集成 |
| `DormManage.Shared/Data/DormDbContext.cs` | HasData seed 新增 Id=16-19 共 4 条 SysFieldPermission |
| `DormManage.Admin/Pages/Settings/_FieldPermissionPanel.cshtml` | 标题徽章「19 字段」+ 说明文字 |

---

## 六、部署升级流程

### 全新安装（空 DB）
1. `init_schema.sql` 创建 SysEmployee 表（**含** IdNumber 列）
2. `DatabaseInitializer` 启动期：`MigrateEmployeeIdNumberColumnAsync` 检测 IdNumber 已存在 → 跳过 ALTER
3. `MigrateFieldPermissionAsync` 插入 19 条 SysFieldPermission seed

### 升级部署（v2.13.179 → v2.13.180）
1. **启动前**：SysEmployee 表无 IdNumber 列
2. **首次启动**：`MigrateEmployeeIdNumberColumnAsync` 检测列不存在 → `ALTER TABLE ADD IdNumber NVARCHAR(18) NULL`
3. **同次启动**：`MigrateFieldPermissionAsync` 插入 4 条新 seed（idnumber/hiredate/leavedate/employeetype）
4. **结果**：SysEmployee 增加 1 列 + SysFieldPermission 增加 4 行

---

## 七、永久教训

1. **隐私字段必须先有 DB 列才能保护**——v2.13.92-179 的 employee.idnumber 只是 FieldKey 字符串，没有对应 DB 列
2. **FieldKey 与 DB 列命名应保持一致**——`employee.idnumber` ↔ `IdNumber NVARCHAR(18)`
3. **ALTER TABLE 必须幂等**——`IF COL_LENGTH('SysEmployee', 'IdNumber') IS NULL` 守卫可重复执行
4. **启动期迁移是数据安全的关键**——不能要求 DBA 手动执行 SQL，部署即生效
5. **多步启动迁移的顺序很重要**——先 ADD COLUMN 再 INSERT seed（避免列不存在时报错）

---

## 八、关联文档

- `220-字段权限清单全量扩展v2.13.180.md` — 字段权限清单扩展（15 → 19）
- `145-字段权限隐私保护-v2.13.92.md` — 原始 5 字段设计
- `216-隐私字段保护语义翻转v2.13.176.md` — deny-by-default 语义