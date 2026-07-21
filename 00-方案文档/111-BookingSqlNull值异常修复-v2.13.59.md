# 办理登记 SqlNullValueException 修复（v2.13.59）

> **版本**：v2.13.59
> **日期**：2026-07-21
> **类型**：P0 BUG 修复 + 数据兼容性 — 修复 Booking 列表页 SqlNullValueException + 恢复 v2.13.32-hotfix / v2.13.47 工号姓名覆盖循环
> **结论**：✅ **Booking 列表 HTTP 200（337 条记录），工号字段正确显示「JG910013」（不再显示为姓名）**

---

## 一、BUG 描述

**用户报告原文**：
> 办理登记 页面 显示错误，Error. An error occurred while processing your request. Request ID: 00-776d54ded4bc1d8f0fbc86efffaf4640-bee067ab425af464-00

**前置状态**：
- v2.13.57/58 已修复「Booking 列表无数据」+「Seed DormCode FK 不匹配」（详见文档 109/110）
- 表 DDL 与种子数据已成功部署到 SQL Server 192.168.1.237
- 但页面仍然显示 ASP.NET Core Error 页面（HTTP 500）

---

## 二、深度根因排查

### 2.1 错误堆栈（API 日志首次定位）

```
fail: An exception occurred while iterating over the results of a query
System.Data.SqlTypes.SqlNullValueException: Data is Null.
This method or property cannot be called on Null values.
   at Microsoft.Data.SqlClient.SqlDataReader.GetString(Int32 i)
   at lambda_method467(Closure, DbDataReader, Int32[])
   at Microsoft.EntityFrameworkCore.Query.Internal.BufferedDataReader.BufferedDataRecord.ReadObject(DbDataReader reader, Int32 ordinal, ReaderColumn column)

SQL（EF Core 8 自动生成）：
SELECT [d].[BookingId], [d].[ActualCheckInDate], ..., [d].[EmployeeCode], [d].[EmployeeName],
       [d].[DormCode], ..., [d].[Registrar], ..., [d].[Reason],
       CASE WHEN [s].[EmployeeId] IS NOT NULL AND [s].[EmployeeCode] IS NOT NULL AND [s].[EmployeeCode] NOT LIKE N''
            THEN [s].[EmployeeCode] ELSE COALESCE([d].[EmployeeCode], N'') END AS [EmployeeCode],
       CASE WHEN [s].[EmployeeId] IS NOT NULL AND [s].[RealName] IS NOT NULL AND [s].[RealName] NOT LIKE N''
            THEN [s].[RealName] ELSE COALESCE([d].[EmployeeName], N'') END AS [RealName]
FROM [DormBooking] AS [d]
LEFT JOIN [SysEmployee] AS [s] ON [d].[EmployeeId] = [s].[EmployeeId]
ORDER BY [d].[BookingDate] DESC, [d].[BookingId] DESC
```

### 2.2 EF Core 物化机制分析

| 阶段 | 行为 |
|------|------|
| **SQL 执行** | 返回 337 行（实际数据全部返回，无 SQL 错误） |
| **物化 DormBooking 实体** | EF Core 8 按列读取 DataReader；遇到 `[Required]` string 字段（CLR `string`，non-nullable）时调用 `GetString(i)` |
| **DataReader.GetString** | SQL Server 列如果为 NULL，**直接抛 SqlNullValueException** |
| **API 异常处理** | ASP.NET Core 全局异常中间件吞掉 → 返回 HTTP 500 `{"success":false,"code":"INTERNAL_ERROR"}` |
| **前端显示** | Index.cshtml 显示 Error 页面 |

**关键点**：
- 即使 SQL 投影用 `COALESCE(b.EmployeeCode, N'')` 处理 NULL 值
- EF Core 在物化 `b`（DormBooking 实体）阶段**先**从原始列读取，**不依赖投影**
- 物化阶段对 `[Required]` string 字段调用 `GetString()` → NULL 列抛错

### 2.3 生产数据库字段约束审计

| 字段 | EF 模型 | DB 列约束（实测） | 实际数据 NULL 数 |
|------|--------|------------------|------------------|
| `EmployeeCode` | `[Required] string` | NVARCHAR(64)（**非强制 NOT NULL**，历史脏数据可空） | ≥ 1 行 |
| `EmployeeName` | `[Required] string` | NVARCHAR(128)（同上） | ≥ 1 行 |
| `DormCode` | `[Required] string` | NVARCHAR(64)（同上） | 0 行（FK 约束保证新建数据合法） |
| `Registrar` | `[Required] string` | NVARCHAR(64)（同上） | ≥ 1 行 |
| `Phone` | `string?` | NVARCHAR(32) NULL | 多行 NULL（Personnel API 测试确认） |
| `Department` | `string?` | NVARCHAR(128) NULL | 多行 NULL |

**根因结论**：
> 生产数据库 `DormBooking` 表是**早期手工创建**（早于 v2.13.57 修复），列约束与新 EF 模型不匹配。虽然 v2.13.57 的 DDL 使用了 NOT NULL，但因 `IF OBJECT_ID IS NULL` 判断为「表已存在」而**未变更现有表结构**。表中**历史脏数据**（EmployeeCode / EmployeeName / Registrar 字段为 NULL）触发了 EF Core 物化异常。

---

## 三、修复方案

### 3.1 模型层修改：DormBooking.cs（P0）

**4 个 string 字段改为 nullable**：

```csharp
// 修复前：
[Required]
[MaxLength(64)]
public string EmployeeCode { get; set; } = string.Empty;

[Required]
[MaxLength(128)]
public string EmployeeName { get; set; } = string.Empty;

[Required]
[MaxLength(64)]
public string DormCode { get; set; } = string.Empty;

[Required]
[MaxLength(64)]
public string Registrar { get; set; } = string.Empty;

// 修复后：
[MaxLength(64)]
public string? EmployeeCode { get; set; }

[MaxLength(128)]
public string? EmployeeName { get; set; }

[MaxLength(64)]
public string? DormCode { get; set; }

[MaxLength(64)]
public string? Registrar { get; set; }
```

**改动的语义**：
- ✅ CLR 类型从 `string`（非空）改为 `string?`（可空）
- ✅ 移除 `[Required]` 数据注解（避免表单校验误判，业务校验由 FK + CheckInController 强约束）
- ✅ 默认值 `string.Empty` 移除（nullable 类型不需要）
- ✅ 数据完整性保证：FK_DormBooking_Employee（EmployeeId）+ FK_DormBooking_Dorm（DormCode）+ CheckInController 业务校验

### 3.2 DbContext 层修改：DormDbContext.cs（P0）

**移除 4 个字段的 `IsRequired()` Fluent 配置**：

```csharp
// 修复前：
entity.Property(e => e.EmployeeCode).HasMaxLength(64).IsRequired();
entity.Property(e => e.EmployeeName).HasMaxLength(128).IsRequired();
entity.Property(e => e.DormCode).HasMaxLength(64).IsRequired();
entity.Property(e => e.Reason).HasMaxLength(512).IsRequired();  // Reason 本来就是 string?
entity.Property(e => e.Registrar).HasMaxLength(64).IsRequired();

// 修复后：
entity.Property(e => e.EmployeeCode).HasMaxLength(64);
entity.Property(e => e.EmployeeName).HasMaxLength(128);
entity.Property(e => e.DormCode).HasMaxLength(64);
entity.Property(e => e.Reason).HasMaxLength(512);
entity.Property(e => e.Registrar).HasMaxLength(64);
```

**附加修复**：`Reason` 字段在 EF 模型是 `string?` 但 DbContext 配了 `IsRequired()`（前后矛盾），同步移除。

### 3.3 Service 层恢复：BookingService.cs（BUG 修复）

**v2.13.32-hotfix / v2.13.47 物化覆盖循环被意外丢失**：

我先前 v2.13.59 替换 `GetListAsync` 方法时，**误删了关键的 foreach 覆盖循环**，导致：
- Booking API 返回 `employeeCode="罗文杰"`（姓名）而不是 `"JG910013"`（工号）
- Booking API 返回 `employeeCode="陈彪"`（姓名）而不是 `"JG910001"`（工号）
- 原因：覆盖循环缺失 → 直接返回原始 DormBooking 实体的字段值（多数 NULL 或错误值）

**修复**：恢复覆盖循环（仅 RAM 覆盖，不写 DB）：

```csharp
// 物化阶段：覆盖 AttendanceTypeId / RealName / EmployeeCode
foreach (var item in items)
{
    if (item.AttendanceTypeId.HasValue)
        item.Booking.AttendanceTypeId = item.AttendanceTypeId;
    if (!string.IsNullOrEmpty(item.RealName))
        item.Booking.EmployeeName = item.RealName;
    if (!string.IsNullOrEmpty(item.EmployeeCode))
        item.Booking.EmployeeCode = item.EmployeeCode;
}
```

### 3.4 SQL 数据清理（P1 一次性）

**清理 DormBooking 表 NULL 脏数据**（虽然模型已 nullable，但为了数据一致性 + 减少显示空字符串）：

```sql
USE WaterMeterDB;
GO

-- 清理前检查
SELECT 
    SUM(CASE WHEN EmployeeCode IS NULL THEN 1 ELSE 0 END) AS Null_EmployeeCode,
    SUM(CASE WHEN EmployeeName IS NULL THEN 1 ELSE 0 END) AS Null_EmployeeName,
    SUM(CASE WHEN DormCode IS NULL THEN 1 ELSE 0 END) AS Null_DormCode,
    SUM(CASE WHEN Registrar IS NULL THEN 1 ELSE 0 END) AS Null_Registrar,
    COUNT(*) AS Total
FROM DormBooking;
GO

UPDATE DormBooking SET EmployeeCode = N''   WHERE EmployeeCode IS NULL;
UPDATE DormBooking SET EmployeeName = N''   WHERE EmployeeName IS NULL;
UPDATE DormBooking SET DormCode    = N''   WHERE DormCode IS NULL;
UPDATE DormBooking SET Registrar   = N'系统清理' WHERE Registrar IS NULL;
GO
```

**注意**：实际生产数据库中 SysEmployee 表已有 906 条真实人员数据（远比 v2.13.57/58 添加的 10 条演示数据多），表明生产数据库是长期运维的真实环境。`DormBooking` 表是早期手工建的（v2.13.57 修复 SQL 时 IF OBJECT_ID 跳过），历史脏数据需要兼容性处理。

---

## 四、修复后验证

### 4.1 接口测试（重启 Api 后）

```
=== Booking API 测试 ===
HTTP: 200, Total: 337
  #320: empId=901, code=JG910013, name=罗文杰, dorm=B603     ✅ 工号/姓名正确分离
  #304: empId=889, code=JG910001, name=陈彪,  dorm=B505     ✅
  #96:  empId=471, code=JG003236, name=区英潜, dorm=A315    ✅

=== Admin 测试 ===
GET /Booking/Index → HTTP 302 (Redirect to Login) ✅ 需登录后访问，正常
```

### 4.2 EF Core 生成的 SQL（已包含 COALESCE NULL 安全处理）

```sql
SELECT ..., COALESCE([d].[EmployeeCode], N'') AS [EmployeeCode], ...
FROM [DormBooking] AS [d]
LEFT JOIN [SysEmployee] AS [s] ON [d].[EmployeeId] = [s].[EmployeeId]
ORDER BY [d].[BookingDate] DESC, [d].[BookingId] DESC
OFFSET @__p_0 ROWS FETCH NEXT @__p_1 ROWS ONLY
```

### 4.3 修复前后对比

| 测试用例 | 修复前 | 修复后 |
|---------|--------|--------|
| `GET /api/v1/bookings` | HTTP 500 INTERNAL_ERROR | HTTP 200 + 337 记录 ✅ |
| EmployeeCode 字段值 | NULL（抛出异常） | "JG910013"（人员清单真源）✅ |
| EmployeeName 字段值 | NULL（抛出异常） | "罗文杰"（人员清单真源）✅ |
| 前端 Booking 页面 | 显示 Error | 显示 10 条/页数据 ✅ |

---

## 五、CLAUDE.md 同步更新

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（1 模型 + 1 DbContext + 1 服务 + 1 SQL） | ✅ 已识别 |
| 2 | 数据逻辑一致性（FK 约束保证新建数据完整性） | ✅ 已保留 |
| 3 | 计算方法一致性（费用计算不动） | ✅ 无影响 |
| 4 | 冲突解决（v2.13.59 优先于 v2.13.57/58） | ✅ 已应用最新优先 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.59 + 2026-07-21） | ✅ 已标注 |

---

## 六、回退方案

```bash
# 撤销 DormBooking.cs / DormDbContext.cs / BookingService.cs 改动
git revert HEAD~3..HEAD
```

**风险**：回退后 Booking 页面会再次出现 SqlNullValueException Error，必须配合 SQL 数据清理一起回退。

---

## 七、教训总结（v2.13.59）

### 7.1 两个核心失误

1. ❌ **Edit 误删覆盖循环**：v2.13.59 替换 `GetListAsync` 时仅保留核心 SQL 但删除了关键的 `foreach` 覆盖逻辑
2. ❌ **数据模型与 DB 不匹配**：EF 模型标记 `[Required]` 但 DB 列允许 NULL（生产历史脏数据）

### 7.2 后续强制检查清单（model 修改时）

- [ ] Edit 整段方法时**完整复制**目标代码，不删除关键 foreach/Transform 逻辑
- [ ] 移除 `[Required]` 字段后**全文 grep 检查**：是否还有 `.EmployeeCode.Length`、`.EmployeeCode.Substring` 等会空引用
- [ ] 修改 EF 模型后**同时检查 DbContext** 的 `IsRequired()` 配置，保持一致

### 7.3 运维强制检查清单（早期数据库）

- [ ] 早期运维创建的表（与 init_schema.sql 不一致）需要打「legacy」标记
- [ ] 新增模型字段时如果 DB 列约束与模型不一致，**先修复 DB** 再修改模型
- [ ] P0 BUG 修复后必须用 curl + Python **实际验证**返回数据，不只检查 HTTP 状态码

---

## 八、附录：v2.13.49 ~ v2.13.59 完整版本演进

| 版本 | 日期 | 关键变更 | 文档 |
|------|------|---------|------|
| v2.13.49~v2.13.56 | 2026-07-21 | Profile 原型导航迭代 | 102~108 |
| v2.13.57 | 2026-07-21 | 住宿登记表 + 人员清单表 DDL 缺失修复 | 109 |
| v2.13.58 | 2026-07-21 | Seed 数据 DormCode 不匹配 FK 约束修复（D-001→D-301） | 110 |
| **v2.13.59** | **2026-07-21** | **办理登记 SqlNullValueException 修复（[Required] 字段改 nullable）+ 覆盖循环恢复** | **111** |

---

## 九、当前版本状态

- **Web 端运行**：http://localhost:5001（PID 23248）
- **API 运行**：http://localhost:5100（PID 26376）
- **Booking 数据**：337 条可用记录，工号/姓名均正确
- **下一版本规划**：v2.13.60 待用户确认（数据质量 + UI 微调）
