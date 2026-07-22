# BUG 修复报告 v2.13.28 — 数据库连接传播断裂 + Settings 页面按钮失效

> **版本**：v2.13.28
> **日期**：2026-07-20
> **优先级**：P0 — 生产级重大 BUG
> **状态**：✅ 已修复 + 编译验证通过

---

## 一、BUG 描述

### Bug 1：数据库连接参数传播断裂（列表页面数据为空）

**现象**：
- 系统启动日志显示 `DB Provider: Sqlite`，而非用户配置的 SQL Server
- 所有列表页面（人员清单、宿舍档案、办理登记等）返回空数据
- 托盘程序系统设置中保存了 SQL Server 参数，但重启后仍使用 SQLite

**根因分析**：

配置传播链路断裂，存在 **三层配置源** 但优先级混乱：

```
┌─────────────────────────────────────────────────────────┐
│ 配置源层级（优先级从高到低）                               │
├──────────────┬──────────────────────────────────────────┤
│ 环境变量      │ DormManage_DB_CONN（托盘注入）            │
│ appsettings  │ Admin/Api/TrayApp 各自的                  │
│              │ Database.Provider 字段                    │
│ 硬编码默认值  │ Server=192.168.1.237;...                 │
└──────────────┴──────────────────────────────────────────┘
```

**具体问题**：

1. `Program.cs` 中 `dbProvider` 变量直接从 `builder.Configuration["Database:Provider"]` 读取
2. 该配置来自子进程自己的 `appsettings.json`（三个项目都有独立的 `appsettings.json`，初始值均为 `"Provider": "Sqlite"`）
3. 托盘程序保存 SQL Server 配置后，只更新了 **托盘自己的** `appsettings.json`，并未同步到 Admin/Api 子进程的 `appsettings.json`
4. 虽然 `ProcessManager.BuildStartInfo()` 注入了 `DormManage_DB_CONN` 环境变量，但 `dbProvider` 在注入之前就已经从 `appsettings.json` 读到了 `"Sqlite"`
5. 结果：即使 `connectionString` 变量拿到了正确的 SQL Server 连接串，但 `dbProvider` 仍然是 `"Sqlite"` → 走了 `UseSqlite()` 分支

**数据流断点**：
```
用户保存 SQL Server 配置
  → TrayApp appsettings.json 更新 ✓
  → db_setting.json 写入 ✓
  → 环境变量 DormManage_DB_CONN 注入 ✓
  → Admin/Api appsettings.json 未更新 ✗
  → dbProvider 从 Admin/Api appsettings.json 读到 "Sqlite" ✗
  → 使用 SQLite 数据库（空数据） ✗
```

### Bug 2：Settings 页面数据库配置按钮不可用

**现象**：
- 访问 `/Settings` 页面时，数据库连接测试/保存按钮无响应
- 只有 URL 带 `?tab=db` 参数时按钮才可用

**根因**：
- `Settings/Index.cshtml` 第 746-754 行的 JS 代码：
```javascript
if (params.get('tab') === 'db') {
    loadDbConfig();
    document.getElementById('btnDbTest').addEventListener('click', runDbTest);
    document.getElementById('btnDbSave').addEventListener('click', saveDbConfig);
}
```
- 事件处理器只在 `?tab=db` 时绑定，用户默认进入 `/Settings` 不带此参数

---

## 二、修复方案

### 修复 1：环境变量优先级提升（核心修复）

**修改文件**：`DormManage.Admin/Program.cs`、`DormManage.Api/Program.cs`

**核心逻辑变更**：
```csharp
// 修复前（错误）：
var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";

// 修复后（正确）：
var envConnStr = Environment.GetEnvironmentVariable("DormManage_DB_CONN");
var envDbPath = Environment.GetEnvironmentVariable("DormManage_DB_PATH");
var envProvider = Environment.GetEnvironmentVariable("DormManage_DB_PROVIDER");

var effectiveProvider = !string.IsNullOrEmpty(envProvider)
    ? envProvider
    : (!string.IsNullOrEmpty(envConnStr) ? "SqlServer"
    : (!string.IsNullOrEmpty(envDbPath) ? "Sqlite"
    : (builder.Configuration["Database:Provider"] ?? "SqlServer")));
```

**优先级链**（从高到低）：
1. `DormManage_DB_PROVIDER` 环境变量（显式覆盖）
2. `DormManage_DB_CONN` 非空 → SqlServer（托盘注入的 SQL Server 连接串）
3. `DormManage_DB_PATH` 非空 → Sqlite（托盘注入的 SQLite 路径）
4. `appsettings.json` 的 `Database.Provider` 字段
5. 默认值 `"SqlServer"`（生产环境）

**同时修复**：
- 所有 `dbProvider` 引用替换为 `effectiveProvider`
- 日志输出使用 `effectiveProvider`

### 修复 2：Settings 页面按钮始终可用

**修改文件**：`DormManage.Admin/Pages/Settings/Index.cshtml`

```javascript
// 修复前：
if (params.get('tab') === 'db') {
    loadDbConfig();
    // 按钮事件绑定...
}

// 修复后：移除 URL 参数依赖
loadDbConfig();
var providerEl = document.getElementById('dbProvider');
if (providerEl) providerEl.addEventListener('change', toggleProviderUI);
var testEl = document.getElementById('btnDbTest');
if (testEl) testEl.addEventListener('click', runDbTest);
var saveEl = document.getElementById('btnDbSave');
if (saveEl) saveEl.addEventListener('click', saveDbConfig);
```

### 修复 3：默认配置修正

**修改文件**：三个 `appsettings.json`

| 文件 | 变更前 | 变更后 |
|------|--------|--------|
| `DormManage.TrayApp/appsettings.json` | `"Provider": "Sqlite"` | `"Provider": "SqlServer"` + 默认连接串 |
| `DormManage.Admin/appsettings.json` | `"Provider": "Sqlite"` | `"Provider": "SqlServer"` + 默认连接串 |
| `DormManage.Api/appsettings.json` | `"Provider": "Sqlite"` | `"Provider": "SqlServer"` + 默认连接串 |

**理由**：生产环境默认使用 SQL Server，SQLite 仅用于开发调试。当托盘程序未运行时（如独立启动 Admin/Api），也应默认连接 SQL Server。

### 修复 4：增强日志可观测性

**修改文件**：`DormManage.TrayApp/Services/ConfigService.cs`、`ProcessManager.cs`

- `ConfigService.Load()`：增加 `DbConnStrLen` 日志
- `ProcessManager.StartApiAsync/StartAdminAsync`：增加 `DB Provider` + `ConnStrLen` 日志
- `ProcessManager.BuildStartInfo`：移除静态方法中的实例成员访问（编译错误修复）

---

## 三、修复后数据流

```
用户保存 SQL Server 配置（托盘系统设置）
  → AppConfigManager.SaveConfigurationAsync()
    → 1. 测试连通性 ✓
    → 2. AES-256 加密密码 ✓
    → 3. 写入 db_setting.json（原子写入）✓
    → 4. 写入 SQL Server SysParameter 表（后台异步）✓
    → 5. 触发 OnDatabaseConfigUpdated 事件 ✓
      → TrayApp ConfigService.UpdateDatabaseSection()
        → 更新 TrayApp appsettings.json ✓
        → 广播 IPC 通知 ✓
  → 用户选择"重启服务"
    → ProcessManager.RestartAllAsync()
      → StartApiAsync()
        → BuildStartInfo(cfg, isApi: true)
          → 注入环境变量 DormManage_DB_CONN ✓
          → 注入环境变量 DormManage_KESTREL_PORT=5100 ✓
      → StartAdminAsync()
        → BuildStartInfo(cfg, isApi: false)
          → 注入环境变量 DormManage_DB_CONN ✓
          → 注入环境变量 DormManage_KESTREL_PORT=5001 ✓

Admin 子进程启动
  → Program.cs
    → envConnStr = Environment.GetEnvironmentVariable("DormManage_DB_CONN")  ← 非空！
    → effectiveProvider = "SqlServer"  ← 环境变量优先级最高！
    → connectionString = envConnStr  ← 使用托盘注入的连接串
    → UseSqlServer(connectionString)  ← 正确连接 SQL Server ✓
    → DatabaseInitializer.InitializeAsync()
      → 连通性探测 → 关键表检测 → 字典种子 → 管理员种子 ✓
    → 列表页面查询数据库 → 返回真实数据 ✓
```

---

## 四、影响范围

| 模块 | 影响 | 修复内容 |
|------|------|---------|
| `DormManage.Admin/Program.cs` | 数据库配置读取 | `dbProvider` → `effectiveProvider`（环境变量优先） |
| `DormManage.Api/Program.cs` | 数据库配置读取 | `dbProvider` → `effectiveProvider`（环境变量优先） |
| `DormManage.TrayApp/appsettings.json` | 默认配置 | Sqlite → SqlServer |
| `DormManage.Admin/appsettings.json` | 默认配置 | Sqlite → SqlServer |
| `DormManage.Api/appsettings.json` | 默认配置 | Sqlite → SqlServer |
| `DormManage.Admin/Pages/Settings/Index.cshtml` | 按钮事件绑定 | 移除 `?tab=db` URL 参数依赖 |
| `DormManage.TrayApp/Services/ConfigService.cs` | 日志增强 | 增加 `DbConnStrLen` |
| `DormManage.TrayApp/Services/ProcessManager.cs` | 日志增强 | 增加 `DB Provider` + `ConnStrLen` 日志 |

---

## 五、编译验证

```bash
$ dotnet build DormManage.sln -c Release
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:08.16
```

---

## 六、回归测试清单

| # | 测试场景 | 期望结果 |
|---|---------|---------|
| 1 | 托盘程序启动 → 系统设置 → 保存 SQL Server 参数 | 配置写入 appsettings.json + db_setting.json |
| 2 | 托盘程序启动 → 系统设置 → 保存后点击"是"重启服务 | Admin/Api 重启，环境变量注入 |
| 3 | 查看托盘日志 | 显示 `DB Provider=SqlServer, ConnStrLen=XXX` |
| 4 | 查看 Admin 启动日志 | 显示 `DB Provider: SqlServer`（不再是 Sqlite） |
| 5 | 访问 /Personnel 页面 | 显示真实人员数据 |
| 6 | 访问 /Dorms 页面 | 显示真实宿舍数据 |
| 7 | 访问 /Settings 页面（不带 ?tab=db） | 数据库配置区域正常加载，测试/保存按钮可用 |
| 8 | 独立启动 Admin（不通过托盘） | 使用 appsettings.json 中的 SqlServer 配置 |

---

## 七、预防复发措施

| 措施 | 说明 |
|------|------|
| **环境变量优先级最高** | 子进程启动时优先读取环境变量，不依赖本地 appsettings.json |
| **默认配置修正** | 三个 appsettings.json 默认 Provider = SqlServer |
| **日志增强** | 启动日志明确显示 Provider、ConnStrLen，便于排查 |
| **Settings 按钮无条件绑定** | 不再依赖 URL 参数 |
| **文档记录** | 本文档 + 技术架构文档同步更新 |

---

## 八、关联文档

- [79-系统启动机制与生产部署文档-v2.13.25.md](./79-系统启动机制与生产部署文档-v2.13.25.md) — 启动机制
- [71-系统设置数据库连接双UI同步需求-v2.13.19.md](./71-系统设置数据库连接双UI同步需求-v2.13.19.md) — 双 UI 同步
- [01-技术架构与系统开发方案.md](./01-技术架构与系统开发方案.md) — 总架构

## v2.13.109 备注：SQLite 已彻底移除

本 BUG（数据库连接传播断裂）的根因是 Provider 配置从 Sqlite 误用为 SqlServer。v2.13.109 起 SQLite Provider 已彻底移除，本 BUG 不会再以任何形式复发（ProjectProvider=Sqlite 启动会硬拒绝失败）。
