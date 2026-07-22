# v2.13.100 personnel:add 权限种子补齐 P0 修复

> **版本**：v2.13.100  
> **日期**：2026-07-22  
> **类型**：P0 BUG 修复（v2.13.99 漏 seed 后续补丁）  
> **影响范围**：人员清单 PageHeader 新增按钮

---

## 一、问题

**用户反馈**：
> 在权限矩阵中，人员清单的"新建"按钮为什么勾选的权限控制项没有生效？

**期望行为**：
- `/Settings?tab=roles` 角色权限矩阵中，应可见「人员清单 → 新增人员」复选框
- admin 默认应自动勾选（HasPermission 返回 true）
- 取消勾选后，PageHeader 上的「新增」按钮应隐藏

**实际行为（修复前）**：
- 权限矩阵中**无**「新增人员」复选框
- 即使手动调整也无法生效
- PageHeader 「新增」按钮的 `PermissionCode="personnel:add"` 永远查不到 → 始终不渲染（但因代码兼容空权限码 = 显示，实际显示了——但权限控制形同虚设）

---

## 二、根因

**v2.13.99 `DatabaseInitializer.MigrateFieldPermissionAsync` 方法只补了 v2.13.92 的 3 个权限码（Id 37/38/39）和 3 条 admin 关联（Id 58/59/60），漏写了 v2.13.97 引入的：**

| 项 | 缺失内容 | 影响 |
|----|---------|------|
| `SysPermission Id=40` | `personnel:add`（新增人员）| 权限矩阵无该复选框 |
| `SysRolePermission Id=61` | admin → Id=40 关联 | 即便矩阵有也关联不上 |

**触发条件**：v2.13.97 引入 Id=40 / Id=61 时未走「启动迁移」路径（因 v2.13.99 之前项目无此机制），靠 EF EnsureCreated 期望新部署生效，但 SQLite 生产 DB 早已存在 → 种子从未落地。

---

## 三、修复（v2.13.100）

### 3.1 `DatabaseInitializer.MigrateFieldPermissionAsync` 扩展

扩展 `permSql` 追加 Id=40 seed，扩展 `rpSql` 追加 Id=61 seed（SQLite + SQL Server 双 provider 同步）。

```sql
-- SQLite (新增)
INSERT INTO SysPermission (Id, PermissionCode, PermissionName, PermissionType, ParentId, Route, Icon, SortOrder, IsActive, IsSystem, CreatedAt)
SELECT 40, 'personnel:add', '新增人员', 2, 9, '/Personnel/Create', 'bi-plus-lg', 7, 1, 0, '2026-07-22 00:00:00'
WHERE NOT EXISTS (SELECT 1 FROM SysPermission WHERE Id=40);

INSERT INTO SysRolePermission (Id, RoleId, PermissionId, CreatedAt)
SELECT 61, 1, 40, '2026-07-22 00:00:00'
WHERE NOT EXISTS (SELECT 1 FROM SysRolePermission WHERE Id=61);
```

SQL Server 等效 SQL 同步追加（带 `IF NOT EXISTS` 守卫）。

### 3.2 `init_schema.sql` 同步追加

末尾追加 2 段 DDL：
- 38. SysPermission Id=40 (`personnel:add`)
- 39. SysRolePermission Id=61 (admin → 40)

确保 SQL Server 真相源与运行时迁移双保险。

---

## 四、变更文件清单

| # | 文件 | 变更 |
|---|------|------|
| 1 | `DormManage.Shared/Services/DatabaseInitializer.cs` | `MigrateFieldPermissionAsync.permSql` 追加 Id=40 seed + `rpSql` 追加 Id=61 seed（SQLite + SQL Server 双分支） |
| 2 | `init_schema.sql` | 末尾追加 v2.13.97 seed 段（Id=40 SysPermission + Id=61 SysRolePermission） |

---

## 五、端到端验证

### 5.1 启动日志（首次部署）

```
[v2.13.100 Migrate] 隐私字段权限迁移完成（SysFieldPermission 表 + 5 字段种子 + 4 权限码 + 4 角色关联，含 v2.13.97 personnel:add 修复）
```

### 5.2 DB 验证

```sql
-- SysPermission 应有 Id=40 行
SELECT * FROM SysPermission WHERE Id = 40;
-- 期望：PermissionCode='personnel:add', PermissionName='新增人员', PermissionType=2, ParentId=9

-- SysRolePermission 应有 Id=61 行
SELECT * FROM SysRolePermission WHERE Id = 61;
-- 期望：RoleId=1, PermissionId=40
```

### 5.3 UI 验证

1. 访问 `/Settings?tab=roles`，编辑 admin 角色
2. 展开「人员清单 (Personnel)」分组
3. **期望**：可见「新增人员 (personnel:add)」复选框
4. 默认应自动勾选
5. 取消勾选 → 保存
6. 访问 `/Personnel`
7. **期望**：PageHeader 「新增」按钮消失

---

## 六、连锁影响

| 模块 | 变化 |
|------|------|
| `PermissionService.GetUserPermissionCodesAsync(admin)` | 之前返回的 codes 集合中无 `personnel:add`；现在包含 |
| `Html.HasPermissionAsync("personnel:add")` (admin) | 之前 false → 现在 true |
| `_RolePanel.cshtml` 角色矩阵渲染 | 之前因 `Model.Permissions` 无 Id=40 而无复选框；现在自动归入 personnel 分组 |
| `Personnel/Index.cshtml` PageHeader primaryAction | 之前按钮渲染逻辑走空权限码分支（永远显示）；现在按真实权限控制 |

---

## 七、教训（v2.13.100）

1. **启动迁移必须覆盖「全部历史缺失」** 而非「最近一次提交」：v2.13.99 MigrateFieldPermissionAsync 只补 v2.13.92 seed 是局部视角；正确做法是审计所有 `DormDbContext.cs` HasData 与现有 DB 状态的 diff，补齐所有缺失项。
2. **「一次性」修复要覆盖全字段历史**：v2.13.97 (personnel:add) 和 v2.13.92 (privacy:field:enable) 都是同一类问题（EF HasData 不迁移）。后续审计应按时间线扫描每一版的 HasData 增量。
3. **CriticalTables 是基础白名单，新增 DbSet 后必须同步**：但即使 CriticalTables 包含 SysPermission，仍需主动检测缺失 seed 行数并补齐。
4. **运行时迁移 vs 真相源必须同步**：init_schema.sql 与 DatabaseInitializer 任何一项漏写都会导致不一致。

---

## 八、后续改进建议（不在本次范围）

1. **通用种子审计脚本**：编写一次性脚本扫描 `DormDbContext.HasData` 与 DB 实际数据，输出 diff 列表。
2. **改用 EF Core Migrations**：长期方案，启用 `dotnet ef migrations` 彻底避免此类问题。
3. **CriticalTables 检测升级**：除「表是否存在」外，加「表行数 ≥ 期望最小值」判断，缺失 seed 行数自动报警。

---

## 九、版本历史

| 版本 | 内容 |
|------|------|
| v2.13.97 | 班组列 + 新增人员权限（引入 Id=40）|
| v2.13.99 | 默认页大小 + 隐私字段权限（引入 MigrateFieldPermissionAsync 但漏写 Id=40）|
| **v2.13.100** | **本次：personnel:add (Id=40) + admin 关联 (Id=61) seed 补齐**|

---

**作者**：Claude Opus 4.8 + Mecall  
**Commit**：pending  
**部署清单**：`publish-final/{Admin,Api,TrayApp,Shared}/`