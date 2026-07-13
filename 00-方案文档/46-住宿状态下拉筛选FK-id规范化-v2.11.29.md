# 住宿状态下拉筛选 FK id 规范化 — v2.11.29

> **日期**：2026-07-13  
> **关联页面**：`billing/employee-bills.html`  
> **关联字典**：`RESIDENCE_STATUS`（基础资料-住宿状态）  
> **关联文档**：
> - `40-多模块住宿状态字典一致性规范-v2.11.21.md`
> - `45-员工账单住宿状态与在职状态字典联动修复-v2.11.28.md`
> - `44-在职状态字典关联引用规范修复-v2.11.27.md`

---

## 1. 修复背景

### 1.1 v2.11.21 规范要求

`RESIDENCE_STATUS` 字典结构（v2.11.21 规范）：

| ID | Code | Name | Badge | 业务含义 |
|----|------|------|-------|---------|
| 1 | LODGED | 已住宿 | bg-primary | BOOKINGS.Status=2 在宿 |
| 2 | NOT_LODGED | 未住宿 | bg-light text-dark | 退房后/已取消/无记录 |
| 3 | PENDING | 待入住 | bg-warning text-dark | BOOKINGS.Status=1 预约 |

**FK 关联**：`SysEmployee.ResidenceStatusId` → `ResidenceStatus.Id`

### 1.2 v2.11.27 R-DICT-06 规范

> **R-DICT-06**：所有下拉筛选 value 一律使用 FK 主键（id），不混用字符串 name/code。

### 1.3 问题诊断

`billing/employee-bills.html` 的 `fReside` 下拉违规使用 `s.code`（LODGED/NOT_LODGED/PENDING）作为 value：

```javascript
// 修复前（违反 R-DICT-06）
RESIDENCE_STATUS.forEach(s => {
    const opt = document.createElement('option');
    opt.value = s.code;  // ❌ 字符串 code
    opt.textContent = s.name;
    fReside.appendChild(opt);
});
```

**影响**：
- 与同页面 `fType`（id=1-5）、`fEmpStatus`（id=1-3）、`fDept`（v2.11.28 改 id）**规范不一致**
- 增加 R-DICT-06 例外条款，破坏"FK 主键统一"的可推理性
- 后续新增住宿状态项（如"调宿中"）需同步 code 字段，前端筛选逻辑冗余

---

## 2. 修复方案

### 2.1 billing/employee-bills.html — fReside value 改 FK id

**修复前**：
```javascript
const fReside = document.getElementById('fReside');
if (fReside && typeof RESIDENCE_STATUS !== 'undefined') {
    RESIDENCE_STATUS.forEach(s => {
        const opt = document.createElement('option');
        opt.value = s.code;
        opt.textContent = s.name;
        fReside.appendChild(opt);
    });
}
```

**修复后**：
```javascript
// v2.11.29 修订：value = s.id（FK），与 EMPLOYEE_TYPES/EMPLOYMENT_STATUSES 等字典保持一致
const fReside = document.getElementById('fReside');
if (fReside && typeof RESIDENCE_STATUS !== 'undefined') {
    RESIDENCE_STATUS.forEach(s => {
        const opt = document.createElement('option');
        opt.value = s.id;  // v2.11.29: 改为 FK 主键
        opt.textContent = s.name;
        fReside.appendChild(opt);
    });
}
```

### 2.2 新增辅助函数：getEmployeeResidenceId(empId)

```javascript
// 辅助函数：通过 employeeId 获取人员的住宿状态 FK ID（关联引用 RESIDENCE_STATUS.id）
// v2.11.29 新增：基于 code 通过 getResidenceStatusByCode(code).id 还原主键
function getEmployeeResidenceId(empId) {
    var code = getEmployeeResidenceCode(empId);
    if (typeof getResidenceStatusByCode === 'function') {
        var s = getResidenceStatusByCode(code);
        return s ? s.id : null;
    }
    // 兜底：字典未加载时按固定映射
    if (code === 'LODGED') return 1;
    if (code === 'NOT_LODGED') return 2;
    if (code === 'PENDING') return 3;
    return null;
}
```

### 2.3 currentData() 筛选逻辑同步更新

**修复前**：
```javascript
const resideCode = document.getElementById('fReside').value;
// ...
if (resideCode) {
    data = data.filter(function (b) {
        var code = getEmployeeResidenceCode(b.employeeId);
        return code === resideCode;
    });
}
```

**修复后**：
```javascript
const resideCode = document.getElementById('fReside').value;
// v2.11.29 修订：fReside.value 现为 RESIDENCE_STATUS.id（FK）
const resideId = resideCode ? parseInt(resideCode) || null : null;
// ...
if (resideId) {
    data = data.filter(function (b) {
        var rid = getEmployeeResidenceId(b.employeeId);
        return rid === resideId;
    });
}
```

---

## 3. 字典联动规范强化（v2.11.29 完善）

### 3.1 全页面筛选下拉 FK 一致性矩阵（v2.11.29 现状）

| 页面 | 下拉 | value 类型 | 关联字典 | 状态 |
|------|------|-----------|---------|------|
| `personnel/list.html` | fDept | id（FK） | DEPARTMENTS | ✅ |
| `personnel/list.html` | fType | id（FK） | EMPLOYEE_TYPES | ✅ |
| `personnel/list.html` | fAttend | id（FK） | ATTENDANCE_TYPES | ✅ |
| `personnel/list.html` | fStatus | id（FK） | EMPLOYMENT_STATUSES | ✅ |
| `billing/employee-bills.html` | fDept | id（FK） | DEPARTMENTS | ✅ v2.11.28 |
| `billing/employee-bills.html` | fType | id（FK） | EMPLOYEE_TYPES | ✅ |
| `billing/employee-bills.html` | fEmpStatus | id（FK） | EMPLOYMENT_STATUSES | ✅ v2.11.28 |
| **`billing/employee-bills.html`** | **fReside** | **id（FK）** | **RESIDENCE_STATUS** | **✅ v2.11.29** |
| `booking/index.html` | type | 字符串（1/2） | — （业务枚举） | ✅ |
| `booking/index.html` | status | id（FK） | BOOKING_STATUS_DICT | ✅ |

> **结论**：所有基础资料字典筛选下拉 value 已统一为 FK id（仅业务枚举如 type=1入住/2退房 保留字符串）。

### 3.2 历史兼容说明（v2.11.29 重要决策）

**问题**：之前版本 `fReside` 存储在 localStorage 的值为 `'LODGED'`/`'NOT_LODGED'`/`'PENDING'`。

**升级策略**：无需特殊处理。原因：
- `filter-persistence.js` 的 `restoreFilters()` 按 fieldId 直接赋值 `el.value = values[fieldId]`
- 如果保存的是字符串 code，新版 select 的 option value 是数字 id，**value 不匹配 → 默认空字符串**（浏览器原生行为）
- 用户首次访问新版 → 重置按钮可清空 → 重新选择（一次性升级成本）

**若需主动迁移**，可在 `restoreFilters()` 后追加：
```javascript
// v2.11.29 兼容：fReside 历史 code 值 → id 值
if (moduleKey === 'employeeBills' && el.id === 'fReside') {
    if (el.value === 'LODGED') el.value = '1';
    else if (el.value === 'NOT_LODGED') el.value = '2';
    else if (el.value === 'PENDING') el.value = '3';
}
```

但当前原型阶段不做迁移（用户量小，重置即可），后续若上线可补充。

---

## 4. 验证结果

| 检查项 | 修复前 | 修复后 |
|--------|--------|--------|
| `fReside` value | code（字符串）| id（FK 主键） ✅ |
| `fReside` 选项 | `LODGED=已住宿, NOT_LODGED=未住宿, PENDING=待入住` | `1=已住宿, 2=未住宿, 3=待入住` ✅ |
| 已住宿筛选命中数 | 468 | 468 ✅ |
| 未住宿筛选命中数 | 17 | 17 ✅ |
| 待入住筛选命中数 | 7 | 7 ✅ |
| 组合筛选（在职 AND 已住宿）| 467 | 467 ✅ |
| 字典联动规范 R-DICT-06 | 部分违反 | 全部遵守 ✅ |
| 控制台错误 | 0 | 0 ✅ |

### 字典关联引用一致性检查

```
fDept:    DEPARTMENTS.id (FK)        ✅ 一致
fType:    EMPLOYEE_TYPES.id (FK)     ✅ 一致
fEmpStatus: EMPLOYMENT_STATUSES.id (FK) ✅ 一致
fReside:  RESIDENCE_STATUS.id (FK)   ✅ 一致
```

---

## 5. 影响范围

| 文件 | 变更 |
|------|------|
| `00-方案文档/04-HTML原型/billing/employee-bills.html` | ✏️ fReside value 改 FK id + 新增 getEmployeeResidenceId() + currentData 筛选逻辑同步 |

---

## 6. 规范要求（v2.11.29 强化）

### 6.1 字典关联引用规范（完善版）

| 规则 | 说明 |
|------|------|
| R-DICT-01 | 字典访问必须使用 `typeof X !== 'undefined'` 守卫 |
| R-DICT-02 | 字典渲染必须遍历字典数组，禁止硬编码映射 |
| R-DICT-03 | 字典字段使用 `.find()` 提取，禁止直接索引 |
| R-DICT-04 | 字典修改必须同步所有引用页面 |
| R-DICT-05 | 新增字典项必须同步更新所有引用页面的初始化逻辑 |
| R-DICT-06 | **所有下拉筛选 value 一律使用 FK 主键（id），不混用字符串 name/code** |
| R-DICT-07 | 跨字典关联场景必须在文档中明确数据来源与联动规则 |
| **R-DICT-08**（v2.11.29 新增）| **业务枚举（如 type=1入住/2退房）允许保留字符串，但基础资料字典（EMPLOYEE_TYPES/DEPARTMENTS/EMPLOYMENT_STATUSES/RESIDENCE_STATUS）必须 FK id** |
| **R-DICT-09**（v2.11.29 新增）| **跨字典派生字段（如 getEmployeeResidenceId = getResidenceStatusByCode(code).id）必须封装为 helper 函数，禁止散落逻辑** |

### 6.2 FK id vs 业务枚举区分

| 类型 | value 形态 | 例子 | 处理 |
|------|-----------|------|------|
| **基础资料字典** | id（数字） | `DEPARTMENTS.id=1, EMPLOYEE_TYPES.id=4` | FK id 强制 |
| **业务状态枚举** | 字符串或数字 | `type=1(入住)/2(退房), isPublished=true/false` | 保持原值 |
| **预留废弃字典** | — | 旧 LIVING_STATUS_*（v2.12.8 已删除）| 不使用 |

---

## 7. 版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v2.11.20 | 2026-07-12 | 住宿状态字典一致性规范 |
| v2.11.21 | 2026-07-12 | 多模块住宿/办理/账单字典一致性规范 |
| v2.11.27 | 2026-07-13 | R-DICT-06 规范定义（FK id 统一）|
| v2.11.28 | 2026-07-13 | employee-bills 新增 fEmpStatus + fDept 改 FK id |
| **v2.11.29** | **2026-07-13** | **fReside 改 FK id（最后一个基础资料字典下拉规范化）+ 新增 R-DICT-08/09** |

---

> **变更路径**：v2.11.21 (字典规范) → v2.11.27 (FK id 规范 R-DICT-06) → v2.11.28 (部分落地) → **v2.11.29 (fReside 落地 + 规范完善 R-DICT-08/09)**

---

## 7. v2.11.31 后续变更记录（产品评审删除）

> **变更日期**：2026-07-13
> **变更范围**：`billing/employee-bills.html`（员工账单）
> **变更内容**：删除整个"住宿状态"列与筛选下拉 + 相关 helper 函数
> **删除原因**：产品评审决议 — 员工账单页面不展示住宿状态
> **保留**：基础资料-住宿状态字典（`RESIDENCE_STATUS`）继续在办理登记、人员清单等模块使用
> **关联文档**：`48-删除员工账单住宿状态列与筛选项-v2.11.31.md`

**v2.11.31 后失效的规范**（仅针对员工账单页面）：
- ❌ fReside FK id 规范（v2.11.29 落地）：本页面已删除 fReside，该规范在其它页面（办理登记筛选如有）继续生效