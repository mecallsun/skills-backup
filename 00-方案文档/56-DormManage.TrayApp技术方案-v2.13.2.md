# DormManage.TrayApp — 托盘守护程序技术方案

> **版本**：v2.13.19  
> **日期**：2026-07-19  
> **状态**：已定稿  
> **关联需求**：`57-DormManage.TrayApp需求规格-v2.13.2.md`（v2.13.19 增量更新）、`71-系统设置数据库连接双UI同步需求-v2.13.19.md`  
> **关联修复报告**：`62-托盘右键异常修复报告-v2.13.4.md`  
> **变更说明**：
> - v2.13.2：DormManage.TrayApp 基础实现（单实例 + 自动启停 + 配置窗口 + 故障自愈）
> - v2.13.3：自启动开关 + IPC 服务端 + 共用页头 Tab 组件
> - v2.13.4：修复右键 → 系统设置 "UI异常，创建窗口出错"
> - **v2.13.19（本版）**：
>   - 新增托盘图标颜色状态机（红/黄/绿）
>   - SettingsForm 数据库配置改为字段式输入（与 Web /Settings 一致）
>   - 集成 `AppConfigManager` 双擎持久化（`db_setting.json` + `SysParameter`）
>   - 新增 IPC `getdbconfig` / `setdbconfig` / `dbconfig.updated` 命令
>   - 清理旧版 WaterMeter 连接串示例与不一致端口描述

---

## 1. 背景

v2.13.0 技术架构文档（`01-技术架构与系统开发方案.md`）与 CLAUDE.md 均明确 DormManage.TrayApp 是部署包的核心组成：

> 1. 运行 `DormManage.TrayApp.exe`（托盘守护程序）
> 2. 托盘程序自动启动：DormManage.Admin.exe（Web 管理端，端口 5001）+ DormManage.Api.exe（PDA 接口服务，端口 5100）

但项目源码中 `DormManage.TrayApp/` 目录始终不存在，仅有 `publish-final/V1.0/start.bat` 替代实现。start.bat 缺乏托盘 UI、配置面板、健康监控、自动重启能力，**生产部署存在 P0 风险**。

代码层面已为托盘协作预留 4 个环境变量：
- `DormManage_KESTREL_PORT`（端口注入，Api 默认 5100、Admin 默认 5001）
- `DormManage_DB_CONN`（SQL Server 连接串）
- `DormManage_DB_PATH`（SQLite 绝对路径）
- `DormManage_IMAGE_ROOT`（图片存储根路径）

本方案正式落地 DormManage.TrayApp 源码。

---

## 2. 设计目标

| 目标 | 说明 |
|------|------|
| **零配置启动** | 部署后双击 EXE 即可启动整套服务 |
| **可视化运维** | 托盘菜单显示服务状态；图标颜色实时反映双服务健康状态 |
| **故障自愈** | Api/Admin 进程异常退出后自动重启 |
| **配置面板** | 端口、数据库字段式参数、图片路径可视化调整 |
| **双 UI 同步** | 托盘 SettingsForm 与 Web /Settings 数据库配置实时同步 |
| **单实例** | 防止重复启动产生端口冲突 |
| **职责清晰** | 仅核心服务端参数配置，无权限控制（与 Web 设置页面职责分离） |

---

## 3. 技术选型

| 维度 | 选型 | 理由 |
|------|------|------|
| UI 框架 | **WinForms** (.NET 8) | 托盘程序体积最小（~10MB），无需 WPF 重型依赖 |
| 运行时 | .NET 8 Desktop Runtime 8.0.x | 与 Admin/Api 一致，部署简单 |
| 进程管理 | `System.Diagnostics.Process` | 标准库，无第三方依赖 |
| 配置存储 | `appsettings.json`（运行时）+ `db_setting.json`（数据库配置双 UI 同步） | 共享配置语义；数据库配置通过 `AppConfigManager` 双擎持久化 |
| HTTP 健康检查 | `HttpClient` | 标准库 |
| 日志 | `Microsoft.Extensions.Logging` + 文件落盘 | 与 Admin/Api 一致 |
| 单实例锁 | `Mutex` + 全局名 `Global\DormManage.TrayApp.SingleInstance` | 系统级互斥 |
| 数据库配置持久化 | `AppConfigManager`（`db_setting.json` + `SysParameter` 表，AES-256 加密） | 双 UI 共享 |
| 双 UI 通信 | TCP/JSON IPC `127.0.0.1:5099` | Web Admin / Api 与 Tray 双向同步 |

**第三方包依赖**（最小化）：
- `Microsoft.Extensions.Configuration.Json`（8.0.0）— 读写 appsettings.json
- `Microsoft.Extensions.Logging`（8.0.0）— 日志抽象
- `Microsoft.Extensions.Logging.File`（可选，社区包）— 文件日志
- 引用 `DormManage.Shared` 以复用 `AppConfigManager`、`DatabaseConfigDto`、`ServiceIpc`（v2.13.19）

---

## 4. 项目结构

```
DormManage.TrayApp/
├── DormManage.TrayApp.csproj    # .NET 8 WinForms 项目
├── Program.cs                    # 入口（单实例锁 + db_setting 同步 + Application.Run）
├── TrayAppContext.cs             # ApplicationContext 子类，管理 NotifyIcon 生命周期 + IPC Server
├── NotifyIconManager.cs          # 托盘图标与菜单封装（含动态颜色图标）
├── IconGenerator.cs              # 32×32 实心圆点图标动态生成器（v2.13.19）
├── Models/
│   ├── AppConfig.cs              # 配置模型（PDA/Web 端口、数据库、图片路径）
│   └── ServiceState.cs           # 服务状态枚举（Stopped/Starting/Running/Crashed/Stopping）
├── Services/
│   ├── ConfigService.cs          # 配置读写（appsettings.json）
│   ├── ProcessManager.cs         # Admin/Api 进程生命周期管理
│   ├── HealthChecker.cs          # HTTP 健康检查
│   ├── LogService.cs             # 文件日志
│   └── AutoStartManager.cs       # 开机自启动
├── Forms/
│   ├── SettingsForm.cs           # 配置窗口（核心服务端参数 + 字段式数据库配置 + 服务启停）
│   └── AboutForm.cs              # 关于窗口
├── Resources/
│   └── tray-icon.ico             # 托盘图标（静态回退，v2.13.19 起优先动态生成）
└── README.md                     # 模块说明
```

---

## 5. 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                  DormManage.TrayApp.exe                       │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Program.cs（单实例锁 → db_setting 同步 → Run）        │  │
│  └────────────────┬─────────────────────────────────────┘  │
│                   ▼                                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  TrayAppContext（ApplicationContext）                   │  │
│  │  ├─ NotifyIconManager（图标 + 右键菜单 + 动态颜色图标）  │  │
│  │  │     ├─ 打开管理后台                                  │  │
│  │  │     ├─ 打开 API 文档                                 │  │
│  │  │     ├─ 服务状态（Api / Admin）                        │  │
│  │  │     ├─ 系统设置...（打开 SettingsForm）               │  │
│  │  │     ├─ 重启所有服务                                   │  │
│  │  │     ├─ 查看日志                                       │  │
│  │  │     ├─ 开机自启动                                     │  │
│  │  │     ├─ 关于                                           │  │
│  │  │     └─ 退出                                           │  │
│  │  ├─ ProcessManager                                       │  │
│  │  │     ├─ StartApiAsync() / StartAdminAsync()             │  │
│  │  │     ├─ StopAllAsync() / StopApiAsync() / StopAdminAsync()│ │
│  │  │     └─ RestartAllAsync() / HandleCrashAsync()          │  │
│  │  ├─ HealthChecker（每 10s 探测一次）                      │  │
│  │  │     ├─ Api: GET http://localhost:{apiPort}/swagger/index.html
│  │  │     └─ Admin: GET http://localhost:{adminPort}/         │  │
│  │  ├─ ConfigService（读写 appsettings.json）                │  │
│  │  ├─ IpcServer（TCP 127.0.0.1:5099 JSON-over-lines）       │  │
│  │  └─ LogService（logs/tray-{date}.log）                   │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
           │                              │
           │ 环境变量注入                    │ 启动子进程
           ▼                              ▼
┌──────────────────────┐    ┌────────────────────────────┐
│  DormManage.Api.exe  │    │   DormManage.Admin.exe     │
│  (PDA 接口服务)       │    │   (Web 管理后台)            │
│  默认端口：5100       │    │   默认端口：5001            │
└──────────────────────┘    └────────────────────────────┘
           │                              │
           └──────────────┬───────────────┘
                          ▼
                ┌──────────────────────┐
                │  数据库              │
                │  SQL Server / SQLite │
                └──────────────────────┘
```

---

## 6. 核心流程

### 6.1 启动流程

```
DormManage.TrayApp.exe 双击启动
    ↓
Program.cs：创建全局 Mutex `Global\DormManage.TrayApp.SingleInstance`
    ├─ 已存在 → 提示"托盘已在运行"并退出
    └─ 创建成功 → 进入下一步
    ↓
加载 appsettings.json（不存在则用默认值生成）
    ↓
从 db_setting.json 加载字段式数据库配置并同步到 appsettings.json（v2.13.19）
    ↓
TrayAppContext.Initialize()：
    ├─ 创建 NotifyIcon + ContextMenuStrip（图标初始为红色）
    ├─ 启动 ProcessManager（异步）
    │     ├─ StartApiAsync：设置 DormManage_KESTREL_PORT=5100、DormManage_DB_CONN、...
    │     │                 Process.Start(DormManage.Api.exe)
    │     └─ StartAdminAsync：同上
    ├─ 启动 HealthChecker 后台任务（10s 间隔）
    ├─ 启动 IpcServer（接收 Web Admin 命令）
    ├─ 订阅 AppConfigManager.OnDatabaseConfigUpdated 事件
    └─ 启动 ProcessManager.Exited 事件监听（故障自愈）
    ↓
Application.Run(context) → 阻塞
```

### 6.2 故障自愈与图标颜色状态机

```
ProcessManager 监听子进程 Exited 事件
    ↓
若退出码 != 0 或非用户主动 Stop：
    ├─ 记录日志 [ERROR] Api/Admin 进程异常退出，code={exitCode}
    ├─ 等待 5s（避免频繁重启）
    ├─ 重新启动该进程
    ├─ 通知 NotifyIcon 更新图标颜色（详见 6.2.1）
    └─ 通知 SettingsForm 更新状态文本（若已打开）
    ↓
HealthChecker 每 10s 探测一次：
    ├─ HTTP 200 → ServiceState.Running
    ├─ HTTP 非 200 / 超时 → 累计失败次数
    └─ 连续 3 次异常 → ServiceState.Crashed，触发 HandleCrashAsync
```

#### 6.2.1 托盘图标颜色状态机（v2.13.19）

`NotifyIconManager` 根据当前 Api / Admin 状态动态绘制实心圆点图标：

| 颜色 | 条件 | 说明 |
|------|------|------|
| **红色** | 非（Api Running 且 Admin Running）且没有任一 Running | 启动中 / 均已停止 / 均异常 |
| **黄色** | Api Running 与 Admin Running 恰好一个满足 | 单服务在线 |
| **绿色** | Api Running 且 Admin Running | 双服务均正常 |

图标生成：使用 `System.Drawing` 在 32×32 透明画布上绘制抗锯齿实心圆，通过 `Bitmap.GetHicon()` 转为 `Icon`。旧图标句柄在替换前调用 `DestroyIcon` 释放，避免 GDI 泄漏。

### 6.3 配置保存流程

```
SettingsForm 点击 [保存]
    ↓
校验字段（SqlServer：服务器/数据库/账号必填；Sqlite：路径必填）
    ↓
构造 DatabaseConfigDto
    ├─ 密码框为空 → DbPassword = "unchanged"（保留旧密码）
    └─ 密码框非空 → 使用新密码
    ↓
AppConfigManager.SaveConfigurationAsync(dto)
    ├─ 测试数据库连通性（失败则阻断）
    ├─ AES-256 加密密码
    ├─ 写入 db_setting.json（atomic rename）
    ├─ 后台写入 SysParameter 表
    └─ 触发 OnDatabaseConfigUpdated 事件
    ↓
ConfigService.UpdateDatabaseSection(dto)
    ├─ 生成 ConnectionString
    └─ 写回 appsettings.json
    ↓
提示用户"配置已保存，是否立即重启服务以生效？"
    ├─ 是 → ProcessManager.RestartAllAsync()
    └─ 否 → 退出 SettingsForm（下次手动重启）
```

### 6.4 退出流程

```
托盘菜单 [退出] / 窗口关闭事件
    ↓
确认对话框"确定要停止所有服务并退出托盘吗？"
    ├─ 否 → 取消
    └─ 是 →
         ├─ ProcessManager.StopAsync()（优雅关闭子进程，等待 5s）
         │     ├─ 子进程未退出 → Process.Kill()
         │     └─ 关闭所有相关资源
         ├─ NotifyIcon.Visible = false
         ├─ 释放全局 Mutex
         └─ Application.Exit()
```

### 6.5 OwnerForm 机制（v2.13.4 关键修复）

> 解决右键 → 系统设置 "创建窗口出错"。

**问题**：原 TrayAppContext 继承 `ApplicationContext`，但 ApplicationContext 默认无主 Form。当右键菜单回调中调用 `Form.ShowDialog()` 无 Owner 时，WinForms 内部尝试隐式创建 Owner 窗口；在 Win11 高 DPI / 主题加载未完成 / 启动时序敏感等场景下，`CreateWindowEx` 失败，抛出 `InvalidOperationException: 创建窗口句柄时出错`。

**修复方案**：在 TrayAppContext 构造时内嵌一个不可见 OwnerForm。

```csharp
private static Form CreateOwnerForm()
{
    var f = new Form
    {
        Name = "TrayAppOwnerForm",
        Text = "DormManage.TrayApp",
        ShowInTaskbar = false,
        FormBorderStyle = FormBorderStyle.None,
        Opacity = 0d,
        Size = new Size(0, 0),
        StartPosition = FormStartPosition.Manual,
        Location = new Point(-32000, -32000),  // 屏幕外
        WindowState = FormWindowState.Normal,
        MinimizeBox = false,
        MaximizeBox = false,
        ControlBox = false,
        Enabled = false  // 禁用所有输入
    };
    _ = f.Handle;  // 关键：强制创建窗口句柄，但不 Show
    return f;
}

// 在 TrayAppContext 构造函数中
_ownerForm = CreateOwnerForm();
MainForm = _ownerForm;  // 让 ApplicationContext 知道存在窗口宿主
```

**所有 ShowDialog 调用规范**：

```csharp
// ✅ 正确：传入 _ownerForm 作 Owner
form.ShowDialog(_ownerForm);

// ❌ 禁止：无 Owner 调用（在无主 Form 的 ApplicationContext 中会失败）
form.ShowDialog();
```

**MessageBox 调用规范**：

```csharp
// ✅ 正确：传入 _ownerForm 作 Owner
MessageBox.Show(_ownerForm, "消息", "标题", MessageBoxButtons.OK, MessageBoxIcon.Warning);

// ✅ 兜底：SafeShow 三层保护
private DialogResult SafeShow(Func<DialogResult> showWithOwner, string fallbackText)
{
    try { return showWithOwner(); }
    catch
    {
        try { return MessageBox.Show(fallbackText, "退出确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question); }
        catch { return DialogResult.No; }
    }
}
```

---

## 7. 配置模型

### 7.1 appsettings.json Schema

```json
{
  "Tray": {
    "ApiPort": 5100,
    "AdminPort": 5001,
    "ApiExecutable": "Api\\DormManage.Api.exe",
    "AdminExecutable": "Admin\\DormManage.Admin.exe",
    "AutoStartServices": true,
    "AutoRestartOnCrash": true,
    "HealthCheckIntervalSeconds": 10
  },
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Data Source=..\dorm.db",
    "SqlitePath": "..\dorm.db"
  },
  "Storage": {
    "ImageRoot": "Storage\\images",
    "LogRoot": "logs"
  }
}
```

### 7.2 AppConfig 模型

```csharp
public class AppConfig
{
    public TraySection Tray { get; set; } = new();
    public DatabaseSection Database { get; set; } = new();
    public StorageSection Storage { get; set; } = new();
}

public class TraySection
{
    public int ApiPort { get; set; } = 5100;
    public int AdminPort { get; set; } = 5001;
    public string ApiExecutable { get; set; } = "Api\\DormManage.Api.exe";
    public string AdminExecutable { get; set; } = "Admin\\DormManage.Admin.exe";
    public bool AutoStartServices { get; set; } = true;
    public bool AutoRestartOnCrash { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 10;
}

public class DatabaseSection
{
    public string Provider { get; set; } = "SqlServer";
    public string ConnectionString { get; set; } = "";
    public string SqlitePath { get; set; } = "";
}

/// <summary>
/// 数据库连接配置契约（字段式，v2.13.19 双 UI 同步）。
/// 字段与 SQL Server 连接字符串一一对应，后端用 SqlConnectionStringBuilder 组装。
/// </summary>
public class DatabaseConfigDto
{
    public string DbServer { get; set; } = "localhost";
    public int DbPort { get; set; } = 1433;
    public string DbName { get; set; } = "DormManage";
    public string DbUser { get; set; } = "sa";
    public string? DbPassword { get; set; }
    public string Provider { get; set; } = "SqlServer";
    public string? SqlitePath { get; set; }
}

public class StorageSection
{
    public string ImageRoot { get; set; } = "Storage\\images";
    public string LogRoot { get; set; } = "logs";
}
```

### 7.3 环境变量注入映射

托盘在启动子进程时注入的环境变量：

| 环境变量 | 来源 | 说明 |
|---------|------|------|
| `DormManage_KESTREL_PORT` | `Tray.ApiPort` 或 `Tray.AdminPort` | Api/Admin 通过此变量绑定端口 |
| `DormManage_DB_CONN` | `Database.ConnectionString`（Provider=SqlServer 时） | 注入 SQL Server 连接串；字段式配置由 `DatabaseConfigDto.BuildConnectionString()` 生成 |
| `DormManage_DB_PATH` | `Database.SqlitePath`（Provider=Sqlite 时） | 注入 SQLite 绝对路径 |
| `DormManage_IMAGE_ROOT` | `Storage.ImageRoot` | 图片存储根路径 |

### 7.4 数据库配置双 UI 同步（v2.13.19）

`AppConfigManager` 作为单例配置中心，负责字段式数据库配置的持久化与广播：

- **本地文件**：`db_setting.json`（AES-256 加密后的密码）
- **数据库表**：`SysParameter`（Category = "Database"）
- **事件广播**：`OnDatabaseConfigUpdated`

Tray 与 Web 的双 UI 同步路径：

```
Tray SettingsForm 保存
    → AppConfigManager.SaveConfigurationAsync(dto)
        → db_setting.json + SysParameter
        → OnDatabaseConfigUpdated
    → TrayAppContext.OnDatabaseConfigUpdated
        → ConfigService.UpdateDatabaseSection(dto)
        → appsettings.json
    → Web /Settings 下次加载时读取最新配置

Web /Settings 保存
    → DbConfigController.SaveConfig(dto)
        → AppConfigManager.SaveConfigurationAsync(dto)
        → db_setting.json + SysParameter
        → OnDatabaseConfigUpdated
    → 兜底：IpcClient 发送 dbconfig.updated 到 Tray
    → TrayAppContext.HandleIpcCommand
        → AppConfigManager.SaveConfigurationAsync / LoadAsync
        → ConfigService.UpdateDatabaseSection
        → appsettings.json
```

---

## 8. 进程管理策略

### 8.1 进程启动

```csharp
// ProcessManager.StartApiAsync 伪代码
var psi = new ProcessStartInfo
{
    FileName = Path.Combine(AppContext.BaseDirectory, _config.Tray.ApiExecutable),
    UseShellExecute = false,        // 必须 false 才能注入环境变量
    CreateNoWindow = false,         // 子进程保留控制台窗口（便于日志）
    WorkingDirectory = Path.GetDirectoryName(_config.Tray.ApiExecutable)!
};
psi.EnvironmentVariables["DormManage_KESTREL_PORT"] = _config.Tray.ApiPort.ToString();
if (_config.Database.Provider == "SqlServer")
    psi.EnvironmentVariables["DormManage_DB_CONN"] = _config.Database.ConnectionString;
else if (_config.Database.Provider == "Sqlite")
    psi.EnvironmentVariables["DormManage_DB_PATH"] = Path.IsPathRooted(_config.Database.SqlitePath)
        ? _config.Database.SqlitePath
        : Path.Combine(AppContext.BaseDirectory, _config.Database.SqlitePath);

var process = Process.Start(psi);
process.EnableRaisingEvents = true;
process.Exited += OnApiExited;
```

### 8.2 优雅停止

```csharp
public async Task StopAsync()
{
    foreach (var p in _processes.Values)
    {
        if (!p.HasExited)
        {
            p.CloseMainWindow();   // 尝试关闭窗口
            if (!p.WaitForExit(5000))
                p.Kill();          // 5s 内未退出则强制结束
        }
    }
}
```

### 8.3 故障自愈

```csharp
private void OnApiExited(object? sender, EventArgs e)
{
    var p = (Process)sender!;
    if (_isStopping) return;  // 用户主动停止，不重启

    _log.Error($"Api 异常退出 code={p.ExitCode}");
    if (_config.Tray.AutoRestartOnCrash)
    {
        Task.Run(async () =>
        {
            await Task.Delay(5000);   // 退避 5s
            await StartApiAsync();
        });
    }
}
```

---

## 9. 健康检查

```csharp
public class HealthChecker
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public async Task<ServiceHealth> CheckApiAsync(int port)
    {
        try
        {
            var resp = await _http.GetAsync($"http://localhost:{port}/swagger/index.html");
            return new ServiceHealth("Api", resp.IsSuccessStatusCode, port);
        }
        catch (Exception ex)
        {
            return new ServiceHealth("Api", false, port, ex.Message);
        }
    }

    public async Task<ServiceHealth> CheckAdminAsync(int port)
    {
        try
        {
            var resp = await _http.GetAsync($"http://localhost:{port}/");
            return new ServiceHealth("Admin", resp.IsSuccessStatusCode, port);
        }
        catch (Exception ex)
        {
            return new ServiceHealth("Admin", false, port, ex.Message);
        }
    }
}
```

---

## 10. UI 设计（SettingsForm）

| 区域 | 控件 | 行为 |
|------|------|------|
| 顶部 header | `Panel (Dock=Top, Height=48, BackColor=#007ACC)` | 蓝色标题"⚙ 系统设置 — 核心服务端参数" |
| 服务端口 | `numApiPort` / `numAdminPort` | 数值输入，范围 1024-65535 |
| 可执行文件 | `txtApiPath` + `btnApiBrowse` / `txtAdminPath` + `btnAdminBrowse` | 文件选择对话框，默认 Api\\DormManage.Api.exe |
| 数据库类型 | `cmbProvider`（SqlServer/Sqlite） | DropDownList |
| 数据库服务器 | `txtDbServer` | Provider=SqlServer 时启用 |
| 端口号 | `numDbPort`（默认 1433） | Provider=SqlServer 时启用 |
| 数据库名称 | `txtDbName` | Provider=SqlServer 时启用 |
| 账号 | `txtDbUser` | Provider=SqlServer 时启用 |
| 密码 | `txtDbPassword`（PasswordChar='*'） | Provider=SqlServer 时启用；留空表示不修改原密码 |
| SQLite 数据库路径 | `txtSqlitePath` + `btnSqliteBrowse` | Provider=Sqlite 时启用，OpenFileDialog |
| 图片路径 | `txtImageRoot` + `btnImageBrowse` | 文件夹选择（FolderBrowserDialog） |
| 自动启动 | `chkAutoStart` / `chkAutoRestart` | 布尔勾选 + 中文说明 |
| 健康检查间隔 | `numHealthInterval` | 5-300 |
| 服务状态 | `lblApiStatus` / `lblAdminStatus`（绿圆/黄三角/红 X） | 实时刷新（1s 定时器） |
| 操作按钮 | `[取消]` `[保存]` `[重启]` `[停止]` `[启动]`（RightToLeft 排列） | 见流程图 |

**布局**：TableLayoutPanel 13 行 × 2 列，统一间距 8px，标题加粗。  
**窗口尺寸**：680×620，MinimumSize 620×560。  
**键盘交互**：ESC = 关闭（不保存）；X 按钮 = 关闭（不保存）。  

### 10.5 异常保护策略（v2.13.4 新增）

**三层兜底架构**：

```
┌─────────────────────────────────────────────────────────────┐
│ 第一层：Font 兜底（SystemFonts 可能为 null）                    │
│   SafeMenuFont() = SystemFonts.MenuFont                       │
│                  ?? SystemFonts.MessageBoxFont                │
│                  ?? new Font("Microsoft YaHei UI", 9f)         │
├─────────────────────────────────────────────────────────────┤
│ 第二层：ShowDialog 必须传 Owner                                 │
│   form.ShowDialog(_ownerForm)   // 严禁无 Owner              │
├─────────────────────────────────────────────────────────────┤
│ 第三层：每个弹窗路径 try-catch + SafeShow 三层兜底              │
│   try { form.ShowDialog(_ownerForm); }                        │
│   catch (Exception ex) {                                       │
│       MessageBox.Show(_ownerForm, ex.Message, ...);           │
│   }                                                            │
└─────────────────────────────────────────────────────────────┘
```

**构造函数异常处理**：SettingsForm 构造函数整体 try-catch，单步失败抛 `InvalidOperationException` 让调用方（TrayAppContext.ShowSettings）接住并友好提示。

**NotifyIconManager 菜单回调**：所有 Click 回调统一 `SafeInvoke` / `SafeInvokeAsync` 包裹，单次失败不影响菜单其他项。

---

## 11. 单实例锁

```csharp
bool createdNew;
_mutex = new Mutex(true, @"Global\DormManage.TrayApp.SingleInstance", out createdNew);
if (!createdNew)
{
    MessageBox.Show("金戈宿舍管理系统托盘已在运行", "提示",
        MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}
```

---

## 12. 日志策略

- 日志目录：`logs/`，文件名 `tray-YYYYMMDD.log`
- 日志格式：`[{timestamp}] [{level}] {message}`，例如 `[2026-07-15 14:30:25.123] [INFO] Api 服务已启动 port=5100`
- 日志级别：INFO / WARN / ERROR
- 日志轮转：单文件 10MB 自动滚动（简化为单日文件，避免依赖 Serilog）
- 关键日志点：服务启停、配置变更、故障自愈、退出

---

## 13. 发布与部署

### 13.1 项目文件配置

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>DormManage.TrayApp</AssemblyName>
    <RootNamespace>DormManage.TrayApp</RootNamespace>
    <ApplicationIcon>Resources\tray-icon.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
  </ItemGroup>
</Project>
```

### 13.2 发布命令

```bash
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj \
  -c Release -r win-x64 --self-contained true \
  -o publish-final/V1.0/TrayApp
```

### 13.3 部署目录结构（最终）

```
publish-final/V1.0/
├── TrayApp/                       # 新增
│   ├── DormManage.TrayApp.exe
│   ├── appsettings.json
│   └── ...
├── Api/                           # 已有
├── Admin/                         # 已有
├── deploy.bat                     # 启动托盘而非直接 start.bat
├── start.bat                      # 保留，作为 start-all-services 的快速通道
├── stop.bat
└── README.md
```

### 13.4 启动入口变更

**deploy.bat** 修改为启动托盘：
```bat
@echo off
cd /d "%~dp0"
start "" "TrayApp\DormManage.TrayApp.exe"
```

**start.bat** 保留，作为无托盘的快速通道（命令行启动 Api/Admin）。

---

## 14. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 端口被占用 | 服务启动失败 | 启动前检测端口占用，提示用户更换 |
| 子进程崩溃循环 | 资源耗尽 | 5 分钟内重启超 3 次则停止自愈，提示用户 |
| appsettings.json 损坏 | 配置丢失 | 启动时校验 JSON，损坏则备份为 .bak 并用默认值 |
| 单实例锁失效（多用户） | 重复启动 | 使用 Global\ 前缀，所有会话共享 |
| 防火墙阻止 HTTP 健康检查 | 健康检查失败误报 | 仅检测 localhost，放行即可 |
| 动态图标 GDI 句柄泄漏 | 托盘图标区域花屏/资源耗尽 | 每次切换调用 `DestroyIcon` 释放旧句柄 |
| IPC 端口 5099 被占用 | Web 无法与 Tray 通信 | `IpcServer.Start` 失败记录日志，不影响托盘主流程 |
| SysParameter 写入失败 | 配置未跨库同步 | 文件写入成功即返回；DB 写入异常记录日志 |
| 密码 "unchanged" 哨兵未处理 | 原密码被覆盖导致连接失败 | `AppConfigManager` 在测试/保存前替换为旧密码 |

---

## 15. 版本演进

| 版本 | 内容 |
|------|------|
| v2.13.2 | DormManage.TrayApp 基础实现：单实例 + 自动启停 + 配置窗口 + 故障自愈 |
| v2.13.3 | 自启动开关（HKCU\Run）+ IPC 服务端（ping/status/start/stop/restart）+ 共用页头 Tab 组件 |
| v2.13.4 | 修复右键 → 系统设置 "UI异常，创建窗口出错" —— OwnerForm 机制 + SettingsForm 重构 + NotifyIconManager 加固 + 异常三层兜底 + 双 UI 职责规范 |
| **v2.13.19（本版本）** | **托盘图标颜色状态机 + SettingsForm 字段式数据库配置 + AppConfigManager 双擎持久化 + IPC 数据库配置同步 + 文档清理** |
| v2.14.0（规划） | 服务端 Web 设置页面接管高级配置（备份恢复、用户角色、筛选缓存），托盘仅保留核心参数 |

---

## 16. 验收物清单

- [x] `DormManage.TrayApp/DormManage.TrayApp.csproj`
- [x] `Program.cs`、`TrayAppContext.cs`、`NotifyIconManager.cs`、`IconGenerator.cs`
- [x] `Models/AppConfig.cs`、`Models/ServiceState.cs`
- [x] `Services/ConfigService.cs`、`ProcessManager.cs`、`HealthChecker.cs`、`LogService.cs`、`AutoStartManager.cs`
- [x] `Forms/SettingsForm.cs`、`AboutForm.cs`
- [x] `Resources/tray-icon.ico`
- [x] `appsettings.json`（默认值）
- [x] 编译 0 错误（Debug / Release）
- [x] 托盘图标颜色状态机验证（红 → 绿 / 黄 / 红）
- [x] SettingsForm 字段式数据库配置保存并写入 `db_setting.json` + `SysParameter`
- [x] Web /Settings 保存后通过 IPC 同步到 Tray
- [x] 冒烟测试通过（双击 EXE → Api/Admin 启动 → 浏览器访问正常）