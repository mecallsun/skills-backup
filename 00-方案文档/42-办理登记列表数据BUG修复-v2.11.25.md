# 办理登记列表数据 BUG 修复 — v2.11.25

> **日期**：2026-07-13  
> **关联原型**：`00-方案文档/04-HTML原型/booking/index.html`  
> **BUG 现象**：办理登记列表显示 0 条数据，控制台报两个错误  
> **根因**：`RESIDENCE_STATUS` 和 `BOOKING_STATUS_DICT` 的 `const` 声明位置在 `normalizeData` IIFE 之后，IIFE 内 `typeof RESIDENCE_STATUS` 触发 TDZ 错误，导致 `mock-data.js` 执行中断，后续 `BOOKING_STATUS_DICT` 未定义  

---

## 1. BUG 诊断

### 1.1 错误信息

```
ERROR: Cannot access 'RESIDENCE_STATUS' before initialization
ERROR: BOOKING_STATUS_DICT is not defined
```

### 1.2 根因链

```
mock-data.js 执行顺序：
  1. PERSONNEL, BOOKINGS, DORMS 等数据定义
  2. registerList 分页组件定义
  3. normalizeData IIFE (line 51652-51936)
     ├─ §1 考勤班次规范化 ✓
     ├─ §2 员工类型规范化 ✓
     ├─ §2b 员工类型 FK 校正 ✓
     ├─ §3 部门规范化 ✓
     ├─ §4 在职状态规范化 ✓
     ├─ §5 EMPLOYEE_BILLS 规范化 ✓
     ├─ §6 DORM_BILLS 规范化 ✓
     └─ v2.11.18 dormCode 同步 (§) 
        └─ line 51910: typeof RESIDENCE_STATUS ← TDZ 错误！
  4. ❌ 执行中断，后续代码不执行
  5. RESIDENCE_STATUS (const, line 52091) ← 从未执行
  6. BOOKING_STATUS_DICT (const, line 52105) ← 从未执行
  7. registerList 等后续工具函数 ← 从未执行

booking/index.html 执行：
  1. 加载 mock-data.js → BOOKING_STATUS_DICT 未定义
  2. BK_STATUS_TEXT/BK_STATUS_CLASS 为空对象
  3. initPage() → render() → getFiltered() → BOOKINGS 有 750 条
  4. 但 BOOKING_STATUS_DICT 未定义 → statusBadge 返回空颜色
  5. ❌ 实际测试：ROW_COUNT=0（PLAYWRIGHT 报错导致 evaluate 失败）
```

### 1.3 TDZ 错误详解

```javascript
// mock-data.js line 51910（normalizeData IIFE 内部）
if (typeof getResidenceStatusCode === 'function' && typeof RESIDENCE_STATUS !== 'undefined') {
    //                                  ^^^^^^^^^^^^^^^^^^
    //                                  const 声明在 line 52091（IIFE 之后）
    //                                  typeof 对 TDZ 中的 const 也抛 ReferenceError！
}

// mock-data.js line 52091（IIFE 之后）
const RESIDENCE_STATUS = [ ... ];  // ← TDZ 区域
```

**关键点**：顶层 `const` 声明在当前语句执行前处于 TDZ（暂时性死区）。`typeof` 操作符对 TDZ 中的变量**不是安全的**——它会抛出 `ReferenceError: Cannot access 'X' before initialization`。

---

## 2. 修复方案

### 2.1 mock-data.js — 提前定义 RESIDENCE_STATUS 和 BOOKING_STATUS_DICT

将 `RESIDENCE_STATUS` 和 `BOOKING_STATUS_DICT` 从 `const` 改为 `var`（兼容 `window` 访问），并提前到 `normalizeData` IIFE 之后、任何引用它们的代码之前。

**修改文件**：`00-方案文档/04-HTML原型/mock-data.js`

**变更**：
1. 在 `normalizeData` IIFE 结束 `})();` 之后立即定义 `RESIDENCE_STATUS`（`var`）和 `BOOKING_STATUS_DICT`（`const`）
2. 删除原位置（line 52091-52135）的重复定义

### 2.2 filter-persistence.js — 修复 getModuleKey() 正则

**修改文件**：`00-方案文档/04-HTML原型/_shared/filter-persistence.js`

**修复前**：
```javascript
var match = pathname.match(/([^/?#]+\.html)/);
// /booking/index.html → 匹配 'index.html' ✗
```

**修复后**：
```javascript
var match = pathname.match(/([^/?#]+\/[^/?#]+\.html)/);
// /booking/index.html → 匹配 'booking/index.html' ✓
```

---

## 3. 验证结果

| 检查项 | 修复前 | 修复后 |
|--------|--------|--------|
| 控制台错误 | 2 个 ERROR | 0 个 |
| 办理登记总数 | 0 | 750 ✅ |
| 第一页行数 | 0 | 10 ✅ |
| RESIDENCE_STATUS | ReferenceError | 正常定义 |
| BOOKING_STATUS_DICT | undefined | 正常定义 |
| statusBadge | 空颜色 | 正确 Badge 颜色 ✅ |

---

## 4. 影响范围

| 文件 | 变更 |
|------|------|
| `00-方案文档/04-HTML原型/mock-data.js` | ✏️ RESIDENCE_STATUS 和 BOOKING_STATUS_DICT 提前定义 + const→var |
| `00-方案文档/04-HTML原型/_shared/filter-persistence.js` | ✏️ getModuleKey() 正则修正 |

---

## 5. 版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v2.11.25 | 2026-07-13 | BUGFIX：RESIDENCE_STATUS TDZ 错误导致 mock-data.js 执行中断 → 提前定义 RESIDENCE_STATUS + BOOKING_STATUS_DICT；修复 filter-persistence.js getModuleKey() 正则 |

---

> **变更路径**：v2.11.21 (BOOKING_STATUS_DICT 引入) → v2.12.8 (RESIDENCE_STATUS 引入) → **v2.11.25 (TDZ BUGFIX)**
