# DormManage.TrayApp

> **金戈宿舍管理系统托盘守护程序**  
> 版本：v2.13.24  
> 日期：2026-07-19

## 职责

托盘进程是整套宿舍管理系统的部署入口，负责：

| 职责 | 说明 |
|------|------|
| 自动启动 | 启动后自动拉起 `DormManage.Api.exe`（PDA 接口服务）和 `DormManage.Admin.exe`（Web 管理后台） |
| 进程守护 | 监控子进程状态，异常退出时自动重启 |
| 健康检查 | 每 10s 探测 HTTP 端点，更新托盘状态 |
| 配置管理 | 端口、数据库、图片路径可视化调整 |
| 单实例 | 全局互斥锁，防止重复启动 |

## 目录结构

```
DormManage.TrayApp/
├── DormManage.TrayApp.csproj
├── Program.cs                  # 入口：单实例锁 + TrayAppContext
├── TrayAppContext.cs           # ApplicationContext 生命周期
├── NotifyIconManager.cs        # 托盘图标 + 菜单
├── Models/
│   ├── AppConfig.cs
│   └── ServiceState.cs
├── Services/
│   ├── ConfigService.cs
│   ├── ProcessManager.cs
│   ├── HealthChecker.cs
│   └── LogService.cs
├── Forms/
│   ├── SettingsForm.cs
│   └── AboutForm.cs
└── Resources/
    └── tray-icon.ico           # 托盘图标（缺省时回退到 SystemIcons.Shield）
```

## 配置

启动前读取同级目录 `appsettings.json`：

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
    "ConnectionString": "Server=...;Database=...;",
    "SqlitePath": ""
  },
  "Storage": {
    "ImageRoot": "Storage\\images",
    "LogRoot": "logs"
  }
}
```

## 部署结构

```
publish-final/V1.0/
├── TrayApp/                    # 本程序
├── Api/                        # DormManage.Api.exe
├── Admin/                      # DormManage.Admin.exe
├── deploy.bat                  # 启动入口（启动托盘）
├── start.bat                   # 无托盘的快速启动（命令行）
└── stop.bat
```

## 启动

```bash
# 开发调试
dotnet run --project DormManage.TrayApp/DormManage.TrayApp.csproj

# 发布
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj -c Release -r win-x64 --self-contained true -o publish-final/V1.0/TrayApp
```

## 端口与环境变量

托盘通过环境变量向子进程注入配置：

| 环境变量 | 来源 | Api/Admin 读取 |
|---------|------|---------------|
| `DormManage_KESTREL_PORT` | `Tray.ApiPort` / `Tray.AdminPort` | `Program.cs` |
| `DormManage_DB_CONN` | `Database.ConnectionString`（Provider=SqlServer） | `Program.cs` |
| `DormManage_DB_PATH` | `Database.SqlitePath`（Provider=Sqlite） | `Program.cs` |

## 关联文档

- `00-方案文档/56-DormManage.TrayApp技术方案-v2.13.2.md`
- `00-方案文档/57-DormManage.TrayApp需求规格-v2.13.2.md`
- `00-方案文档/01-技术架构与系统开发方案.md` §1.2 / §2.1
- `CLAUDE.md` §9.2 启动流程

## 许可

仅限金戈宿舍管理系统内部使用。

---

## v2.13.109 备注：SQLite Provider 彻底移除

自 v2.13.109 起，DormManage 运行时仅支持 SQL Server。SQLite 代码路径、EF Core SQLite Provider、SQLite 备份恢复逻辑均已移除。历史配置中的 SqlitePath 字段仅为旧配置反序列化兼容，不代表运行时继续支持 SQLite。