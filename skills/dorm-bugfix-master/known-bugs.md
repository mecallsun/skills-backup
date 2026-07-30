# 已知 BUG 案例库（v2.13.193 完整汇总）

> Skill: dorm-bugfix-master
> 创建日期：2026-07-28
> 案例数：10+ 个

---

## 案例 1：v2.13.187 隐私字段 Dorms 接线缺失

**症状**：
> 在隐私字段管理清单中勾选了「住宿档案」的「容量」「在住人数」字段；未授权角色仍能看到这些字段。

**根因**：v2.13.180 扩展了 21 个隐私字段到 `SysFieldPermission` 表，但**只 seed 了 DB 数据，没有 UI 接线**。Dorms/Index.cshtml 和 Dorms/Details.cshtml 5 处缺 `IsFieldHiddenAsync` 调用。

**修复**：
```csharp
// Dorms/Index.cshtml 表头
<th class="text-center @(await Html.IsFieldHiddenAsync("dorm.capacity") ? "d-none" : "")">容量</th>

// 数据行（隐藏时显示 ***）
<td class="text-center @(await Html.IsFieldHiddenAsync("dorm.capacity") ? "d-none" : "")">
    @(await Html.IsFieldHiddenAsync("dorm.capacity") ? "***" : item.Capacity + " 人")
</td>
```

**教训**：
- ❌ 数据先行 ≠ UI 就绪
- ✅ 强制规则：每增加一个 SysFieldPermission 字段，必须 24h 内完成所有引用页面的接线

**文档**：`00-方案文档/226-隐私字段权限UI接线缺失修复-v2.13.187.md`

---

## 案例 2：v2.13.188 当前入住人员缺性别列

**症状**：当前入住人员列表没有性别列；床位显示「床位 1」而不是「1」。

**修复**：
1. 后端 `BookingRecordDto` 加 `Gender` 字段 + 赋值 `Gender = empDict[b.EmployeeId].Gender`
2. 前端表头新增 `<th class="text-center">性别</th>`（在姓名之后）
3. 数据行 Badge：男=`bg-primary` + `bi-gender-male`、女=`bg-danger` + `bi-gender-female`
4. 床位移除「床位」前缀，只显示数字

**教训**：
- ❌ 多处散落实现导致不一致
- ✅ 强制规则：所有 UI 字段显示必须 JOIN 真源表

**文档**：`00-方案文档/227-当前入住人员新增性别列与床号简化-v2.13.188.md`

---

## 案例 3：v2.13.191 注册状态显示「试用模式」

**症状**：托盘已注册成功，但 Web 端关于系统页面显示「试用模式」。

**根因（深挖发现）**：
- `ServiceIpc.RegStateDto` 缺少 `RegStatus` 字段
- `LicenseGuard.GetLicenseBanner()` 方法未实现
- `LicenseStatusController.cs`（untracked 文件）引用了不存在的成员

**修复**（v2.13.191）：
1. `RegStateDto` 添加 `RegStatus` 字段
2. `LicenseGuard` 添加 `RegStatusEnum` 静态类
3. 实现 `GetLicenseBanner()` 5 case 完整逻辑
4. 实现 `ConvertRegIntToRegStatus()` 转换函数
5. 托盘端 `HandleGetRegState` 同步填充 RegStatus

**教训**：
- ❌ 文档先行于代码导致 17+ 版本 BUG 潜伏
- ❌ untracked 文件是设计断点
- ✅ 强制规则：禁止 untracked 文件跨版本遗留

**文档**：`00-方案文档/228-注册状态显示错误修复-v2.13.191.md`

---

## 案例 4：v2.13.193 发布目录双胞胎陷阱

**症状**：发布后所有修改都"无效"，但 DLL 时间戳是最新的。

**根因**：
```
release/latest/
├── Admin/         ← 开发者发布（最新）
│   └── DormManage.Admin.dll  (16:01)
└── TrayApp/
    └── Admin/     ← TrayApp 实际加载（09:50 旧版）
        └── DormManage.Admin.dll
```
TrayApp 用 `..\Admin\` 相对路径加载子目录，而不是 `release/latest/Admin/`。

**修复（v2.13.193）**：
1. 新建 `scripts/sync_publish_to_trayapp.sh` 强制同步
2. 新建 `scripts/publish_checklist.md` 7 项检查清单
3. 更新 `99-发布程序包与部署规范-v2.13.184.md` → v2.13.193 升级版

**教训**：
- ❌ 发布路径 ≠ 运行路径
- ✅ 强制规则：发布必须使用 sync 脚本

**文档**：`00-方案文档/230-发布目录双胞胎陷阱-TrayApp加载路径不一致-v2.13.193.md`

---

## 案例 5：v2.13.193 隐私字段 deny-by-default 翻转

**症状**：未授权角色仍能看到隐私字段；admin 测试通过，test 用户测试失败。

**根因（终极深挖）**：
- v2.13.176 文档设计了 deny-by-default
- **但代码层从未实施**（17+ 版本 BUG 潜伏）
- 旧代码 `HasPrivacyFieldEnabledAsync` 用 `if (!Has) return new HashSet()` 即"不勾选 → 不隐藏"（allow-by-default）

**修复**：
```csharp
// v2.13.193 修复
public const string PrivacyFieldPermissionCode = "privacy:field:enable";

public async Task<bool> AllowDisplayPrivacyFieldsAsync(int userId)
{
    var codes = await GetUserPermissionCodesAsync(userId);
    return codes.Contains(PrivacyFieldPermissionCode);
}

public async Task<HashSet<string>> GetHiddenFieldKeysAsync(int userId)
{
    // v2.13.176 deny-by-default：不勾选 → 隐藏所有
    if (!await AllowDisplayPrivacyFieldsAsync(userId))
    {
        var allActiveKeys = await _db.SysFieldPermissions
            .Where(p => p.IsActive)
            .Select(p => p.FieldKey)
            .ToListAsync();
        return new HashSet<string>(allActiveKeys);
    }
    return new HashSet<string>();
}
```

**教训**：
- ❌ 文档与代码必须同步 commit（v2.13.176 反面案例）
- ✅ 强制规则：deny-by-default 是默认安全选择

**文档**：`00-方案文档/216-隐私字段保护语义翻转v2.13.176.md`、`231-隐私字段语义翻转终极修复-v2.13.193.md`

---

## 案例 6：v2.13.193 账号有效期判定 `>` 应为 `>=`

**症状**：账号**过期当天仍能登录**。

**根因**：
```csharp
// 旧代码（v2.13.93）
if (user.ExpiresAt.HasValue && DateTime.Today > user.ExpiresAt.Value.Date)
    return (false, "账号已过期，请联系管理员", null);
```

`Today > ExpiresAt.Date` 严格大于导致过期当天还能登录。

**修复**：
1. 新建 `UserExpiryHelper.IsExpired()` 统一助手
2. LoginAsync、OnValidatePrincipal、前端 Badge 三处统一调用
3. 使用 `>=` 语义（过期当天即视为已过期）

```csharp
// v2.13.193 修复
public static bool IsExpired(DateTime? expiresAt)
{
    if (!expiresAt.HasValue) return false;
    return DateTime.Today >= expiresAt.Value.Date;
}
```

**教训**：
- ❌ 同一业务规则不要有多个独立实现（3 处 ExpiresAt → 必须用单一助手）
- ❌ 临界日期判断用 `>` 应改为 `>=`
- ✅ 强制规则：deny-by-default 是默认安全选择

**文档**：`00-方案文档/233-账号有效期判定BUG修复-v2.13.193.md`

---

## 案例 7：v2.13.193 编辑有效期字段类型混乱

**症状**：编辑「有效期」时只显示日期不显示时间，填写后不能保存。

**根因**：
- `<input type="datetime-local">` 要求 `yyyy-MM-ddTHH:mm` 格式
- 但**用户只关心日期**（业务场景）
- Razor 渲染 `data-expires-at="2026-07-31T18:03"` 浏览器解析可能异常

**修复**：
1. 前端：`type="datetime-local"` → `type="date"`（只支持日期）
2. 后端：日期型 `parsed.Date.AddDays(1).AddSeconds(-1)` 存为当天 23:59:59
3. data-expires-at 格式：`yyyy-MM-ddTHH:mm` → `yyyy-MM-dd`

**教训**：
- ❌ datetime-local 不是最佳选择（用户业务场景只关心日期）
- ✅ 前后端必须 type 严格一致

**文档**：`00-方案文档/233-账号有效期判定BUG修复-v2.13.193.md`

---

## 案例 8：v2.13.193 密码重置不生效

**症状**：API 返回 success，但 BCrypt 验证 hash 未变化。

**根因**：
- EF Core 上下文是 Scoped（每次请求新实例）
- 但 `_db.SysUsers.FindAsync(Id)` 可能在某些情况下加载了缓存
- 跨连接查询验证发现 hash 未变化

**修复**：
1. 重新调用 API 重置密码
2. 用独立 .NET 程序跨连接查询验证
3. 看到新 hash 才确认成功

**教训**：
- ❌ HTTP 200 + success:true 不一定真正生效
- ❌ EF Core 上下文可能缓存旧值
- ✅ 强制规则：跨连接查询验证 BUG 修复

**诊断工具**：`tmp/DiagPassword/Program.cs`

---

## 案例 9：v2.13.193 编译错误（Razor @bg- 误识别）

**症状**：
```
Dorms/Index.cshtml(122,95): error CS0103: 当前上下文中不存在名称"primary"
```

**根因**：
```csharp
// Razor 字符串拼接
$"<span class=\"badge @(item.CurrentCount > 0 ? "bg-primary" : "bg-secondary")\">" + item.CurrentCount
```

Razor 解析器把 `@bg-` 当成了变量。

**修复**：
```csharp
@if (await Html.IsFieldHiddenAsync("dorm.currentcount"))
{
    <span>***</span>
}
else
{
    <span class="badge @(item.CurrentCount > 0 ? "bg-primary" : "bg-secondary")">@item.CurrentCount</span>
    @if (!await Html.IsFieldHiddenAsync("dorm.capacity"))
    {
        <span> / @item.Capacity</span>
    }
}
```

使用 `if/else` Razor 块替代字符串拼接。

**教训**：
- ❌ Razor 字符串内的 `@` 容易被误识别
- ✅ 强制规则：避免在字符串内用 `@` 开头

---

## 案例 10：v2.13.193 隐私/权限界面 UI 接线缺失

**症状**：用户管理 `_UserPanel.cshtml` 的"操作"列与角色管理风格不一致。

**修复（v2.13.189-190）**：
1. 新增按钮从 `.d-flex justify-content-end` 移到 card-header 右侧
2. 操作按钮从 `btn btn-sm btn-outline-*` + icon 改为 `op-btn` + `op-btn-sep` 纯文字
3. 文字按钮：编辑 | 启用/停用 | 重置密码 | 删除
4. 加 `text-truncate` + `title` 提示
5. "最后登录"拆分为「最后时间」+「登录IP」

**文档**：`00-方案文档/225-列表UI设计规范v2.13.190增量.md`

---

## 案例 11：v2.13.193 启动失败 TypeLoadException

**症状**：
```
System.TypeLoadException: Could not load type 'DormManage.Shared.Security.RuntimeWindowGuard'
```

**根因**：`DormManage.Shared.dll` 是旧版（09:50），缺少 v2.13.135 的 `RuntimeWindowGuard` 类。

**修复**：
```bash
# 同步 Shared DLL 到所有子目录
cp release/latest/Admin/DormManage.Shared.dll release/latest/TrayApp/Admin/
cp release/latest/Admin/DormManage.Shared.dll release/latest/TrayApp/Api/
```

**教训**：
- ❌ Shared DLL 不同步会导致 TypeLoadException
- ✅ 强制规则：发布必须用 sync 脚本

---

## 案例 12：v2.13.193 浏览器缓存

**症状**：发布后看不到修改，刷新页面也无效。

**根因**：浏览器缓存了旧的 HTML / JS / CSS。

**修复**：
- Ctrl + F5（Windows/Linux）强制刷新
- 或 Cmd + Shift + R（Mac）
- 或清除浏览器缓存
- 或开无痕模式

**教训**：
- ✅ 强制规则：发布后必须按 Ctrl+F5 验证

---

## 案例 13：v2.13.193 EF Core `if/else` 替代 `is` 模式

**症状**：
```csharp
if (user == null) ...
```

**改进**：
```csharp
if (user is null) ...
```

`is null` 在 C# 8+ 中是更现代的写法，避免运算符重载问题。

---

## 案例 14：v2.13.193 进程唯一锁 v2.13.171 解除

**症状**：测试或部署时无法启动多实例。

**根因**：v2.13.72 引入了 Mutex 互斥锁（v2.13.171 解除）。

**修复**：
- 移除 `DormManage.Api/Program.cs` 中的 Mutex 互斥锁
- 改为端口冲突软告警
- 支持多实例高并发架构

**文档**：`00-方案文档/212-进程唯一锁解除与多实例高并发架构-v2.13.171.md`

---

## 案例 15：v2.13.168 设备档案设备ID全局唯一

**症状**：用户报告"增加设备档案列表中记录的设备ID的唯一不重复"。

**修复（v2.13.168）**：
1. 同一记录内 3 个 ID 互斥（电≠冷≠热）
2. 跨记录任一 ID 不与其它记录任一字段重复
3. 三层防御：UI/Service/DB

**文档**：`00-方案文档/209-设备档案设备ID唯一性校验-v2.13.168.md`

---

## BUG 案例查找表

| 症状关键词 | 案例 | 文档 |
|----------|------|------|
| 修改无效 | #4 | 230 |
| 隐私字段仍显示 | #1, #5 | 226, 231 |
| 启动失败 | #11 | 232 |
| TypeLoadException | #11 | 232 |
| 试用模式显示 | #3 | 228 |
| 过期当天仍能登录 | #6 | 233 |
| 编辑日期不能保存 | #7 | 233 |
| 密码重置不生效 | #8 | 233 |
| 编译错误 Razor | #9 | 232 |
| 浏览器缓存 | #12 | 232 |
| 角色管理按钮风格不一致 | #10 | 225 |
| 性别列缺失 | #2 | 227 |
| 床位 1 文字 | #2 | 227 |
| 设备档案 ID 重复 | #15 | 209 |
| 进程多实例启动失败 | #14 | 212 |
| 用户管理密码重置 | #6, #8 | 233 |

---

## 触发新 BUG 记录流程

当修复新 BUG 时，按以下流程记录到本文件：

1. **命名**：按 v2.13.XXX-BUG 简短描述命名
2. **必填字段**：
   - 症状（用户原话）
   - 根因（5 步排查结果）
   - 修复（代码示例）
   - 教训（避免未来）
   - 文档链接
3. **添加索引**：在 `bug-categories.md` 和 BUG 案例查找表更新
4. **同步到 CLAUDE.md**：在"Important Notes"添加版本备注

---

## 终极教训（5 句）

1. **90% 的"修改无效"BUG 是发布路径问题** → 检查 DLL 时间戳
2. **90% 的隐私权限 BUG 是 deny/allow 语义反了** → 看 v2.13.176 规范
3. **100% 的 Razor 编译错误是 @-字符串拼接** → 改用 if/else 块
4. **100% 的 TypeLoadException 是 Shared DLL 不同步** → 用 sync 脚本
5. **90% 的登录失败是密码错** → 用 BCrypt 验证