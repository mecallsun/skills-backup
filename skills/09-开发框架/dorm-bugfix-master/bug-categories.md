# BUG 类别详细分类（7 大类）

> Skill: dorm-bugfix-master
> 创建日期：2026-07-28
> 基于 v2.13.187-193 全部已知 BUG 案例整理

---

## 类别 1：发布目录同步问题 🔴 P0

### 1.1 发布目录双胞胎陷阱
- **症状**：用户报告修改无效（如"性别列没显示"、"床位文字没去掉"）
- **根本原因**：发布只更新 `release/latest/Admin/`，但 TrayApp 加载 `release/latest/TrayApp/Admin/`
- **修复**：新增 `scripts/sync_publish_to_trayapp.sh` 强制同步
- **出现频次**：v2.13.193 期间发生 2 次
- **文档**：`00-方案文档/230-发布目录双胞胎陷阱-TrayApp加载路径不一致-v2.13.193.md`

### 1.2 Shared DLL 不同步
- **症状**：Admin 启动报 `TypeLoadException: Could not load type 'Xxx'`
- **根本原因**：`DormManage.Shared.dll` 是旧版，缺少 v2.13.135 的 `RuntimeWindowGuard` 类
- **修复**：同步 Shared DLL 到所有子目录（4 处）
- **出现频次**：v2.13.191 期间发生 1 次
- **文档**：`00-方案文档/232-BUG解决经验与防错指南-v2.13.193综述.md`

### 1.3 发布命令默认输出路径错误
- **症状**：发布后 TrayApp 加载的是旧版本
- **根本原因**：开发者习惯发布到 `release/latest/Admin/`，但 TrayApp 加载 `release/latest/TrayApp/Admin/`
- **修复**：使用 `scripts/sync_publish_to_trayapp.sh` 强制同步

---

## 类别 2：隐私/权限语义问题 🔴 P0

### 2.1 v2.13.176 deny-by-default 翻转未实施
- **症状**：用户未授权隐私字段权限，但**仍能看到**隐私字段（如容量、住人数）
- **根本原因**：v2.13.176 文档设计了 deny-by-default，但代码层 `HasPrivacyFieldEnabledAsync` 仍按 v2.13.92 的 allow-by-default 实现
- **B.G.** 17+ 个版本 BUG 潜伏
- **修复**：重命名 `HasPrivacyFieldEnabledAsync` → `AllowDisplayPrivacyFieldsAsync`，逻辑反向
- **文档**：`00-方案文档/216-隐私字段保护语义翻转v2.13.176.md`、`231-隐私字段语义翻转终极修复-v2.13.193.md`

### 2.2 隐私字段 Dorms 接线缺失
- **症状**：Dorms/Index.cshtml 容量/在住人数列直接渲染，未调用 `IsFieldHiddenAsync`
- **根本原因**：v2.13.180 扩展了 21 个隐私字段，但只 seed 了 DB，没有 UI 接线
- **修复**：在 Dorms/Index.cshtml 和 Dorms/Details.cshtml 5 处添加 `IsFieldHiddenAsync` 调用
- **文档**：`00-方案文档/226-隐私字段权限UI接线缺失修复-v2.13.187.md`

### 2.3 RegStatus 字段缺失
- **症状**：托盘已注册，但 Web 端仍显示"试用模式"
- **根本原因**：`RegStateDto` 缺少 `RegStatus` 字段（v2.13.169 文档要求但未实现）
- **修复**：添加 `RegStatus` 字段 + 实现 `GetLicenseBanner()` 方法
- **文档**：`00-方案文档/228-注册状态显示错误修复-v2.13.191.md`

### 2.4 跨权限测试缺失
- **症状**：admin 测试通过，但普通用户仍能看到隐私字段
- **根本原因**：只用 admin 测试，遗漏未授权角色
- **修复**：必须同时测试 admin + 未授权角色
- **强制规则**：任何隐私字段修改必须跨权限测试

---

## 类别 3：注册/许可证问题 🔴 P0

### 3.1 v2.13.169 RegStatus 拆分未完成
- **症状**：Web 端显示错误的注册状态
- **根本原因**：文档设计但代码未实施
- **修复**：拆分 `RegStatus` 枚举（Unregistered=-1/Valid=1/Expired=2/Invalid=3）
- **文档**：`00-方案文档/228-注册状态显示错误修复-v2.13.191.md`

### 3.2 账号有效期判定错误（`>` 应为 `>=`）
- **症状**：账号**过期当天仍能登录**
- **根本原因**：`Today > ExpiresAt` 严格大于，账号当天还能用
- **修复**：改为 `Today >= ExpiresAt`，并新增 `UserExpiryHelper` 统一助手
- **文档**：`00-方案文档/233-账号有效期判定BUG修复-v2.13.193.md`

### 3.3 账号有效期字段类型混乱
- **症状**：编辑时 `datetime-local` 显示不完整，填写后不能保存
- **根本原因**：datetime-local 要求 `yyyy-MM-ddTHH:mm`，但用户只关心日期
- **修复**：改为 `type="date"`，后端用 `parsed.Date.AddDays(1).AddSeconds(-1)` 存为当天结束
- **文档**：`00-方案文档/233-账号有效期判定BUG修复-v2.13.193.md`

---

## 类别 4：UI 一致性问题 🟡 P1

### 4.1 用户管理面板与角色管理风格不一致
- **症状**：用户管理用彩色按钮（btn-outline-*），角色管理用纯文字按钮（op-btn）
- **根本原因**：v2.12.x 历史遗留样式未统一
- **修复**：统一为 op-btn + op-btn-sep
- **文档**：`00-方案文档/225-列表UI设计规范v2.13.190增量-操作列截断card-header按钮与最后登录拆分.md`

### 4.2 详情页缺性别列
- **症状**：当前入住人员列表没有性别列
- **修复**：在姓名后新增性别列，按 EmployeeId 关联 SysEmployee.Gender
- **文档**：`00-方案文档/227-当前入住人员新增性别列与床号简化-v2.13.188.md`

### 4.3 床位前缀冗余
- **症状**：床位列显示"床位 1"而不是"1"
- **修复**：移除"床位"前缀，只显示数字

### 4.4 列表行强制一行显示
- **症状**：长字段让表格变形
- **修复**：使用 `text-truncate` + `title` 属性 + `op-cell` 类（white-space: nowrap）

### 4.5 Card-header 操作按钮位置
- **症状**：新增按钮在 table 上方独立位置
- **修复**：移到 card-header 右侧

### 4.6 最后登录列拆分
- **症状**：合并的"最后登录"列难以解析
- **修复**：拆分为"最后时间" + "登录IP"两列（v2.13.186 规范）

---

## 类别 5：编译错误 🟡 P1

### 5.1 Razor 语法错误（@bg- 误识别为变量）
- **症状**：`@item.CurrentCount > 0 ? "bg-primary" : "bg-secondary"` 在字符串拼接中报错
- **根本原因**：Razor 解析器把 Razor 表达式 `@bg-` 当成了变量
- **修复**：使用 `if/else` Razor 块替代字符串拼接

### 5.2 TypeLoadException（DLL 版本不一致）
- **症状**：Admin 启动报 `Could not load type 'Xxx'`
- **根本原因**：Shared DLL 旧版，缺少新类
- **修复**：同步 Shared DLL 到所有子目录

### 5.3 CS0103 变量未定义
- **症状**：Razor `@bg-primary` 等被解析为变量
- **修复**：避免在字符串内用 `@` 开头

---

## 类别 6：数据/业务规则 🟡 P1

### 6.1 BCrypt 密码重置不生效
- **症状**：API 返回 success，但 hash 未变化
- **根本原因**：EF Core 上下文缓存了旧值
- **修复**：跨连接查询验证 hash

### 6.2 test:123456 登录失败（实际密码是 test123）
- **症状**：用户认为密码是 123456，但数据库中是 test123
- **根本原因**：之前 reset 操作未真正保存
- **修复**：通过 API 真正重置密码

### 6.3 权限界面显示不一致
- **症状**：角色管理显示权限勾选，用户管理不显示
- **修复**：统一使用相同的权限矩阵

---

## 类别 7：部署/运行环境 🟡 P1

### 7.1 TrayApp 加载子目录不一致
- **症状**：发布后修改无效
- **根本原因**：TrayApp 用 `..\Admin\` 相对路径加载子目录
- **修复**：sync 脚本同步

### 7.2 EF Core 缓存跨请求
- **症状**：API 修改不生效
- **修复**：DbContext 是 Scoped（每次请求新实例），但要跨连接查询验证

### 7.3 浏览器缓存
- **症状**：用户看到旧版页面
- **修复**：Ctrl+F5 强制刷新

### 7.4 端口冲突
- **症状**：5001/5100 端口被占用
- **修复**：taskkill + 重新启动

---

## 类别 8（新增）：跨组件一致性 🟡 P1

### 8.1 前端 type 与后端解析不一致
- **症状**：datetime-local 与 DateTime.TryParse 行为不一致
- **修复**：前后端都用 `yyyy-MM-dd` 格式

### 8.2 多处独立实现导致不一致
- **症状**：LoginAsync、OnValidatePrincipal、前端 Badge 三处都有 ExpiresAt 判断
- **根本原因**：3 处独立实现，没有统一助手
- **修复**：新增 `UserExpiryHelper` 统一助手

---

## 类别 9（新增）：开发工作流问题 🟡 P1

### 9.1 git 操作可能回滚
- **症状**：git checkout/stash pop 后修改丢失
- **修复**：Edit 后立即 `git diff` 确认

### 9.2 发布流程缺少同步步骤
- **症状**：只 `dotnet publish`，没 cp 到兄弟目录
- **修复**：使用 sync 脚本

### 9.3 缺少自动化测试
- **症状**：每次发布都靠手动验证
- **修复**：CI/CD 集成发布同步

---

## 严重度评级

| 等级 | 描述 | 响应时间 | 案例 |
|------|------|----------|------|
| 🔴 P0 | 核心功能不可用/数据泄露 | 立即修复 | 发布双胞胎、隐私字段反转 |
| 🟡 P1 | 重要功能 BUG | 24h 内 | UI 不一致、TypeLoadException |
| 🟢 P2 | 一般优化 | 下个版本 | 文档优化、注释补充 |
| ⚪ P3 | 建议 | 有空再说 | 重构、性能调优 |

---

## 触发本 Skill 时的优先级判断

1. 用户报告"修改无效"或"发布后看不见效果" → 加载 `bug-categories.md#1 发布目录同步问题`
2. 用户报告"权限不生效"或"隐私字段仍显示" → 加载 `bug-categories.md#2 隐私/权限语义问题`
3. 用户报告"启动失败"或"找不到类" → 加载 `bug-categories.md#7 部署/运行环境`
4. 用户报告"密码错"或"登录错" → 加载 `bug-categories.md#6 数据/业务规则`

---

**使用建议**：每个 BUG 修复前都应先查看本文件确定类别，再加载对应的 `known-bugs.md` 查看是否已有类似案例。