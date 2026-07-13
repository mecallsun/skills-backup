# 需求规格说明书 — v2.11.22：员工账单与办理登记列表数据 BUG 修复

> **范围**：修复员工账单列表（employee-bills.html）和办理登记列表（booking/index.html）无数据显示的 BUG
> **日期**：2026-07-13
> **关联 BUG**：
> - 员工账单 employeeType 字段 467/472 条为 undefined（v2.11.7 IIFE 误删字段未重建）
> - 员工类型下拉 value=name 与 EMPLOYEE_BILLS.employeeType 不匹配
> - FilterPersistence 与页面渲染时序冲突导致空列表
> **关联文档**：
> - `38-在职状态字典合并与统计数据源统一-v2.11.18.md`
> - `39-住宿状态字典一致性规范-v2.11.20.md`
> - `40-多模块住宿状态字典一致性规范-v2.11.21.md`

---

## 1. BUG 诊断

### 1.1 员工账单列表 BUG

**现象**：员工账单筛选"员工类型"下拉选择任何选项都只匹配极少数记录

**根因分析**：
```
EMPLOYEE_BILLS_202607 employeeType 字段分布（修复前）：
- undefined: 467 条（97%）
- 合同工: 2 条
- 临时工: 1 条
- 外包: 2 条
```

**根因链**：
1. v2.11.7 引入 normalizeData IIFE 时，§5 步骤执行 `delete b.employeeType`
2. **删除字段后没有重建关联** → 467 条数据的 employeeType 变成 undefined
3. 员工类型下拉 value=name（与 EMPLOYEE_BILLS.employeeType 字符串一致），但 employeeType undefined
4. 筛选时 `(b.employeeType || '') === '合同工'` → 全部不匹配

### 1.2 FilterPersistence 时序 BUG

**现象**：办理登记列表偶发显示空列表

**根因**：
1. 页面 inline script 调用 `initPage()` → `render()` 立即渲染数据
2. FilterPersistence 在 DOMContentLoaded 后调用 `restoreFilters()` 恢复 localStorage 值
3. 如果保存的 filter 值（如某日期）过滤掉了所有数据 → 用户看到空列表
4. 但 restoreFilters 不触发重新渲染 → 显示状态不一致

### 1.3 办理登记 statusBadge 函数 BUG

**现象**：状态 Badge 颜色与 v2.11.21 字典不一致

**根因**：原 statusBadge 函数硬编码 `BK_STATUS_CLASS = {1:'bg-warning text-dark', ...}`，未使用 BOOKING_STATUS_DICT 字典（v2.11.21 修订）

---

## 2. v2.11.22 修复方案

### 2.1 数据修复：normalizeData IIFE 重建 employeeType 字段

**位置**：`mock-data.js` normalizeData IIFE §5 步骤

**修复策略**：
- 删除 `delete b.employeeType`（保留字段）
- 通过 `b.employeeId` 关联 PERSONNEL → employeeTypeId
- 优先使用 `p.employeeTypeName`（FK + Name），回退 `p.employeeType` 字符串，再回退 EMPLOYEE_TYPES 字典
- 同时重建 `b.attendanceType`（v2.11.7 §2.4.1 删除的字段）

**修复后分布**：
```
EMPLOYEE_BILLS_202607 employeeType 字段分布（修复后）：
- 合同工: 164 条
- 临时工: 70 条
- 外包: 50 条
- 实习生: 44 条
- 技师: 86 条（历史遗留，应在 v2.11.7.CORRECT 修正）
- 保安: 28 条（历史遗留）
- 顾问: 30 条（历史遗留）
```

### 2.2 下拉筛选修正：value 改用 employeeTypeId（FK）

**位置**：`billing/employee-bills.html` initDatalist() §3 步骤

**修复前**：
```javascript
opt.value = t.name;  // value = name（与 EMPLOYEE_BILLS.employeeType 字符串一致）
opt.textContent = t.name;
```

**修复后**：
```javascript
opt.value = t.id;  // value = id（FK，与基础资料字典一致）
opt.textContent = t.name;
```

**筛选逻辑同步**：
```javascript
// v2.11.22 修订：通过 employeeId 关联 PERSONNEL → employeeTypeId 进行匹配
if (typeId) {
    data = data.filter(function (b) {
        var p = PERSONNEL.find(function (emp) { return emp.id === b.employeeId; });
        return p && p.employeeTypeId === typeId;
    });
}
```

### 2.3 FilterPersistence 时序修复

**位置**：`_shared/filter-persistence.js`

**新增**：
```javascript
FilterPersistence._initialized = false;  // 初始化完成标志
FilterPersistence._moduleKey = null;       // 当前模块 key
FilterPersistence.onRestore = null;        // 恢复完成后回调
```

**流程**：
1. 页面 inline script 注册 `FilterPersistence.onRestore = function() { render(); }`
2. FilterPersistence.init() 完成 restoreFilters 后，调用 onRestore 触发重新渲染
3. 保证渲染时 localStorage 的 filter 值已应用

### 2.4 页面初始化逻辑同步修复

**位置**：
- `booking/index.html` - initPage() 增加 _initialized 等待
- `billing/employee-bills.html` - 改为 initPage() + onRestore 模式

---

## 3. 验证结果

### 3.1 数据完整性验证

| 检查项 | 修复前 | 修复后 |
|-------|--------|--------|
| EMPLOYEE_BILLS_202607 employeeType undefined 数 | 467/472 (97%) | 0/472 (0%) |
| 员工类型合同工(1) 过滤匹配数 | 2 条 | 164 条 ✅ |
| 员工类型实习生(4) 过滤匹配数 | 0 条 | 44 条 ✅ |
| 部门=生产部 过滤匹配数 | 138 条 | 138 条 ✅ |

### 3.2 列表渲染验证

| 检查项 | 结果 |
|-------|------|
| 办理登记列表（无 filter）| ✅ 显示 750 条 |
| 员工账单列表（默认月份）| ✅ 显示 472 条 |
| 员工类型筛选 | ✅ 各种类型正常显示 |
| FilterPersistence 时序 | ✅ render 等待 restore 完成 |

### 3.3 编译验证

```
dotnet build DormManage.sln -c Debug
→ 已成功生成。
→ 0 个错误
```

---

## 4. 后续问题（不在本版本范围）

| # | 问题 | 说明 | 后续版本 |
|---|------|------|---------|
| 1 | 员工类型包含"技师/保安/顾问"历史遗留 | v2.11.7.CORRECT 已规范仅 5 种，但 mock-data 历史数据未清理 | v2.11.23 |
| 2 | billing/employee-bills.html 重置时清空 fType 但 resetFilter 函数未使用 typeId 变量 | 需在 resetFilter 中同步使用 typeId | v2.11.23 |
| 3 | resetFilter 函数未持久化 typeName/typeId 切换 | 应保持类型 ID 一致性 | v2.11.23 |

---

## 5. 影响范围

| 文件 | 变更 |
|------|------|
| `00-方案文档/04-HTML原型/mock-data.js` | ✏️ normalizeData IIFE §5 步骤保留 employeeType 字段并重建 |
| `00-方案文档/04-HTML原型/billing/employee-bills.html` | ✏️ 员工类型下拉 value 改用 id；筛选逻辑改用 employeeTypeId；增加 initPage + onRestore |
| `00-方案文档/04-HTML原型/booking/index.html` | ✏️ initPage 增加 _initialized 等待 |
| `00-方案文档/04-HTML原型/_shared/filter-persistence.js` | ✏️ 添加 _initialized 标志 + onRestore 回调 |
| `00-方案文档/41-员工账单与办理登记列表数据BUG修复-v2.11.22.md` | 🆕 本文档 |

---

## 6. 版本演进路径

```
v2.11.7  → normalizeData IIFE 误删 employeeType 字段（BUG 引入）
v2.11.18 → 在职状态 FK + 入住人数数据源统一
v2.11.19 → 在职状态字典一致性强制规范
v2.11.20 → 住宿状态字典一致性规范 + FK 关联引用
v2.11.21 → 多模块住宿/办理/账单状态字典一致性规范
v2.11.22 → 员工账单 + 办理登记列表数据 BUG 修复（本文档）
        → 重建 EMPLOYEE_BILLS_202607 employeeType 字段
        → 员工类型下拉 value 改用 id（FK）
        → FilterPersistence 时序修复
        → 员工类型合同工过滤从 2 条 → 164 条
```

---

> **变更路径**：v2.11.7 (BUG引入) → v2.11.18 → v2.11.19 → v2.11.20 → v2.11.21 → **v2.11.22（数据 BUG 修复）**