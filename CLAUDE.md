# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project: 金戈宿舍管理系统 (JINGE Dormitory Management System)

.NET 8 Razor Pages + API self-hosted dormitory management system with tray daemon, RBAC, and PDA support.

### Architecture Overview

```
DormManage.sln (4 projects)
├── DormManage.Shared   →  EF Core entities + DbContext + business services + DTOs
├── DormManage.Api      →  REST API (Kestrel, port 5100) + Swagger
├── DormManage.Admin    →  Razor Pages UI (Kestrel, port 5001) + Cookie auth + embedded API
└── DormManage.TrayApp  →  WinForms tray daemon (process management + health check + IPC)
```

**Key architectural decisions:**
- Single source of truth: `DormManage.Shared` contains ALL entities, DbContext, services, DTOs, enums
- No Areas; flat controller directories with explicit `[Route]` attributes
- Mixed routing: some controllers use `api/v1/...`, others use `api/...` (no global template)
- Database dual-provider: SQL Server (production) / SQLite (dev), switched via `Database:Provider` config
- Tray injects config via environment variables: `DormManage_KESTREL_PORT`, `DormManage_DB_CONN`, `DormManage_DB_PATH`
- IPC: TCP `127.0.0.1:5099` JSON-over-lines protocol between Web Admin and TrayApp
- No JWT; Cookie-based auth for Admin UI; API has no inbound auth (trusted local network)

### Build, Run & Test

```bash
# Build (Debug)
dotnet build DormManage.sln -c Debug

# Build (Release)
dotnet build DormManage.sln -c Release

# Publish
dotnet publish DormManage.Admin/DormManage.Admin.csproj -c Release -r win-x64 --self-contained true -o publish-final/Admin
dotnet publish DormManage.Api/DormManage.Api.csproj -c Release -r win-x64 --self-contained true -o publish-final/Api
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj -c Release -r win-x64 --self-contained true -o publish-final/TrayApp

# Run individual projects
dotnet run --project DormManage.Admin/DormManage.Admin.csproj
dotnet run --project DormManage.Api/DormManage.Api.csproj
dotnet run --project DormManage.TrayApp/DormManage.TrayApp.csproj
```

**Startup sequence:** Run `DormManage.TrayApp.exe` → auto-starts Admin (:5001) + Api (:5100) → opens browser to `http://localhost:5001`.

**Smoke test endpoints:**
- API health: `GET /api/v1/system/dbhealth/quick` → 200
- API docs: `GET /swagger/index.html`
- Admin login: `GET /Account/Login` (credentials: admin / admin123)

### Code Structure

| Layer | Location | Responsibility |
|-------|----------|---------------|
| **Entities** | `Shared/Models/*.cs` | EF Core entities with FK navigation; BaseEntity base class |
| **DbContext** | `Shared/Data/DormDbContext.cs` | 62 DbSets, fluent API config, audit stamping (CreatedAt/UpdatedAt), seed data |
| **Services** | `Shared/Services/*.cs` | Business logic: Basics, Booking, Personnel, Dorm, Billing, Dashboard, DatabaseHealth, FilterCache |
| **API Controllers** | `Api/Controllers/*/` | REST endpoints (14 controllers across 10 dirs) |
| **Razor Pages** | `Admin/Pages/*/` | 35 page models across 10 functional areas |
| **TrayApp** | `TrayApp/` | WinForms tray daemon: ProcessManager, HealthChecker, ConfigService, IpcServer |

### Domain Modules

| Module | Routes | Controllers | Pages | Key Entities |
|--------|--------|-------------|-------|-------------|
| **Dashboard** | `/` | — | `Index.cshtml` | DashboardService (7 KPIs, 8 charts) |
| **Auth/RBAC** | `/Account/*` | `Auth/UserController`, `Auth/RoleController` | `Login`, `Logout`, `Profile` | SysUser, SysRole, SysUserRole, SysPermission |
| **Booking** | `/Booking/*`, `/api/v1/bookings` | `BookingController` | `Index`, `Edit`, `CheckIn`, `CheckOut` | DormBooking（EmployeeName ↔ SysEmployee.RealName 双管齐下同步：v2.13.33 GetListAsync 实时覆盖 + Repair API 写回） |
| **Dorms** | `/Dorms/*`, `/api/dorms` | `DormsController` | `Index`, `Create`, `Edit`, `Details`, `History` | Dorm |
| **Personnel** | `/Personnel/*`, `/api/v1/personnel` | `PersonnelController` | `Index`, `Create`, `Edit`, `Import` | SysEmployee |
| **Meter** | `/Meter/*`, `/api/meter/*` | `MeterController` | `Index`, `Entry`, `Edit`, `Detail`, `Import` | MeterRecord |
| **Billing** | `/BillingStandard/*`, `/DormBilling/*`, `/EmployeeBilling/*`, `/api/v1/billing/*` | `BillingController` | `Index/Create/Edit` (standards), `Index/Details` (bills) | BillingStandard, DormBilling, EmployeeBilling |
| **Basics** | `/Basics`, `/api/basics/*` | `BasicsController` | `Index` (stub) | Department, Building, Floor, Address, EmployeeType, AttendanceType, MeterUnit, ResidenceStatus, EmploymentStatus, Team |
| **Settings** | `/Settings/*` | `SystemController`, `BackupController`, `DbHealthController`, `IntegrationController` | `Index`, `User`, `Role` | SysConfig, SysOpLog, SysIntegration, AppVersion |
| **PDA** | `/api/v1/pda/*`, `/api/v1/appversion/*` | `PdaController`, `AppVersionController` | — | PdaDevice, MeterImage, AppVersion |

### Key Patterns

**Service layer:** All services are `AddScoped<>`, inject `DormDbContext` directly (no interface abstraction for DbContext). Services mix interface + implementation in same file.

**Entity naming:** Real SQL Server columns differ from C# property names — mapped via Fluent API:
- `Dorm.Id` → `DormId` column
- `SysEmployee.Id` → `EmployeeId` column  
- `DormBooking.Id` → `BookingId` column
- `MeterRecord.Id` → `RecordId` column
- `SysUser.Id` → `UserId` column, `UserName` → `Username`, `Phone` → `Mobile`
- `SysRole.Id` → `RoleId` column

**Enums:** Mix of C# `enum` types and `static class` int constants. MeterRecord uses proper enum; others use constants. Always check the model file for actual values.

**Concurrency:** Booking operations use serializable transactions with execution policy retry. Dorm deletion blocks if active bookings exist.

**Hot-reload config:** v2.13.32 introduced `AppConfigRuntime` singleton + `IDbContextFactory<DormDbContext>` for runtime DB connection switching (no restart needed). Files: `AppConfigManager.cs`, `AppConfigRuntime.cs`, `DatabaseOperationInterceptor.cs`, `DatabaseConfigFileWatcher.cs`.

**Filter persistence:** User filter conditions stored in localStorage (browser) + optional database cache via `SysUserFilterCache` (cross-device sync). Module: `ISysUserFilterCacheService`.

### Configuration

| Source | Key | Purpose |
|--------|-----|---------|
| `appsettings.json` (TrayApp) | `Tray.ApiPort` / `AdminPort` | Kestrel ports for child processes |
| `appsettings.json` (TrayApp) | `Database.Provider` / `ConnectionString` | DB provider selection |
| Env var | `DormManage_KESTREL_PORT` | Override Kestrel bind port |
| Env var | `DormManage_DB_CONN` | SQL Server connection string (plaintext) |
| Env var | `DormManage_DB_PATH` | SQLite database absolute path |
| Env var | `DormManage_IMAGE_ROOT` | PDA image storage root |

### Documentation

- **SOP & Plans:** `00-方案文档/01-技术架构与系统开发方案.md`, `02-SOP开发流程规范.md`
- **Requirements:** `00-方案文档/07-办理登记需求-v2.11.md`, `08-抄表记录需求-v2.11.md`, `09-系统设置需求-v2.11.md`, `06-宿舍与住宿记录需求-v2.10.md`
- **UI Design:** `00-方案文档/35-列表页面统一UI设计规范-v2.11.4.md`, `37-共用页头与Tab页签导航设计规范-v2.12.md`
- **Baseline:** `00-方案文档/05-原型与代码基线对照.md` (25 prototype pages ↔ 26 Razor views)
- **Delivery Reports:** `00-方案文档/68-模块100%对齐交付报告-v2.13.10.md`, `69-优化项交付报告-v2.13.11.md`
- **v2.13.24 全量交付：** `00-方案文档/78-v2.13.24最终交付报告.md` + `75-数据库Schema与代码映射文档-v2.13.24.md` + `76-入住记录与抄表记录业务深度文档-v2.13.24.md`
- **v2.13.32 数据源热加载：** `00-方案文档/85-数据源热加载与EF拦截器日志-v2.13.32.md` — `AppConfigRuntime` 单例 + `IDbContextFactory` + `DatabaseOperationInterceptor` + `DatabaseConfigFileWatcher` 跨进程同步
- **v2.13.33 办理入住 BUG + 工号姓名关联修复：** `00-方案文档/86-办理入住与工号姓名关联修复-v2.13.33.md` — BUG #1 selectCiEmp 卡死 + BUG #2 EmployeeName 双管齐下同步（GetListAsync 实时覆盖 + Repair API 写回 + PageHeader 修复按钮）
- **v2.13.34 办理入住弹窗 100% 原型对齐：** `00-方案文档/87-办理入住弹窗100%原型对齐-v2.13.34.md` — checkInModal 11 项不一致修复（单列布局 + form-card 系列样式 + 操作类型 radio + 姓名模糊搜索 + emp-info-card 横排 + 考勤班次 Badge + 校验 alert + 提交按钮 + breadcrumb）
- **v2.13.35 入住弹窗按钮与关闭交互设计：** `00-方案文档/88-入住弹窗按钮与关闭交互设计-v2.13.35.md` — 5 关闭 + 1 提交触发器（取消/X/ESC/backdrop 统一 confirmCloseCheckIn + form 包裹让 type=submit 工作 + Enter 自动提交 + modal backdrop=static/keyboard=false + form-actions 快捷键提示）
- **v2.13.36 办理入住独立页面 1:1 克隆原型：** `00-方案文档/89-办理入住独立页面1比1克隆原型-v2.13.36.md` — 架构升级（Modal → 独立 Razor Page /Booking/CheckIn）+ 1:1 复刻原型 booking/check-in.html 三层结构 + 4 区块 + 替换 mock-data.js 为真实后端 API + 保留 checkOutModal 快速退房
- **v2.13.37 Dashboard 首页 100% 原型对齐：** `00-方案文档/90-Dashboard首页100%原型对齐-v2.13.37.md` — 完全反向适配（KPI 3/4 静态显示 + 月份选择器硬编码 3 项 + 版本号硬编码 + 图表图例 2026年/2025年），Dashboard 综合对齐度 98% → 100%
- **v2.13.38 Booking 全部 100% 原型对齐：** `00-方案文档/91-Booking全部100%原型对齐-v2.13.38.md` — 4 页面逐项修复：PageHeader actions OnClick 渲染 + Index 删除/导出 BUG + Edit Type=2 状态选项 + 后端 Status 字段 + CheckIn DTO FK 字段 + CheckOut 重大重构（form-card 架构 + 员工信息卡 + dateHint 校验）
- **HTML Prototypes:** `00-方案文档/04-HTML原型/` 共 25 个原型页面 + `_shared/` 共享资源（v2.12.3 起统一为「共用页头 + Tab 页签切换」三层架构）。

### Important Notes

- **HTML原型目录已存在** — `00-方案文档/04-HTML原型/` 目录包含 25 个原型页面 + mock-data.js（1.1MB Mock 数据）+ _shared/ 共享资源（v2.12.3 起移除原 Tier 2 紧凑型图标导航条，统一为 Tab 栏）。
- **项目当前版本：** v2.13.75（2026-07-21 分页器少数据 BUG 修复 — 根因：v2.13.71 partial 的 `@if (totalPages > 0)` 让 partial 在 TotalCount < PageSize（如 4 users）时**完全不渲染**（包括 dropdown）；用户无法切换 pageSize。修复：分离两个职责——dropdown + 统计信息由 `TotalCount > 0` 控制（始终显示，让用户随时切换 pageSize）；翻页数字由 `totalPages > 1` 控制（避免单页显示无意义单数字）。端到端验证：4 users 等少数据页面 dropdown 始终显示 + 翻页数字隐藏。详见 `00-方案文档/126-分页器少数据BUG修复-v2.13.75.md`）
- **v2.13.74**（2026-07-21 每页条数 dropdown BUG 修复 — 根因：8 个 PageModel 的 `PageSize` 全部缺失 `[BindProperty(SupportsGet = true)]`（3 个甚至 `{ get; }` 只读）；`_PaginationPartial` JS 设置 `?pageSize=N` 跳转，但 PageModel 不读此参数 → 列表永远硬编码 20。修复：8 个 PageModel 全部加 BindProperty + `{ get; set; }`；BillingStandard 替换 hardcoded 20 → PageSize；UserPanel 加 pageSize/pageIndex 双别名。端到端测试 5 个有数据页面 100% 通过 pageSize=20/50/100 测试。详见 `00-方案文档/125-每页条数分页BUG修复-v2.13.74.md`）
- **v2.13.73**（2026-07-21 权限矩阵种子数据修复 — 根因：EF Core `HasData()` 仅在 EnsureCreated/migrations 时 INSERT，生产 SQL Server 手动 DDL 后从未同步。SysPermission 0 rows → +18 rows / SysRolePermission 0 rows → +29 rows。修复前 Settings?tab=roles 权限矩阵 Modal 0 个 checkbox（仅 JS 字符串中的 `.perm-cb`），修复后 18 个 checkbox 全部渲染（10 顶级模块 + 8 按钮权限）。代码 0 变更，纯 DB 修复。详见 `00-方案文档/124-权限矩阵种子数据修复-v2.13.73.md`）
- **v2.13.72**（2026-07-21 进程唯一性单实例保护 — Admin + Api 补全全局命名 Mutex 守卫；TrayApp 早已具备（v2.13.2 引入）；Mutex 名称分别为 `Global\DormManage.TrayApp.SingleInstance.v2` / `*.Admin.SingleInstance.v1` / `*.Api.SingleInstance.v1`；top-level statements 同步检查（早于 `WebApplication.CreateBuilder`）保证冲突时 host 不构建、其他 hosted service 不启动；端到端双实例测试：第二个实例 stderr 输出 3 行 `[SINGLE-INSTANCE]` 冲突提示 + 2s 后自动终止，进程列表只剩第一个实例。详见 `00-方案文档/123-进程唯一性单实例保护-v2.13.72.md`）
- **v2.13.71**（2026-07-21 列表分页 100% 原型对齐全局 — 新增共享分页组件 `_PaginationPartial` + 8 列表页全部接入（Personnel/Booking/Dorms/Meter/DormBilling/EmployeeBilling/BillingStandard/_UserPanel）；智能截断 « 1 2 3 … 65 » + 每页 [10/20/50/100] dropdown + 统计 "共 N 条 · 第 X-Y 条 · 共 Z 页"；localStorage pageSize 持久化）
- **v2.13.76**（2026-07-21 RBAC 三级权限控制全面实施 — 菜单/页面/按钮三级权限控制 + 权限矩阵自动级联（子勾选 → 父必选；父反选 → 子全清）；`IPermissionService` + `MenuViewComponent` 重写（C# 类 + DI）+ `PagePermissionFilter`（Razor Pages 路由级守卫）+ `PageAction.PermissionCode` + `Html.HasPermissionAsync`；HttpContext.Items 每请求缓存避免 N+1 查询；详见 `00-方案文档/127-RBAC三级权限控制-v2.13.76.md`）
- **v2.13.77**（2026-07-21 退房联动清理房号床位 — `SyncEmployeeDormCodeAsync` 扩展 `bedNo` 可选参数；CheckInAsync 改走统一入口；7 个 Booking 路径全部联动：`SysEmployee.DormCode` + `BedNo` + `ResidenceStatusId` 三件套同步；Dorm 端 CurrentCount/已入住人员列表由 `DormBooking.Status=2` 派生查询实时联动（无需 schema 迁移）；修复 v2.13.24 引入 Booking.BedNo 但漏写 SysEmployee.BedNo 的迟到 BUG；详见 `00-方案文档/128-退房联动清理房号床位-v2.13.77.md`）
- **v2.13.78**（2026-07-21 人员清单班组 FK 关联 P0 修复 — EF Core 模型「过度声明」了 `SysEmployee.Team` 字符串孤儿属性但 DB 实际无此列，导致列表「班组」列永久显示"-"；修复：① 添加 `Team` 导航属性 `[ForeignKey(nameof(TeamId))]`；② DormDbContext 移除 v2.13.6 加入的 `entity.Ignore(e.Team)` + 配置 FK 关系；③ Personnel Index 加 `.Include(e => e.Team)` + DTO 读 `.Name`；④ PersonnelService.GetListAsync/ExportCsvAsync 字符串班组名→`TeamId` FK 解析；⑤ 6 处遗留 `Team = teamName` 赋值移除；详见 `00-方案文档/129-人员清单班组FK关联修复-v2.13.78.md`）
- **v2.13.79**（2026-07-21 抄表记录 BIGINT 类型对齐 P0 修复 — `MeterRecord.RecordId` 在 SQL Server 是 `BIGINT IDENTITY(1,1)`，但 EF 模型继承 `BaseEntity.Id`（`int`）导致 EF 物化阶段 `Int64 → Int32` cast 失败 → /Meter 列表 Error；v2.13.6 用「取值在 int 范围内安全」错误假设规避，v2.13.68 用 `HasColumnType("int")` 仅影响 DDL 不解决读回 cast；修复：`MeterRecord` 加 `[Column("RecordId")] public new long Id` + DbContext 移除无效 `HasColumnType("int")`；端到端验证 /Meter + /Meter/Detail + /api/meter/records 全部 HTTP 200；详见 `00-方案文档/130-抄表记录BIGINT类型对齐-v2.13.79.md`）
- **v2.13.24 数据库：** 31 EF 实体 100% 对齐 SQL 真理源 init_schema.sql，3 张表 DDL 补充完整（31→33 张），业务深度 25 字段全补，双向联动 12 条规则全部实现；**v2.13.33 起 14 条联动（含 EmployeeName 双管齐下同步：实时覆盖 + Repair 写回）**
- **数据库默认值：** `192.168.1.237` / `WaterMeterDB` / `__DB_USER__` / `__DB_PASSWORD__`（v2.13.22 统一到生产环境；AppConfigManager + AesEncryptor 加密存储；v2.13.32 起通过 `AppConfigRuntime` 支持运行时热加载，无需重启服务）
- **Swagger enabled in all environments** — not gated behind `IsDevelopment()`.
- **No CORS, no HTTPS redirect** — assumes trusted local network deployment.
- **Swagger enabled in all environments** — not gated behind `IsDevelopment()`.
- **No CORS, no HTTPS redirect** — assumes trusted local network deployment.
- **Only `FilterCacheController` has `[Authorize]`** — API endpoints rely on network trust; the Booking controller reads current user from `X-User-Name` header.
- **Warning suppression:** Projects globally suppress `CS1998`, `CS8602`, `CS8629`, `CS0618`. These are known nullable/async warnings acknowledged by the team.
- **Data cleanup:** `DataCleanupHostedService` runs at startup to normalize invalid FK references in employees.
- **进程唯一性单实例保护（P0 架构约束）：** DormManage.TrayApp / Admin / Api 三个可执行文件均使用全局命名 Mutex 防止重复启动：TrayApp 早在 v2.13.2 已实现，**Admin + Api 在 v2.13.72 补全**。Mutex 名称：
  - `Global\DormManage.TrayApp.SingleInstance.v2`
  - `Global\DormManage.Admin.SingleInstance.v1`
  - `Global\DormManage.Api.SingleInstance.v1`
  
  **强制规则**：使用 `using var _singleInstanceMutex = new Mutex(initiallyOwned: true, name: "...", out bool createdNew)` 在 top-level statements 顶部（早于 `WebApplication.CreateBuilder` / `ApplicationConfiguration.Initialize`）同步检查 — 失败时 `Console.Error.WriteLine` + `Thread.Sleep(2000)` + `return`（早于 IHostedService 守卫，避免其他 hosted service 已启动）。任何新增可执行文件必须遵循同样的命名规范与 `using var` 同步检查模式。详见 `00-方案文档/123-进程唯一性单实例保护-v2.13.72.md`。
- **Git redact filter:** `filter.redactdb` replaces DB credentials with `__DB_USER__`/`__DB_PASSWORD__` on `git add`; working tree keeps real values.
- **v2.13.32 数据源热加载：** 通过 `AppConfigRuntime` 单例 + `IDbContextFactory<DormDbContext>`，Web 端或托盘修改数据库配置并保存后，**无需重启服务**即可让 Api/Admin 下次请求自动切换到新连接；`DatabaseOperationInterceptor` 输出 `[DB-CONN]` / `[DB-EXEC]` / `[DB-EXEC-SLOW]` 运行时日志，提供连接可观测性（详见 `Settings → 数据库连接` 页面顶部"🗄️ Server/Database"徽章，30s 轮询）。**配套修复**：托盘 SettingsForm 加"测试连接"按钮 + AppConfigManager.SaveConfigurationAsync 写 SysParameter 不再使用密文（v2.13.32-hotfix）。
- **v2.13.33 Repair API：** Booking 模块新增 `POST /api/v1/bookings/repair-employee-names`，用于批量回填历史 `DormBooking.EmployeeName`（按 `EmployeeId` 优先 / `EmployeeCode` 次之对齐 `SysEmployee.RealName`，返回 `updated/skipped/notFound` 计数）。**`/Booking` 页面 PageHeader 新增「修复姓名关联」按钮**。同时修复 BUG #1：`selectCiEmp` 卡死（增加 `ciSearchResults/coSearchResults` 缓存，按 empId 查员工并完整填充 `dataset.empId` + 员工信息展示区）。
- **v2.13.34 checkInModal 100% 原型对齐：** 11 项 UI/交互不一致全部修复（单列布局 + form-card 系列样式 + 操作类型 radio 切换入住/退房 + 姓名模糊搜索 + emp-info-card 横排 + 考勤班次 Badge 渲染 + 校验 alert 3 种状态 + "提交"按钮 + breadcrumb）。**PageHeader 移除冗余"办理退房"按钮**（合并到 checkInModal 的 opType=2 分支）。
- **v2.13.35 按钮与关闭交互设计：** 5 关闭 + 1 提交触发器统一化设计：`<form id="checkInForm">` 包裹整个 form-card → type=submit 工作 → Enter 自动触发 submitCheckIn；取消/X 按钮改 `id` 绑定 confirmCloseCheckIn（已选员工时弹"放弃当前录入"确认）；ESC/backdrop 关闭走 `hide.bs.modal` 拦截；modal 配 `data-bs-backdrop="static"` + `data-bs-keyboard="false"` 强制拦截；form-actions 左侧增加快捷键提示 `<kbd>Enter</kbd> 提交 · <kbd>Esc</kbd> 关闭`。
- **v2.13.39 Dorms 全部 100% 原型对齐：** Details 页面 3 列（员工类型/已入住/操作）+ 房间数字段 + checkoutResident JS；commit 46bf28e。
- **v2.13.40 Personnel 全部 100% 原型对齐：** Import 11 列模板 + FK 映射持久化 + MarkLeft BUG 修复 + 文案"员工→人员"统一；commit 88d11e3。
- **v2.13.41 Meter 全部 100% 原型对齐：** Detail 6 字段补全 + 左右分栏 + 照片占位 + 3 操作按钮；Entry 用量计算+红色校验+首次提示；Index 覆盖率 alert+progress bar；commit cc09f95。
- **v2.13.42 BillingStandard P0 BUG 修复 + 视觉对齐：** 3 个 BUG 修复（SaveChangesAsync 缺失/日期校验反转/去重"全部"）+ Edit.Id 修复 + 4 状态 Badge；commit 3de4e8e。
- **v2.13.43 DormBilling 视觉对齐 + P1 修复：** Details 楼层字段（楼栋+楼层）+ Index 单位后缀（m³/度）+ 真实导出 + 孤立 location.reload 删除 + 序号 text-muted；commit a1c2aea。
- **v2.13.44 EmployeeBilling 全部 100% 原型对齐：** Index 在职状态筛选 + 4 列（冷/热/电/在住）+ 详情入口 + 真实导出；Details 拆水分 + 分摊依据卡；BillingService 扩展 8 参数；commit f2de172。
- **v2.13.45 Basics P1/P2 微调对齐：** 班组列名"排序"→"排序号" + 页头 .page-header 规范；审计 90% A 级无 P0；commit c1923ff。
- **v2.13.46 Settings 全部 100% 原型对齐 + Profile 个人中心文档整合：** Settings/Index 5 个 mock 接真 API（备份/PDA 版本/系统集成/测试连接）+ Integration[id] 字段命名错误修复 + OnPostSaveIntegrationAsync 重实现 + Toast 组件；Settings/User JS BUG 修复 + 启停按钮；SysUserSelfService 6 敏感操作加 SysOpLog 审计（文档 80 §5.5）；Profile 个人中心 18 项功能 100% 已实现确认 + 6 区块专业布局建议（头像/工号/通知偏好/操作日志/Tab 缓存）+ 9 项待补充功能清单 + 数据模型 + API 端点建议；commit e7e160d。
- **v2.13.47 Booking 工号同步 BUG 修复（P0 数据一致性）：** `DormBooking.EmployeeCode` 是冗余字段，原 `BookingService.GetListAsync` 只 JOIN `SysEmployee.RealName` 覆盖姓名，未处理工号，导致人员在「人员清单」改工号后 Booking 列表显示旧工号。修复：(1) GetListAsync 匿名投影增加 `EmployeeCode`（按 `EmployeeId` FK JOIN），物化阶段覆盖 `DormBooking.EmployeeCode`（仅 RAM）；(2) 关键词筛选使用 `x.EmployeeCode`（档案）优先 + 回退 `x.Booking.EmployeeCode`（冗余）；(3) `RepairBookingEmployeeNamesAsync` 扩展为同时回填 EmployeeCode + EmployeeName；(4) 新增 `SysEmployeeLite` 内部 DTO。**核心原则**：住宿登记的员工信息以 `SysEmployee.Id`（人员清单的记录 ID）为 FK 存入 `DormBooking.EmployeeId`，列表展示必须通过 JOIN 实时取最新档案。commit d3a4c57。详见 `100-Booking工号同步BUG修复-v2.13.47.md`。
- **v2.13.48 Booking 数据关系文档定稿：** 正式在 `60-菜单导航与数据关系全景图`（升级到 v2.13.48）和 `07-办理登记需求-v2.11.md §3.1` 中固化"人员清单为唯一真源 + DormBooking.EmployeeId FK 关联"原则，强制后续任何 DormBooking 列表查询必须 JOIN SysEmployee。同步策略汇总表（EmployeeCode/Name/Department/Phone/AttendanceTypeId 5 个字段）：运行时实时覆盖 + Repair API 一次性写回。
- **v2.13.59 办理登记 SqlNullValueException 修复（P0 数据兼容性）：** EF Core 物化 DormBooking 实体时遇到生产数据库历史脏数据（[Required] string 字段实际为 NULL）→ 抛 SqlNullValueException → API HTTP 500 + 页面 Error。修复三层：(1) `DormBooking.cs` 4 个字段（EmployeeCode/EmployeeName/DormCode/Registrar）改为 `string?` 移除 `[Required]`；(2) `DormDbContext.cs` 移除 4 个字段的 `IsRequired()` 配置（与模型一致）；(3) **恢复 v2.13.32-hotfix / v2.13.47 覆盖循环**（原本误删导致工号字段显示为姓名）。修复后 Booking API HTTP 200，返回 337 条记录，工号正确显示「JG910013」/姓名「罗文杰」与 SysEmployee JOIN 真源一致。详见 `111-BookingSqlNull值异常修复-v2.13.59.md`。**核心教训**：Edit 整段方法时必须完整保留关键 foreach/Transform 逻辑；EF 模型 [Required] 字段应同时保证 DB 列 NOT NULL + 干净数据，否则使用 `string?`。
- **10 阶段 30 Razor 页面 100% 原型对齐全收官：** v2.13.37~v2.13.46 共 10 个版本递进完成，10 份对齐文档（90~99）齐备。
- **v2.13.55 Profile 原型导航 JS 依赖链修复：** profile/index.html 补全 `<script src="../_shared/storage-keys.js">` 加载（之前遗漏 → mountTabBar() 抛 ReferenceError → Tab 栏不显示）。详见 `107-Profile原型JS依赖链缺失修复-v2.13.55.md`。
- **v2.13.56 Profile 禁止作为第 11 主菜单原则（硬约束）：** ❌ **禁止修改 `tab-bar.js` 的 `FIXED_TABS` 数组增加 `tab-profile`**，❌ **禁止在 Profile 页面隐藏其他 10 个 Tab**，✅ **Profile 仅通过顶部「用户胶囊」`<a class="user-pill" href="../profile/index.html">` 进入**。Profile 是「Tier 4 二级子页面（账号设置）」，不是「Tier 3 主菜单 Tab（业务模块）」。任何 PR 修改 FIXED_TABS 数组必须经 ADR 评审；Code Review 必驳回。详见 `108-Profile禁止作为第11主菜单原则-v2.13.56.md`。
- **v2.13.57 住宿登记表 + 人员清单表 DDL 缺失 P0 修复：** `01_DDL_Schema.sql`（253 行）原只有 9 张表，缺失 `DormBooking` + `SysEmployee` → SQL Server 运维执行 DDL 后 EF Core 查询 → Invalid object name → 页面 Error。新增 2 张表 DDL + FK_DormBooking_Employee + FK_DormBooking_Dorm + 20 条种子数据。详见 `109-住宿登记表缺失SQL修复-v2.13.57.md`。
- **v2.13.58 Seed 数据 DormCode 不匹配 FK 约束修复：** v2.13.57 Seed 用 D-001~D-005，但 Dorm 表种子是 D-301~D-402 → FK 约束让脚本崩溃。**修正所有 DormCode 为 D-301~D-402**。**教训**：添加 FK 约束后必须自检所有引用表种子数据一致性。详见 `110-SeedDormCodeFK不匹配修复-v2.13.58.md`。
- **v2.13.60 宿舍详情页 Error + 100% 原型对齐：** Details 页面 5 字段缺失 → ALTER TABLE 补列 + UPDATE Barcode=DormCode WHERE NULL + 移除序号/手机列 + 调整列顺序（部门→员工类型→考勤班次）+ 基本信息卡改 `table table-sm table-borderless`；详见 `112-宿舍详情页错误修复与原型对齐-v2.13.60.md`。
- **v2.13.61 费用标准 启用勾选 + 适用员工类型 FK 关联 P0 修复（部分失败）：** `<input type="hidden" asp-for="Input.IsActive" value="false" />` 被 ModelState 覆盖 + `new bool IsActive` 与 BaseEntity 冲突 + ApplicableType 字符串 → ApplicableTypeId FK + `FK_BillingStandard_EmployeeType` 约束 + datalist → EmployeeType 真源 select；详见 `113-费用标准启用勾选与员工类型FK修复-v2.13.61.md`。
- **v2.13.62 费用标准 启用勾选 manual hidden 移除彻底修复：** v2.13.61 错误保留 manual hidden + asp-for 自动生成 hidden → **3 个 Input.IsActive 字段**（2 hidden + 1 checkbox）→ ModelBinder 取第一个值（永远 false）→ 勾选不生效。**修复**：移除 manual hidden，只留 `<input asp-for="Input.IsActive" type="checkbox" />`，让 asp-for 自动管理 hidden（1 hidden + 1 checkbox）。**验证**：端到端 curl POST 测试，未勾选 → IsActive=False、已勾选 → IsActive=True 均能正确持久化。详见 `114-费用标准启用勾选二次修复-v2.13.62.md`。**核心教训**：`asp-for` 复选框会自动追加 hidden field；手动加 hidden 一定会让 ModelBinder 取错值。
- **v2.13.63 费用标准 时段重叠检查按员工类型筛选 P0 修复：** 用户反馈"启用此标准勾选没有生效"——根因是 BillingService.SaveStandardAsync 时段重叠检查未按 `ApplicableTypeId` 过滤，导致不同员工类型的标准被错误互斥（用户启用 Id=3（TypeId=1）时被已激活的 Id=2（TypeId=2）拒绝）。修复：query 添加 `.Where(s => s.ApplicableTypeId == standard.ApplicableTypeId)` 过滤 + 改为真正的双时段重叠检查（`newStart <= existingEnd AND existingStart <= newEnd`，null 端视为无穷大）；错误消息精准化为「该员工类型在此时段已存在启用中的费用标准」。验证：跨类型启用（Id=2 + Id=3 均为 Active）✅、同类型冲突（Id=1 + Id=3 同 TypeId）拒绝 ✅。详见 `115-费用标准时段重叠类型筛选-v2.13.63.md`。**核心教训**：业务规则互斥检查必须按业务主键（员工类型/部门/楼栋等）分组；时段重叠做真正的双向检查，避免反向推断。
- **v2.13.64 系统设置 用户管理 P0 修复 + 1:1 原型对齐：** 用户报告"完成系统设置中用户管理的新增与编辑功能"。发现 4 个 BUG：(1) "启用/停用"按钮调用 `/api/v1/auth/users/{id}/enable` `/disable` 但 Controller 中不存在 → 404 静默失败；(2) 编辑 Modal 角色回显用 `lbl.textContent.trim().startsWith(name)` 名称前缀匹配易误判；(3) 删除用户未清理 SysUserRole 等 4 个子表 → 500 DB_ERROR；(4) UI 缺少搜索/筛选/分页，与原型 settings/index.html 用户 Tab 不符。修复：(a) UserController 添加 `POST /{id}/enable` 和 `/{id}/disable` 端点；(b) DeleteUser 级联清理 SysUserRole/SysUserSecurityQuestion/SysOpLog/SysUserFilterCache 4 子表；(c) UserViewModel 新增 `RoleIds` 字段、JS 用 Id 严格匹配替代名称前缀匹配；(d) User.cshtml 添加 filter-card 搜索/角色/状态筛选 + 分页页尾（pagination-footer）；(e) OnPostDeleteAsync 同步级联修复。端到端验证：新增/更新/重置密码/启用/停用/删除全部 200/302 成功；搜索 `Search=view` 显示 1 条匹配；状态 `Status=active` 显示 4 条；admin 删除/停用被 PROTECTED 拒绝。详见 `116-用户管理完整化-v2.13.64.md`。**核心教训**：JS 调用的每个 API 端点都必须在 Controller 实现；删除操作必须级联清理所有 FK 引用子表；CRUD 完成后用 curl 全功能测试。
- **v2.13.65 系统设置 用户管理 Index 占位符 P0 修复 + AJAX 全改造：** 收到用户第 3 次反馈（v2.13.65 关键线索：弹窗"原型演示：新增用户"）。**根因**：用户实际访问的是 `/Settings` 主页的「用户管理」Tab（line 357-409），该段仍保留原型的 `onclick="alert('原型演示：新增用户')"` 占位代码；独立可用页面 `/Settings/User` 自 v2.13.18 起就有，但 Tab 段从未替换；tab-roles 同问题（line 416 「原型演示：新增角色」）；服务启停函数也是原型 placeholder。**4 层修复**：(1) Settings/Index.cshtml 第 357-409 / 411-454（tab-users / tab-roles）整段替换为引导卡 `<a class="btn btn-primary btn-lg" href="/Settings/User">`；服务启停 toggleService() 改为 fetch /api/v1/system/service-control；(2) User.cshtml 全面 AJAX 化 — 移除 `asp-page-handler`，改用 `<form id="...">` + JS submit listener + Bootstrap 5 原生 `data-bs-toggle="modal"` + data-* 属性携带用户 ID / 姓名 / 角色 ID 等；表单提交带 loading spinner；(3) 页面级 antiforgery token 注入 — `@inject IAntiforgery` + 隐藏 input，JS 读 `pageAntiforgeryToken` 通过 `RequestVerificationToken` header 提交；(4) UserModel handler 改返回 `JsonResult(new { success, message, userId })` 而非 `RedirectToPage`；(5) **删除操作改用 `ExecuteDeleteAsync`** 直发 SQL DELETE 修复 `DbUpdateConcurrencyException`（EF Core 在 Remove+SaveChangesAsync 时携带所有原值做 WHERE，属性不一致 → 0 rows affected → 异常）。**端到端 4 操作 curl 验证**：Create HTTP 200 + `{"success":true,"userId":10}` → Update HTTP 200 → ResetPwd HTTP 200 → Delete HTTP 200；最终 DB 状态 3 个用户（admin + pda001 + viewer）正确。Settings/Index 「原型演示」grep 命中数：从原 4+ → 0。详见 `117-用户管理按钮原型占位符修复-v2.13.65.md`。**三大教训**：❌「弹 alert 是原型」= 占位符遗留 = P0 必修，全局 grep 必须 0 命中；❌ AJAX 化时一定要注入 antiforgery token（asp-page-handler 自动注入的 hidden 不会出现在纯 HTML form）；❌ 删除操作慎用 `Remove + SaveChangesAsync` —— 优先用 `ExecuteDeleteAsync` 直发 SQL。
- **v2.13.66 住宿登记列表「部门」未与员工档案同步 P0 修复：** 用户报告"住宿登记的列表中，已有员工记录及员工的档案 id,但部门没有关联显示"。**根因**：BookingService.GetListAsync LINQ 投影只 JOIN SysEmployee 取考勤班次/姓名/工号（v2.13.32-hotfix / v2.13.47），**没有 JOIN 取部门**；DTO 的 Department 直接用 `x.Booking.Department`（冗余字段，未覆盖）。**修复 5 处**：(1) LINQ 投影新增 `Department = emp?.Department ?? b.Department`；(2) 部门筛选条件使用 JOIN 优先 + 回退冗余；(3) BookingListDto 改用 `x.Department ?? x.Booking.Department`；(4) 物化覆盖循环新增 `if (!IsNullOrEmpty(item.Department)) item.Booking.Department = item.Department`；(5) RepairBookingEmployeeNamesAsync + SysEmployeeLite 同步扩展 Department。**端到端验证**：337 条记录全部正确显示部门；筛选 `department=生产部` 返回 234 条匹配；Web /Booking 表格行部门显示"其他/生产部"等与 SysEmployee 档案完全一致。详见 `118-住宿登记部门同步BUG修复-v2.13.66.md`。**核心教训**：❌「人员维度冗余字段」必须 JOIN 覆盖 + 物化覆盖 + 筛选 + Repair API 同步扩展 4 件套齐备；❌ v2.13.32-hotfix / v2.13.47 / v2.13.66 三次同主题 BUG 揭示出**同模式 BUG 必须全字段审计** —— RealName/EmployeeCode/Department 是同一模式（冗余字段同步），未来新增冗余字段必须一次性把 JOIN + 覆盖 + 筛选 + Repair 都加上。
- **v2.13.67 系统设置子 Tab 嵌入重组：** 用户报告"系统设置子菜单的用户管理及角色与权限菜单项必须与数据库连接/备份/PDA 版本等子 Tab 并列显示，删除独立相关进入界面"。**根因**：v2.13.65 修复"原型演示"alert 占位符时采用引导卡跳独立页面（/Settings/User + /Settings/Role）的过渡方案，体验割裂。**实施方案**：partial class IndexModel + partial view 嵌入。(1) 新增 `UserPanelPartial.cs` + `RolePanelPartial.cs`（partial class 合并到 IndexModel）；(2) 新增 `_UserPanel.cshtml` + `_RolePanel.cshtml`（去掉 @page + Layout 的 partial view）；(3) IndexModel 改为 partial class + 加 _db 字段 + LoadUserPanelAsync/LoadRolePanelAsync；(4) Settings/Index.cshtml 顶部加 antiforgery token + tab-users/tab-roles 替换为 `<partial>` + Scripts section 加 User/Role 的 JS；(5) Handler URL 改为 `/Settings?handler=UserCreate/RoleCreate` 等前缀避免冲突；(6) 删除 User.cshtml / User.cshtml.cs / Role.cshtml / Role.cshtml.cs 4 个独立文件。**端到端验证**：Role + User 6 个 CRUD 操作全部 success:true；旧路由 /Settings/User / /Settings/Role 返回 404；"进入用户管理"等引导文案全局 grep = 0 命中。详见 `119-用户管理角色权限子Tab嵌入-v2.13.67.md`。**核心教训**：❌「引导卡跳独立页面」是技术债的过渡方案，体验割裂，必须**嵌入而不是引导**；❌ partial class 共享一个 PageModel 时，handler 命名必须带前缀避免冲突；❌ Antiforgery token 由父页面一次性注入；❌ partial view 不能定义 @section Scripts，JS 由父页面统一管理。
- **v2.13.68 原型功能完整性强制规则（CLAUDE.md 全局升级）：** 用户在多次迭代中反复反馈"按钮无响应""自动导出失败""批量导入点了只弹演示提示"等占位符遗留问题。在全局 CLAUDE.md「软件开发项目文档冲突检查与同步规则」内**新增 P0 级规则**：「原型功能完整性强制规则」7 项检查：(1) 功能 1:1 实现，禁 alert/placeholder/演示版；(2) 全局 grep `alert('原型演示\|原型演示` 必须 = 0；(3) 每个按钮/链接都有可执行入口；(4) 业务硬约束在 Service 层真实实现（不只前端 JS alert）；(5) 操作真实落库（不 TempData 跳走）；(6) 变更前先对比 HTML 原型；(7) 无法 1:1 时**主动显式告知**，禁止隐藏。同步创建项目级 SOP 文档 `00-方案文档/120-原型功能完整性开发规范-v2.13.68.md`，含 4 类典型反例修复案例（占位符 / 导入跳走 / 导出无反应 / handler 404）+ 3 个自动化检测脚本（scan_placeholders / check_buttons / check_handlers）。**生效范围**：所有未来软件开发任务 + 当前已发现 3 个 P0 占位符必修（v2.13.65 alert 已修；v2.13.68 手动实录占位符修复中；v2.13.69 批量抄表占位符修复中）。**核心教训**：占位符是技术债，首次"演示"就会被遗忘，**禁止"先演示再补"模式**——一次性真实实现或显式告知用户降级范围。
