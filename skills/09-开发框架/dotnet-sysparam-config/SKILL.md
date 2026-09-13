---
name: dotnet-sysparam-config
description: PC Web 系统设置→系统参数模块开发规范。涵盖后端 validKeys 白名单 + Controller 实现 + 前端 Vue 3 对接 + 布尔/枚举字段映射 + 原型 1:1 对齐。
license: MIT
ai_tier: tier-2
domain: dotnet
language: csharp
stage: backend
output: web-api
complexity: medium
platform: windows
priority: 70
tags:
  - dotnet
  - aspnetcore
  - system-config
  - vue3
  - element-plus
  - entity-framework
  - sysconfig
triggers:
  - 系统参数
  - sysconfig
  - SystemConfigController
  - 允许注册
  - 短信验证
  - 注册策略
references:
  - SystemConfigController: DormManage.Api/Controllers/System/SystemConfigController.cs
---

# dotnet-sysparam-config Skill

## 功能概述

开发 PC Web 系统设置 → 系统参数模块，包含：
1. 后端：ASP.NET Core Controller + EF Core + validKeys 白名单 + 枚举字段映射
2. 前端：Vue 3 + Element Plus 表单 + API 调用 + 保存逻辑
3. 文档：HTML 原型同步 + 后端扩展方法

## 字段类型与 API 映射

### 布尔字段（true/false）

| 后端 Key | 前端字段 | 映射规则 |
|---------|---------|---------|
| `sys_reg_enable` | `allowRegister` | `true` ↔ `'true'`，`false` ↔ `'false'` |
| `allow_register` | 同上 | 兼容旧 key |

```typescript
// 前端 loadSysParams()
const res = await getSysConfig('sys_reg_enable')
if (res.success && res.data) {
  sysParams.allowRegister = res.data.value === 'true'
}

// 前端 saveSysParams()
await setSysConfig('sys_reg_enable', sysParams.allowRegister ? 'true' : 'false')
```

### 枚举字段（整数映射）

| 后端 Key | 前端字段 | 映射规则 | 枚举值 |
|---------|---------|---------|-------|
| `sys_reg_verify_method` | `smsVerify` | `false` → `'0'`（无），`true` → `'1'`（短信） | 0=无 1=短信 2=邮箱 3=即时验证码 |
| `sms_verify_enabled` | 同上 | `true` → `'1'`，`false` → `'0'`（旧 key 兼容） |

```typescript
// load: '1' → true, '0' → false
sysParams.smsVerify = res.data.value === '1'

// save: true → '1', false → '0'
await setSysConfig('sys_reg_verify_method', sysParams.smsVerify ? '1' : '0')
```

### 数值字段（范围校验）

| 后端 Key | 前端字段 | 校验规则 |
|---------|---------|---------|
| `sys_session_timeout` | `sessionTimeout` | 整数，范围 5-1440 分钟 |
| `sys_default_password` | `defaultPassword` | 字符串，无校验 |

```typescript
// 后端校验（SystemConfigController）
else if (req.Key == "sys_session_timeout") {
    if (!int.TryParse(req.Value, out int v) || v < 5 || v > 1440)
        return ApiResponse.Fail("INVALID_VALUE", "session_timeout 必须是 5-1440 之间的整数");
}
```

## 后端实现要点

### 1. validKeys 白名单（必须维护）

```csharp
var validKeys = new[]
{
    // 注册策略
    "allow_register", "sms_verify_enabled",
    "sys_reg_enable", "sys_reg_verify_method",
    // 系统参数扩展
    "sys_default_password", "sys_session_timeout"
};
if (!validKeys.Contains(req.Key))
    return ApiResponse.Fail("INVALID_KEY", $"key '{req.Key}' 不在允许列表中");
```

**规则**：每新增一个系统参数 key，必须同时加入 validKeys 白名单，否则 POST 返回 400。

### 2. GetDefaultValue / GetDefaultDescription（枚举完整性）

```csharp
private static string GetDefaultValue(string key) => key switch
{
    "sys_reg_enable" => "true",
    "sys_reg_verify_method" => "0",
    "sys_default_password" => "123456",
    "sys_session_timeout" => "30",
    _ => ""
};

private static string GetDefaultDescription(string key) => key switch
{
    "sys_reg_enable" => "主开关：是否允许注册",
    "sys_reg_verify_method" => "验证方式（0=无 1=短信 2=邮箱 3=即时验证码）",
    "sys_default_password" => "新用户默认密码",
    "sys_session_timeout" => "会话超时时间（分钟）",
    _ => ""
};
```

### 3. GetAllConfigs 默认值兜底

```csharp
if (!result.Any(c => c.Key == "sys_reg_enable"))
    result.Add(new ConfigDto { Key = "sys_reg_enable", Value = "true", Description = "主开关：是否允许注册" });
// 同理 sys_reg_verify_method / sys_default_password / sys_session_timeout
```

## 前端 Vue 实现要点

### 1. 独立 try/catch（防级联失败）

```typescript
const loadSysParams = async () => {
  try {
    const res: any = await getSysConfig('sys_reg_enable')
    if (res.success && res.data) sysParams.allowRegister = res.data.value === 'true'
  } catch (e) { console.error('[Settings] load sys_reg_enable', e) }

  try {
    const res: any = await getSysConfig('sys_reg_verify_method')
    if (res.success && res.data) sysParams.smsVerify = res.data.value === '1'
  } catch (e) { console.error('[Settings] load sys_reg_verify_method', e) }

  // defaultPassword / sessionTimeout 同理...
}
```

### 2. 并行保存

```typescript
const saveSysParams = async () => {
  await Promise.all([
    setSysConfig('sys_reg_enable', sysParams.allowRegister ? 'true' : 'false'),
    setSysConfig('sys_reg_verify_method', sysParams.smsVerify ? '1' : '0'),
    setSysConfig('sys_default_password', sysParams.defaultPassword),
    setSysConfig('sys_session_timeout', sysParams.sessionTimeout.toString())
  ])
  ElMessage.success('系统配置已保存')
}
```

### 3. API 层函数

```typescript
// src/api/modules.ts
export const getSysConfig = (key: string) =>
  request.get<any>(`/api/v2/system/config?key=${key}`)

export const setSysConfig = (key: string, value: string) =>
  request.post<any>('/api/v2/system/config', { key, value })
```

## HTML 原型同步规范

每次新增/修改系统参数，必须同步更新：

```
00-方案文档/04-HTML原型/settings/index.html
└── Tab 7: 系统参数
    ├── 版本 badge → 更新（如 v3.4.10）
    ├── 参数卡片数量 → 与实际字段一致
    └── 保存按钮 → 存在且有 onclick
```

**卡片布局规范**：

```html
<div class="row g-3">
  <!-- 布尔参数：form-check form-switch -->
  <div class="col-md-6">
    <div class="card border-primary">
      <div class="card-body">
        <h6>允许注册</h6>
        <div class="form-check form-switch fs-5">
          <input class="form-check-input" type="checkbox" id="switchAllowRegister">
          <label class="form-check-label">启用注册入口</label>
        </div>
      </div>
    </div>
  </div>
  <!-- 数值参数：input type=number -->
  <div class="col-md-6">
    <div class="card border-success">
      <div class="card-body">
        <h6>会话超时</h6>
        <div class="d-flex align-items-center gap-2">
          <input type="number" id="inputSessionTimeout" value="30" min="5" max="1440" style="max-width:100px;">
          <label>分钟</label>
        </div>
      </div>
    </div>
  </div>
</div>
```

## 永久教训

| 教训 | 说明 |
|------|------|
| **validKeys 必须同步** | 新增 key 不在白名单 → POST 400，错误隐蔽 |
| **枚举映射必须文档化** | `smsVerify` ↔ `sys_reg_verify_method` 映射关系必须显式记录 |
| **独立 try/catch** | 多 key 并发加载时，一个失败不能导致全部失败 |
| **默认值的 GetAllConfigs 兜底** | 未配置的 key 在 GetAllConfigs 中也要有默认值 |
| **HTML 原型同步** | 代码改完必须同步更新原型，否则下次参考原型会误导 |

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0 | 2026-08-15 | 初始版本，基于 v3.4.10 经验沉淀 |
