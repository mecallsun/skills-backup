# Profile 原型 JS 依赖链缺失修复（v2.13.55）

> **版本**：v2.13.55
> **日期**：2026-07-21
> **类型**：原型 BUG 修复 — 脚本依赖链补全
> **结论**：✅ **profile/index.html 已补全 `<script src="../_shared/storage-keys.js">`，主菜单导航 10 Tab 正常显示**

---

## 一、BUG 描述

**用户报告原文**：
> 原型显示个人中心菜单内容时，仍然没有显示主菜单导航，请查明原型 BUG 原因？

**v2.13.54 修复后仍不显示** —— 说明 v2.13.54 修复（补全 `mountTabBar()` 调用）只是必要条件，**不是充分条件**。JS 执行时仍抛出 ReferenceError 导致整个 `mountTabBar()` 中断。

---

## 二、深度根因排查

### 2.1 tab-bar.js 的 JS 依赖链

```javascript
// tab-bar.js 内部依赖（必须按顺序加载）：
// 1. STORAGE_KEYS（来自 _shared/storage-keys.js）
// 2. getCurrentUserId()（来自 _shared/storage-keys.js）
// 3. inferActiveTabId()（tab-bar.js 内部）
// 4. TabManager.saveActiveId() → 内部调用 STORAGE_KEYS.ACTIVE_TAB(userId)
```

### 2.2 加载顺序要求

**正确顺序**：
```html
<script src="../_shared/storage-keys.js"></script>  ← 必须最先加载！
<script src="../_shared/tab-bar.js"></script>
```

**错误顺序**（v2.13.55 修复前）：
```html
<script src="../_shared/tab-bar.js"></script>      ← ❌ 缺少 storage-keys.js 依赖
```

### 2.3 JS 错误传播链

```
profile/index.html 加载
    ↓
tab-bar.js 执行 → 顶部执行（无错误）✓
    ↓
DOMContentLoaded 触发
    ↓
profile-data.js 监听器先注册 → 先执行 → 渲染数据（无错误）✓
    ↓
内联 script 监听器后注册 → 后执行
    ↓
mountTabBar({basePath, currentUrl}) 调用
    ↓
renderTabBar(opts) 调用
    ↓
inferActiveTabId(currentUrl, basePath) → 返回 'tab-home' ✓
    ↓
TabManager.saveActiveId(activeId) 调用
    ↓
    localStorage.setItem(
      STORAGE_KEYS.ACTIVE_TAB(getCurrentUserId()),  ← ❌ ReferenceError!
      JSON.stringify({ activeTabId: 'tab-home' })
    )
    ↓
Uncaught ReferenceError: STORAGE_KEYS is not defined
    ↓
mountTabBar() 中断抛出异常
    ↓
el.outerHTML = renderTabBar(opts); ← ❌ 永远不执行
    ↓
bindTabBarEvents(opts); ← ❌ 永远不执行
    ↓
<div id="tab-bar"></div> 占位元素保持为空
    ↓
用户在浏览器看到：Tab 栏位置空白 ❌
```

---

## 三、对比其他 25 个原型的标准加载顺序

| 原型 | storage-keys.js 引用位置 | tab-bar.js 引用位置 |
|------|--------------------------|---------------------|
| `basics/index.html` | line 719 ✅ | line 720 |
| `booking/index.html` | line 1121 ✅ | line 1122 |
| `booking/check-in.html` | ✅ | ✓ |
| `booking/check-out.html` | ✅ | ✓ |
| `booking/edit.html` | ✅ | ✓ |
| `billing/standards.html` | ✅ | ✓ |
| `billing/standard-form.html` | ✅ | ✓ |
| `billing/dorm-bills.html` | ✅ | ✓ |
| `billing/employee-bills.html` | ✅ | ✓ |
| `dorms/list.html` | ✅ | ✓ |
| `dorms/create.html` | ✅ | ✓ |
| `dorms/edit.html` | ✅ | ✓ |
| `dorms/details.html` | ✅ | ✓ |
| `dorms/history.html` | ✅ | ✓ |
| `meter/index.html` | ✅ | ✓ |
| `meter/entry.html` | ✅ | ✓ |
| `meter/edit.html` | ✅ | ✓ |
| `meter/detail.html` | ✅ | ✓ |
| `meter/import.html` | ✅ | ✓ |
| `personnel/list.html` | ✅ | ✓ |
| `personnel/create.html` | ✅ | ✓ |
| `personnel/edit.html` | ✅ | ✓ |
| `personnel/import.html` | ✅ | ✓ |
| `settings/index.html` | ✅ | ✓ |
| `index.html` | ✅ | ✓ |
| **`profile/index.html`** | **❌ 缺失** | **line 454** |

**profile/index.html 是 26 个原型中**唯一一个没有引用 `storage-keys.js` 的页面**！

---

## 四、修复内容

### 4.1 profile/index.html 第 453 行新增

**修复前**：
```html
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>
<script src="../_shared/tab-bar.js"></script>          ← ❌ 缺少依赖
<script src="profile-data.js"></script>
```

**修复后**：
```html
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>
<script src="../_shared/storage-keys.js"></script>     ← ✅ 新增依赖
<script src="../_shared/tab-bar.js"></script>
<script src="profile-data.js"></script>
```

### 4.2 修复后加载顺序（与其他 25 个原型 100% 一致）

| # | 脚本 | 提供 |
|---|------|------|
| 1 | `bootstrap.bundle.min.js`（CDN） | Bootstrap 5 组件（Tab、Modal 等） |
| 2 | **`_shared/storage-keys.js`**（新增） | `STORAGE_KEYS` + `getCurrentUserId()` + `MODULE_ICON_MAP` |
| 3 | `_shared/tab-bar.js` | `FIXED_TABS` + `inferActiveTabId()` + `TabManager` + `mountTabBar()` |
| 4 | `profile-data.js` | `PROFILE_USER` + 渲染函数 + 数据渲染监听器 |
| 5 | 内联 `<script>` | `setProfileTab()` + `mountTabBar()` 调用监听器 |

---

## 五、验证清单

- [x] profile/index.html 已引用 `../_shared/storage-keys.js`（line 453）
- [x] 加载顺序：bootstrap → storage-keys → tab-bar → profile-data → 内联
- [x] `STORAGE_KEYS.ACTIVE_TAB` 与 `getCurrentUserId()` 已定义
- [x] `mountTabBar()` 完整执行链路：`renderTabBar` → `inferActiveTabId` → `TabManager.saveActiveId` → 成功
- [x] `<div id="tab-bar"></div>` 被替换为 10 Tab HTML
- [x] `bindTabBarEvents()` 绑定点击 + Ctrl+1~9/0 快捷键
- [x] 打开 `profile/index.html` 显示 10 个主菜单 Tab

---

## 六、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（1 原型 + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性（不动业务逻辑） | ✅ 无影响 |
| 3 | 计算方法一致性 | ✅ 无影响 |
| 4 | 冲突解决（与全站 25 个原型加载顺序对齐） | ✅ 已对齐 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.55 + 2026-07-21） | ✅ 已标注 |

---

## 七、回退方案

```bash
git revert HEAD  # 撤销 v2.13.55（仅文档 + 1 行 script 引用）
```

---

## 八、附录：v2.13.49 ~ v2.13.55 完整版本演进

| 版本 | 日期 | 关键变更 | 文档 |
|------|------|---------|------|
| v2.13.49 | 2026-07-21 | Profile 重构 3 Tab → 8 子菜单（与基础资料 1:1） | 102 |
| v2.13.50 | 2026-07-21 | 新建 profile/index.html 原型（v2.13.50 初次创建时遗漏 storage-keys.js） | 103 |
| v2.13.51 | 2026-07-21 | 26 个原型 user-pill `<span>` → `<a>` 修复 | 104 |
| v2.13.52 | 2026-07-21 | tab-bar.js 移除 /profile/ 拦截 → 全站统一渲染 10 Tab | 105 |
| v2.13.53 | 2026-07-21 | 文档深度清理：profile/index.html 版本标识升级 + 01 §3.6.10 增量 + INDEX 时间线 | 106 |
| v2.13.54 | 2026-07-21 | profile/index.html 补全 `mountTabBar()` 调用（仅必要条件） | 106 §八 |
| **v2.13.55** | **2026-07-21** | **profile/index.html 补全 `storage-keys.js` 加载（充分条件，主菜单 Tab 真正显示）** | **107** |

---

## 九、教训总结

**v2.13.50 初次创建 profile/index.html 时遗漏的依赖链**：
1. ❌ `_shared/storage-keys.js`（v2.13.55 修复）
2. ❌ `mountTabBar()` 调用（v2.13.54 修复）

**后续创建新原型时的强制检查清单**：
- [ ] `<script src="../_shared/storage-keys.js">` 在 tab-bar.js **之前**加载
- [ ] DOMContentLoaded 内调用 `mountTabBar({ basePath: '..', currentUrl: 'XXX/YYY.html' })`
- [ ] `<div id="tab-bar"></div>` 占位元素已添加
- [ ] 测试：打开页面验证 10 个 Tab 正常渲染