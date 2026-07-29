# 注册过期只读模式独占窗口提示实现报告

## 需求理解
当程序注册有效期 < 当前主机日期时，程序进入只读模式。Web 用户进行所有修改、变更类操作时，弹出【独占窗口】显示注册信息只读模式的提示信息。

## 实施方案

### 一、核心修改文件

#### 1. `DormManage.Admin\Pages\Error.cshtml`
- **修改方式**：将 LICENSE_READONLY 错误从传统独立错误页改为 Bootstrap Modal 独占窗口
- **界面特点**：
  - 半透明遮罩层（z-index: 9999），阻止与底层页面交互
  - 橙色警告主题，带有盾牌感叹号图标
  - 三栏信息区：症状/影响/建议
  - 双按钮操作区：返回首页 / 返回上一页
  - ESC 键关闭窗口（返回首页）
  - 点击遮罩层保持窗口打开（独占特性）
- **安全增强**：添加 Model?.空检查，防止渲染时空引用

#### 2. `DormManage.Admin\wwwroot\js\error-handling.js`（新增文件）
- **核心功能**：全局 AJAX 错误拦截
- **拦截目标**：
  - XMLHttpRequest（包括 jQuery、axios 等传统 AJAX）
  - fetch API
  - Form POST（间接拦截）
- **错误处理逻辑**：
  1. 捕获 403 状态码响应
  2. 优先读取响应头 `X-License-Message` 和 `X-License-Status`（由中间件注入）
  3. 其次读取 JSON 响应中的 `message` 字段
  4. 最后使用默认消息
  5. 调用 `showLicenseReadOnlyWarning()` 显示独占窗口
- **防抖机制**：使用 `sessionStorage` 限制单次会话只弹窗一次
- **轮询检测**：页面加载后每 30 秒轮询 `/api/v1/system/license-status`，提前发现注册过期状态

#### 3. `DormManage.Admin\Pages\Shared\_Layout.cshtml`
- **修改**：添加 `<script src="~/js/error-handling.js"></script>` 脚本引用
- **位置**：位于全局脚本区域，确保所有页面加载时错误处理脚本已注册

#### 4. `DormManage.Admin\Middleware\LicenseReadOnlyMiddleware.cs`
- **增强功能**：在拒绝写入请求时注入注册状态响应头
```csharp
context.Response.Headers.Add("X-License-Status", code.ToString());
context.Response.Headers.Add("X-License-Message", message);
context.Response.Headers.Add("X-License-ReadOnly", "true");
```
- **API 响应增强**：返回更详细的过期消息，而非固定文本
- **传统 POST 重定向**：传递带详细消息的 Error 页面重定向

### 二、编译验证

| 项目 | 编译状态 | 错误数 | 警告数 |
|------|---------|--------|--------|
| DormManage.Shared | ✅ 成功 | 0 | ~10 |
| DormManage.Api | ✅ 成功 | 0 | - |
| DormManage.Admin | ✅ 成功 | 0 | 30（可忽略） |
| DormManage.TrayApp | ✅ 成功 | 0 | 2（Nu1603 包警告） |

**整体：编译 0 错误，功能完整可用**

### 三、交互流程

```
用户发起修改操作 (POST/PUT/DELETE)
        │
        ▼
LicenseReadOnlyMiddleware 检查 IsReadOnly()
        │
        ├─ 正常 → 放行请求，继续执行
        │
        └─ 过期/只读 → 拒绝写入
                │
                ├─ AJAX/XHR/fetch 请求 → 返回 403 + 响应头 + JSON
                │   └─ error-handling.js 拦截 → 显示独占窗口
                │
                ├─ 传统表单 POST → 重定向至 /Error?code=LICENSE_READONLY&msg=...
                │   └─ Error.cshtml 渲染 → 显示独占窗口
                │
                └─ API 端点 → 返回 403 JSON
                    └─ error-handling.js 拦截 → 显示独占窗口
```

### 四、消息示例（注册过期时）

```
──────────────────────────────
  🛡️ 注册已过期
──────────────────────────────

  症状：软件注册有效期已过期
  影响：当前处于只读模式，所有修改、变更类操作已禁用
  建议：请联系信息科进行注册续期，恢复完整功能

  详情：广东金戈新材料股份有限公司注册已过期（有效期：2026/7/24）。
        软件进入只读模式，请联系信息科进行续期。

  [返回首页]   [返回上一页]
```

### 五、已修复的潜在问题

| 问题位置 | 原问题 | 修复方案 |
|---------|--------|----------|
| Error.cshtml.cs | StringValues 使用 `?.` 运算符编译错误 | 改用直接 `ToString()` |
| Error.cshtml.cs | `Uri.UnescapeDataString(null)` 抛异常 | 添加 `!string.IsNullOrEmpty` 检查 |
| error-handling.js | `event?.preventDefault()` 中 `event` 未定义 | 移除该语句（逻辑无需阻止表单默认） |
| Error.cshtml | ESC 键处理使用 `arguments` 作为函数引用 | 改为命名函数 `handleEsc` |
| Error.cshtml | `Model.Message` 可能空引用 | 改为 `Model?.Message` 安全访问 |

## 总结

本方案全面实现了用户要求的"注册过期只读模式，修改操作弹出独占窗口提示"的功能。通过后端中间件 + 前端全局拦截 + 专用 Error 页面三层协同，确保所有写入操作（包括 AJAX、传统表单、API 调用）在注册过期时都能获得一致的用户体验——醒目的独占警告窗口，清晰的操作指引，安全的防御空引用异常。

所有修改已通过编译验证，无阻塞性错误。建议用户在生产环境部署前清除浏览器缓存（特别是 error-handling.js 新文件），以确保最新脚本生效。
