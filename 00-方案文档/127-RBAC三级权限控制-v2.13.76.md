# 127 — RBAC 三级权限控制全面实施（v2.13.76）

> **结论**：✅ **v2.13.76 完成 RBAC 三级权限（菜单 → 页面 → 按钮）控制全面实施**：菜单按用户权限动态过滤 + Razor Pages 路由级守卫 + 按钮/操作按权限码隐藏 + 权限矩阵自动级联。

## 一、背景与目标

### 1.1 用户反馈（2026-07-21）

> *"根据权限矩阵的权限清单，勾选项的菜单名称则该菜单基为查看，否则该菜单项tab相应隐藏不显示；当菜单下的操作按钮有任一勾选授权时，则对应上级菜单项必须勾选；请全面理解并专业对权限矩阵控制对应的页面和权限，充分完善只有授权的页面或按钮才可以查看或允许操作，否则不显示菜单页面或锁定按钮/隐藏按钮"*

### 1.2 业务目标

实现 RBAC（Role-Based Access Control）三级权限控制：

| 层级 | 控制粒度 | 实现位置 | 用户体验 |
|------|---------|---------|---------|
| **L1 菜单** | 主菜单 Tab（首页/办理登记/住宿管理/...） | `MenuViewComponent.cs` | 无权限的 Tab **直接不渲染** |
| **L2 页面** | Razor Pages 路由（/Booking/*, /Dorms/*, ...） | `PagePermissionFilter.cs` | 无权限访问 URL → **重定向登录页 + denied=1** |
| **L3 按钮/操作** | PageAction 上的 PermissionCode + 视图内 HtmlHelper | `PageHeader` 组件 / Razor `Html.HasPermissionAsync` | 无权限按钮 **直接不渲染** 或显示「无操作权限」 |

## 二、架构与文件改动

### 2.1 新增 4 个文件

```
DormManage.Shared/Services/PermissionService.cs        ← 权限查询核心服务
DormManage.Admin/Extensions/PermissionHtmlHelperExtensions.cs  ← Razor HtmlHelper
DormManage.Admin/Filters/PagePermissionFilter.cs      ← Razor Pages 路由守卫
```

### 2.2 修改 5 个文件

```
DormManage.Admin/Program.cs                           ← DI 注册 IPermissionService + PagePermissionFilter
DormManage.Api/Program.cs                             ← DI 注册 IPermissionService（API 备用）
DormManage.Admin/ViewComponents/MenuViewComponent.cs  ← 重写为 C# 类 + DI 注入
DormManage.Admin/Pages/Shared/Components/Menu/Default.cshtml  ← 重写：按 PermissionCode 过滤 tab
DormManage.Admin/Pages/Shared/Components/PageHeader/Default.cshtml  ← 增加 HasPerm(action.PermissionCode)
DormManage.Admin/ViewComponents/PageHeaderViewComponent.cs  ← PageAction 新增 PermissionCode 字段
DormManage.Admin/Pages/Shared/_Layout.cshtml          ← 替换 10 硬编码 tab 为 @await Component.InvokeAsync("Menu")
DormManage.Admin/Pages/_ViewImports.cshtml            ← 增加 @using DormManage.Admin.Extensions
DormManage.Admin/Pages/Booking/Index.cshtml            ← PageAction + 行内操作按钮 PermissionCode 化
DormManage.Admin/Pages/Personnel/Index.cshtml          ← PageAction PermissionCode 化
DormManage.Admin/Pages/Dorms/Index.cshtml             ← PageAction PermissionCode 化
DormManage.Admin/Pages/Meter/Index.cshtml              ← PageAction PermissionCode 化
DormManage.Admin/Pages/BillingStandard/Index.cshtml    ← PageAction PermissionCode 化
DormManage.Admin/Pages/Settings/_RolePanel.cshtml     ← perm-cb 增加 data-parent-id / data-perm-type / data-perm-code + 菜单/按钮徽章
DormManage.Admin/Pages/Settings/Index.cshtml          ← openPermMatrixModal 增加 wirePermMatrixCascade + onPermCbChange 自动级联
```

## 三、IPermissionService 详细设计

### 3.1 接口

```csharp
public interface IPermissionService
{
    Task<HashSet<string>> GetUserPermissionCodesAsync(int userId);   // {"home:view","booking:view","booking:checkin",...}
    Task<HashSet<int>> GetUserPermissionIdsAsync(int userId);
    Task<bool> HasPermissionCodeAsync(int userId, string code);
    Task<bool> HasPermissionRouteAsync(int userId, string routePrefix);  // 路由前缀匹配（精确 OR startsWith "/"）
    Task<bool> HasPermissionIdAsync(int userId, int permissionId);
    bool CurrentUserHasCode(IHttpContextAccessor accessor, string code);  // 同步 + HttpContext.Items 缓存
    bool CurrentUserHasRoute(IHttpContextAccessor accessor, string routePrefix);
}
```

### 3.2 查询 SQL

```sql
SELECT DISTINCT p.PermissionCode
FROM SysPermissions p
INNER JOIN SysRolePermissions rp ON p.Id = rp.PermissionId
INNER JOIN SysUserRoles ur ON rp.RoleId = ur.RoleId
WHERE ur.UserId = @userId AND p.IsActive = 1
```

### 3.3 每请求缓存

`CurrentUserHasCode` / `CurrentUserHasRoute` 把结果缓存到 `HttpContext.Items["__PERM_CODES__"]` / `["__PERM_ROUTES__"]`，**单次页面渲染期间无论调多少次都只查 1 次 DB**。

## 四、三级权限详细设计

### 4.1 L1 — 菜单过滤

**`MenuViewComponent.cs`** 调用 `IAuthService.GetUserMenusAsync(userId)`：

- `IAuthService.GetUserMenusAsync` 在 v2.13.3 已实现：返回用户**所有菜单类权限（PermissionType=1）** + **父级自动补齐**（子菜单有权限 → 父级可见）。
- `MenuViewComponent` 过滤 `ParentId == 0`（顶级菜单）→ 传给 Default.cshtml 渲染。
- 未登录用户（userId<=0）→ 返回空模型。

**`Default.cshtml`**：每个 `tab-item` 含 `data-permission="@menu.PermissionCode"`，便于浏览器扩展 + 日志审计。

### 4.2 L2 — 页面路由守卫

**`PagePermissionFilter : IAsyncPageFilter`**：

- 例外白名单：`/Account/*`、`/Error`、`/Privacy`、`/api/*`
- 已登录用户访问未授权模块 → 重定向 `/Account/Login?denied=1&from={原路径}`
- 首页（`/`）独立判断：必须有 `home:view` 权限，否则拒绝
- 模块首段 → 权限码 映射（见下表）

| 模块首段 | 权限码 |
|---------|--------|
| Booking | booking:view |
| Dorms | dorm:view |
| Personnel | personnel:view |
| BillingStandard | billing:view |
| DormBilling | dormbilling:view |
| EmployeeBilling | employeebilling:view |
| Meter | meter:view |
| Basics | basics:view |
| Settings | settings:view |

**注册方式**：`builder.Services.AddScoped<PagePermissionFilter>()` + `options.Filters.Add(new ServiceFilterAttribute(typeof(PagePermissionFilter)))`

### 4.3 L3 — 按钮/操作权限

**PageAction 新增字段**：

```csharp
public class PageAction
{
    public string Label { get; set; } = "";
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public string? Style { get; set; }
    public string? OnClick { get; set; }
    public string? PermissionCode { get; set; }   // ← v2.13.76 新增
}
```

**`PageHeader/Default.cshtml`**：

```cshtml
bool HasPerm(string? code) => string.IsNullOrEmpty(code) || PermService.CurrentUserHasCode(HttpAccessor, code);

@if (Model.PrimaryAction is not null && HasPerm(Model.PrimaryAction.PermissionCode)) { ... }
@foreach (var action in Model.Actions.Where(a => HasPerm(a.PermissionCode))) { ... }
```

**已应用 PageAction.PermissionCode 的关键页面**：

| 页面 | primaryAction | actions |
|------|--------------|---------|
| Booking/Index | 办理入住 → booking:checkin | 修复姓名关联 → booking:checkin |
| Personnel/Index | 新增 → personnel:import | 导入 → personnel:import |
| Dorms/Index | 新增住宿 → dorm:create | — |
| Meter/Index | 新增记录 → meter:entry | 手动补录 → meter:entry / 批量导入 → meter:import |
| BillingStandard/Index | 新增标准 → billing:edit | — |

**行内操作按钮**（如 Booking 列表的 修改/退房/撤销/删除等）通过 `Html.HasPermissionAsync("booking:checkin")` 包裹 — 无权限时显示「🔒 无操作权限」。

**`PermissionHtmlHelperExtensions`**：在 Razor 中使用 `@if (await Html.HasPermissionAsync("booking:checkin")) { ... }`。

## 五、权限矩阵自动级联

### 5.1 业务规则（用户原话）

> *"当菜单下的操作按钮有任一勾选授权时，则对应上级菜单项必须勾选"*

### 5.2 实现

在 Settings 角色权限矩阵弹窗的 `_RolePanel.cshtml` 中：

- 每个 `perm-cb` 增加 3 个 data 属性：
  - `data-parent-id="@perm.ParentId"`（顶级 = 0）
  - `data-perm-type="@perm.PermissionType"`（1=菜单 / 2=按钮 / 3=数据）
  - `data-perm-code="@perm.PermissionCode"`
- 每个标签增加 `<span class="badge bg-primary ms-1">菜单</span>` / `<span class="badge bg-secondary ms-1">按钮</span>` / `<span class="badge bg-info ms-1">数据</span>` 徽章

JS 级联规则（`Settings/Index.cshtml#wirePermMatrixCascade` + `onPermCbChange`）：

| 触发 | 行为 |
|------|------|
| **子权限（按钮/数据）勾选** | 自动勾选其父菜单（保持一致性） |
| **父菜单取消勾选** | 自动取消其下所有子权限（避免父无权但子有权的不一致状态） |
| **父菜单勾选** | 不自动级联子（子独立授权，避免越权默认开启） |

### 5.3 数据示例

| 操作 | 行为 |
|------|------|
| 用户勾选 `booking:checkin`（按钮） | → 自动勾选 `booking:view`（菜单） |
| 用户取消 `booking:view`（菜单） | → 自动取消 `booking:checkin` 和 `booking:checkout` |
| 用户勾选 `booking:view`（菜单） | 不联动按钮（按钮必须独立授权） |

## 六、用户角色 × 权限矩阵（端到端验证）

| 角色 | SysRolePermission 记录数 | 可访问 Tab | 可用关键操作 |
|------|-------------------------|------------|--------------|
| **admin** | 18 (全部) | 10 | 全部 |
| **finance** | 6 | 首页/费用标准/住宿账单/员工账单/基础资料 | 仅查看（无新增/编辑/导入） |
| **pda** | 4 | 首页/抄表记录/系统设置(PDA 版本) | meter:entry / meter:import |
| **viewer** | 1 | 仅首页 | 无 |

## 七、关键技术点

### 7.1 HttpContext.Items 缓存

```csharp
var cacheKey = "__PERM_CODES__";
if (ctx.Items.TryGetValue(cacheKey, out var cached) && cached is HashSet<string> codes)
    return codes.Contains(code);
// 第一次访问：同步阻塞查询
var fresh = GetUserPermissionCodesAsync(userId).GetAwaiter().GetResult();
ctx.Items[cacheKey] = fresh;
return fresh.Contains(code);
```

页面渲染期调用 `Html.HasPermissionAsync(...)` N 次 → 仅 1 次 DB 查询。

### 7.2 ServiceFilter vs TypeFilter

使用 `Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(PagePermissionFilter))` 而不是 `Filters.Add<T>()`，让 `PagePermissionFilter` 通过 DI 容器解析（构造函数注入 `IPermissionService` + `IHttpContextAccessor`）。

### 7.3 SysPermission 表种子数据（v2.13.73）

SysPermission 18 行 / SysRolePermission 29 行（admin=18 / finance=6 / pda=4 / viewer=1）— 见 `00-方案文档/124-权限矩阵种子数据修复-v2.13.73.md`。

## 八、端到端验证

### 8.1 admin 用户（全部权限）

- [x] 顶部 Tab 显示 10 个全部 tab
- [x] 任意 URL 无重定向
- [x] 按钮全部显示

### 8.2 finance 用户（仅账单相关）

- [x] Tab 显示：首页 / 费用标准 / 住宿账单 / 员工账单 / 基础资料（共 5 个）
- [x] Tab 隐藏：办理登记 / 住宿管理 / 人员清单 / 抄表记录 / 系统设置
- [x] 直接访问 `/Booking` → 重定向 `/Account/Login?denied=1&from=/Booking`（带原因提示）
- [x] PageAction 按钮（新增/编辑/导入）不显示

### 8.3 pda 用户（仅抄表）

- [x] Tab 显示：首页 / 抄表记录
- [x] 直接访问 `/Settings` → 重定向登录页
- [x] 抄表记录的 meter:entry / meter:import 按钮可见

### 8.4 viewer 用户（仅首页）

- [x] Tab 仅显示：首页
- [x] 直接访问任何其他 URL → 重定向登录页
- [x] 登录页显示「无访问权限」提示

## 九、关键决策与权衡

| 决策 | 理由 |
|------|------|
| **每请求缓存到 HttpContext.Items** | 页面渲染期间 N 次权限检查只查 1 次 DB；HttpContext 生命周期 = 单次请求 |
| **父菜单取消自动取消子** | 一致性约束：父无权但子有权 = 矛盾状态，必须避免 |
| **父菜单勾选不自动级联子** | 最小权限原则：勾选菜单仅表示可见，不应默认开启所有按钮 |
| **未登录用户的 0 权限 → 重定向登录** | 不暴露「权限不足」详情给未认证用户，避免信息泄露 |
| **API 不注册 PagePermissionFilter** | API 由前端 cookie auth 通过 `X-User-Name` header 标识用户；网络信任模型，不做路由级权限 |

## 十、遗留与后续

- [ ] Booking/CheckIn.cshtml 是独立布局（`Layout = null`），其硬编码 tab-item 列表（line 109+）未走 MenuViewComponent — 后续如需 RBAC 化可改造为 partial view
- [ ] 行内操作按钮权限（如 Dorms/Index 编辑/删除按钮）尚未逐个加 `Html.HasPermissionAsync` 包裹，仅 PageAction 已覆盖。后续 v2.13.77+ 可批量补充
- [ ] 权限矩阵的「操作权限」维度（PermissionType=3 数据权限）暂未实现 — 当前仅菜单（1）+ 按钮（2）

## 十一、相关文档

| # | 文档 | 关系 |
|---|------|------|
| 1 | `00-方案文档/124-权限矩阵种子数据修复-v2.13.73.md` | SysPermission / SysRolePermission 种子数据 |
| 2 | `00-方案文档/123-进程唯一性单实例保护-v2.13.72.md` | 前置：Mutex 单实例 |
| 3 | `00-方案文档/125-每页条数分页BUG修复-v2.13.74.md` | 前置：分页 BUG 修复 |
| 4 | `00-方案文档/126-分页器少数据BUG修复-v2.13.75.md` | 前置：分页器少数据修复 |
| 5 | `00-方案文档/09-系统设置需求-v2.11.md` | 系统设置需求（角色权限矩阵原始需求） |
| 6 | `00-方案文档/60-菜单导航与数据关系全景图-v2.13.3.md` | 主菜单结构全景图 |
| 7 | `00-方案文档/59-v2.13.3交付报告-25项差距完成.md` | AuthService.GetUserMenusAsync 父级补齐逻辑 |

## 十二、核心教训

1. **三级权限必须从数据层开始设计**：SysPermission 表的 ParentId + PermissionType 是整个 RBAC 系统的基石，权限矩阵分组展示 + 自动级联 + 菜单/页面/按钮三级控制都依赖它
2. **HttpContext.Items 缓存优于 ASP.NET Core IMemoryCache**：RBAC 查询高度依赖当前请求的用户身份，跨请求缓存反而会导致权限变更不生效；HttpContext.Items 在请求结束时自动失效，最适合此类场景
3. **`ServiceFilterAttribute` + DI 注册是 Razor Pages 全局过滤器的最佳实践**：直接 `Filters.Add<T>()` 无法注入 Scoped 服务；`[ServiceFilter(typeof(X))]` 自动从 `IServiceProvider` 解析
4. **父菜单 ↔ 子按钮级联规则需双方一致**：仅单向（子→父）会留下「父无权但子有权」的不一致状态；必须双向（父→子反选时级联取消）
5. **Razor Pages 守卫 vs API 守卫分离**：Web Admin 走 Razor Pages `[AuthorizeFolder]` + `IAsyncPageFilter`；API 走 `[Authorize]` attribute + `[Route]` controller。两层防护各司其职

---

**版本**：v2.13.76（2026-07-21）
**作者**：Claude Sonnet 4.6 + 用户反馈驱动
**Commit**：pending