# 员工账单列表数据 BUG 修复 — v2.11.26

> **日期**：2026-07-13  
> **关联原型**：`00-方案文档/04-HTML原型/billing/employee-bills.html`  
> **BUG 现象**：员工分摊列表显示 0 条数据，控制台报 `Cannot read properties of undefined (reading 'reset')`  
> **根因**：`const list = registerList(...)` 声明在 `initPage()` 调用之后，`initPage()` → `render()` → `list.reset()` 访问未初始化的 const 变量  

---

## 1. BUG 诊断

### 1.1 错误信息

```
ERROR: Cannot read properties of undefined (reading 'reset')
```

### 1.2 根因链

```
employee-bills.html 脚本执行顺序：
  1. initPage() 调用 (line 451)
     └─ render() 调用 (line 449)
        └─ list.reset() ← list 是 undefined！❌
  2. const list = registerList({...}) (line 454) ← 太晚了

原因：render() 函数定义在 list 声明之前，但函数体在**调用时**才求值 list。
此时 list 尚未赋值（const 声明在 line 454，initPage 在 line 451）。
```

### 1.3 与 v2.11.25 的区别

| 维度 | v2.11.25 (办理登记) | v2.11.26 (员工账单) |
|------|---------------------|---------------------|
| 错误类型 | `Cannot access 'RESIDENCE_STATUS' before initialization` (TDZ) | `Cannot read properties of undefined (reading 'reset')` |
| 根因 | `const RESIDENCE_STATUS` 声明位置在引用它的 IIFE 之后 | `const list` 声明位置在调用它的函数之后 |
| 共同点 | 都是 `const` 声明顺序导致的执行时序问题 | |

---

## 2. 修复方案

### 2.1 employee-bills.html — 调整 list 声明顺序

**修改文件**：`00-方案文档/04-HTML原型/billing/employee-bills.html`

**变更前**：
```javascript
function initPage() {
    // ...
    render();  // ← list 未赋值
}
initPage();  // ← 调用 render()

const list = registerList({...});  // ← 太晚
```

**变更后**：
```javascript
var list;  // ← 先声明（var 提升，值为 undefined）

function initPage() {
    // ...
    setTimeout(function() { render(); }, 0);  // ← 延迟到 list 赋值后
}
initPage();  // ← 调度 render，但立即返回

list = registerList({...});  // ← 赋值
```

**修复要点**：
1. `var list` 提前声明（函数作用域提升，避免 TDZ）
2. `initPage()` 中的 `render()` 改为 `setTimeout(fn, 0)` 延迟执行
3. 利用 JavaScript 单线程特性：`setTimeout` 回调在当前 script 执行完毕后运行，此时 `list` 已赋值
4. 移除底部独立的 `render()` 调用（避免双重渲染）

---

## 3. 验证结果

| 检查项 | 修复前 | 修复后 |
|--------|--------|--------|
| 控制台错误 | 1 个 ERROR | 0 个 ✅ |
| 员工账单总数 | 0 | 492 ✅ |
| 第一页行数 | 0 | 10 ✅ |
| list 变量 | undefined | registerList 实例 ✅ |

---

## 4. 影响范围

| 文件 | 变更 |
|------|------|
| `00-方案文档/04-HTML原型/billing/employee-bills.html` | ✏️ var list 提前声明 + initPage render 延迟 + 移除重复 render 调用 |

---

## 5. 版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v2.11.26 | 2026-07-13 | BUGFIX：list const 声明在 initPage 之后导致 render() 调用 list.reset() 时 list 为 undefined → var list 提前声明 + setTimeout 延迟渲染 |

---

> **变更路径**：v2.11.22 (FilterPersistence onRestore) → **v2.11.26 (list 时序 BUGFIX)**
