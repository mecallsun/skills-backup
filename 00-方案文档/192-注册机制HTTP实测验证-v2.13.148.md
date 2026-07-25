# 注册机制 HTTP 实测验证 — v2.13.148

> **日期**：2026-07-24
> **类型**：HTTP 真实运行验证
> **状态**：✅ **完整通过**
> **关联**：[v2.13.147 终极汇总](./191-注册授权全流程终极汇总-v2.13.147.md)

---

## 一、测试环境

| 组件 | 配置 |
|------|------|
| 主机 | Windows 11 Pro 10.0.26200 |
| 机器码 | `BFEBFBFF000A06A4AA2E3B0E`（本机真实 WMI） |
| 公司名 | 广东金戈新材料股份有限公司 |
| CDKEY | `3B55C-A8LE9-3865B-FBE56-C1DC0`（NPGS 36 进制，含 'L'）|
| 有效期 | 2026-07-26 |
| 服务端口 | Admin :5001 / Api :5100 / IPC :5099 |
| 发布版本 | v2.13.147 |

## 二、测试架构

```
托盘进程 (DormManage.TrayApp)
├─ LicenseMonitor (5s 周期) → RegisterSdk.CheckReg()
├─ IPC Server (:5099) ← Admin/Api 查询
└─ ProcessManager → 启动 Admin/Api 子进程

Admin (DormManage.Admin :5001)
├─ LicenseGuard (30s 缓存 + IPC 轮询)
├─ LicenseReadOnlyMiddleware → 检查 IsReadOnly
└─ Razor Pages

Api (DormManage.Api :5100)
├─ LicenseGuard (30s 缓存 + IPC 轮询)
├─ LicenseReadOnlyMiddleware → 检查 IsReadOnly
└─ REST API Controllers
```

## 三、9 步实测矩阵

| 步骤 | 状态 | 测试 | 期望 | 实测 | 结果 |
|------|------|------|------|------|------|
| 1 | 已注册 | 当前状态 | RegInt=1, 🔓 | RegInt=1, 🔓 | ✅ |
| 2 | 已注册 | POST /api/v1/personnel | 非 403 | **HTTP 200**（业务正常） | ✅ |
| 2 | 已注册 | POST /api/v1/dorms | 非 403 | **HTTP 404**（路由不存在） | ✅ |
| 3 | 清除 | DeleteRegAll | 已清除 | ✅ 已清除 | ✅ |
| 4 | 等待 | 32s 缓存刷新 | IPC 拉到新状态 | ✅ 拉取成功 | ✅ |
| 5 | 未注册 | POST /api/v1/personnel | **403** | **HTTP 403** | ✅ |
| 5 | 未注册 | POST /api/v1/dorms | **403** | **HTTP 403** | ✅ |
| 6 | 未注册 | GET /Account/Login | 200 | **HTTP 200** | ✅ |
| 6 | 未注册 | GET /swagger/index.html | 200 | **HTTP 200** | ✅ |
| 7 | 重新注册 | WriteRegItem + CheckReg | RegInt=1 | 🎉 注册成功 | ✅ |
| 8 | 等待 | 32s 缓存刷新 | IPC 拉到新状态 | ✅ 拉取成功 | ✅ |
| 9 | 重新注册 | POST /api/v1/personnel | 非 403 | **HTTP 200** | ✅ |

## 四、关键响应内容（拦截 403）

清除注册后，POST 返回内容：

```json
{
  "success": false,
  "code": "LICENSE_READONLY",
  "message": "软件未注册或注册已过期，所有修改类操作已禁用。请联系信息科进行注册。"
}
```

来源：`DormManage.Api/Middleware/LicenseReadOnlyMiddleware.cs:65 RejectWrite()`

## 五、关键 IPC 响应

注册有效时 TrayApp IPC `getregstate` 返回：

```json
{
  "Success": true,
  "Message": "ok",
  "Data": {
    "RegInt": 1,
    "SN": "BFEBFBFF000A06A4AA2E3B0E",
    "CDKEY": "3B55C-A8LE9-3865B-FBE56-C1DC0",
    "LTDName": "广东金戈新材料股份有限公司",
    "RegDate": "2026-07-26T00:00:00",
    "UseTimes": 0,
    "DetectedAtUtc": "2026-07-24T22:24:18.0217179Z"
  }
}
```

## 六、机制核心工作流

### 6.1 注册成功 → 写权限开放

```
LicenseForm.BtnReg_Click
  ↓
RegisterSdk.CheckRegCDKey (算法 A 公司名路径 + GBK + 36 进制)
  → RegInt=1
  ↓
RegisterSdk.WriteRegItem → HKLM/HKCU/DPAPI
  ↓
LicenseGuard.ResetCache (Web/Api 子进程缓存)
  ↓
30s 后 IPC 轮询拉取最新状态 → LicenseGuard.IsReadOnly=false
  ↓
LicenseReadOnlyMiddleware 放行 POST/PUT/DELETE
```

### 6.2 取消注册 → 全局只读

```
LicenseForm 「取消注册」按钮 / RegisterSdk.DeleteRegAll
  ↓
HKLM/HKCU/license.dat 全部清除
  ↓
[TrayApp] LicenseMonitor 5s 周期 → RegisterSdk.CheckReg() 返回 RegInt=-1
[TrayApp] IPC getregstate 响应 → Data.RegInt=-1
  ↓
[Admin/Api] 30s 缓存过期 → IPC 拉取 → LicenseGuard.IsReadOnly=true
  ↓
LicenseReadOnlyMiddleware 拦截 POST/PUT/DELETE → 403 LICENSE_READONLY
  ↓
GET/HEAD/OPTIONS 仍可通过（read-only 模式可读）
```

### 6.3 关键时序

```
T+0s   用户点「取消注册」
T+0s   HKLM/HKCU/license.dat 清除
T+0s   POST 请求 → 仍 200（30s 缓存还未过期）
T+1s   IPC 拉取新状态 → IsReadOnly=true  ← 但需等到下次刷新
T+30s  LicenseGuard 缓存过期 → 下次 IsReadOnly 调用触发 IPC 拉取
T+30s  后续 POST → 403 LICENSE_READONLY
```

**关键**：取消注册后**最多 30 秒** 任意 POST 仍可能通过——这是 LicenseGuard 30s 缓存的设计权衡（避免每次 HTTP 请求都做 IPC）。

## 七、可清理的过时描述

### 过时 1：「取消注册后立即全局只读」
**原描述**：v2.13.136 早期描述「取消注册后立刻进入只读模式」
**过时原因**：v2.13.137 IPC 反转后，子进程有 30s 缓存，**最多 30s 后才进入只读**
**修正**：明确说明「最多 30 秒延迟」

### 过时 2：「LicenseReadOnlyMiddleware 只在 Api 端生效」
**原描述**：v2.13.136 文档
**过时原因**：v2.13.137 起 Admin 端**也有独立的 LicenseReadOnlyMiddleware**（位置 `DormManage.Admin/Middleware/`）
**修正**：明确说明 Admin + Api 双端中间件

### 过时 3：「LicenseGuard 默认允许」
**原描述**：v2.13.136 概念
**过时原因**：v2.13.137 + v2.13.143 后的实际行为是**默认只读**（更安全的方向）
**修正**：明确说明「LicenseGuard 默认只读，注册有效才放行」

### 过时 4：「TrayApp 实时推送注册状态」
**原描述**：v2.13.137 注释暗示 push 机制
**过时原因**：实际只是**子进程 30s 轮询 IPC getregstate**，TrayApp 不主动推送
**修正**：明确说明「30s 轮询机制 + LicenseMonitor 5s 周期」

## 八、产物清单

| # | 位置 | 说明 |
|---|------|------|
| 1 | `publish-final/TrayApp/*` | v2.13.147 TrayApp（含 5-2-0 解锁 + 防暗桩）|
| 2 | `publish-final/Admin/*` | v2.13.147 Admin UI |
| 3 | `publish-final/Api/*` | v2.13.147 API Service |
| 4 | `publish-final/DormManage-v2.13.147_*.zip` | 发布包（199 MB）|
| 5 | `tmp/LicenseTest/` | CLI 注册测试工具（status/register/validate/clear/simulate）|

## 九、永久教训（4 条）

1. **真实运行验证 > 单元测试** —— LicenseGuard 30s 缓存 IPC 等设计决策只能在真实运行时序中暴露，单元测试无法覆盖
2. **多进程架构必须明确权威点** —— TrayApp 是注册校验唯一权威，Admin/Api 必须通过 IPC 拉取，不能各自调用 RegisterSdk
3. **缓存设计「最多延迟 X 秒」要明示** —— LicenseGuard 30s 缓存意味着取消注册后最多 30s 仍可写入，这是预期行为，不是 BUG
4. **CLI 测试工具必备** —— GUI 应用测试难时，提供 CLI 工具（tmp/LicenseTest）让用户从命令行做完整注册生命周期验证，是软件工程的标准实践

---

**作者**：Claude Opus 4.8 + Mecall
**Commit**：pending
**关联文档**：[191-注册授权全流程终极汇总-v2.13.147.md](./191-注册授权全流程终极汇总-v2.13.147.md)