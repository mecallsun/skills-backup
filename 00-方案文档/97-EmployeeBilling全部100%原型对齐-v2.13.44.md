# EmployeeBilling 全部 100% 原型对齐（v2.13.44）

> **版本**：v2.13.44
> **日期**：2026-07-21
> **类型**：2 页面 P0 必修 + 服务层扩展
> **影响文件**：`EmployeeBilling/{Index, Details}.cshtml` + `BillingService.cs` + `EmployeeBilling/Index.cshtml.cs` + `EmployeeBilling/Details.cshtml.cs`

---

## 一、审计结果（v2.13.43 之前）

| # | Razor 页面 | 综合对齐度 | 评级 |
|---|-----------|----------|------|
| 1 | `EmployeeBilling/Index.cshtml` | 75% | B |
| 2 | `EmployeeBilling/Details.cshtml` | 65% | C+ |

**审计核心发现**：
- **Index 75%**：筛选少 1 项（在职状态）、列表少 4 列（冷水分摊/热水分摊/电分摊/在住人数）、无详情入口
- **Details 65%**：水费合并未拆（应拆为冷水/热水 2 张卡片）、缺分摊依据（ShareRatio + ResidentCount）、走老 WaterAmount 单一字段
- **服务层 100%**：扩展 GetEmployeeBillsAsync 第 3 参，添加 EmploymentStatusId

---

## 二、v2.13.44 实施变更

### 2.1 P0-1 服务层扩展 EmploymentStatusId 筛选

**文件**：`DormManage.Shared/Services/BillingService.cs` line 36-40, 374-415

**变更**：接口 + 实现新增 `int? employmentStatusId` 参数：

```csharp
/// <summary>v2.13.44 查询员工账单列表（新增在职状态筛选）</summary>
Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int? departmentId, int? employeeTypeId, int? residenceStatusId, int? employmentStatusId, int page, int pageSize);
```

实现 LINQ 追加：

```csharp
(!employmentStatusId.HasValue || e.EmploymentStatusId == employmentStatusId.Value) &&
```

**老版本重载自动委托新版本**（避免破坏其它调用方）：

```csharp
public async Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(...int? residenceStatusId, int page, int pageSize)
    => await GetEmployeeBillsAsync(..., null, page, pageSize);  // 在职状态传 null
```

### 2.2 P0-2 PageModel 扩展 EmploymentStatusDropdownItem + 4 字段

**文件**：`DormManage.Admin/Pages/EmployeeBilling/Index.cshtml.cs`

**新增**：

```csharp
/// <summary>v2.13.44 新增：在职状态筛选</summary>
[BindProperty(SupportsGet = true)]
public int? EmploymentStatusId { get; set; }

public List<EmploymentStatusDropdownItem> EmploymentStatuses { get; set; } = new();
```

**EmployeeBillingDto 新增 6 字段**（含 EmployeeId 用于详情跳转）：

```csharp
public int EmployeeId { get; set; }              // 详情跳转
public decimal ColdShareAmount { get; set; }     // 冷水
public decimal HotShareAmount { get; set; }      // 热水
public decimal ElectricityShareAmount { get; set; } // 电
public int ResidentCount { get; set; }           // 同住人数
public decimal ShareRatio { get; set; }          // 分摊比例
```

**OnGetAsync 加载**：

```csharp
EmploymentStatuses = await _db.EmploymentStatuses
    .Where(e => e.IsActive)
    .OrderBy(e => e.Id)
    .Select(e => new EmploymentStatusDropdownItem { Id = e.Id, Name = e.Name })
    .ToListAsync();
```

**EmploymentStatusDropdownItem 类**：

```csharp
public class EmploymentStatusDropdownItem { public int Id; public string Name = ""; }
```

### 2.3 P0-3 Index 视图：在职状态筛选 + 4 列 + 详情入口

**文件**：`DormManage.Admin/Pages/EmployeeBilling/Index.cshtml`

**新增筛选下拉**：

```html
<select name="EmploymentStatusId" class="form-select">
    <option value="">全部</option>
    @foreach (var item in Model.EmploymentStatuses) { ... }
</select>
```

**列表新增 4 列**（共 12 列 + 序号 = 13）：

| 列 | 内容 |
|----|------|
| 冷水分摊 | ¥@item.ColdShareAmount.ToString("N2") |
| 热水分摊 | ¥@item.HotShareAmount.ToString("N2") |
| 电分摊 | ¥@item.ElectricityShareAmount.ToString("N2") |
| 在住人数 | @item.ResidentCount 人 |

**详情入口按钮**：

```html
<button class="btn btn-sm btn-outline-info" onclick="viewDetail(@item.Id, ...)" title="查看详情">
    <i class="bi bi-eye"></i> 查看
</button>
```

**分页 URL 同步**：所有 href 追加 `&EmploymentStatusId=@Model.EmploymentStatusId`

### 2.4 P0-4 真实导出功能

**文件**：`Index.cshtml` `exportBilling(event)` 函数

**修复**：占位 `alert('✓ 原型演示')` → 真实 `<a>` 标签下载：

```javascript
function exportBilling(event) {
    event.preventDefault();
    var month = '@Model.BillingMonth';
    var url = '/api/v1/billing/employee-bills/export?billingMonth=' + encodeURIComponent(month);
    var a = document.createElement('a');
    a.href = url;
    a.download = '员工分摊账单_' + month + '.xlsx';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}
```

### 2.5 P0-5 Details 拆分水费 + 加分摊依据

**文件**：`EmployeeBilling/Details.cshtml.cs` + `Details.cshtml`

**PageModel 重构**：
- 新增 `Bill` 属性（类型 `DormManage.Shared.Models.EmployeeBilling`，全限定避免与命名空间冲突）
- OnGetAsync 新增 `int? id` 参数，优先按 ID 加载真实账单，回填 `employeeId/month`
- `BillingSummary` 拆 `WaterAmount` → `ColdAmount + HotAmount`，新增 `ShareRatio + ResidentCount`

**视图新增 4 张卡片**：

| 卡片 | 内容 |
|------|------|
| 冷水分摊 | ¥@Model.Summary.ColdAmount.ToString("F2") |
| 热水分摊 | ¥@Model.Summary.HotAmount.ToString("F2") |
| 分摊比例 | @shareRatioPercent%（如 50.0%） |
| 同住人数 | @Model.Summary.ResidentCount 人 |

**账单明细新增"冷水费分摊/热水费分摊" 2 行**。

---

## 三、验证清单

- [x] BillingService.GetEmployeeBillsAsync 8 参数扩展 + 老版本委托
- [x] IndexModel EmploymentStatuses + EmploymentStatusId + 6 字段 DTO
- [x] Index 在职状态筛选 + 4 列 + 详情按钮 + 真实导出
- [x] Details 拆水分（冷/热）+ 分摊依据卡（比例/人数）
- [x] `_Layout.cshtml` brand-version → v2.13.44
- [x] `NotifyIconManager.cs` → v2.13.44
- [x] `dotnet build DormManage.sln -c Release` → 0 错误
- [x] 3 项目 publish-final/ 发布
- [x] UTF-16 验证 v2.13.44（Admin/Tray ✓，Api 无版本号）
- [ ] Git 提交

---

## 四、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（2 Razor + 1 Service + 2 PageModel + 1 文档 + 2 全局版本号） | ✅ 已识别 |
| 2 | 数据逻辑一致性（不动分摊算法） | ✅ 已保留 |
| 3 | 计算方法一致性（冷水/热水/电分摊金额保持原算法） | ✅ 已保留 |
| 4 | 冲突解决（开发方案 > 需求文档 > 原型） | ✅ 完全对齐原型 |
| 5 | 文档同步（本文档先于代码完成） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.44 + 2026-07-21） | ✅ 已标注 |

---

## 五、回退方案

```bash
git revert HEAD  # 撤销 v2.13.44
```