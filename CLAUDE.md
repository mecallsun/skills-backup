# 金戈宿舍管理系统 — Claude Code 项目配置

> **项目名称**：金戈宿舍管理系统  
> **配置版本**：v2.13.8  
> **生效日期**：2026-07-16  
> **适用对象**：所有参与本项目的 Claude Code 会话

---

## 1. 项目概述

本项目是基于 .NET 8 + Razor + EF Core 的宿舍管理系统，采用 EXE 自托管部署架构。

### 1.1 技术栈

| 层级 | 技术选型 |
|------|---------|
| 后端框架 | .NET 8 ASP.NET Core |
| 前端框架 | Razor Pages + Bootstrap 5 + jQuery |
| ORM | Entity Framework Core 8 |
| 数据库 | SQLite（开发）/ SQL Server（生产） |
| 部署 | EXE 自托管 + 托盘守护进程 |

### 1.2 部署环境要求（v2.12.42 同步更新）

| 组件 | 支持版本 |
|------|---------|
| **操作系统** | **Windows 11 / Windows Server 2019 / Windows Server 2016 / Windows Server 2022**（含 Windows 10 兼容） |
| **数据库** | **SQL Server 2014 / 2017 / 2019 / 2022**（向下兼容 SQL Server 2014 及以上版本，含 SQL Server Express） |
| **.NET 运行时** | .NET 8 Desktop Runtime 8.0.x（必需） |

### 1.2.1 支持的终端类型（v2.12.42 扩展）

| 终端类型 | 平台 | 功能范围 | 状态 |
|---------|------|---------|------|
| **安卓 PDA 终端** | Android 8.0+ | 完整扫码 + 抄表 + 上传 | ✅ V1.0 已实现 |
| **安卓平板终端**（12 寸屏幕自适应） | Android 8.0+ | 功能范围同 PDA 终端 | ✅ V1.0 已实现 |
| **Web 访问管理前端** | Win/Mac/Linux 浏览器 | 全套管理功能 | ✅ V1.0 已实现 |
| **小程序移动端** | 微信 / 钉钉 / 支付宝 | **功能范围待定义** | ⚠️ 规划中（v2.13+） |

### 1.3 项目结构

```
宿舍管理系统/
├── 00-方案文档/           # 需求规格、原型、SOP 流程文档
├── 01-Database/           # 数据库迁移脚本、种子数据
├── 05-Standalone/        # 独立部署模块
├── DormManage.Api/        # API 服务（EXE）
├── DormManage.Admin/      # Web 管理后台（EXE）
├── DormManage.TrayApp/   # 托盘守护程序（EXE）
├── DormManage.Shared/     # 共享库（Models/DbContext/Services）
├── publish-final/         # 部署包输出目录
└── CLAUDE.md             # 本文件
```

---

## 2. 强制执行：软件开发 SOP 流程

> ⚠️ **所有软件开发任务必须严格遵守以下 6 阶段 SOP 流程，禁止跳过任何阶段。**

### 2.1 六阶段 SOP 流程

```
┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐
│ ① 开发方案 │ →  │ ② 功能需求 │ →  │ ③ 原型设计 │ →  │ ④ 编程开发 │ →  │ ⑤ 功能测试 │ →  │ ⑥ 交付部署 │
│   顶层架构 │    │   详细规格 │    │   快速验证 │    │   业务落地 │    │   验证确认 │    │   打包发布 │
└────────────┘    └────────────┘    └────────────┘    └────────────┘    └────────────┘    └────────────┘
```

### 2.2 各阶段要求

#### ① 开发方案（20%）
- [ ] 架构选型决策
- [ ] 模块依赖图设计
- [ ] ER 图 + 字段表
- [ ] API 端点表 + 跳转矩阵

**验收物**：技术方案文档（`01-技术架构与系统开发方案.md` 第 N 章）

#### ② 功能需求（20%）
- [ ] 业务需求梳理
- [ ] 功能详细说明（字段、校验、逻辑）
- [ ] API 契约定义
- [ ] 需求文档精简（仅保留本次交付内容）

**验收物**：需求规格文档（`XX-需求规格-vX.XX.md`）

#### ③ 原型设计（15%）
- [ ] HTML 原型页面开发
- [ ] Mock 数据填充
- [ ] 跳转逻辑实现
- [ ] 产品/业务方验收签字

**验收物**：`04-HTML原型/` 目录 + 验收签字

#### ④ 编程开发（25%）
- [ ] 按分层顺序实现（Shared → Api → Admin → TrayApp）
- [ ] 与原型字段/跳转一一对照
- [ ] 遵循代码规范
- [ ] 编译通过（0 错误）

**验收物**：编译报告 + `05-原型与代码基线对照.md`

#### ⑤ 功能测试（10%）
- [ ] 冒烟测试
- [ ] 功能用例测试
- [ ] 边界与异常测试
- [ ] 缺陷修复

**验收物**：测试报告（`XX-测试报告-vX.XX.md`）

#### ⑥ 交付部署（10%）
- [ ] 发布打包（Release 模式）
- [ ] 交付包打包（Embedded.zip + Bootstrapper.exe）
- [ ] 部署说明编写
- [ ] 用户手册（如需要）

**验收物**：`publish-final/` 目录 + `README.md`

### 2.3 阶段阻塞规则

| 当前阶段 | 阻塞条件 |
|---------|---------|
| ② 功能需求 | ① 开发方案未完成 |
| ③ 原型设计 | ② 功能需求未完成 |
| ④ 编程开发 | ③ 原型设计未验收 |
| ⑤ 功能测试 | ④ 编程开发未通过 |
| ⑥ 交付部署 | ⑤ 功能测试未通过 |

> **规则**：未完成前置阶段的验收，禁止进入下一阶段。

---

## 2.1 文档冲突检查与同步规则（强制）

> ⚠️ **所有变更修改或新增需求/功能开发任务时，必须优先执行此规则**。

对「开发方案」及「功能需求 / 原型设计 / 设计规范」的项目相关所有文档进行冲突检查，并按如下内容 / 数据逻辑 / 计算方法等 进行更新或补充对应文档的相关描述。

### 2.1.1 检查清单

| # | 检查项 | 优先级 |
|---|--------|--------|
| 1 | 字段定义、状态枚举、业务规则一致性 | 高 |
| 2 | 费用计算、统计口径、公式推导一致性 | 高 |
| 3 | API 端点、请求/响应格式一致性 | 中 |
| 4 | 页面布局、UI 风格一致性 | 中 |
| 5 | 数据库表结构、索引约束一致性 | 高 |

### 2.1.2 UI 风格统一规范（参照 `00-方案文档/35-列表页面统一UI设计规范-v2.11.4.md`）

| 区域 | 规范 |
|------|------|
| **页头** | 图标 + 标题 + 总数 + 主操作按钮 |
| **筛选条件区域** | `flex-nowrap` 一行排列，自适应字段宽度 |
| **列表** | 第一列为"序号"（跨页连续编号） |
| **页尾** | 统计信息 + 分页器 |
| **响应式** | 768px 以下筛选条件自动改为 2 列布局 |
| **查询/重置按钮** | 高度固定 38px，宽度固定 100px，风格完全一致（圆角 6px，字体 14px，图标 14px，padding 0 16px，flex 居中） |
| **筛选条件持久化** | 所有列表页筛选条件值优先写入 localStorage（用户退出登录时若未勾选"存储筛选条件"则清除）；若勾选"存储筛选条件"则在个人中心写入数据库缓存表（SysUserFilterCache），下次登录自动加载至对应模块筛选区；提供"清除"按钮一键清空所有模块缓存值 |
| **双 UI 职责划分** | 托盘系统配置窗口（SettingsForm）仅保留核心服务端参数（PDA/Web 端口、数据库、图片路径、服务启停、保存），无权限控制；Web 端系统设置承载全部功能（用户角色/备份恢复/系统集成/筛选缓存等），受角色权限管控 |

---

## 3. 代码规范

### 3.1 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 类名 | PascalCase | `PersonnelService` |
| 方法名 | PascalCase | `GetByIdAsync` |
| 公共属性 | PascalCase | `EmployeeCode` |
| 私有字段 | `_camelCase` | `_dbContext` |
| 局部变量 | camelCase | `employeeList` |
| 常量 | PascalCase | `DefaultPageSize` |
| 接口 | `I` + PascalCase | `IPersonnelService` |

### 3.2 注释规范

| 元素 | 要求 |
|------|------|
| 公共类 | XML 文档注释（summary/param/returns） |
| 公共方法 | XML 文档注释（summary/param/returns/exception） |
| 复杂业务逻辑 | 行内注释说明 |
| 关键操作日志 | Console.WriteLine |

### 3.3 异常处理规范

- 业务异常使用 `InvalidOperationException`
- 控制器捕获异常返回 `ApiResponse.Error(code, message)`
- 批量操作使用事务：`db.Database.BeginTransactionAsync()`

### 3.4 API 响应格式

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}
```

---

## 4. 前端规范

### 4.1 HTML 原型规范

| 规范 | 说明 |
|------|------|
| 单文件 | 每个页面一个 `.html` 文件 |
| CDN 引用 | Bootstrap 5 + Bootstrap Icons |
| 字段命名 | `id`/`name` 与 Razor 字段对齐 |
| 跳转方式 | `href="#page-name"`（原型）/ `href="/Controller/Action"`（生产） |
| 中文界面 | 所有文案使用中文 |

### 4.2 Razor 视图规范

| 规范 | 说明 |
|------|------|
| 布局 | 使用 `Views/Shared/_Layout.cshtml` |
| 表单验证 | 使用 `asp-validation-for` + jQuery Validation |
| 分页 | 使用 `PagedResult<T>` |
| 按钮样式 | Bootstrap 5 按钮类（`btn-success`/`btn-primary` 等） |
| 状态 Badge | `bg-success`/`bg-warning`/`bg-secondary` |

---

## 5. 数据库规范

### 5.1 表命名

- 单数形式：`Dorm` / `Employee` / `BillingStandard`
- 带前缀的系统表：`SysUser` / `SysRole` / `SysPermission`

### 5.2 字段命名

- 使用 PascalCase：`EmployeeCode` / `LeaveDate`
- 主键：`Id`（int/bigint）
- 审计字段：`CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy`
- 软删除：`IsDeleted`（bool）
- 状态：`Status`（int，1=正常/2=离职/3=待入职）

### 5.3 索引规范

- 唯一约束：`UX_TableName_ColumnName`
- 普通索引：`IX_TableName_ColumnName`
- 外键索引：自动创建

---

## 6. Git 提交规范

### 6.1 分支命名

| 类型 | 格式 | 示例 |
|------|------|------|
| 功能分支 | `feature/功能名称` | `feature/personnel-module` |
| 修复分支 | `fix/问题描述` | `fix/export-excel-error` |
| 文档分支 | `docs/文档类型` | `docs/requirements-v211` |

### 6.2 提交信息

```
<类型>(<模块>): <简短描述>

[可选的详细说明]

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

**类型**：feat / fix / docs / style / refactor / test / chore

### 6.3 示例

```
feat(personnel): 新增员工离职功能

- 添加 Status 字段变更逻辑
- 实现软删除机制
- 更新列表筛选条件

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

## 7. 文档规范

### 7.1 文档目录结构

```
00-方案文档/
├── 01-技术架构与系统开发方案.md    # 顶层架构文档
├── 02-SOP开发流程规范.md          # SOP 流程规范
├── 03-XX需求规格-vX.XX.md        # 功能需求文档
├── 04-HTML原型/                   # HTML 原型页面
├── 05-原型与代码基线对照.md       # 基线对照报告
└── XX-测试报告-vX.XX.md          # 测试报告
```

### 7.2 文档版本号

- 格式：`v主版本.子版本`
- 主版本：重大功能迭代
- 子版本：功能增强/修复

### 7.3 文档模板

需求规格文档头部：
```markdown
# XX 功能需求规格

> **版本**：vX.XX  
> **日期**：YYYY-MM-DD  
> **状态**：草稿/评审中/已定稿  
> **关联原型**：04-HTML原型/xx.html
```

---

## 8. 测试规范

### 8.1 冒烟测试清单

每次代码提交后必须执行：

```
✅ Api 启动 → /api/v1/system/dbhealth/quick 返回 200（数据库连接正常）
✅ Api 启动 → /swagger/index.html 可访问
✅ Api → /api/dorms、/api/v1/bookings、/api/v1/personnel、/api/meter/records 均返回 200（SQL Server/SQLite 一致）
✅ Admin 启动 → /Account/Login 页面可访问
✅ Bootstrapper → 自动解压 + 启动 TrayApp
✅ 核心 CRUD 操作正常
```

> **v2.13.6 订正**：原清单的 `/health`、`/api/v1/xx/dictionaries` 端点实际不存在；健康检查真实端点为 `/api/v1/system/dbhealth/quick`。字典由各基础资料端点提供，无统一 dictionaries 端点。

### 8.2 测试用例模板

| 用例ID | 模块 | 用例名称 | 输入 | 预期输出 | 实际结果 | 状态 |
|--------|------|---------|------|---------|---------|------|
| TC-001 | Personnel | 新增员工 | 工号/姓名/部门/类型 | 创建成功 | | Pass |

---

## 9. 部署规范

### 9.1 发布命令

```bash
# 编译（Debug 验证）
dotnet build DormManage.sln -c Debug

# 发布（Release 最终）
dotnet publish DormManage.Admin/DormManage.Admin.csproj -c Release -r win-x64 --self-contained true -o publish-final/Admin
dotnet publish DormManage.Api/DormManage.Api.csproj -c Release -r win-x64 --self-contained true -o publish-final/Api
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj -c Release -r win-x64 --self-contained true -o publish-final/TrayApp
```

### 9.2 启动流程（V2.13.2 同步更新）

1. 运行 `DormManage.TrayApp.exe`（托盘守护程序，部署于 `publish-final/V1.0/TrayApp/`）
2. 托盘程序自动启动：
   - `DormManage.Admin.exe`（Web 管理端，端口 5001，可通过 SettingsForm 调整）
   - `DormManage.Api.exe`（PDA 接口服务，端口 5100，可通过 SettingsForm 调整）
3. 托盘菜单"打开管理后台"自动启动浏览器访问 `http://localhost:5001`
4. PDA 终端通过 `http://<服务器IP>:5100` 访问抄表接口

> **V2.13.2 变更**：Api 端口从 5000 → 5100（CLAUDE.md v2.13.0 描述有误，已修正）。托盘通过环境变量 `DormManage_KESTREL_PORT` 注入端口，`DormManage_DB_CONN`/`DormManage_DB_PATH` 注入数据库连接。

### 9.2 部署包结构

```
publish-final/
├── DormManage.Bootstrapper.exe    # 引导程序
├── Embedded.zip                   # 嵌入式部署包
├── Admin/                         # Web 后台
├── Api/                           # API 服务
├── TrayApp/                       # 托盘守护
├── Bootstrapper/                  # 引导程序目录
└── README.md                      # 部署说明
```

---

## 10. 配置文件

| 文件 | 用途 |
|------|------|
| `appsettings.json` | 应用配置 |
| `appsettings.Development.json` | 开发环境配置 |
| `appsettings.Production.json` | 生产环境配置 |
| `dorm.db` | SQLite 数据库文件 |
| `logs/` | 日志目录 |

---

## 11. 相关文档

| 文档 | 路径 | 说明 |
|------|------|------|
| SOP 开发流程规范 | `00-方案文档/02-SOP开发流程规范.md` | 6 阶段软件开发流程 |
| 技术架构文档 | `00-方案文档/01-技术架构与系统开发方案.md` | 顶层架构设计 |
| 原型基线对照 | `00-方案文档/05-原型与代码基线对照.md` | 原型与代码一致性 |

---

## 12. 版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v1.0 | 2026-07-11 | 初始版本，定义 SOP 流程和代码规范 |
| v1.1 | 2026-07-12 | 新增文档冲突检查与同步规则；新增列表页面统一UI设计规范 |
| v1.2 | 2026-07-14 | 修正主解决方案文件名笔误；关联 v2.12.43 页面 500 修复报告 |
| v2.13.0 | 2026-07-14 | 新增 RBAC 认证权限体系、Cookie 认证、强制登录控制、托盘守护程序、用户/角色独立管理页面 |
| v2.13.1 | 2026-07-15 | 新增登录页面（/Account/Login）、登出页面（/Account/Logout）、管理员种子数据（admin/admin123）、BCrypt 密码加密 |
| v2.13.2 | 2026-07-15 | **补全 DormManage.TrayApp 源码**（WinForms 托盘守护）：单实例锁 + 自动启停 Admin/Api + 故障自愈 + 健康检查 + SettingsForm 配置窗口；修复 ConfigService.PropertyNameCaseInsensitive 反序列化 BUG；新增 56/57 技术方案与需求规格文档 |
| v2.13.3 | 2026-07-15 | **补全 25 项 v2.13.1 差距清单**：P0（托盘补全） + P1（16 项：菜单权限/用户角色页面/首页7图表+聚合服务/PDA上传+抄表作废/确认退房/数据库深度验证/服务启停IPC/备份恢复/用户管理API/人员Excel导入导出/账单详情/SysPermission字段） + P2（9 项：月度选择器/编辑页员工信息/床位号字段/容量约束/系统集成API/PDA版本API/班组筛选/通用Excel导出/共用页头Tab组件） |
| **v2.13.4** | **2026-07-16** | **P0 修复：托盘右键 → 系统设置 "UI异常，创建窗口出错"** —— TrayAppContext 内嵌不可见 OwnerForm + NotifyIconManager 加固 + SettingsForm 拆分 + Font/ShowDialog/SafeShow 三层兜底；菜单项"设置..."改为"系统设置..."与双 UI 职责规范一致。详见 `00-方案文档/62-托盘右键异常修复报告-v2.13.4.md` |
| **v2.13.5** | **2026-07-16** | **修复：详情/编辑/历史页跳转链接 404** —— 提交 5a522ef 将 5 个页面路由改为纯 `@page`（id 走查询字符串）后，列表页与页内跳转链接仍为路径式（`/Dorms/Details/5`），点击后由 500 变 404。补齐 5 处链接为查询式（`?id=`）：Dorms/Index（详情+修改）、Dorms/Details（编辑）、Dorms/History（宿舍详情）、Booking/Index（修改）。已实启服务登录验证全链路 200。详见 `00-方案文档/64-详情编辑页路由链接修复验证报告-v2.13.5.md` |
| **v2.13.6** | **2026-07-16** | **修复：EF 实体与真实 SQL Server schema 对齐（核心模块）** —— Api（Provider=SqlServer）多端点 500，根因为 EF 实体主键/列名/类型与真实 WaterMeterDB 不一致（SQLite 下被掩盖）。修复 Dorm→DormId、DormBooking→BookingId、MeterRecord→RecordId、SysEmployee→EmployeeId 主键映射 + DormBooking Type→BookingType/TINYINT + SysEmployee Team Ignore。连真实 SQL Server 验证核心模块 7/7 端点 200，SQLite（Admin）重建 dorm.db 后回归无退。**遗留（待决策）**：Sys*/RBAC 子系统与真实库结构性分裂 + 缺表。详见 `00-方案文档/65-EF实体与真实Schema对齐修复报告-v2.13.6.md` |
| **v2.13.7** | **2026-07-16** | **RBAC 上 SQL Server（补表+实体重构）** —— 补真理源缺表：SysRole 补 `SortOrder` 列 + 新增 `SysPermission`/`SysRolePermission` 表（幂等迁移脚本 `01-Database/migrations/v2.13.7-rbac-tables.sql`，已对现网库执行）。重构 Sys* 实体：SysUser（ToTable+Id→UserId+UserName→Username+Phone→Mobile+LastLoginTime→LastLoginAt+Ignore EmployeeId/UpdatedAt）、SysRole（Id→RoleId）、SysUserRole（复合主键 UserId+RoleId+去 Id）。双 provider 验证：SQL Server `/api/v1/auth/users`+`/roles` 200、SQLite 登录+用户/角色页面 200。详见 `00-方案文档/66-RBAC上SQLServer补表与实体对齐报告-v2.13.7.md` |
| **v2.13.8** | **2026-07-16** | **系统装配·架构同步·深度测试** —— ①修复选床/入住并发竞态（CheckIn/ConfirmCheckIn/UndoCheckOut 加可串行化事务+执行策略重试），真实 SQL Server 并发测试通过（8并发/容量1→仅1成功）；②修复真实缺陷 `DormBooking.UpdatedAt NOT NULL` 插入失败（DbContext 统一 SaveChanges 审计字段填充）；③端到端回归双 provider 全通过（SQL Server 10/10、SQLite 7 页 200）；④架构边界澄清：本系统为 **Kestrel 自托管+REST+本地 IPC，无 IIS/websocket**，Android PDA 源码在独立仓库经 APK 分发。新增并发测试工具 `tests/RaceTest`。详见 `00-方案文档/67-系统装配架构同步与深度测试报告-v2.13.8.md` |
