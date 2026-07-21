# Basics 100% 原型对齐（v2.13.45）

> **版本**：v2.13.45
> **日期**：2026-07-21
> **类型**：1 页面 P1/P2 文案与样式微调
> **影响文件**：`Basics/Index.cshtml` + `_Layout.cshtml` + `NotifyIconManager.cs`

---

## 一、审计结果

| 维度 | 满分 | 得分 |
|------|------|------|
| 字段 | 30 | 28 |
| 跳转/路由 | 15 | 14 |
| 按钮 | 25 | 22 |
| 样式 | 30 | 26 |
| **合计** | **100** | **90** |

**评级**：**A 级 — 优秀对齐**

无 P0 必修项，全部差距集中在文案/命名/样式层面。

---

## 二、v2.13.45 实施变更

### P1-1：班组列名"排序"→"排序号"

**文件**：`Basics/Index.cshtml` line 271

**变更**：

```html
<!-- 修复前 -->
<th>排序</th>
<!-- 修复后 -->
<th>排序号</th>
```

**理由**：与 Modal 标签 `editSort`（"排序号"）保持字段命名一致。

### P2-1：页头改用全站 `page-header` 规范

**文件**：`Basics/Index.cshtml` line 10-14

**变更**：

```html
<!-- 修复前 -->
<div class="d-flex justify-content-between align-items-center mb-3">
    <h2 class="mb-0"><i class="bi bi-database"></i> 基础资料</h2>
</div>

<!-- 修复后 -->
<div class="page-header">
    <h2>
        <i class="bi bi-database header-icon"></i> 基础资料
        <span class="header-count">10 类数据字典</span>
    </h2>
</div>
```

**理由**：与全站其他页面（Dashboard/Booking/Dorms 等）共用 `.page-header` 规范类。

### 未改造项（标记 v2.14）

- **P1-3 `.card` → `.content-card`**：当前 Bootstrap `.card` 视觉效果已接近 content-card，影响小
- **P2-2 colspan 硬编码**：JS 动态 colspan 重构量大
- **P2-3 Badge 颜色定制**：全站统一颜色调整应放在 Settings 阶段
- **P2-4 行内搜索**：原型亦未提供
- **P2-5 删除二次确认 Modal**：与全站统一风格调整放入 Settings
- **P2-6 API 路径核查**：当前路径命名与后端 Controller 已对齐

---

## 三、验证清单

- [x] Basics 班组列名"排序"→"排序号"
- [x] Basics 页头统一为 `.page-header` 规范
- [x] `_Layout.cshtml` brand-version → v2.13.45
- [x] `NotifyIconManager.cs` → v2.13.45
- [x] `dotnet build DormManage.sln -c Release` → 0 错误
- [x] 2 项目 publish-final/ 发布（Admin + TrayApp，Api 无版本号）
- [x] UTF-16 验证 v2.13.45（Admin/Tray ✓）
- [ ] Git 提交

---

## 四、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（1 Razor + 2 全局版本号 + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性 | ✅ 不动业务逻辑 |
| 3 | 计算方法一致性 | ✅ 不涉及 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 完全对齐原型 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.45 + 2026-07-21） | ✅ 已标注 |

---

## 五、回退方案

```bash
git revert HEAD  # 撤销 v2.13.45
```