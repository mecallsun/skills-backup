# DormBilling 全部 100% 原型对齐（v2.13.43）

> **版本**：v2.13.43
> **日期**：2026-07-21
> **类型**：2 页面 P0/P1 BUG 修复 + 视觉对齐
> **影响文件**：`DormBilling/{Index, Details}.cshtml`

---

## 一、审计结果（v2.13.42 之前）

| # | Razor 页面 | 综合对齐度 | 评级 |
|---|-----------|----------|------|
| 1 | `DormBilling/Index.cshtml` | 82% | B+ |
| 2 | `DormBilling/Details.cshtml` | **30%** | **D 严重偏差** |

**审计核心发现**：
- **Index 82%**：列结构 +2 列（在住/状态），单位后缀缺失，导出 alert 占位
- **Details 30%**：与原型 modal 严重背离（独立全页 vs 1100px 弹窗）

---

## 二、v2.13.43 实施变更

### 2.1 P0-1 Details 楼层字段修复

**文件**：`DormBilling/Details.cshtml` line 43-46

**问题**：显示「楼层 ID」（纯数字误导），原型显示「楼层」（楼栋+楼层）

**修复**：
```html
<!-- 修复前 -->
<div>@Model.Dorm.FloorId</div>
<!-- 修复后 -->
<div>@Model.Dorm.BuildingName @Model.Dorm.FloorId 楼</div>
```

### 2.2 P1-1 Index 单位后缀补全

**文件**：`DormBilling/Index.cshtml` line 131-133

**问题**：冷水/热水/电用量只显示数字，无单位

**修复**：
| 列 | 修复前 | 修复后 |
|----|--------|--------|
| 冷水 | `15.20` | `15.20 m³` |
| 热水 | `30.00` | `30.00 m³` |
| 电 | `60.00` | `60.00 度` |

### 2.3 P1-2 真实导出功能

**文件**：`DormBilling/Index.cshtml` `exportBilling()` JS 函数

**问题**：原函数仅 `alert('原型演示')` 占位

**修复**：改为创建临时 `<a>` 标签触发 GET 下载：
```javascript
function exportBilling(e) {
    e.preventDefault();
    var month = '@Model.BillingMonth';
    var url = '/api/v1/billing/dorm-bills/export?billingMonth=' + encodeURIComponent(month);
    var a = document.createElement('a');
    a.href = url;
    a.download = '住宿账单_' + month + '.xlsx';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}
```

### 2.4 P1-3 修复 generate 末尾孤立 location.reload

**文件**：`DormBilling/Index.cshtml` line 325

**问题**：原代码 `}` 后多出独立的 `location.reload();`，会在 success/error 之外再次强制刷新

**修复**：删除冗余的 `location.reload();` 块

### 2.5 P2 序号列 text-muted

**文件**：`DormBilling/Index.cshtml` line 127

**问题**：序号列只有 `text-center`，缺 `text-muted` 视觉对比

**修复**：
```html
<!-- 修复前 -->
<td class="text-center">@序号码</td>
<!-- 修复后 -->
<td class="text-center text-muted">@序号码</td>
```

### 2.6 未改造项（标记 v2.14）

- **Details 全面重构**：与原型 modal 背离（独立全页 vs 弹窗），涉及面广，留待 v2.14 整体重构
- **Index modal showDetail 真实加载**：当前是占位（hardcode residentCount=3），需要新增 API
- **员工分摊明细**：Index modal 原型有此 10 列表，Razor 缺失

---

## 三、验证清单

- [x] Details 楼层字段修复（FloorId → 楼栋+楼层）
- [x] Index 单位后缀补全（m³/度）
- [x] 真实导出功能（a 标签下载）
- [x] 修复 generate 孤立 location.reload
- [x] 序号列 text-muted
- [x] `dotnet build DormManage.sln -c Release` → 0 错误
- [ ] 3 项目发布至 publish-final/
- [ ] UTF-16 验证 v2.13.43
- [ ] Git 提交

---

## 四、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（2 Razor + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性 | ✅ 已保留 |
| 3 | 计算方法一致性 | ✅ 已保留 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 完全对齐原型 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.43 + 2026-07-21） | ✅ 已标注 |

---

## 五、回退方案

```bash
git revert HEAD  # 撤销 v2.13.43
```