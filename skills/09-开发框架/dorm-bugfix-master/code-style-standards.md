# 代码规范标准（DormManage v2.13.193+）

> Skill: dorm-bugfix-master
> 创建日期：2026-07-28
> 基于所有已知 BUG 修复经验整理

---

## 概述

本文档定义 DormManage 项目的代码规范，**目的是防止引入新的 BUG**。
所有规范都来源于实际 BUG 案例的教训。

---

## 1. 隐私字段相关规范

### 1.1 必须有 `IsFieldHiddenAsync()` 调用

**问题**（案例 1）：Dorms/Index.cshtml 缺隐私字段接线，导致授权无效。

**规范**：
- 任何 `SysFieldPermission` 表新增字段，**必须 24h 内**完成所有引用页面的 `IsFieldHiddenAsync()` 接线
- 表头、单元格、详情页都要检查
- 必须用 `text-truncate` + `title` + `IsFieldHiddenAsync` 三件套

**强制模板**：
```html
<!-- 表头 -->
<th class="@(await Html.IsFieldHiddenAsync("module.fieldkey") ? "d-none" : "")">字段名</th>

<!-- 单元格（隐藏时显示 ***） -->
<td class="@(await Html.IsFieldHiddenAsync("module.fieldkey") ? "d-none" : "")">
    @(await Html.IsFieldHiddenAsync("module.fieldkey") ? "***" : item.FieldName)
</td>
```

### 1.2 必须跨权限测试

**问题**（案例 5）：admin 测试通过但普通用户仍能看到。

**规范**：
- 任何隐私/权限修改必须测试 **2 个角色**：admin + 未授权角色
- 仅 admin 测试 = 100% 漏测

**测试命令**：
```bash
# 1. Login as admin
curl -c cookies.txt http://localhost:5001/Account/Login -d "UserName=admin&Password=admin123"

# 2. Get private page  
curl -b cookies.txt http://localhost:5001/Dorms

# 3. Check for d-none class
grep -c "d-none" dorms.html  # Should be 0 (admin sees all)

# 4. Login as test (unauthorized)
curl -c cookies.txt http://localhost:5001/Account/Login -d "UserName=test&Password=123456"

# 5. Check privacy fields are hidden
grep -c "d-none" dorms.html  # Should be > 0
```

---

## 2. 账号有效期相关规范

### 2.1 必须用统一助手函数

**问题**（案例 6）：3 处独立 ExpiresAt 实现导致不一致。

**规范**：
- 任何 ExpiresAt 逻辑必须通过 `UserExpiryHelper.IsExpired()` / `DaysUntilExpiry()`
- 禁止直接写 `DateTime.Today > expiresAt.Value.Date` 等内联代码

**位置**：`DormManage.Shared/Extensions/UserExpiryHelper.cs`

### 2.2 临界日期判断用 `>=`

**问题**：账号**过期当天仍能登录**（`>` 严格大于）

**规范**：
- ✅ 业务语义：账号有效期到 `ExpiresAt.Date`，则该日结束前有效
- ✅ 代码语义：`DateTime.Today >= expiresAt.Value.Date`
- ❌ 反例：`DateTime.Today > expiresAt.Value.Date`（过期当天还能登录）

### 2.3 日期型 vs datetime-local

**问题**（案例 7）：用户只关心日期，datetime-local 强制要求时间。

**规范**：
- 业务场景只关心日期 → 用 `<input type="date">`
- 业务场景关心具体时刻 → 用 `<input type="datetime-local">`
- 后端解析：`DateTime.TryParse(ExpiresAt, out var parsed)` + `parsed.Date.AddDays(1).AddSeconds(-1)` 存为当天结束

---

## 3. 发布相关规范

### 3.1 必须使用 sync 脚本

**问题**（案例 4）：发布只更新 `release/latest/Admin/`，TrayApp 加载 `release/latest/TrayApp/Admin/`。

**规范**：
- ❌ 禁止单独 `dotnet publish -o release/latest/Admin/`
- ✅ 必须用 `scripts/sync_publish_to_trayapp.sh`

**强制流程**：
```bash
# 步骤 1：构建
dotnet build DormManage.sln -c Release

# 步骤 2：发布到所有目录
dotnet publish ... -o release/latest/Admin
dotnet publish ... -o release/latest/Api
dotnet publish ... -o release/latest/TrayApp

# 步骤 3：同步（强制）
bash scripts/sync_publish_to_trayapp.sh --skip-build

# 步骤 4：验证
stat -c '%y' release/latest/TrayApp/Admin/DormManage.Admin.dll
# 应该是刚刚发布的时间
```

### 3.2 Shared DLL 必须全子目录同步

**问题**（案例 11）：Shared DLL 不同步导致 `TypeLoadException`。

**规范**：
- 每次发布必须 cp Shared DLL 到 4 处：
  - `release/latest/Admin/DormManage.Shared.dll`
  - `release/latest/Api/DormManage.Shared.dll`
  - `release/latest/TrayApp/Admin/DormManage.Shared.dll`
  - `release/latest/TrayApp/Api/DormManage.Shared.dll`

### 3.3 .cshtml Views 必须同步

**规范**：
- 每次发布必须 `cp -rf release/latest/Admin/Pages release/latest/TrayApp/Admin/`
- 因为 TrayApp 通过 Razor RuntimeCompilation 加载 .cshtml

### 3.4 发布前必做 7 项验证

参见 `scripts/publish_checklist.md`。

---

## 4. Razor 视图规范

### 4.1 字符串拼接避免 `@xxx` 误识别

**问题**（案例 9）：`@bg-primary` 被识别为变量。

**规范**：
- ❌ 避免 `"@(condition ? "bg-primary" : "bg-secondary")"`
- ✅ 使用 `if/else` Razor 块：
```csharp
@if (condition)
{
    <span class="badge bg-primary">...</span>
}
else
{
    <span class="badge bg-secondary">...</span>
}
```

### 4.2 隐私字段接线三件套

**规范**：
```html
<!-- 表头 + 单元格 + title 提示 -->
<th class="text-truncate @(await Html.IsFieldHiddenAsync("module.field") ? "d-none" : "")">字段</th>
<td class="text-truncate" title="@item.Field">
    @(await Html.IsFieldHiddenAsync("module.field") ? "***" : item.Field)
</td>
```

### 4.3 操作按钮统一文字化

**规范**（v2.13.189+）：
- ❌ 禁止 `<button class="btn btn-sm btn-outline-primary">编辑</button>`
- ✅ 使用 `<button class="op-btn">编辑</button>`
- ✅ 按钮间用 `<span class="op-btn-sep"> </span>` 分隔
- ✅ 单元格加 `class="text-center op-cell"`

### 4.4 列表字段溢出处理

**规范**：
- 长字段必须 `text-truncate` + `title`
- 表格加 `table-layout: fixed; width: 100%;` 让列宽固定

---

## 5. 服务端规范

### 5.1 业务逻辑统一助手函数

**问题**：3 处 ExpiresAt 实现不一致。

**规范**：
- 同一业务规则必须只有 1 个助手函数
- 助手函数必须有完整 XML doc
- 助手函数必须可单元测试

**示例**：
```csharp
// 创建助手函数
public static class UserExpiryHelper
{
    public static bool IsExpired(DateTime? expiresAt) { ... }
    public static int? DaysUntilExpiry(DateTime? expiresAt) { ... }
}

// 3 处调用
if (UserExpiryHelper.IsExpired(user.ExpiresAt)) ...
if (UserExpiryHelper.IsExpired(user.ExpiresAt)) ...
var days = UserExpiryHelper.DaysUntilExpiry(u.ExpiresAt);
```

### 5.2 业务逻辑必须 deny-by-default

**问题**：v2.13.176 翻转前是 allow-by-default。

**规范**：
- 任何"可见/不可见"判断：**deny-by-default**
- 默认隐藏，**显式授权**才显示
- 隐私、付费、敏感数据等都是 deny-by-default 场景

### 5.3 密码 BCrypt 处理

**问题**：HTTP 200 + success:true 不一定真生效。

**规范**：
- 密码修改后必须**跨连接查询验证**
- 不能只信 API 返回值
- 用独立 .NET 程序（`tmp/DiagPassword/`）验证 hash

### 5.4 EF Core 异步调用

**规范**：
- ✅ 所有 DB 调用必须 `async` + `await`
- ✅ 不能 `Wait()` 或 `.Result`
- ❌ 禁止阻塞 EF 异步流

---

## 6. 命名规范

### 6.1 方法名必须反映语义

**问题**：`HasPrivacyFieldEnabledAsync` 语义模糊（实际是 allow-by-default）。

**规范**：
- ✅ `AllowDisplayPrivacyFieldsAsync`（明确表达：勾选 = 允许显示）
- ✅ `IsExpired`、`DaysUntilExpiry`（明确动作）
- ❌ `HasEnabled`（模糊不清）

### 6.2 变量命名

**规范**：
- 布尔：`isExpired`（前缀 `is`/`has`/`can`/`should`）
- 时间：`expiresAt`（明确是日期）
- 字段：`isActive`（明确含义）

---

## 7. 注释规范

### 7.1 关键 BUG 修复必须加注释

**规范**：
```csharp
// v2.13.193 修正：使用 >= 语义，**过期当天即视为已过期**（更严格安全策略）
// 旧代码：if (Today > ExpiresAt) ← BUG
if (UserExpiryHelper.IsExpired(expiresAt)) ...
```

### 7.2 助手函数必须有完整 XML doc

```csharp
/// <summary>
/// v2.13.193 账号有效期判断助手
/// 统一 LoginAsync + OnValidatePrincipal + 前端 Badge 显示的判断逻辑
/// </summary>
public static class UserExpiryHelper { ... }
```

---

## 8. 测试规范

### 8.1 跨权限测试

- 任何隐私/权限修改必须测试 **admin + 未授权角色**
- 不能只测 admin

### 8.2 真实数据验证

- 密码修改后 BCrypt 验证 hash 真的变了
- ExpiresAt 修改后看 DB 真的变了
- 不能只信 API 返回 success

### 8.3 跨进程测试

- TrayApp 启动后访问 5001 端口
- 不是只看 DLL 时间戳

---

## 9. Git 规范

### 9.1 文档和代码必须同步 commit

**问题**：v2.13.176 文档改了但代码没改（17+ 版本 BUG）。

**规范**：
- 任何"v2.13.x 翻转"必须同 commit 改文档+代码
- commit message 必须写明"`flip + code + doc`"

### 9.2 Edit 后立即 `git diff`

**规范**：
- Edit 操作完成后立即 `git diff` 确认修改已保存
- 防止 git checkout/stash 静默回滚

### 9.3 untracked 文件必须立即处理

**问题**：v2.13.169 untracked 文件 `LicenseStatusController.cs` 引发编译错误。

**规范**：
- 任何 untracked 文件必须在当次 commit 中处理
- ❌ 禁止 untracked 文件跨版本遗留

---

## 10. 部署规范

### 10.1 部署前停止所有进程

```bash
taskkill /F /IM DormManage.TrayApp.exe
taskkill /F /IM DormManage.Admin.exe
taskkill /F /IM DormManage.Api.exe
sleep 3
```

### 10.2 部署后 7 项验证

参见 `scripts/publish_checklist.md`。

### 10.3 部署后浏览器必须 Ctrl+F5

```bash
# 强刷
Ctrl + F5
# 或开无痕模式
```

---

## 11. 反模式（强制禁止）

| 反模式 | 案例 | 替代方案 |
|--------|------|----------|
| 字符串内 `@bg-xxx` | v2.13.193 编译错误 | if/else Razor 块 |
| 单独 `dotnet publish` | v2.13.193 双胞胎 | sync 脚本 |
| 多处独立业务逻辑 | v2.13.193 有效期 | 统一助手函数 |
| 只测 admin | v2.13.176 翻转 | 必测未授权角色 |
| HTTP 200 即信 | v2.13.193 密码 | 跨连接查询 |
| 文档先于代码 | v2.13.176 翻转 | 文档+代码同步 |
| datetime-local 通用 | v2.13.193 有效期 | date 类型 |
| 模糊命名 | HasEnabled | AllowDisplay |

---

## 12. 违规检查清单（每次发布前）

- [ ] 所有隐私字段都有 `IsFieldHiddenAsync` 接线
- [ ] 所有 ExpiresAt 逻辑用 `UserExpiryHelper`
- [ ] Razor 字符串内没有 `@xxx` 误识别
- [ ] 发布使用 sync 脚本
- [ ] Shared DLL 同步到 4 处
- [ ] 跨权限测试通过
- [ ] 真实数据验证通过
- [ ] git diff 确认修改已保存
- [ ] 文档已创建
- [ ] known-bugs 已更新
- [ ] CLAUDE.md 已同步

---

**使用建议**：每次代码审查前对照本清单检查，避免引入新 BUG！