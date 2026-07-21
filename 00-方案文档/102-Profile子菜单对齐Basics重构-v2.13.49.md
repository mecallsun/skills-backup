# Profile 子菜单对齐 Basics 重构（v2.13.49）

> **版本**：v2.13.49
> **日期**：2026-07-21
> **类型**：UI 重构 — 个人中心子菜单结构调整
> **影响文件**：`Account/Profile.cshtml` + `Account/Profile.cshtml.cs` + `_Layout.cshtml` + `NotifyIconManager.cs`

---

## 一、问题背景

**用户要求**：调整个人中心的子菜单结构与风格，按基础资料子菜单的完整一致。

**重构前个人中心结构**（v2.13.46）：
```
┌─────────────────────────────────────────────────────┐
│  个人中心                                            │
├──────────────────────┬──────────────────────────────┤
│  左侧账号信息卡       │  右侧顶部 nav-tabs（3 个）    │
│  (固定信息展示)       │  ├ 基本资料                  │
│                      │  ├ 账号安全                  │
│                      │  └ 偏好设置                  │
│                      │  下方 tab-content             │
└──────────────────────┴──────────────────────────────┘
```

**重构后个人中心结构**（v2.13.49）：
```
┌─────────────────────────────────────────────────────┐
│  个人中心                              8 类账号设置   │
├──────────────────────┬──────────────────────────────┤
│  左侧 200px pills    │  右侧 tab-content（8 个）    │
│  ├ 账号总览           │  ├ pane-overview            │
│  ├ 基本资料           │  ├ pane-profile             │
│  ├ 修改密码           │  ├ pane-password            │
│  ├ 安全问题           │  ├ pane-security            │
│  ├ 微信绑定           │  ├ pane-wechat              │
│  ├ 偏好设置           │  ├ pane-prefs               │
│  ├ 筛选缓存           │  ├ pane-filter              │
│  └ 操作日志           │  └ pane-logs                │
└──────────────────────┴──────────────────────────────┘
```

---

## 二、与 Basics 结构 1:1 对齐

### 2.1 顶层容器（与 Basics 一致）

```html
<!-- v2.13.49：与 Basics 完全一致的 page-header -->
<div class="page-header">
    <h2>
        <i class="bi bi-person-circle header-icon"></i> 个人中心
        <span class="header-count">8 类账号设置</span>
    </h2>
</div>

<!-- v2.13.49：与 Basics 完全一致的 200px pills + tab-content -->
<div style="display: flex; align-items: flex-start; gap: 12px;">
    <div style="flex-shrink: 0; width: 200px;">
        <div class="card">
            <div class="card-body p-2">
                <div class="nav flex-column nav-pills profile-nav" role="tablist">
                    <!-- 8 个 pills -->
                </div>
            </div>
        </div>
    </div>
    <div style="flex: 1 1 0%;">
        <div class="tab-content" id="profileTabContent">
            <!-- 8 个 tab-pane -->
        </div>
    </div>
</div>
```

### 2.2 8 个子菜单项

| # | 子菜单 | 图标（Bootstrap Icons） | 内容 |
|---|--------|:----:|------|
| 1 | 账号总览 | `bi-person-vcard` | 用户名/显示名/角色/手机/邮箱/最近登录/微信绑定状态/退出 |
| 2 | 基本资料 | `bi-person` | 显示名/手机/邮箱/当前密码验证 |
| 3 | 修改密码 | `bi-key` | 原密码 + 新密码 + 确认密码 |
| 4 | 安全问题 | `bi-shield-question` | 2 个问题 + 答案（密文） |
| 5 | 微信绑定 | `bi-wechat` | OpenID 绑定/解绑 |
| 6 | 偏好设置 | `bi-sliders` | 深色模式/紧凑布局（预留） |
| 7 | 筛选缓存 | `bi-hdd` | 筛选条件同步/清除 |
| 8 | 操作日志 | `bi-clock-history` | 本人最近 20 条操作（v2.14 实施） |

### 2.3 与 Basics 的对比

| 维度 | Basics | Profile (v2.13.49 前) | Profile (v2.13.49 后) |
|------|--------|----------------------|---------------------|
| 顶部页头 | `<div class="page-header">` | 自定义 `d-flex justify-content-between` | **`<div class="page-header">` ✅** |
| header-icon | `bi bi-database` | 缺 | **`bi-person-circle header-icon` ✅** |
| header-count | `10 类数据字典` | 缺 | **`8 类账号设置` ✅** |
| 布局容器 | `display: flex; align-items: flex-start; gap: 12px;` | `row` + `col-lg-4/8` 网格 | **与 Basics 1:1 一致 ✅** |
| 左侧导航 | `width: 200px` pills | `col-lg-4` 账号信息卡 | **`width: 200px` pills ✅** |
| 导航 CSS 类 | `nav flex-column nav-pills basics-nav` | `nav-tabs`（顶部横排） | **`nav flex-column nav-pills profile-nav` ✅** |
| pills 样式 | 自定义 6px 圆角 + 蓝色激活态 | 缺 | **与 Basics 1:1 ✅** |
| 右侧内容 | `flex: 1 1 0%;` 多 `tab-pane` | `col-lg-8` 单 tab-content | **与 Basics 1:1 ✅** |
| URL ?tab= 持久化 | ✅ | ❌ | **✅** |

---

## 三、实施细节

### 3.1 子菜单拆分逻辑

原账号安全 Tab 包含 3 个独立功能，拆分为 3 个子菜单（更符合"一菜单一职责"原则）：

| 原 Tab | 拆分为 | 理由 |
|--------|--------|------|
| 账号安全 → 修改密码 | **修改密码** 子菜单 | 高频操作，独立入口 |
| 账号安全 → 安全问题 | **安全问题** 子菜单 | 密码找回场景，独立入口 |
| 账号安全 → 微信绑定 | **微信绑定** 子菜单 | 第三方登录，独立入口 |

原偏好设置 Tab 拆分为：

| 原 Tab | 拆分为 | 理由 |
|--------|--------|------|
| 偏好设置 → 筛选缓存 | **偏好设置** + **筛选缓存** 子菜单 | 前者面向 UI/体验，后者面向数据存储，独立入口 |

### 3.2 新增子菜单

| 子菜单 | 状态 | 说明 |
|--------|------|------|
| 账号总览 | v2.13.49 实施 | 复用原左侧账号信息卡字段，移入 tab-pane |
| 操作日志 | v2.14 实施 | 数据源 `SysOpLog WHERE UserId = [当前登录用户]`；v2.13.49 仅显示预留占位 |

### 3.3 pills 样式（与 Basics 1:1）

```css
.profile-nav .nav-link {
    border-radius: 6px;
    padding: 0.6rem 0.85rem;
    margin-bottom: 2px;
    font-size: 0.88rem;
    color: #495057;
    background: transparent;
}
.profile-nav .nav-link i { margin-right: 0.5rem; font-size: 1rem; }
.profile-nav .nav-link:hover { background: #f0f4f8; }
.profile-nav .nav-link.active {
    background: #1976d2;
    color: #fff;
    font-weight: 500;
}
```

### 3.4 URL ?tab= 持久化（与 Basics 一致）

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

### 3.5 PageModel 扩展

```csharp
/// <summary>v2.13.49 P0：当前激活的左侧导航子菜单（默认 overview）</summary>
[BindProperty(SupportsGet = true)]
public string ActiveTab { get; set; } = "overview";

/// <summary>v2.13.49 P0：子菜单总数（page-header 显示用）</summary>
public int SubMenuCount => 8;
```

---

## 四、验证清单

- [x] Profile.cshtml 重构为左侧 200px pills + 右侧 tab-pane
- [x] 8 个子菜单（账号总览/基本资料/修改密码/安全问题/微信绑定/偏好设置/筛选缓存/操作日志）
- [x] pills 样式与 Basics 1:1（颜色/圆角/激活态）
- [x] 顶部 page-header（与 Basics 一致的 header-icon + header-count）
- [x] Profile.cshtml.cs 新增 ActiveTab + SubMenuCount 属性
- [x] URL ?tab= 持久化（与 Basics 一致）
- [x] `_Layout.cshtml` brand-version → v2.13.49
- [x] `NotifyIconManager.cs` → v2.13.49
- [x] `dotnet build DormManage.sln -c Release` → 0 错误
- [x] 2 项目 publish-final/ 发布
- [x] UTF-16 验证 v2.13.49（Admin/Tray ✓）
- [ ] Git 提交

---

## 五、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（2 Profile 文件 + 2 全局版本号 + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性（不动 SysUserSelfService 业务逻辑） | ✅ 已保留 |
| 3 | 计算方法一致性（不动密码强度/绑定流程） | ✅ 已保留 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 与 Basics 风格一致 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.49 + 2026-07-21） | ✅ 已标注 |

---

## 六、回退方案

```bash
git revert HEAD  # 撤销 v2.13.49
```