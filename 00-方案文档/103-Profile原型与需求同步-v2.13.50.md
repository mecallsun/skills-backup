# Profile 原型与需求文档同步（v2.13.50）

> **版本**：v2.13.50
> **日期**：2026-07-21
> **类型**：原型同步 + 需求文档升级
> **影响文件**：`04-HTML原型/profile/index.html`（新建）+ `profile-data.js`（新建）+ `80-个人中心与账号安全功能需求文档-v2.13.26.md`（升级到 v2.13.50）+ `05-原型与代码基线对照.md`

---

## 一、变更背景

**v2.13.49 重构内容**：个人中心从 v2.13.46 的 3 Tab（基本资料/账号安全/偏好设置）重构为 8 子菜单（账号总览/基本资料/修改密码/安全问题/微信绑定/偏好设置/筛选缓存/操作日志），与基础资料 1:1 风格。

**v2.13.50 同步任务**：
1. **创建 HTML 原型** `00-方案文档/04-HTML原型/profile/index.html`（与 Razor v2.13.49 1:1 对齐）
2. **创建 Mock 数据** `profile-data.js`（账号信息 + 缓存模块列表 + 操作日志）
3. **升级需求文档** `80-个人中心与账号安全功能需求文档`（v2.13.26 → v2.13.50）
4. **更新基线对照** `05-原型与代码基线对照.md`（25 → 26 个原型）

---

## 二、HTML 原型（profile/index.html）

### 2.1 整体结构（与基础资料 1:1）

```
┌──────────────────────────────────────────────────────────────┐
│  Tier 1: 顶部品牌栏（48px）                                     │
├──────────────────────────────────────────────────────────────┤
│  Tier 3: Tab 页签栏（40px）                                     │
├──────────────────────────────────────────────────────────────┤
│  page-header: 图标 + 个人中心 + 「8 类账号设置」 计数            │
│                                                              │
│  ┌────────────┐  ┌─────────────────────────────────────────┐ │
│  │ 200px pills │  │ tab-content（8 个 pane）                  │ │
│  │ (profile-   │  │                                          │ │
│  │  nav)       │  │  pane-overview / pane-profile / ...      │ │
│  └────────────┘  └─────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

### 2.2 8 个子菜单（原型映射）

| # | pane id | 子菜单 | 图标 | 演示数据 |
|---|---------|--------|:----:|----------|
| 1 | `pane-overview` | 账号总览 | `bi-person-vcard` | 用户名 admin / 显示名 系统管理员 / 角色 系统管理员 / 手机 13800138000 / 邮箱 admin@jinge.local / 最近登录 2026-07-21 09:18 192.168.1.50 / 微信已绑定 |
| 2 | `pane-profile` | 基本资料 | `bi-person` | 显示名/手机/邮箱表单 + 当前密码二次验证 |
| 3 | `pane-password` | 修改密码 | `bi-key` | 原密码 + 新密码（带 4 级强度条 弱/中/强）+ 确认新密码 |
| 4 | `pane-security` | 安全问题 | `bi-shield-question` | 问题 1: 您母亲的名字是？ / 问题 2: 您第一所小学的名称是？ |
| 5 | `pane-wechat` | 微信绑定 | `bi-wechat` | 已绑定 OpenID 脱敏展示（o6_bm1JZq_xxx...）+ 解绑按钮 |
| 6 | `pane-prefs` | 偏好设置 | `bi-sliders` | 深色模式 / 紧凑布局 / Enter 提交 / Esc 关闭 4 个 Switch |
| 7 | `pane-filter` | 筛选缓存 | `bi-hdd` | 7 模块缓存列表 + 清除云端 + 清除本地 |
| 8 | `pane-logs` | 操作日志 | `bi-clock-history` | v2.14 实施预留（表格占位 + 数据源说明） |

### 2.3 URL ?tab= 持久化

```javascript
function setProfileTab(tab) {
    var url = new URL(window.location.href);
    url.searchParams.set('tab', tab);
    window.history.replaceState({}, '', url);
}
document.addEventListener('DOMContentLoaded', function() {
    var url = new URL(window.location.href);
    var tab = url.searchParams.get('tab');
    if (tab) {
        var btn = document.querySelector('.profile-nav button[data-bs-target="#pane-' + tab + '"]');
        if (btn) btn.click();
    }
});
```

---

## 三、Mock 数据（profile-data.js）

### 3.1 数据结构

```javascript
const PROFILE_USER = {
    userId: 1, userName: 'admin', displayName: '系统管理员',
    roles: ['系统管理员'], mobile: '13800138000', email: 'admin@jinge.local',
    lastLoginAt: '2026-07-21 09:18', lastLoginIp: '192.168.1.50',
    isWeChatBound: true, weChatOpenId: 'o6_bm1JZq_xxx...', weChatBindAt: '2026-05-12 14:30'
};

const PROFILE_CACHE_MODULES = [
    { moduleName: 'personnel',     moduleDisplay: '人员清单', updatedAt: '2026-07-19 16:24' },
    { moduleName: 'booking',       moduleDisplay: '办理登记', updatedAt: '2026-07-20 09:15' },
    { moduleName: 'meter',         moduleDisplay: '抄表记录', updatedAt: '2026-07-18 11:02' },
    // ... 7 模块
];

const PROFILE_SECURITY_QUESTIONS = [
    { qNo: 1, question: '您母亲的名字是？', answerMasked: '已设置（密文存储）' },
    { qNo: 2, question: '您第一所小学的名称是？', answerMasked: '已设置（密文存储）' }
];

const PROFILE_OPERATION_LOGS = [
    { time: '2026-07-21 09:18:23', ip: '192.168.1.50', action: 'Login', target: '登录系统' },
    { time: '2026-07-20 17:42:11', ip: '192.168.1.50', action: 'ChangePassword', target: '修改密码' },
    // ... v2.14 启用
];
```

### 3.2 渲染函数

- `renderOverview(user)` — 渲染账号总览
- `renderCacheList(items)` — 渲染筛选缓存列表
- `renderOpLogs(logs)` — 渲染操作日志（v2.14）
- DOMContentLoaded 自动调用

---

## 四、需求文档升级（80 号 → v2.13.50）

### 4.1 关键变更

| 段落 | 变更 |
|------|------|
| 版本号 | v2.13.26 → **v2.13.50** |
| 范围 | 4 大模块 → **8 大子菜单**（v2.13.49 重构） |
| 结论 | ✅ 4 项 → **✅ 8 子菜单 + 18 项功能 100% 已实现** |
| 增量说明 | 新增「v2.13.50 增量说明」章节（3 Tab → 8 子菜单对照表 + 对齐基础资料说明 + 原型同步说明） |
| 1.1 功能模块 | 重写为 8 行表格（含图标 + 实现版本 + 子菜单拆分理由） |
| 1.2 用户决策 | 新增「子菜单结构」「头部风格」2 项决策 |

### 4.2 子菜单拆分对照表（v2.13.50 新增）

| 原 Tab（v2.13.46） | 新子菜单（v2.13.49+） | 拆分理由 |
|--------------------|----------------------|----------|
| 左侧固定账号信息卡 | **账号总览** | 复用到 tab-pane |
| 基本资料 | **基本资料** | 不变 |
| 账号安全 → 修改密码 | **修改密码** | 高频操作独立入口 |
| 账号安全 → 安全问题 | **安全问题** | 密码找回场景独立入口 |
| 账号安全 → 微信绑定 | **微信绑定** | 第三方登录独立入口 |
| 偏好设置 → 界面偏好 | **偏好设置** | 拆出独立入口 |
| 偏好设置 → 筛选缓存 | **筛选缓存** | 面向数据存储独立入口 |
| （无） | **操作日志** 🆕 | v2.14 实施 |

---

## 五、基线对照表升级（05 号）

| # | 原型 | Razor 页面 | v2.13.50 状态 |
|---|------|------------|--------------|
| 25 | ~~`dorms/details.html`~~ | ~~`Dorms/Details.cshtml`~~ | — |
| **26** | **`profile/index.html`** 🆕 | `Account/Profile.cshtml` | ✅ ✅ ✅ |

**v2.13.50 修正**：新增第 26 行 `profile/index.html` 原型，实际为 **26 个原型 ↔ 26 个 Razor 视图**。

---

## 六、验证清单

- [x] `04-HTML原型/profile/index.html` 新建（与基础资料 1:1 风格 + 8 子菜单）
- [x] `04-HTML原型/profile/profile-data.js` 新建（PROFILE_USER + PROFILE_CACHE_MODULES + PROFILE_SECURITY_QUESTIONS + PROFILE_OPERATION_LOGS）
- [x] `80-个人中心与账号安全功能需求文档` 升级 v2.13.26 → v2.13.50（新增 v2.13.50 增量说明 + 8 子菜单表格）
- [x] `05-原型与代码基线对照.md` 新增第 26 行（profile/index.html）
- [ ] Git 提交

---

## 七、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（2 原型文件 + 2 需求/基线文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性（与 v2.13.49 Razor 实现 1:1 对齐） | ✅ 已对齐 |
| 3 | 计算方法一致性（密码强度 4 级与 Razor profile.js 一致） | ✅ 已保留 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 原型与 Razor 实现同步 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.50 + 2026-07-21） | ✅ 已标注 |

---

## 八、回退方案

```bash
git revert HEAD  # 撤销 v2.13.50 原型与文档同步
```