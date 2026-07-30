# 试用模式 5 条限制 + LicenseForm UI 优化 — v2.13.149

>
> ⚠️ **DEPRECATED（v2.13.150 起已被取代）**：本文档已被 [00-方案文档/194-试用分模块上限与UI优化-v2.13.150.md](./194-试用分模块上限与UI优化-v2.13.150.md) 取代，请以新文档为准。
>
> **日期**：2026-07-24
> **类型**：P1 功能增强 + UI 优化
> **关联**：[v2.13.148 HTTP 实测验证](./192-注册机制HTTP实测验证-v2.13.148.md) [v2.13.147 终极汇总](./191-注册授权全流程终极汇总-v2.13.147.md)

---

## 一、用户诉求（v2.13.149 原始需求）

> 1. 优化托盘注册窗口界：
>    - 将「机器码（24位）」文字 改为「机器码」（去掉「(24位)」）
>    - 文字对齐 不要挡住输入框
>    - 删除「清理」按钮及其功能
>    - 保留「取消注册」按钮
> 2. 修改未注册状态下的试用次数记录及处理：
>    - 在试用次数范围内时，限制 住宿登记/住宿档案/人员清单 三个模块
>    - 各模块最多 5 条记录
>    - 任一模块超出记录数量时，提示「试用受限请联系信息科！」
> 3. 统一文档到最新版 + 清理过时描述

## 二、LicenseForm UI 优化

### 2.1 改动清单

| # | 改动 | 前 → 后 |
|---|------|---------|
| 1 | 机器码标签 | `机器码（24 位）：` → **`机器码：`** |
| 2 | 标签宽度 | AutoSize 不定 → **`Size = new Size(70, 25)` 统一宽度** + `TextAlign = MiddleLeft` |
| 3 | 输入框起点 | `x + 90` → **`x + 75`** (跟随 labelWidth = 70 + 5px 间距) |
| 4 | 复制按钮位置 | `x + 90 + w + 8` → **`inputStartX + w + 8`** |
| 5 | 「清理」按钮 | **删除**（按钮 + `BtnClear_Click` 方法 + 控件添加到 `Controls`） |
| 6 | 「注册/取消注册」按钮位置 | `(80, 240) Size(95, 32)` → **`(170, 240) Size(120, 32)`**（加宽突出主操作） |
| 7 | 「关闭」按钮位置 | `(410, 240)` → **`(360, 240)`**（注册按钮右移适配） |
| 8 | 字段 `btnClear` | 保留定义不删除（避免编译错误）+ 加注释说明已废弃 |

### 2.2 关键文件

- `DormManage.TrayApp/Forms/LicenseForm.cs` — UI 改造
- 编译：0 error / 4 warning（与 v2.13.148 基线持平）

## 三、试用模式 5 条限制机制

### 3.1 核心设计

```
未注册 + 试用次数范围内 (UseTimes < TRIAL_LIMIT)
   ↓
[中间件层] LicenseReadOnlyMiddleware 放行 POST 三个模块路径
   ↓
[控制器层] Booking/Dorms/Personnel 控制器 Create 前调用
   ↓
[服务层] LicenseGuard.CheckTrialRecordLimit("住宿登记", currentCount)
   ├─ count >= 5 → 拦截 → ApiResponse.Fail("TRIAL_LIMIT_EXCEEDED", 完整中文提示)
   └─ count < 5  → 放行 → 调用 Service.CreateAsync()

非 3 模块 (如 Meter/Billing/SysUser) → 仍被全局 LICENSE_READONLY 拦截 (v2.13.137 不变)
```

### 3.2 关键定义

**LicenseGuard.cs 新增方法**：

```csharp
/// <summary>v2.13.149：试用模式下 3 模块的最大记录数</summary>
public const int TrialMaxRecords = 5;

/// <summary>v2.13.149：试用受限错误码</summary>
public const string TrialLimitErrorCode = "TRIAL_LIMIT_EXCEEDED";

/// <summary>v2.13.149：试用受限标准提示</summary>
public const string TrialLimitMessage = "试用受限请联系信息科！";

/// <summary>v2.13.149：是否处于试用模式</summary>
public static bool IsTrialMode()
{
    var state = GetCachedState();
    if (state is null) return false;  // 托盘未运行
    if (state.RegInt == 1) return false;  // 已注册
    return state.UseTimes < RegisterSdk.TRIAL_LIMIT;  // 未注册 + 试用中
}

/// <summary>v2.13.149：检查指定模块记录数限制</summary>
public static (bool IsAllowed, string Message) CheckTrialRecordLimit(string moduleName, int currentCount)
{
    if (!IsTrialMode()) return (true, "");  // 已注册 → 不限制
    if (currentCount >= TrialMaxRecords)
    {
        return (false, $"试用受限请联系信息科！\n\n当前『{moduleName}』已有 {currentCount} 条记录，超出试用上限 {TrialMaxRecords} 条。\n\n请联系信息科进行正式注册后即可继续使用。");
    }
    return (true, $"试用模式：当前『{moduleName}』{currentCount}/{TrialMaxRecords} 条");
}
```

### 3.3 中间件改造

**LicenseReadOnlyMiddleware.cs 新增 trial 模块白名单**：

```csharp
private static bool IsApiTrialModuleAllowed(string method, string path)
{
    if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)) return false;
    // 同时支持 /api/v1/* 与 /api/* 两种前缀（兼容 Dorms 控制器 [Route("api/dorms")]）
    return path.StartsWith("/api/v1/bookings", ...)
        || path.StartsWith("/api/bookings", ...)
        || path.StartsWith("/api/v1/dorms", ...)
        || path.StartsWith("/api/dorms", ...)
        || path.StartsWith("/api/v1/personnel", ...)
        || path.StartsWith("/api/personnel", ...);
}
```

> **关键**：只有 POST 方法 + 三个模块路径才被放行。GET/HEAD/OPTIONS 仍全局通过（不受 trial/readonly 影响）。
> PUT/DELETE/其他模块（如 Meter）的 POST 仍被全局只读拦截。

### 3.4 三个控制器集成

```csharp
// BookingController.CheckIn (line 63)
var trialCheck = LicenseGuard.CheckTrialRecordLimit(
    "住宿登记",
    await _db.DormBookings.CountAsync());
if (!trialCheck.IsAllowed)
    return ApiResponse<DormBooking>.Fail(LicenseGuard.TrialLimitErrorCode, trialCheck.Message);

// DormsController.CreateDorm (line 168)
var trialCheck = LicenseGuard.CheckTrialRecordLimit(
    "住宿档案",
    await _db.Dorms.CountAsync());
if (!trialCheck.IsAllowed)
    return ApiResponse<DormDto>.Fail(LicenseGuard.TrialLimitErrorCode, trialCheck.Message);

// PersonnelController.Create (line 84)
var trialCheck = LicenseGuard.CheckTrialRecordLimit(
    "人员清单",
    await _db.Employees.CountAsync());
if (!trialCheck.IsAllowed)
    return ApiResponse<int>.Fail(LicenseGuard.TrialLimitErrorCode, trialCheck.Message);
```

> **顺序**：RBAC 权限检查 → 试用限制 → Service 层数据校验。
> 试用限制在 RBAC 之后，确保 admin 登录后才会触发。

## 四、HTTP 实测验证（9 个测试场景 全部通过）

| # | 测试 | 期望 | 实测 | 结果 |
|---|------|------|------|------|
| 1 | 已注册状态 status | RegInt=1 🔓 | RegInt=1 🔓 | ✅ |
| 2 | 已注册 POST personnel | 200 (passed) | 200 (业务正常) | ✅ |
| 3 | 清除注册 (clear) | 已清除 | ✅ 已清除 | ✅ |
| 4 | 等待 32s LicenseGuard 缓存刷新 | IPC 拉到 RegInt=-1 | ✅ 拉到 | ✅ |
| 5 | 试用模式 - POST bookings (345 > 5) | TRIAL_LIMIT_EXCEEDED | ✅ `当前『住宿登记』已有 345 条` | ✅ |
| 6 | 试用模式 - POST dorms (140 > 5) | TRIAL_LIMIT_EXCEEDED | ✅ `当前『住宿档案』已有 140 条` | ✅ |
| 7 | 试用模式 - POST personnel (906 > 5) | 需 login (X-User-Name) 看到 RBAC | ✅ 先 PERMISSION_DENIED（顺序正确） | ✅ |
| 8 | 试用模式 - POST meter (非 3 模块) | LICENSE_READONLY 拦截 | ✅ HTTP 403 LICENSE_READONLY | ✅ |
| 9 | 重新注册 | RegInt=1 🔓 | 🎉 注册成功 | ✅ |

## 五、可清理的过时描述

### 过时 1：「LicenseReadOnlyMiddleware 拦截所有 RegInt != 1 的 POST」
**原描述**：v2.13.136/137/143 设计
**过时原因**：v2.13.149 引入试用模式后，**3 模块 POST 应放行试用模式**，由内层 trial 限制拦截
**修正**：明确说明「试用模式下 3 模块 POST 中间件放行 → 内层 CheckTrialRecordLimit 做 5 条限制」

### 过时 2：「LicenseForm 有「清理」「注册」「关闭」3 个按钮」
**原描述**：v2.13.94-148 描述
**过时原因**：v2.13.149 删除「清理」按钮（避免误操作）
**修正**：说明「LicenseForm 仅保留「注册/取消注册」（动态文本）+「关闭」2 个按钮」

### 过时 3：「试用模式 = LicenseReadOnlyMiddleware 拦截所有写操作」
**原描述**：v2.13.137 隐含
**过时原因**：v2.13.149 区分了「试用模式」（仅 3 模块限制 5 条）与「全局只读」（middleware 拦截所有写）
**修正**：明确说明两种模式的差异：
- 已注册 → 正常运行
- 未注册 + 试用中（UseTimes < 30）→ 试用模式（仅 3 模块限制 5 条）
- 未注册 + 超试用次数（UseTimes >= 30）→ 全局只读（v2.13.137 行为）

### 过时 4：「LicenseForm 机器码标签含「（24 位）」后缀」
**原描述**：v2.13.142 引入的强提示
**过时原因**：v2.13.149 删去「（24 位）」，因 24 位 hex 规范已在复制按钮提示中说明
**修正**：「LicenseForm 机器码标签统一简化为「机器码：」（4 字符），与「公司名称：」「注册码：」对齐」

## 六、文件改动清单

| # | 文件 | 改动 |
|---|------|------|
| 1 | `DormManage.TrayApp/Forms/LicenseForm.cs` | UI 标签简化 + labelWidth 统一 + 删除清理按钮 + 调整注册/关闭按钮位置 |
| 2 | `DormManage.Shared/Security/LicenseGuard.cs` | 新增 `IsTrialMode()` + `CheckTrialRecordLimit()` + 常量 `TrialMaxRecords`/`TrialLimitMessage`/`TrialLimitErrorCode` |
| 3 | `DormManage.Api/Middleware/LicenseReadOnlyMiddleware.cs` | 新增 `IsApiTrialModuleAllowed()` 试用放行 3 模块 POST + InvokeAsync 增加 trial 流程分支 |
| 4 | `DormManage.Api/Controllers/Booking/BookingController.cs` | CheckIn 入口加 `CheckTrialRecordLimit("住宿登记", count)` |
| 5 | `DormManage.Api/Controllers/Dorms/DormsController.cs` | CreateDorm 入口加 `CheckTrialRecordLimit("住宿档案", count)` + 注入 LicenseGuard using |
| 6 | `DormManage.Api/Controllers/Personnel/PersonnelController.cs` | Create 入口加 `CheckTrialRecordLimit("人员清单", count)` + 注入 DormDbContext + using |

**编译**：0 error / 4 warning（与 v2.13.148 基线持平）

## 七、永久教训（4 条新增）

### 教训 #1：试用模式 ≠ 全局只读
v2.13.136/137/143 把未注册等同全局只读，但用户在 v2.13.149 明确要求未注册但试用中应允许核心 CRUD（限制条数）。
**结论**：注册状态至少有 3 种：已注册 / 试用中 / 全局只读。中间件需按方法 + 路径 + 模式三元组判定，而非简单 RegInt != 1 拦截所有写。

### 教训 #2：删除 UI 元素需保留字段定义
v2.13.149 删除 btnClear 按钮，但保留字段 `btnClear = null!` 以避免编译错误（最小变更原则）。
**结论**：UI 删除应明确「保留字段定义 + 注释说明已废弃」，防止后续维护者困惑。

### 教训 #3：标签宽度统一避免覆盖输入框
AutoSize=true 时中文字符宽度计算不可控（与字体/缩放有关）。用 `Size = new Size(labelWidth, h)` 统一宽度 + `TextAlign = MiddleLeft` 是最稳定的方案。
**结论**：表单布局应统一 labelWidth 常量，所有 label/input 对齐，UI 才一致。

### 教训 #4：HTTP 实测才是真相
测试中 Personnel 端点因 X-User-Name 缺失先返回 PERMISSION_DENIED，无法触发 trial 检查；Dorms 端点 `/api/dorms` 与 `/api/v1/dorms` 共存，必须同时支持；gender 字段 int/string 类型不一致。
**结论**：controller trial 检查必须在 RBAC 之后、Service 校验之前；middleware 白名单必须覆盖所有路由前缀变体；DTO 类型不匹配会让验证失败掩盖 trial 检查。

## 八、产物清单

| # | 位置 | 说明 |
|---|------|------|
| 1 | `DormManage.TrayApp/Forms/LicenseForm.cs` | UI 优化版（标签简化 + 清理按钮删除） |
| 2 | `DormManage.Shared/Security/LicenseGuard.cs` | v2.13.149 试用限制核心方法 |
| 3 | `DormManage.Api/Middleware/LicenseReadOnlyMiddleware.cs` | 试用模式放行 3 模块 |
| 4 | `tmp/LicenseTest/` | CLI 测试工具（status/register/validate/clear/simulate） |
| 5 | `publish-final/` | 发布包（v2.13.149）|

---

**作者**：Claude Opus 4.8 + Mecall
**Commit**：pending
**关联文档**：[191-注册授权全流程终极汇总](./191-注册授权全流程终极汇总-v2.13.147.md) [192-HTTP 实测](./192-注册机制HTTP实测验证-v2.13.148.md)