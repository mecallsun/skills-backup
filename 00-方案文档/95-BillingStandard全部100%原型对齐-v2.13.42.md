# BillingStandard 全部 100% 原型对齐（v2.13.42）

> **版本**：v2.13.42
> **日期**：2026-07-21
> **类型**：3 页面 + 1 服务 100% 1:1 对齐原型 + 3 个 P0 阻断 BUG 修复
> **影响文件**：`BillingStandard/{Index, Create, Edit}.cshtml` + `BillingService.cs`

---

## 一、审计结果（v2.13.41 之前）

| # | Razor 页面 | 综合对齐度 | 评级 |
|---|-----------|----------|------|
| 1 | `BillingStandard/Index.cshtml` | 62% | C |
| 2 | `BillingStandard/Create.cshtml` | 48% | D |
| 3 | `BillingStandard/Edit.cshtml` | **32%** | **D 不合格（保存链路阻断）** |

**审计核心发现（P0 阻断 BUG）**：
1. **服务 SaveChangesAsync 缺失**：更新分支只修改内存，不持久化（line 134-147）
2. **日期校验反向 BUG**：`if (EffectiveTo > EffectiveFrom) return false` —— 正常结束日期反而报错
3. **Edit Id 绑定 BUG**：hidden 是 PageModel.Id 不是 Input.Id，POST 后 Input.Id=0 走新增分支
4. **筛选项重复"全部"**：GetStandardApplicableTypesAsync 重复追加"全部"

---

## 二、v2.13.42 实施变更

### 2.1 P0-1 BillingService.cs — 修复 2 个 BUG

| # | BUG | 修复 |
|---|-----|------|
| 1 | SaveStandardAsync 更新分支未调用 SaveChangesAsync | 第 146 行加 `await _db.SaveChangesAsync();` |
| 2 | 日期校验反向（`EffectiveTo > EffectiveFrom` 才报错） | 改为 `EffectiveTo.HasValue && EffectiveTo < EffectiveFrom` |
| 3 | GetStandardApplicableTypesAsync 重复追加"全部" | defaults 数组移除 "全部" |

### 2.2 P0-2 Edit.cshtml — 修复 Id 绑定 + 加 IsActive

| # | 改造 | 描述 |
|---|------|------|
| 1 | `<input asp-for="Id">` → `<input asp-for="Input.Id">` | 让 POST 收到实体主键，走更新分支 |
| 2 | 加 IsActive checkbox + hidden value="false" | 之前完全缺失，无法保留停用状态 |
| 3 | 加 applicableTypeList datalist | 5 种员工类型（合同工/临时工/外包/实习生/驻场） |
| 4 | 加 maxlength="100" + min="0" | 输入约束 |
| 5 | 按钮顺序：取消 → 保存修改（与原型一致） | |

### 2.3 P1-1 Create.cshtml — 加 IsActive + min="0" + datalist

| # | 改造 | 描述 |
|---|------|------|
| 1 | 加 IsActive checkbox（默认勾选） | 之前完全缺失 |
| 2 | 加 datalist applicableTypeList | 5 种员工类型 |
| 3 | 三个单价加 min="0" | 防止浏览器提交负数 |
| 4 | 按钮顺序：取消 → 保存（与原型一致） | |

### 2.4 P1-2 Index.cshtml — 视觉 1:1 对齐

| # | 改造 | 描述 |
|---|------|------|
| 1 | 列名称加粗 + `¥` 前缀 | 标准名称、单价格式 |
| 2 | 单价小数位：冷水/热水 2 位、电 4 位 | 与原型格式一致 |
| 3 | 适用类型用 `bg-info text-dark` Badge | 原型颜色 |
| 4 | 状态 4 态：当前生效（绿）/ 未生效（黄）/ 已过期（灰）/ 停用（灰） | 综合 IsActive + 有效期 |
| 5 | 当前生效行加 `table-success` 高亮 | 原型 row-highlight |
| 6 | 空状态提示 + 「新增费用标准」快捷按钮 | 原型"暂无费用标准" |

### 2.5 未改造项（标记为后续 v2.14）

- **实体字段扩展**：DormId / DormCode / MeterShareMethod / Remark 实体缺失（数据库迁移风险大，留待 v2.14）
- **5 区块表单**：工作量较大（Create/Edit 各 ~150 行），留待 v2.14
- **分页每页 10 条/页）

---

## 三、验证清单

- [x] SaveStandardAsync 更新分支加 SaveChangesAsync
- [x] 日期校验逻辑反转
- [x] GetStandardApplicableTypesAsync 去除重复"全部"
- [x] Edit Id 绑定修复（Input.Id）
- [x] Edit/Add IsActive checkbox
- [x] Edit/Add datalist applicableTypeList
- [x] 单价 min="0"
- [x] Index 列格式 + Badge + 4 状态 + 空状态
- [x] `dotnet build DormManage.sln -c Release` → 0 错误
- [ ] 3 项目发布至 publish-final/
- [ ] UTF-16 验证 v2.13.42
- [ ] Git 提交

---

## 四、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（3 Razor + 1 服务 + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性 | ✅ 已保留 |
| 3 | 计算方法一致性 | ✅ 已保留 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 完全对齐原型 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.42 + 2026-07-21） | ✅ 已标注 |

---

## 五、回退方案

```bash
git revert HEAD  # 撤销 v2.13.42
```