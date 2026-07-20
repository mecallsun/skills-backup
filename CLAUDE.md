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
- **HTML Prototypes:** `00-方案文档/04-HTML原型/` 共 25 个原型页面 + `_shared/` 共享资源（v2.12.3 起统一为「共用页头 + Tab 页签切换」三层架构）。

### Important Notes

- **HTML原型目录已存在** — `00-方案文档/04-HTML原型/` 目录包含 25 个原型页面 + mock-data.js（1.1MB Mock 数据）+ _shared/ 共享资源（v2.12.3 起移除原 Tier 2 紧凑型图标导航条，统一为 Tab 栏）。
- **项目当前版本：** v2.13.34（2026-07-20 入住弹窗 100% 原型对齐版）
- **v2.13.24 数据库：** 31 EF 实体 100% 对齐 SQL 真理源 init_schema.sql，3 张表 DDL 补充完整（31→33 张），业务深度 25 字段全补，双向联动 12 条规则全部实现；**v2.13.33 起 14 条联动（含 EmployeeName 双管齐下同步：实时覆盖 + Repair 写回）**
- **数据库默认值：** `192.168.1.237` / `WaterMeterDB` / `__DB_USER__` / `__DB_PASSWORD__`（v2.13.22 统一到生产环境；AppConfigManager + AesEncryptor 加密存储；v2.13.32 起通过 `AppConfigRuntime` 支持运行时热加载，无需重启服务）
- **Swagger enabled in all environments** — not gated behind `IsDevelopment()`.
- **No CORS, no HTTPS redirect** — assumes trusted local network deployment.
- **Swagger enabled in all environments** — not gated behind `IsDevelopment()`.
- **No CORS, no HTTPS redirect** — assumes trusted local network deployment.
- **Only `FilterCacheController` has `[Authorize]`** — API endpoints rely on network trust; the Booking controller reads current user from `X-User-Name` header.
- **Warning suppression:** Projects globally suppress `CS1998`, `CS8602`, `CS8629`, `CS0618`. These are known nullable/async warnings acknowledged by the team.
- **Data cleanup:** `DataCleanupHostedService` runs at startup to normalize invalid FK references in employees.
- **Git redact filter:** `filter.redactdb` replaces DB credentials with `__DB_USER__`/`__DB_PASSWORD__` on `git add`; working tree keeps real values.
- **v2.13.32 数据源热加载：** 通过 `AppConfigRuntime` 单例 + `IDbContextFactory<DormDbContext>`，Web 端或托盘修改数据库配置并保存后，**无需重启服务**即可让 Api/Admin 下次请求自动切换到新连接；`DatabaseOperationInterceptor` 输出 `[DB-CONN]` / `[DB-EXEC]` / `[DB-EXEC-SLOW]` 运行时日志，提供连接可观测性（详见 `Settings → 数据库连接` 页面顶部"🗄️ Server/Database"徽章，30s 轮询）。**配套修复**：托盘 SettingsForm 加"测试连接"按钮 + AppConfigManager.SaveConfigurationAsync 写 SysParameter 不再使用密文（v2.13.32-hotfix）。
- **v2.13.33 Repair API：** Booking 模块新增 `POST /api/v1/bookings/repair-employee-names`，用于批量回填历史 `DormBooking.EmployeeName`（按 `EmployeeId` 优先 / `EmployeeCode` 次之对齐 `SysEmployee.RealName`，返回 `updated/skipped/notFound` 计数）。**`/Booking` 页面 PageHeader 新增「修复姓名关联」按钮**。同时修复 BUG #1：`selectCiEmp` 卡死（增加 `ciSearchResults/coSearchResults` 缓存，按 empId 查员工并完整填充 `dataset.empId` + 员工信息展示区）。
- **v2.13.34 checkInModal 100% 原型对齐：** 11 项 UI/交互不一致全部修复（单列布局 + form-card 系列样式 + 操作类型 radio 切换入住/退房 + 姓名模糊搜索 + emp-info-card 横排 + 考勤班次 Badge 渲染 + 校验 alert 3 种状态 + "提交"按钮 + breadcrumb）。**PageHeader 移除冗余"办理退房"按钮**（合并到 checkInModal 的 opType=2 分支）。
