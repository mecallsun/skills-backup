# Profile 禁止作为第 11 主菜单原则（v2.13.56）

> **版本**：v2.13.56
> **日期**：2026-07-21
> **类型**：**设计原则硬约束**（不可违反）
> **结论**：❌ **禁止在 10 主菜单 Tab 栏中新增第 11 项「个人中心」**
> **入口**：✅ **仅通过顶部「用户胶囊」`<a class="user-pill" href="../profile/index.html">` 进入**

---

## 一、原则声明（不可违反）

```
┌─────────────────────────────────────────────────────────────────┐
│  ⚠️ 硬性设计原则（v2.13.56 起固定）                                │
│                                                                 │
│  ❌ 禁止修改 _shared/tab-bar.js 中的 FIXED_TABS 数组              │
│     增加 { id: 'tab-profile', title: '个人中心', ... }            │
│                                                                 │
│  ❌ 禁止修改 4-页头与 Tab 页签导航的任何位置                       │
│     为 Profile 预留 Tab 位                                       │
│                                                                 │
│  ❌ 禁止在任何原型/Razor 页面的 Tier 3 Tab 栏                      │
│     渲染与 Profile 相关的 Tab                                    │
│                                                                 │
│  ✅ Profile 仅通过以下入口进入：                                   │
│     <a class="user-pill" href="../profile/index.html">            │
│                                                                 │
│  ✅ Profile 是「二级子页面」，不属于 10 主菜单业务模块              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 二、为何禁止新增第 11 项

### 2.1 10 主菜单 Tab 的设计意图（v2.12.2 起固定）

10 个 Tab 对应**业务模块**，每个 Tab 都关联一个具体的业务操作领域：

| # | Tab | 业务模块 | 对应实体 |
|---|-----|---------|---------|
| 1 | `tab-home` | 首页 | DashboardService（KPI/图表） |
| 2 | `tab-booking` | 办理登记 | DormBooking |
| 3 | `tab-dorms` | 住宿管理 | Dorm |
| 4 | `tab-personnel` | 人员清单 | SysEmployee |
| 5 | `tab-billing` | 费用标准 | BillingStandard |
| 6 | `tab-dorm-bills` | 住宿账单 | DormBilling |
| 7 | `tab-employee-bills` | 员工账单 | EmployeeBilling |
| 8 | `tab-meter` | 抄表记录 | MeterRecord |
| 9 | `tab-basics` | 基础资料 | Department/Building/Floor/... |
| 10 | `tab-settings` | 系统设置 | SysUser/SysRole/SysParameter/... |

### 2.2 Profile 不属于业务模块

**Profile 是账号设置（个人维度）**，不是业务模块（系统维度）：

| 维度 | 业务模块（10 主菜单） | Profile（个人账号设置） |
|------|---------------------|------------------------|
| **作用对象** | 系统数据（住宿/人员/账单） | 当前登录用户 |
| **数据范围** | 全部数据 | 仅本人数据 |
| **导航上下文** | 横向切换业务模块 | 顶部胶囊 → 个人子页面 |
| **Tab 高亮** | 当前业务模块 | 默认 tab-home（不影响） |
| **快捷键** | Ctrl+1~9/0 | 无独立快捷键 |
| **角色权限** | 受 RBAC 管控 | 不受 RBAC 管控（仅本人） |

**结论**：Profile 在**用户视角、权限模型、数据范围**上都与 10 主菜单 Tab 完全不同，**强行纳入会导致以下问题**：

### 2.3 强行新增第 11 项的 4 大副作用

| # | 副作用 | 后果 |
|---|--------|------|
| 1 | **Ctrl+1~9/0 快捷键失效** | 现有 10 个 Tab 用 Ctrl+1~9/0 切换；新增第 11 项后没有 Ctrl+ 对应 |
| 2 | **Tab 栏溢出** | 10 个 Tab 在 1920×1080 已接近满屏；新增 11 项需滚动 |
| 3 | **用户视角混乱** | Profile 与业务模块并列，破坏"业务 vs 账号"的清晰分层 |
| 4 | **权限模型割裂** | Profile 不受 RBAC 管控但 Tab 高亮与其他业务 Tab 视觉一致 |

---

## 三、唯一允许的入口

### 3.1 顶部品牌栏用户胶囊（v2.13.51 修复）

```html
<!-- Tier 1 顶部品牌栏 -->
<div class="top-bar">
    ...
    <a class="user-pill" href="../profile/index.html">
        <i class="bi bi-person-circle"></i>
        <span>管理员</span>
    </a>
    ...
</div>
```

**全 26 个原型 + 26 个 Razor 页面**统一使用此入口：
- `04-HTML原型/index.html` → `profile/index.html`
- `04-HTML原型/{module}/index.html` 等 1 级子目录 → `../profile/index.html`
- `04-HTML原型/profile/index.html`（自身）→ `../profile/index.html`（自刷新）
- `DormManage.Admin/Pages/Shared/_Layout.cshtml` → `/Account/Profile`

### 3.2 入口的视觉标识

```
┌──────────────────────────────────────────────────────────────┐
│ [Logo] 金智住宿管理系统 v2.13.55  [DB]  [👤 管理员]  [退出]   │
│                                              ↑                │
│                                          唯一入口              │
└──────────────────────────────────────────────────────────────┘
```

### 3.3 进入 Profile 后的层级关系

```
主菜单（Tier 3，横向 Tab，固定 10 个，业务模块）：
├ 首页 / 办理登记 / 住宿管理 / 人员清单 / 费用标准 /
├ 住宿账单 / 员工账单 / 抄表记录 / 基础资料 / 系统设置
│
└ [用户胶囊点击 → Profile 入口（不进入主菜单 Tab 栏）]
    ↓
二级子页面（Tier 4 内，左侧 200px pills，垂直，账号设置）：
├ 账号总览 / 基本资料 / 修改密码 / 安全问题 /
├ 微信绑定 / 偏好设置 / 筛选缓存 / 操作日志
```

**关键**：Profile 是**「Tier 4 二级子页面」**，**永远不是 Tier 3 主菜单 Tab**。

---

## 四、技术约束（代码层面）

### 4.1 tab-bar.js FIXED_TABS 数组硬约束

```javascript
// _shared/tab-bar.js
const FIXED_TABS = [
  { id: 'tab-home',          title: '首页',       ... },  // 1
  { id: 'tab-booking',       title: '办理登记',   ... },  // 2
  { id: 'tab-dorms',         title: '住宿管理',   ... },  // 3
  { id: 'tab-personnel',     title: '人员清单',   ... },  // 4
  { id: 'tab-billing',       title: '费用标准',   ... },  // 5
  { id: 'tab-dorm-bills',    title: '住宿账单',   ... },  // 6
  { id: 'tab-employee-bills',title: '员工账单',   ... },  // 7
  { id: 'tab-meter',         title: '抄表记录',   ... },  // 8
  { id: 'tab-basics',        title: '基础资料',   ... },  // 9
  { id: 'tab-settings',      title: '系统设置',   ... }   // 10
];
// ❌ 禁止添加：{ id: 'tab-profile', title: '个人中心', url: 'profile/index.html' }
// ❌ 禁止减少或重新编号现有 10 项
```

**变更流程**：
- 修改 FIXED_TABS 数组 → 必须先创建 ADR（架构决策记录）并经用户确认
- 任何 PR 修改此数组 → Code Review 必驳回

### 4.2 mountTabBar 行为硬约束

```javascript
function mountTabBar(opts) {
  const el = document.getElementById('tab-bar');
  if (!el) return;

  // ⚠️ v2.13.52 设计决策：所有页面（包括 /profile/）统一渲染 Tab 栏
  // 个人中心页面保留顶部品牌栏 + 10 主菜单 Tab 导航，与全站 26 个 Razor/HTML 保持一致
  // v2.13.56 强化：Profile 不属于主菜单 Tab，仅作为子页面通过顶部胶囊进入
  el.outerHTML = renderTabBar(opts);
  bindTabBarEvents(opts);
}
```

**禁止行为**：
- ❌ 在 mountTabBar 内根据 URL 跳过 Tab 渲染
- ❌ 为 Profile 添加特殊的 Tab 高亮逻辑
- ❌ 在 Profile 页面隐藏其他 10 个 Tab

### 4.3 推断激活 Tab 行为约束

```javascript
function inferActiveTabId(currentUrl, basePath) {
  // ...现有 10 个 Tab 匹配规则...
  // 默认首页（profile 页面也走此分支）
  return 'tab-home';
}
```

**Profile 页面激活 Tab 行为**：
- URL `profile/index.html` 不匹配任何前缀
- **默认高亮 `tab-home`**（仅视觉提示，不影响用户操作）
- 用户可自由点击其他 9 个 Tab 跳转

---

## 五、文档同步清单

### 5.1 已更新文档

| 文档 | 状态 |
|------|------|
| 105-Profile原型保留主菜单导航设计-v2.13.52.md | ✅ 已存在（保留 10 Tab 设计） |
| 106-Profile导航过时描述深度清理-v2.13.53.md | ✅ 已存在 |
| 107-Profile原型JS依赖链缺失修复-v2.13.55.md | ✅ 已存在 |
| **108-Profile禁止作为第11主菜单原则-v2.13.56.md** | ✅ **本文档（新增硬约束）** |

### 5.2 需要更新的位置

| 文件 | 更新内容 |
|------|---------|
| `01-技术架构与系统开发方案.md` | §3.6.10 增量补充 v2.13.56 原则 |
| `CLAUDE.md` | 「Important Notes」增加 v2.13.56 设计原则 |
| `INDEX.md` | 时间线追加 v2.13.56 条目 |

---

## 六、PR / Code Review 检查清单

任何涉及 Tier 3 Tab 栏或 Profile 入口的 PR 必须满足：

- [ ] **未修改** `tab-bar.js` 的 `FIXED_TABS` 数组（保持 10 项）
- [ ] **未新增** `tab-profile` 相关 Tab ID
- [ ] **未在 Profile 页面** 隐藏其他 10 个 Tab
- [ ] **Profile 入口仍为** 顶部 `user-pill` `<a>` 链接
- [ ] **未引入** `tab-profile` 相关 CSS class
- [ ] **已阅读** 本文 §一 原则声明

---

## 七、回退方案

```bash
git revert HEAD~6..HEAD  # 撤销 v2.13.49 ~ v2.13.56（如需彻底回退）
git revert HEAD           # 仅撤销 v2.13.56 本原则文档
```

---

## 八、附录：Profile 与 10 主菜单的层级关系图

```
┌─────────────────────────────────────────────────────────────────┐
│                         全站导航架构                              │
├─────────────────────────────────────────────────────────────────┤
│  Tier 1: 顶部品牌栏                                              │
│  ├ 系统名 + 版本号 + DB 徽章                                     │
│  └ 👤 用户胶囊 [唯一 Profile 入口]                                │
├─────────────────────────────────────────────────────────────────┤
│  Tier 3: 10 主菜单 Tab 栏（v2.13.56 起硬约束固定 10 项）          │
│  ├ 首页 | 办理登记 | 住宿管理 | 人员清单 | 费用标准 |             │
│  └ 住宿账单 | 员工账单 | 抄表记录 | 基础资料 | 系统设置          │
├─────────────────────────────────────────────────────────────────┤
│  Tier 4: 业务页面                                                │
│  ├ 业务模块页（10 Tab 对应）                                     │
│  └ [Profile 子页面 — 不通过 Tab 进入，仅通过用户胶囊]            │
│     └ 左侧 200px pills（8 子菜单）+ 右侧 8 pane                  │
└─────────────────────────────────────────────────────────────────┘
```

**关键**：**Tier 1 → Tier 3 → Tier 4（业务模块页）** 是主流程；**Tier 1 → Tier 4（Profile 子页面）** 是账号设置支线。

**Profile 永远不上 Tier 3 Tab 栏**。

---

## 九、版本演进附录

| 版本 | 日期 | 关键变更 | 文档 |
|------|------|---------|------|
| v2.13.49 | 2026-07-21 | Profile 重构 3 Tab → 8 子菜单 | 102 |
| v2.13.50 | 2026-07-21 | 新建 profile/index.html 原型 | 103 |
| v2.13.51 | 2026-07-21 | 26 个原型 user-pill 跳转修复 | 104 |
| v2.13.52 | 2026-07-21 | 保留主菜单导航设计定稿 | 105 |
| v2.13.53 | 2026-07-21 | 文档深度清理 | 106 |
| v2.13.54 | 2026-07-21 | mountTabBar 调用补全 | 106 §八 |
| v2.13.55 | 2026-07-21 | storage-keys.js 依赖补全 | 107 |
| **v2.13.56** | **2026-07-21** | **Profile 禁止作为第 11 主菜单原则硬约束** | **108** |