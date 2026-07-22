# v2.13.109 SQLite 彻底移除专项

> **版本**：v2.13.109  
> **日期**：2026-07-22  
> **类型**：P1 架构精简 + 安全加固（v2.13.108 IDENTITY_INSERT 静默失败的根因治理）

---

## 一、用户原话

> "选择A自动修复；另外，去掉SQLite可以吗？为什么要保留这个数据库方式？"

---

## 二、为什么必须彻底移除 SQLite

| # | 理由 | 说明 |
|---|------|------|
| 1 | **生产从未真正用 SQLite** | v2.13.28 起所有 `appsettings.json` 默认 `Provider=SqlServer`，SQLite 仅是 dev 残留路径 |
| 2 | **v2.13.108 BUG 的直接根因** | `SysPermission/SysRolePermission` 迁移 SQL 在 SQLite 与 SQL Server 行为不一致（IDENTITY 列），`try/catch` 静默吞掉异常 → 用户感知不到 → 按钮永久不显示。**双 provider 是这类 BUG 的温床** |
| 3 | **代码维护负担** | ~600-700 行 SQLite 分支代码需要永久同步双套逻辑 |
| 4 | **EnsureCreated 仅对新库有效** | 生产 DB 是手工 `init_schema.sql` 部署的，`EnsureCreated()` 形同虚设 |
| 5 | **架构复杂度爆炸** | AppConfigManager / AppConfigRuntime / DatabaseConfigDto / DbConfigController / DatabaseConfigFileWatcher 全部要维护双 provider 路径 |
| 6 | **备份/恢复双路径复杂** | SQLite 走 zip 抽取 .db；SQL Server 走 BACKUP DATABASE WITH COMPRESSION |
| 7 | **配置环境变量链路冗余** | `DormManage_DB_PATH` 仅 SQLite 使用，下线后托盘环境变量链路简化 |

---

## 三、用户已确认决策

| # | 决策 | 选择 |
|---|------|------|
| 1 | `checkdb.csproj`（孤立 9 行 SQLite 工具项目） | ✅ **删除整个 csproj** |
| 2 | SQLite → SQL Server 数据迁移工具 | ✅ **不需要**（生产 v2.13.28 起已 SqlServer，dorm.db 仅开发残留） |
| 3 | 历史 `db_setting.json` `Provider=Sqlite` 防御策略 | ✅ **硬拒绝 + 启动失败**（明确错误，不静默降级） |

---

## 四、变更范围

### 4.1 源代码（13 文件）

| # | 文件 | 关键改动 |
|---|------|---------|
| 1 | `DormManage.Shared/Models/DatabaseConfig.cs` | `SqlitePath` 加 `[Obsolete]` 保留兼容 |
| 2 | `DormManage.Shared/Services/AppConfigRuntime.cs` | 删除 `BuildFallback().SqlitePath` |
| 3 | `DormManage.Shared/Services/AppConfigManager.cs` | 删除 `using Microsoft.Data.Sqlite` + `SqliteConnectionStringBuilder`；`TestDbConnectionAsync` 硬拒绝 Sqlite；保存 DTO 不再传播 SqlitePath |
| 4 | `DormManage.Shared/Services/DatabaseInitializer.cs` | 删除 EnsureCreated + sqlite_master + 4 段双 SQL（约 250 行）；保留 SQL Server 单 provider 语法 |
| 5 | `DormManage.Shared/Services/DatabaseHealthService.cs` | 删除 sqlite_master 分支，参数化查询 `INFORMATION_SCHEMA.TABLES` |
| 6 | `DormManage.Api/Program.cs` | 删除 UseSqlite 分支 + EnsureCreated；硬拒绝非 SqlServer |
| 7 | `DormManage.Api/Controllers/System/BackupController.cs` | 删除 SQLite 备份/恢复整段路径；新增 SQL Server 临时 `.bak` + 临时目录 `finally` 清理 + zip 仅含当前 .bak + SQL 标识符转义 |
| 8 | `DormManage.Api/Controllers/System/DbConfigController.cs` | SaveConfig / TestConnection 硬拒绝非 SqlServer；IPC payload 移除 sqlitePath |
| 9 | `DormManage.Admin/Program.cs` | 与 Api 对齐 |
| 10 | `DormManage.Admin/Pages/Settings/Index.cshtml` | 删除 SQLite option + .sqlite-only div；JS payload 强制 provider='SqlServer' |
| 11 | `DormManage.TrayApp/Services/ConfigService.cs` | `UpdateDatabaseSection` 简化为 SQL Server 单路径；硬拒绝非 SqlServer |
| 12 | `DormManage.TrayApp/Services/ProcessManager.cs` | 删除 `DormManage_DB_PATH` 环境变量注入分支 |
| 13 | `DormManage.TrayApp/Forms/SettingsForm.cs` | 删除 `_txtSqlitePath` / `_btnSqliteBrowse` / `BrowseSqliteFile()`；ComboBox 仅 SqlServer；保存逻辑简化 |
| 14 | `DormManage.TrayApp/Models/AppConfig.cs` | `DatabaseSection.SqlitePath` 加 `[Obsolete]` 保留兼容 |

### 4.2 NuGet 包 + 工具清理

| # | 文件 | 改动 |
|---|------|------|
| 15 | `DormManage.Shared/DormManage.Shared.csproj` | 删除 `Microsoft.EntityFrameworkCore.Sqlite 8.0.10` + `Microsoft.Data.Sqlite 8.0.0` |
| 16 | `DormManage.Admin/DormManage.Admin.csproj` | 删除同上 2 个 PackageReference |
| 17 | `checkdb.csproj` | **删除整个 csproj 文件**（孤立、无源码） |

### 4.3 SQL 脚本

| # | 文件 | 改动 |
|---|------|------|
| 18 | `scripts/seed_v2.13.103_personnel_add.sql` | **重写**：移除 `PRAGMA` / `INSERT OR IGNORE`；改用 SQL Server `IF NOT EXISTS` + `SET IDENTITY_INSERT ON/OFF` + 事务包裹 |

### 4.4 历史清理

| # | 文件 | 改动 |
|---|------|------|
| 19 | 根目录 `dorm.db` (491,520 bytes) | **删除文件** |
| 20 | `.gitignore` | SQLite 规则保留并加注释说明 |

---

## 五、Provider 硬拒绝实现（关键安全点）

### 5.1 Api/Admin 启动校验

```csharp
if (!string.Equals(cfg.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Database provider must be SqlServer (got: '{cfg.Provider}'). " +
        "Current version no longer supports SQLite. " +
        "Please update db_setting.json to set provider=SqlServer.");
}
```

### 5.2 DbConfigController SaveConfig 校验

```csharp
if (!string.Equals(config.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    return ApiResponse.Fail("UNSUPPORTED_PROVIDER", "当前版本仅支持 SQL Server，请设置 provider=SqlServer");
```

### 5.3 TrayApp ConfigService.UpdateDatabaseSection 校验

```csharp
if (!string.Equals(dto.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("当前版本仅支持 SQL Server（SQLite 已于 v2.13.109 移除）");
```

---

## 六、`SqlitePath` 字段保留规范（向后兼容）

`DatabaseConfigDto.SqlitePath` 与 `DatabaseSection.SqlitePath` 均加 `[Obsolete]`：

```csharp
[Obsolete("SQLite provider has been removed; retained for legacy configuration deserialization.")]
[JsonPropertyName("sqlitePath")]
public string? SqlitePath { get; set; }
```

**保留理由**：
- 旧 `db_setting.json` / `appsettings.json` 含 `sqlitePath` 字段可正常反序列化（避免启动失败）
- 旧 IPC payload 兼容
- 渐进式升级（一个完整版本周期后再考虑删除）

---

## 七、变更后 grep 守卫

排除 `obj/`、`bin/`、`publish-final/`、`*.deps.json`、`*.assets.json`、`*.cache`、`dorm.db` 后，源码中 `Sqlite` 只允许：

- ✅ `[Obsolete]` 字段（`SqlitePath` 兼容）
- ✅ DTO 兼容字段（`DbConfigController` IPC payload）
- ✅ 历史 BUG 报告（事实保留 + 「v2.13.109 移除」声明）
- ❌ **其他位置全部应清零**

---

## 八、端到端验证

| 模块 | 测试 | 期望 |
|------|------|------|
| 编译 | `dotnet build DormManage.sln -c Release` | 0 error |
| Admin 启动 | `DormManage.Admin.exe` | 日志含 `[Startup] ✓ 数据库连接成功（Provider: SqlServer）` + IDENTITY_INSERT 验证通过 |
| Api 启动 | `DormManage.Api.exe` | Swagger http://localhost:5100/swagger/index.html HTTP 200 |
| 托盘 UI | 右键 → 数据库设置 | Provider 下拉仅 SqlServer（禁用） |
| 硬拒绝测试 | 改 `db_setting.json` 为 `provider=Sqlite` 启动 | 启动失败 + 明确错误信息 |
| 备份 | `POST /api/v1/system/backup/create` | 返回 .zip（仅含 .bak 文件） |
| 恢复 | `POST /api/v1/system/backup/restore` | SQL Server `SINGLE_USER` + `RESTORE` + `MULTI_USER`，临时目录 finally 清理 |
| 人员清单「新增」按钮 | `/Personnel` | 按钮显示（v2.13.108 修复确认） |

---

## 九、相关历史 BUG 报告

本版本彻底移除 SQLite 后，以下历史 BUG 不再可能复发：

1. **v2.13.108** IDENTITY_INSERT 静默失败（已修复 + 移除根因）
2. **v2.13.28** 数据库连接传播断裂（Provider 默认值修复已合并到单 provider）
3. **v2.13.6/7/8** EF 实体 SQLite vs SQL Server 不一致（双 provider 架构 BUG）
4. **v2.13.4** 托盘右键异常（Provider 切换相关）

---

**作者**：Claude Opus 4.8 + Mecall  
**Commit**：pending  
**部署清单**：`publish-final/{Admin,Api,TrayApp,Shared}/`（重新发布）  
**打包脚本**：`package-deploy.ps1`（v2.13.109 包）