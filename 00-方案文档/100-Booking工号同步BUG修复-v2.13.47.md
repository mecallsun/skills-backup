# Booking 工号同步 BUG 修复（v2.13.47）

> **版本**：v2.13.47
> **日期**：2026-07-21
> **类型**：P0 数据一致性 BUG 修复
> **影响文件**：`DormManage.Shared/Services/BookingService.cs` + `_Layout.cshtml` + `NotifyIconManager.cs`

---

## 一、BUG 描述

**现象**：办理登记列表（/Booking）显示的工号（EmployeeCode）与「人员清单」档案中当前工号不一致。

**根因**：
- `DormBooking.EmployeeCode` 是冗余字段（登记入住时写入）
- `BookingService.GetListAsync` 原实现只 JOIN 了 `SysEmployee.RealName`，未 JOIN `EmployeeCode`
- 列表视图直接显示 `item.EmployeeCode`（即 DormBooking 自己冗余存的字段）
- 一旦人员在「人员清单」改了工号，办理登记列表仍显示老工号

**用户报告原文**：
> 办理登记列表 存在重大BUG，没有显示统一 数据库连接的库中的数据，其中的 工号是登记入住记录 的员工 工号，使用 人员清单 记录 id 进行 FK 关联

**核心要求**：工号也应该从 `SysEmployee`（人员清单）实时取，通过 `EmployeeId` 作为 FK 关联，而不是从 `DormBooking.EmployeeCode` 冗余字段读。

---

## 二、修复方案

### 2.1 P0-1 GetListAsync 实时同步工号（与姓名同步策略一致）

**文件**：`DormManage.Shared/Services/BookingService.cs` line 170-251

**变更**：在 JOIN SysEmployee 的匿名投影中增加 `EmployeeCode` 字段，物化阶段覆盖到 `DormBooking.EmployeeCode`：

```csharp
// v2.13.47：实时取最新工号（人员清单为唯一真源；档案缺失时回退登记时写入的工号）
var query =
    from b in _db.DormBookings
    join emp in _db.Employees on b.EmployeeId equals emp.Id into empGroup
    from emp in empGroup.DefaultIfEmpty()
    select new
    {
        Booking = b,
        AttendanceTypeId = (int?)(emp != null ? emp.AttendanceTypeId : b.AttendanceTypeId),
        RealName = emp != null && !string.IsNullOrEmpty(emp.RealName) ? emp.RealName : b.EmployeeName,
        // v2.13.47 BUG：实时取最新工号
        EmployeeCode = emp != null && !string.IsNullOrEmpty(emp.EmployeeCode) ? emp.EmployeeCode : b.EmployeeCode
    };
```

**物化覆盖**：

```csharp
// v2.13.47 BUG：覆盖显示用工号（仅 RAM，DB 不写；与姓名覆盖策略一致）
if (!string.IsNullOrEmpty(item.EmployeeCode))
{
    item.Booking.EmployeeCode = item.EmployeeCode;
}
```

**关键词筛选同步更新**：原代码只查 `x.Booking.EmployeeCode`，现改为优先查 `x.EmployeeCode`（档案）回退 `x.Booking.EmployeeCode`（冗余）：

```csharp
query = query.Where(x =>
    x.EmployeeCode.ToLower().Contains(keyword) ||
    x.Booking.EmployeeCode.ToLower().Contains(keyword) ||
    x.Booking.EmployeeName.ToLower().Contains(keyword) ||
    x.RealName.ToLower().Contains(keyword) ||
    (x.Booking.Phone != null && x.Booking.Phone.Contains(keyword)));
```

### 2.2 P0-2 Repair API 同步回填工号

**文件**：`BookingService.RepairBookingEmployeeNamesAsync` line 263-330

**变更**：除原有 `EmployeeName` 回填外，扩展为同时回填 `EmployeeCode`：

```csharp
// v2.13.47：仅修正 RealName/EmployeeCode 都不一致的记录
bool nameChanged = !string.IsNullOrEmpty(emp.RealName) && !string.Equals(emp.RealName, b.EmployeeName, StringComparison.Ordinal);
bool codeChanged = !string.IsNullOrEmpty(emp.EmployeeCode) && !string.Equals(emp.EmployeeCode, b.EmployeeCode, StringComparison.Ordinal);
if (!nameChanged && !codeChanged) { skipped++; continue; }

var entity = await _db.DormBookings.FindAsync(b.Id);
if (entity is null) continue;
if (nameChanged) entity.EmployeeName = emp.RealName;
if (codeChanged) entity.EmployeeCode = emp.EmployeeCode;
entity.UpdatedAt = DateTime.Now;
affected.Add(entity);
updated++;
```

**新增私有 DTO**：`SysEmployeeLite`（避免对外部 SysEmployee 实体的强依赖）。

**影响**：
- `/Booking` 页面顶部「修复姓名关联」按钮（v2.13.32-hotfix）现在会一并修复工号
- 一键同步 DormBooking 表所有记录的 EmployeeCode + EmployeeName 到 SysEmployee 档案

---

## 三、修复效果

| 场景 | 修复前 | 修复后 |
|------|--------|--------|
| 人员清单改工号后再查看 Booking 列表 | 显示旧工号 ❌ | 显示新工号 ✅ |
| 关键词筛选输入新工号 | 搜不到旧 DormBooking ❌ | 能搜到（按实时工号匹配） ✅ |
| 点击「修复姓名关联」 | 只回填姓名 ❌ | 同时回填姓名 + 工号 ✅ |
| 列表显示与人员清单一致性 | 经常不一致 ❌ | 100% 一致 ✅ |

---

## 四、验证清单

- [x] GetListAsync 匿名投影增加 EmployeeCode
- [x] 物化阶段覆盖 DormBooking.EmployeeCode
- [x] 关键词筛选使用实时 EmployeeCode 优先
- [x] Repair API 同时回填 EmployeeCode + EmployeeName
- [x] 新增 SysEmployeeLite 内部 DTO
- [x] `_Layout.cshtml` brand-version → v2.13.47
- [x] `NotifyIconManager.cs` → v2.13.47
- [x] `dotnet build DormManage.sln -c Release` → 0 错误
- [x] 2 项目 publish-final/ 发布
- [x] UTF-16 验证 v2.13.47（Admin/Tray ✓）
- [ ] Git 提交

---

## 五、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（1 Service + 2 全局版本号 + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性（FK 关联保持 EmployeeId） | ✅ 已保留 |
| 3 | 计算方法一致性（覆盖仅 RAM，DB 不写 — 与姓名同步策略一致） | ✅ 已保留 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 人员清单为唯一真源原则 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.47 + 2026-07-21） | ✅ 已标注 |

---

## 六、回退方案

```bash
git revert HEAD  # 撤销 v2.13.47
```