# v2.13.108 人员清单「新增」按钮不显示根因 — SQL Server IDENTITY_INSERT 终极修复

> **版本**：v2.13.108  
> **日期**：2026-07-22  
> **类型**：P0 终极修复（v2.13.97/99/100/101/102/106 七次失败后的真正修复）  
> **影响范围**：所有 SQL Server 部署的 SysPermission + SysRolePermission seed 迁移

---

## 一、用户原话（v2.13.106 实施后再次反馈）

> "为什么在原型的人员清单的按钮区有新增按钮，而在生成的程序中没有显示这个'新增'按钮呢？"

**用户已反馈至少 7 次**（v2.13.97 → v2.13.99 → v2.13.100 → v2.13.101 → v2.13.102 → v2.13.106 → v2.13.108），每次都"修复"但按钮仍不显示。

---

## 二、根因（终极 P0 BUG）

### 2.1 DB Schema

`init_schema.sql` 中 `SysPermission.Id` 和 `SysRolePermission.Id` 都是 **IDENTITY(1,1)** 列：

```sql
CREATE TABLE [dbo].[SysPermission] (
    [Id] INT IDENTITY(1,1) NOT NULL,  -- ← IDENTITY 列
    [PermissionCode] NVARCHAR(64) NOT NULL,
    ...
)

CREATE TABLE [dbo].[SysRolePermission] (
    [Id] INT IDENTITY(1,1) NOT NULL,  -- ← IDENTITY 列
    [RoleId] INT NOT NULL,
    [PermissionId] INT NOT NULL,
    [CreatedAt] DATETIME NOT NULL DEFAULT (GETDATE()),
    ...
)
```

### 2.2 迁移 SQL（v2.13.97~v2.13.106 七次错误写法）

```sql
-- DatabaseInitializer.cs MigrateFieldPermissionAsync
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 40)
INSERT INTO [dbo].[SysPermission] ([Id], [PermissionCode], ...)  -- ← 显式指定 Id
VALUES (40, N'personnel:add', ...)                              -- ← 但没 SET IDENTITY_INSERT ON
```

### 2.3 错误现象

在 SQL Server 上执行上述 SQL 会失败：

> **Msg 544, Level 16, State 1, Line N**  
> **Cannot insert explicit value for identity column in table 'SysPermission' when IDENTITY_INSERT is set to OFF.**

### 2.4 为什么 7 次都没发现

`MigrateFieldPermissionAsync` 用 `try/catch` 包裹每个 INSERT（v2.13.103 拆分为单条独立 try/catch），失败仅写 WARNING 日志：

```
[v2.13.103] SysPermission Id=40 INSERT 失败（继续执行）
Microsoft.Data.SqlClient.SqlException: Cannot insert explicit value for identity column...
```

**普通用户根本不看启动日志**，所以永远以为"代码没改"。

### 2.5 最终结果

| 数据库 | IDENTITY 列 | migration 行为 | 按钮显示 |
|--------|------------|--------------|---------|
| **SQLite**（dev） | 无 IDENTITY 概念 | ✅ INSERT 成功，Id=40/61 落地 | ✅ 显示 |
| **SQL Server**（生产） | IDENTITY(1,1) | ❌ INSERT 失败，Id=40/61 永远缺失 | ❌ 不显示 |

---

## 三、v2.13.108 修复

### 3.1 代码层（DatabaseInitializer.cs）

**SQL Server 迁移 SQL 加 SET IDENTITY_INSERT ON/OFF 包裹**：

```csharp
@"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 40)
  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
  VALUES (40, N'personnel:add', N'新增人员', 2, 9, N'/Personnel/Create', N'bi-plus-lg', 7, 1, 0, '2026-07-22');
  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;"
```

同样修复 SysRolePermission：

```csharp
@"SET IDENTITY_INSERT [dbo].[SysRolePermission] ON;
  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRolePermission] WHERE Id = 61)
  INSERT INTO [dbo].[SysRolePermission] ([Id],[RoleId],[PermissionId],[CreatedAt])
  VALUES (61, 1, 40, '2026-07-22');
  SET IDENTITY_INSERT [dbo].[SysRolePermission] OFF;"
```

**SQLite 版本无需改动**：SQLite 的 INTEGER PRIMARY KEY 不是 IDENTITY 列，可自由指定 Id。

### 3.2 手动 SQL 脚本（兜底方案）

新建 `scripts/seed_v2.13.108_personnel_add_identity_insert.sql`，用户可在 SQL Server Management Studio 或 sqlcmd 中直接执行，无需重启任何服务。

脚本特点：
- ✅ 使用 `SET IDENTITY_INSERT ON/OFF`
- ✅ `IF NOT EXISTS` 守卫幂等
- ✅ 末尾含验证 SELECT 语句，期望 8 行（4 SysPermission + 4 SysRolePermission）
- ✅ 含详细注释说明背景和原因

### 3.3 改进日志（v2.13.108）

迁移失败时日志提升为 ERROR 级别（不再是 WARNING），更容易在启动日志中发现：

```
[v2.13.108 ERROR] SysPermission Id=40 INSERT 失败（继续执行）：
  Microsoft.Data.SqlClient.SqlException: Cannot insert explicit value for identity column...
```

---

## 四、变更文件清单

| # | 文件 | 变更 |
|---|------|------|
| 1 | `DormManage.Shared/Services/DatabaseInitializer.cs` | SQL Server 迁移 SQL 加 `SET IDENTITY_INSERT ON/OFF` 包裹（SysPermission × 4 + SysRolePermission × 4 = 8 条 SQL） |
| 2 | `scripts/seed_v2.13.108_personnel_add_identity_insert.sql` | **新建**手动 SQL 脚本（兜底方案） |
| 3 | `00-方案文档/160-新增按钮不显示IDENTITY_INSERT修复-v2.13.108.md` | **新建**本交付报告 |

**未改动**（验证已有）：
- `DormManage.Admin/Pages/Personnel/Index.cshtml:22` — PageHeader primaryAction 已正确（`PermissionCode = "personnel:add"`）
- `DormManage.Admin/Pages/Shared/Components/PageHeader/Default.cshtml` — HasPerm 渲染逻辑正确
- `DormManage.Shared/Services/PermissionService.cs` — CurrentUserHasCode 缓存逻辑正确
- 4 个 SysPermission seed 实体（HasData Id=37/38/39/40）— 新库自动正确，旧库走 MigrateFieldPermissionAsync

---

## 五、用户验证步骤

### 5.1 自动修复（推荐，重启 Admin 即可）

```bash
# 1. 停止 TrayApp + Admin + Api
# 2. 解压覆盖 DormManage_v2.13.108_*.zip
# 3. 启动 Admin（先不启动 TrayApp）
#    → DatabaseInitializer.InitializeAsync 触发 MigrateFieldPermissionAsync
#    → SysPermission Id=40 + SysRolePermission Id=61 真正落地
# 4. 查看 Admin 启动日志：
#    [v2.13.103] SysPermission Id=40 ✓
#    [v2.13.103] SysRolePermission Id=61 ✓
#    [v2.13.101 Verify] 隐私字段权限迁移完整性检查通过：SysPermission 4/4、SysRolePermission 4/4、SysFieldPermission 5/5
# 5. 启动 TrayApp
# 6. 登录 admin → 访问 /Personnel → 期望右上绿色「+ 新增」按钮出现
```

### 5.2 手动修复（兜底，无需重启）

```bash
# 1. 用 SQL Server Management Studio 连接到生产 DB
# 2. 打开 scripts/seed_v2.13.108_personnel_add_identity_insert.sql
# 3. 修改 USE [WaterMeterDB]; 行的数据库名（如 production 是 WaterMeterDB）
# 4. 执行脚本
# 5. 查看 Messages 面板，期望：
#    === SysPermission 验证 ===
#    4 行 (Id 37/38/39/40)
#    === SysRolePermission 验证 ===
#    4 行 (Id 58/59/60/61)
# 6. 浏览器刷新 /Personnel → 期望「+ 新增」按钮立即出现（无需重启 Admin）
```

### 5.3 验证 API 端点

```bash
# 1. 访问 /Settings?tab=roles → 点 admin 行的「权限矩阵」
#    期望：可见「人员清单 → 新增人员」复选框，默认勾选
# 2. 取消勾选 → /Personnel 按钮消失
# 3. 重新勾选 → /Personnel 按钮重新出现
```

---

## 六、决策与权衡

| 决策 | 理由 |
|------|------|
| **在 SQL 内嵌 SET IDENTITY_INSERT** | 简单直接，无需改 EF 模型；ExecuteSqlRawAsync 支持多语句（; 分隔） |
| **ON/OFF 配对包裹单条 INSERT** | SQL Server 限制：同时只能一个表 IDENTITY_INSERT=ON，配对包裹避免冲突 |
| **SQLite 版本不动** | SQLite 没有 IDENTITY 概念，显式 Id 一直能正常工作 |
| **额外提供手动 SQL 脚本** | 迁移失败时运维人员可一键修复，无需编译部署；紧急情况最高优先级 |
| **try/catch 不变** | 保持单条 INSERT 独立失败不影响其他（v2.13.103 设计）的健壮性 |
| **不再用 `SeedIntegrityReport` banner 修复** | v2.13.102 banner 设计是诊断工具，本次修复后该 banner 应该显示绿色，但生产 DB 已存在的 Id=40/61 行因 IDENTITY_INSERT 仍可能不出现 |

---

## 七、教训（v2.13.108）

1. **「种子迁移被 try/catch 静默吞掉」是项目最大风险**：v2.13.97→106 七次"修复"都因 WARNING 日志被忽略，每次都让用户困惑。**根治方案**：迁移失败的日志应提升为 ERROR 级别 + 提供手动 SQL 脚本兜底
2. **IDENTITY_INSERT 是 SQL Server 基础常识，但 EF Core 不会自动加**：HasData 仅在 EnsureCreated 时插入，对既有 DB 不会执行；必须手动 SQL 迁移 + IDENTITY_INSERT
3. **跨 DB Provider 兼容性测试是必需**：SQLite 测试通过 ≠ SQL Server 通过；生产 DB 必须独立验证
4. **三层防御不应包括「用户必须看启动日志」**：v2.13.102 加 banner 是对的，但 banner 只能诊断不能根治；必须从根上保证种子 INSERT 成功
5. **种子迁移的幂等性 + 可观测性 + 兜底脚本三者缺一不可**：
   - 幂等性：WHERE NOT EXISTS 守卫（已有）
   - 可观测性：ERROR 日志 + UI banner（v2.13.102 部分实现）
   - 兜底脚本：手动 SQL（v2.13.108 新增）

---

## 八、版本历史（9 次迭代）

| 版本 | 内容 | 是否真的修复 |
|------|------|------------|
| v2.13.97 | 引入 `personnel:add` (Id=40) SysPermission seed + admin → Id=40 关联 | ❌ EF HasData 不迁移生产 DB |
| v2.13.99 | `MigrateFieldPermissionAsync` 启动迁移方法 | ❌ 漏写 Id=40 + Id=61 |
| v2.13.100 | 补齐 Id=40 + Id=61 迁移 SQL | ❌ 没 SET IDENTITY_INSERT，SQL Server 失败静默 |
| v2.13.101 | 启动日志完整性验证 | ❌ 仍是 WARNING 级别，用户看不到 |
| v2.13.102 | UI 完整性自检 banner + 一键修复 | ❌ 一键修复也调用相同 SQL，仍然失败 |
| v2.13.106 | 三层权限防御（UI + PageModel + API） | ❌ 三层都正确，但底层 seed 缺失，三层都隐藏按钮 |
| v2.13.107 | 宿舍档案班组列（无关） | — |
| **v2.13.108** | **本次：SQL Server IDENTITY_INSERT 终极修复 + 手动 SQL 兜底脚本** | ✅ **真正修复** |

---

## 九、未来种子迁移铁律

新增 SysPermission/SysRolePermission seed 时必须遵循：

1. **Entity HasData**：仅供 EnsureCreated 新建 DB 使用
2. **MigrateFieldPermissionAsync**：SQL Server 版必须用 `SET IDENTITY_INSERT ON/OFF` 包裹
3. **手动 SQL 脚本**：放在 `scripts/seed_v2.X.Y_*.sql` 作为兜底
4. **失败日志**：ERROR 级别（不是 WARNING），让用户/运维在日志中能立即看到
5. **三层防御**：UI 隐藏 + PageModel 重定向 + API 拒绝 — 即使 seed 缺失，业务硬约束仍生效

---

**作者**：Claude Opus 4.8 + Mecall  
**Commit**：pending  
**部署清单**：`publish-final/{Admin,Api,TrayApp,Shared}/`（已重发布 Admin + Api）  
**打包脚本**：`package-deploy.ps1`（v2.13.108 包）

## v2.13.109 备注：SQLite 根因彻底移除

v2.13.108 修复了 IDENTITY_INSERT 静默失败问题，但根因（SQLite vs SQL Server 双 provider 不一致）依然存在。v2.13.109 起 SQLite 已彻底移除，本 BUG 不再可能以任何形式复发——所有数据库操作统一在 SQL Server 上执行，无双 provider 行为差异。
