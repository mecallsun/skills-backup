# v2.13.181 — admin 角色启动期自动补齐权限

**日期**：2026-07-27
**性质**：基础需求 + BUG 修复
**核心变更**：所有发布版本启动期必须自动授予 admin 角色全部权限

---

## 一、需求

用户原话：
> 当前登记管理员账号出现权限不足；请默认发布版本都需初始化系统管理员角色为全部权限的表数据；这是所有系统开发方案中的基本需求

---

## 二、问题根因

任何可能途径（升级 Bug / 手动误操作 / 旧版 DB 部署 / 错误测试）导致：
- **admin 角色（RoleId=1）缺失 SysRolePermission 记录** → admin 用户登录后没权限
- **admin 用户（UserId=1）缺失 SysUserRole 关联** → admin 角色绑定不到用户

**两层级缺陷**：
1. `SysRole → SysRolePermission → SysPermission`（角色无权限）
2. `SysUser → SysUserRole → SysRole`（用户无角色）

---

## 三、核心改动

### 3.1 v2.13.181 — admin 角色补齐所有 Active 权限

**文件**：`DormManage.Shared/Services/DatabaseInitializer.cs`

```csharp
public static async Task<bool> GrantAdminAllPermissionsAsync(
    DormDbContext db, ILogger logger, CancellationToken ct)
{
    const int adminRoleId = 1;
    
    // 1. 找出所有 admin 缺失的 Active 权限
    var missing = new List<(int Id, string Code, string Name)>();
    using (var cmd = new SqlCommand(@"
        SELECT p.Id, p.PermissionCode, p.PermissionName
        FROM [SysPermission] p
        WHERE p.IsActive = 1
          AND p.Id NOT IN (
              SELECT PermissionId FROM [SysRolePermission] WHERE RoleId = @adminRoleId
          )", conn))
    {
        cmd.Parameters.AddWithValue("@adminRoleId", adminRoleId);
        using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync())
            missing.Add((rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2)));
    }
    
    // 2. 逐条 INSERT 缺失权限（IF NOT EXISTS 幂等）
    foreach (var (id, code, name) in missing)
    {
        const string insertSql = @"
            IF NOT EXISTS (SELECT 1 FROM [SysRolePermission] WHERE RoleId = @rid AND PermissionId = @pid)
                INSERT INTO [SysRolePermission] (RoleId, PermissionId, CreatedAt) VALUES (@rid, @pid, GETDATE())";
        using var ic = new SqlCommand(insertSql, conn);
        ic.Parameters.AddWithValue("@rid", adminRoleId);
        ic.Parameters.AddWithValue("@pid", id);
        int n = await ic.ExecuteNonQueryAsync(ct);
    }
}
```

**关键修复**：`var conn = (SqlConnection)db.Database.GetDbConnection();` — **不要用 `using`** 关闭 EF Core 共享连接！否则后续 migration 全部失败。

### 3.2 v2.13.182 — admin 用户关联 admin 角色

**文件**：`DormManage.Shared/Services/DatabaseInitializer.cs`

```csharp
public static async Task<bool> EnsureAdminUserRoleAsync(
    DormDbContext db, ILogger logger, CancellationToken ct)
{
    var conn = (SqlConnection)db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
    
    // 1. 找 admin 用户 Id
    int adminUserId;
    using (var cmd = new SqlCommand("SELECT UserId FROM [SysUser] WHERE UserName = 'admin'", conn))
    {
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result == null) return true; // admin 用户不存在
        adminUserId = Convert.ToInt32(result);
    }
    
    // 2. 检查是否已有 admin 角色关联
    using (var checkCmd = new SqlCommand(
        "SELECT COUNT(*) FROM [SysUserRole] WHERE UserId = @uid AND RoleId = 1", conn))
    {
        checkCmd.Parameters.AddWithValue("@uid", adminUserId);
        int existing = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct));
        if (existing > 0) return true; // 已有关联
    }
    
    // 3. INSERT 关联（注意 SysUserRole 表无 CreatedAt 列）
    using (var insertCmd = new SqlCommand(
        "INSERT INTO [SysUserRole] (UserId, RoleId) VALUES (@uid, 1)", conn))
    {
        insertCmd.Parameters.AddWithValue("@uid", adminUserId);
        await insertCmd.ExecuteNonQueryAsync(ct);
    }
}
```

**关键修复**：v2.13.181 必须在 v2.13.182 之后执行（先有权限再有角色关联才有效）。

### 3.3 StartupReport 新增字段

```csharp
public bool AdminAllPermsGranted { get; set; }     // v2.13.181
public bool AdminUserRoleEnsured { get; set; }     // v2.13.182
```

---

## 四、部署验证

### 4.1 重置 admin 状态模拟异常

```sql
DELETE FROM SysUserRole WHERE UserId = 1;
DELETE FROM SysRolePermission WHERE RoleId = 1;
```

### 4.2 启动 Admin（生产模式）

启动日志输出（实测）：

```
[v2.13.182] admin 用户 (UserId=1) 已自动关联 admin 角色（影响行数=1）
[v2.13.181] admin 角色缺失 36 项权限，开始自动补齐
[v2.13.181] admin 角色自动补齐完成：尝试 36 项 / 实际授权 36 项
```

### 4.3 验证结果

| 指标 | 修复前 | 修复后 |
|------|-------|-------|
| admin (UserId=1, RoleId=1) 关联 | 0 | **1** ✅ |
| admin (RoleId=1) 权限数 | 0 | **50** ✅ |
| SysPermission active | 50 | 50 |

---

## 五、关键 BUG 修复

### 5.1 BUG A：`SysUserRole` 表无 `CreatedAt` 列

`SysUserRole` 表的 schema：
```sql
CREATE TABLE SysUserRole (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    CONSTRAINT PK_SysUserRole PRIMARY KEY (UserId, RoleId),
    ...
);
```

**修复**：INSERT SQL 不带 `CreatedAt`：
```sql
INSERT INTO [SysUserRole] (UserId, RoleId) VALUES (@uid, 1)
```

### 5.2 BUG B：`using (var conn = ...)` 关闭 EF Core 共享连接

`db.Database.GetDbConnection()` 返回的是 EF Core 池化的连接。`using` 块结束时**会关闭连接**——这破坏 EF Core 后续操作的连接状态。

**修复**：不用 `using`，手动控制连接状态：
```csharp
var conn = (SqlConnection)db.Database.GetDbConnection();
if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
```

### 5.3 BUG C：v2.13.181 必须在 v2.13.182 之前

v2.13.181 给 admin 角色授权所有权限；v2.13.182 给 admin 用户关联 admin 角色。**逻辑顺序**：先有权限 → 再有角色关联。

**修复**：在 `InitializeAsync` 中调整迁移调用顺序：
```csharp
// 第 4.5 步：v2.13.182 — admin 用户关联 admin 角色（先做）
report.AdminUserRoleEnsured = await EnsureAdminUserRoleAsync(db, logger, ct);

// 第 4.6 步：v2.13.181 — admin 角色补齐所有权限（后做）
report.AdminAllPermsGranted = await GrantAdminAllPermissionsAsync(db, logger, ct);
```

---

## 六、改动文件清单

| 文件 | 改动 |
|------|------|
| `DormManage.Shared/Services/DatabaseInitializer.cs` | 新增 `GrantAdminAllPermissionsAsync`（v2.13.181）+ `EnsureAdminUserRoleAsync`（v2.13.182）+ StartupReport 字段 + InitializeAsync 调用顺序调整 |

---

## 七、永久教训

1. **基本需求（admin 全权）不能依赖历史数据** — 启动期必须自动修复（不依赖用户手动操作）
2. **`db.Database.GetDbConnection()` 不能用 `using`** — EF Core 池化连接的生命周期由框架管理
3. **`SysUserRole` 是复合主键表** — 字段只有 UserId + RoleId，**没有 CreatedAt**（v2.13.180 SysRolePermission 才需要 CreatedAt）
4. **EF Core EF Core 主键 / 复合主键差异** — SysUserRole 没有 Id 列，INSERT 必须用 IF NOT EXISTS 守卫
5. **迁移顺序很重要** — v2.13.181 (权限) 必须在 v2.13.182 (角色关联) 之前执行

---

## 八、最新程序包

`E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\release\_archive\DormManage-v2.13.181_20260727_002651.zip`（200.25 MB）

部署后启动 TrayApp → 自动执行两次迁移 → admin 用户/角色/权限自动补齐 → 登录 admin 即可全功能使用。