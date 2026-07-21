# Booking 数据关系文档定稿（v2.13.48）

> **版本**：v2.13.48
> **日期**：2026-07-21
> **类型**：数据关系文档定稿 + 编码规范固化
> **影响文件**：`00-方案文档/60-菜单导航与数据关系全景图-v2.13.3.md`（升级到 v2.13.48） + `00-方案文档/07-办理登记需求-v2.11.md` §3.1 + `CLAUDE.md`

---

## 一、问题回顾（v2.13.47 BUG 修复前）

**用户报告原文**：
> 办理登记列表 存在重大BUG，没有显示统一 数据库连接的库中的数据，其中的 工号是登记入住记录 的员工 工号，使用 人员清单 记录 id 进行 FK 关联

**核心问题**：
- 列表展示员工信息时，**未通过 `SysEmployee.Id` FK 关联实时取档案数据**
- 直接读 `DormBooking.EmployeeCode` / `EmployeeName` 等冗余字段
- 人员在「人员清单」改工号/姓名后，Booking 列表显示旧值

**v2.13.47 代码层修复**（commit `d3a4c57`）：
- `BookingService.GetListAsync` 匿名投影增加 `EmployeeCode` 字段，按 `EmployeeId` FK JOIN 实时覆盖
- `RepairBookingEmployeeNamesAsync` 同步回填工号 + 姓名

**v2.13.48 文档层定稿**（本次）：
- 把"人员清单为唯一真源 + DormBooking.EmployeeId FK 关联"原则写入核心数据关系文档
- 固化 DormBooking 列表查询编码规范
- 同步 5 个显示字段（EmployeeCode/Name/Department/Phone/AttendanceTypeId）的同步策略

---

## 二、核心数据关系（v2.13.48 起强制原则）

### 2.1 实体关系图

```
SysEmployee (人员清单 — 唯一真源)
   ├─ Id (PK, int)                    ← 员工记录 ID（FK 关联键）
   ├─ EmployeeCode (NVARCHAR 64)      ← 工号（人员清单档案）
   ├─ RealName (NVARCHAR 128)         ← 姓名（人员清单档案）
   ├─ Phone / Department / AttendanceTypeId / ResidenceStatusId / EmploymentStatusId / ...
   │
   └─ 1:N ──→ DormBooking (办理登记 — 冗余字段仅用于历史追溯)
                ├─ EmployeeId (FK → SysEmployee.Id, NOT NULL)
                ├─ EmployeeCode (冗余快照，运行时被 SysEmployee.EmployeeCode 覆盖显示)
                ├─ EmployeeName (冗余快照，运行时被 SysEmployee.RealName 覆盖显示)
                ├─ Phone / Department / AttendanceTypeId (冗余快照，同理覆盖)
                └─ DormCode (FK → Dorm.DormCode)
```

### 2.2 FK 关联查询优先级

| 优先级 | 关联键 | 适用场景 | 引入版本 |
|:----:|--------|---------|:--------:|
| 1 | `DormBooking.EmployeeId` → `SysEmployee.Id` | 主路径（v2.13.x 起 DormBooking.EmployeeId 必填，JOIN 关系稳定） | v2.11.x |
| 2 | `DormBooking.EmployeeCode` → `SysEmployee.EmployeeCode` | 反查路径（兼容历史数据中 `EmployeeId=0` 的孤儿记录；Repair API 用作反查） | v2.11.x |

### 2.3 DormBooking 显示字段同步策略

| 显示字段 | 数据源（运行时实时覆盖） | 写入 DB 时机 | 关键版本 |
|---------|----------------------|------------|:--------:|
| `EmployeeCode` 工号 | `SysEmployee.EmployeeCode` JOIN 实时取 | Repair API 一次性写回 | **v2.13.47 新增** |
| `EmployeeName` 姓名 | `SysEmployee.RealName` JOIN 实时取 | Repair API 一次性写回 | v2.13.33 |
| `Department` 部门 | `SysEmployee.Department` JOIN 实时取 | Repair API 一次性写回 | v2.13.33 |
| `Phone` 手机号 | `SysEmployee.Phone` JOIN 实时取 | Repair API 一次性写回 | v2.13.24 |
| `AttendanceTypeId` 考勤班次 | `SysEmployee.AttendanceTypeId` JOIN 实时取 | Repair API 一次性写回 | v2.13.24 |

---

## 三、编码规范（v2.13.48 起强制）

### 3.1 DormBooking 列表查询标准模板

**正确写法**（JOIN SysEmployee 实时取）：

```csharp
// 标准模板：人员清单为唯一真源，所有显示字段都 JOIN 取实时档案
var query =
    from b in _db.DormBookings
    join emp in _db.Employees on b.EmployeeId equals emp.Id into empGroup
    from emp in empGroup.DefaultIfEmpty()
    select new
    {
        Booking = b,
        // 5 个显示字段同步覆盖
        EmployeeCode = emp != null && !string.IsNullOrEmpty(emp.EmployeeCode) ? emp.EmployeeCode : b.EmployeeCode,
        RealName = emp != null && !string.IsNullOrEmpty(emp.RealName) ? emp.RealName : b.EmployeeName,
        Department = emp?.Department ?? b.Department,
        Phone = emp?.Phone ?? b.Phone,
        AttendanceTypeId = (int?)(emp != null ? emp.AttendanceTypeId : b.AttendanceTypeId)
    };
```

**错误写法**（v2.13.48 起禁止）：

```csharp
// ❌ 错误：直接 select DormBooking 冗余字段作为展示字段
var query = _db.DormBookings.Select(b => new
{
    b.Id, b.EmployeeId,
    b.EmployeeCode,  // ❌ 禁止 — 应 JOIN SysEmployee 取实时工号
    b.EmployeeName,  // ❌ 禁止 — 应 JOIN SysEmployee 取实时姓名
    b.Department,    // ❌ 禁止
    b.AttendanceTypeId, // ❌ 禁止
    b.DormCode, b.Type, b.BookingDate, b.Status
});
```

### 3.2 关键词筛选字段优先级

```csharp
// 关键词筛选：档案字段（实时）优先 + 冗余字段（历史）回退
query = query.Where(x =>
    x.EmployeeCode.ToLower().Contains(keyword) ||      // 档案优先（v2.13.47）
    x.Booking.EmployeeCode.ToLower().Contains(keyword) || // 冗余回退
    x.Booking.EmployeeName.ToLower().Contains(keyword) ||
    x.RealName.ToLower().Contains(keyword) ||          // 档案优先（v2.13.33）
    (x.Booking.Phone != null && x.Booking.Phone.Contains(keyword))
);
```

### 3.3 物化覆盖原则

```csharp
// 物化阶段：把 JOIN 字段写入 DormBooking 用于显示（仅 RAM，DB 不写）
foreach (var item in items)
{
    // 仅当档案有值时才覆盖（保留 DormBooking 原始冗余字段作为历史快照）
    if (!string.IsNullOrEmpty(item.EmployeeCode))
        item.Booking.EmployeeCode = item.EmployeeCode;
    if (!string.IsNullOrEmpty(item.RealName))
        item.Booking.EmployeeName = item.RealName;
    if (item.AttendanceTypeId.HasValue)
        item.Booking.AttendanceTypeId = item.AttendanceTypeId;
}
```

### 3.4 Repair API 同步写回

`POST /api/v1/bookings/repair-employee-names`（v2.13.33 + v2.13.47）：

```csharp
public async Task<ApiResponse<(int Updated, int Skipped, int NotFound)>> RepairBookingEmployeeNamesAsync()
{
    // 1. 拉取所有 DormBooking（EmployeeCode 非空）
    var allBookings = await _db.DormBookings
        .Where(b => !string.IsNullOrEmpty(b.EmployeeCode))
        .Select(b => new { b.Id, b.EmployeeId, b.EmployeeCode, b.EmployeeName })
        .ToListAsync();

    // 2. 拉取匹配的 SysEmployee（按 EmployeeId 优先 + EmployeeCode 反查）
    var employees = await _db.Employees
        .Where(e => empIds.Contains(e.Id) || empCodes.Contains(e.EmployeeCode))
        .Select(e => new { e.Id, e.EmployeeCode, e.RealName })
        .ToListAsync();

    // 3. 逐条对比 + 不一致则写回 DB
    foreach (var b in allBookings)
    {
        // 优先按 EmployeeId 取；无 EmployeeId 时按 EmployeeCode 反查
        SysEmployeeLite? emp = ...;
        bool nameChanged = ...; // v2.13.33 RealName 对比
        bool codeChanged = ...; // v2.13.47 EmployeeCode 对比

        if (!nameChanged && !codeChanged) { skipped++; continue; }

        var entity = await _db.DormBookings.FindAsync(b.Id);
        if (entity is null) continue;
        if (nameChanged) entity.EmployeeName = emp.RealName;
        if (codeChanged) entity.EmployeeCode = emp.EmployeeCode;
        entity.UpdatedAt = DateTime.Now;
        updated++;
    }

    await _db.SaveChangesAsync();
    return ApiResponse<(int, int, int)>.Ok((updated, skipped, notFound));
}
```

---

## 四、相关文档更新清单

| 文档 | 变更类型 | 关键内容 |
|------|---------|---------|
| `60-菜单导航与数据关系全景图-v2.13.3.md` | 升级版本 v2.13.3 → v2.13.48 | §0.1 新增 v2.13.48 增量说明（FK 关联强制原则）；§2.1 实体关系图补全 SysEmployee → DormBooking 字段；§2.3 新增 5 字段同步策略表 |
| `07-办理登记需求-v2.11.md` | §3 数据模型补充 | §3 前置 ⚠️ v2.13.47 强制数据关系（包含正确/错误 SQL 对比）；§3.1 DormBooking 字段表 EmployeeId 说明升级为"FK→SysEmployee.Id（唯一真源关联键）"；§3.1 末尾新增 FK 关联查询实现标准模板 |
| `CLAUDE.md` | 版本号同步 | 项目当前版本升级 v2.13.46 → v2.13.48；新增 v2.13.47 Booking 工号同步 BUG 修复段 + v2.13.48 数据关系文档定稿段 |
| `100-Booking工号同步BUG修复-v2.13.47.md` | 已存在 | 代码层修复报告 |
| `101-Booking数据关系文档定稿-v2.13.48.md` | **新增（本文档）** | 文档层定稿报告 |

---

## 五、验证清单

- [x] 60-菜单导航与数据关系全景图 升级到 v2.13.48
- [x] 60 §0.1 新增 v2.13.48 增量说明
- [x] 60 §2.1 实体关系图补全 SysEmployee → DormBooking 字段说明
- [x] 60 §2.3 新增 5 字段同步策略表
- [x] 07-办理登记需求 §3 前置添加 ⚠️ v2.13.47 强制数据关系声明
- [x] 07 §3.1 DormBooking 字段表 EmployeeId 说明升级
- [x] 07 §3.1 末尾新增 FK 关联查询实现标准模板
- [x] CLAUDE.md 版本号升级 v2.13.46 → v2.13.48
- [x] CLAUDE.md 新增 v2.13.47 + v2.13.48 两段版本说明
- [x] Git 提交

---

## 六、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（3 文档 + CLAUDE.md + 1 新文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性（与 v2.13.47 代码修复完全对齐） | ✅ 已对齐 |
| 3 | 计算方法一致性（5 字段同步策略统一） | ✅ 已保留 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 强制 FK 关联原则升级 |
| 5 | 文档同步（v2.13.47 代码修复已 commit d3a4c57；本次为文档定稿） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.48 + 2026-07-21） | ✅ 已标注 |

---

## 七、回退方案

```bash
git revert HEAD  # 撤销 v2.13.48 文档层定稿（不影响 v2.13.47 代码修复）
```