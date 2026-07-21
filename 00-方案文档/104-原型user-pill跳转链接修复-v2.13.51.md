# 原型 user-pill 跳转链接修复（v2.13.51）

> **版本**：v2.13.51
> **日期**：2026-07-21
> **类型**：HTML 原型 BUG 修复 — 顶部账号链接
> **影响文件**：26 个原型 html + `_shared/layout-tab.css`

---

## 一、BUG 描述

**用户报告原文**：
> 为什么在原型中，点击登录的账号时，没有显示个人中心的子菜单相关信息的页面？

**根因**：
所有 26 个 HTML 原型（booking/dorms/personnel/billing/basics/settings/meter/profile/index）的顶部品牌栏 user-pill 都是用 `<span class="user-pill">` 实现的，**没有 `<a>` 链接**，无法点击跳转。

**对比 Razor `_Layout.cshtml`**（已正确）：
```html
<a href="/Account/Profile" class="user-pill">
    <i class="bi bi-person-circle"></i>
    @displayName
</a>
```

**Razor 模板已正确实现**，但 HTML 原型**遗漏了跳转链接**，导致点击无响应。

---

## 二、修复方案

### 2.1 批量替换 26 个原型的 user-pill

```html
<!-- 修复前（所有 26 个原型） -->
<span class="user-pill">
    <i class="bi bi-person-circle"></i>
    <span>管理员</span>
</span>

<!-- 修复后 -->
<a class="user-pill" href="../profile/index.html">
    <i class="bi bi-person-circle"></i>
    <span>管理员</span>
</a>
```

**链接路径规则**（按层级）：
| 原型层级 | href 值 |
|---------|---------|
| `04-HTML原型/index.html`（顶层） | `profile/index.html` |
| `04-HTML原型/profile/index.html`（自身） | `../profile/index.html`（自刷新） |
| `04-HTML原型/{module}/index.html` 等（1 级子目录） | `../profile/index.html` |

### 2.2 共享 CSS 补充（layout-tab.css）

```css
/* v2.13.51 补充：user-pill 作为链接的样式 */
.top-bar .user-pill {
    /* 保留原有视觉 */
    background: #f5f7fa;
    border-radius: 24px;
    padding: 0.3rem 0.8rem;
    font-size: 0.85rem;
    display: flex;
    align-items: center;
    gap: 0.4rem;
    /* 新增 */
    text-decoration: none;
    color: #5f6368;
    transition: background 0.15s;
}
.top-bar a.user-pill:hover {
    background: #e8eaed;
    color: #1976d2;
}
```

---

## 三、批量修复结果

```
✅ 04-HTML原型/index.html                  → profile/index.html
✅ 04-HTML原型/basics/index.html           → ../profile/index.html
✅ 04-HTML原型/billing/dorm-bills.html     → ../profile/index.html
✅ 04-HTML原型/billing/employee-bills.html → ../profile/index.html
✅ 04-HTML原型/billing/standard-form.html  → ../profile/index.html
✅ 04-HTML原型/billing/standards.html      → ../profile/index.html
✅ 04-HTML原型/booking/check-in.html       → ../profile/index.html
✅ 04-HTML原型/booking/check-out.html      → ../profile/index.html
✅ 04-HTML原型/booking/edit.html           → ../profile/index.html
✅ 04-HTML原型/booking/index.html          → ../profile/index.html
✅ 04-HTML原型/dorms/create.html           → ../profile/index.html
✅ 04-HTML原型/dorms/details.html          → ../profile/index.html
✅ 04-HTML原型/dorms/edit.html             → ../profile/index.html
✅ 04-HTML原型/dorms/history.html          → ../profile/index.html
✅ 04-HTML原型/dorms/list.html             → ../profile/index.html
✅ 04-HTML原型/meter/detail.html           → ../profile/index.html
✅ 04-HTML原型/meter/edit.html             → ../profile/index.html
✅ 04-HTML原型/meter/entry.html            → ../profile/index.html
✅ 04-HTML原型/meter/import.html           → ../profile/index.html
✅ 04-HTML原型/meter/index.html            → ../profile/index.html
✅ 04-HTML原型/personnel/create.html       → ../profile/index.html
✅ 04-HTML原型/personnel/edit.html         → ../profile/index.html
✅ 04-HTML原型/personnel/import.html       → ../profile/index.html
✅ 04-HTML原型/personnel/list.html         → ../profile/index.html
✅ 04-HTML原型/profile/index.html          → ../profile/index.html（自刷新）
✅ 04-HTML原型/settings/index.html         → ../profile/index.html

合计：26 个原型全部修复
```

---

## 四、修复效果

| 场景 | 修复前 | 修复后 |
|------|--------|--------|
| 打开任意原型页面 | 顶部「管理员」是 span，无点击响应 | 「管理员」是可点击链接 ✅ |
| 点击「管理员」胶囊 | 无反应 ❌ | 跳转到 `profile/index.html` ✅ |
| hover 效果 | 无 | 背景色变深 + 文字变蓝 ✅ |
| 与 Razor 实现一致性 | 不一致 ❌ | 完全一致 ✅ |

---

## 五、验证清单

- [x] 26 个原型 user-pill `<span>` → `<a>` 替换
- [x] `_shared/layout-tab.css` 补充 `.user-pill` 链接样式（text-decoration: none + hover）
- [x] 链接路径按层级自动计算（顶层/1 级子目录/自身）
- [x] profile/index.html 自身链接到 `../profile/index.html`（自刷新）
- [x] Git 提交

---

## 六、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（26 原型 + 1 共享 CSS + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性（不动数据） | ✅ 无影响 |
| 3 | 计算方法一致性 | ✅ 无影响 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 与 Razor _Layout 一致 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.51 + 2026-07-21） | ✅ 已标注 |

---

## 七、回退方案

```bash
git revert HEAD  # 撤销 v2.13.51 user-pill 跳转链接修复
```