# 金戈宿舍管理系统 — 软件开发 SOP 流程规范

> **版本**：v3.0  
> **生效日期**：2026-07-11  
> **目的**：将软件开发流程固化为可复用的标准动作，确保「开发方案 → 功能需求 → 原型设计 → 编程开发 → 功能测试 → 交付部署」六阶段无遗漏，所有软件开发必须严格遵守此 SOP 流程。

---

## 1. SOP 全景（6 阶段 18 步骤）

```
┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐
│ ① 开发方案 │ →  │ ② 功能需求 │ →  │ ③ 原型设计 │ →  │ ④ 编程开发 │ →  │ ⑤ 功能测试 │ →  │ ⑥ 交付部署 │
│   顶层架构 │    │   详细规格 │    │   快速验证 │    │   业务落地 │    │   验证确认 │    │   打包发布 │
└────────────┘    └────────────┘    └────────────┘    └────────────┘    └────────────┘    └────────────┘
   架构师           产品经理          UI/前端           后端/全栈         QA 测试         运维/部署
```

### 1.1 阶段工作量分配

| 阶段 | 工作量占比 | 关键产出 | 阻塞条件 |
|------|-----------|---------|---------|
| ① 开发方案 | 20% | 架构图、ER 图、API 表、技术选型 | — |
| ② 功能需求 | 20% | 需求规格文档、字段表、业务规则、跳转矩阵 | ① 未完成 |
| ③ 原型设计 | 15% | HTML 原型页面、产品/业务方验收签字 | ② 未完成 |
| ④ 编程开发 | 25% | 编译通过的源代码 | ③ 未验收 |
| ⑤ 功能测试 | 10% | 测试用例执行报告、缺陷修复 | ④ 未通过 |
| ⑥ 交付部署 | 10% | 发布包、部署说明、用户手册 | ⑤ 未通过 |

---

## 2. 阶段 ① — 开发方案（20%）

### 步骤 1.1：技术选型决策

| 维度 | 决策 | 备注 |
|------|------|------|
| 技术栈 | .NET 8 + EF Core + Razor + Bootstrap 5 | 已固化 |
| 数据库 | SQLite（开发）/ SQL Server（生产） | 双驱动 |
| 部署 | EXE 自托管 + 托盘守护 | 零 IIS 依赖 |
| 鉴权 | JWT（API）+ Cookie（Web） | 双轨制 |

### 步骤 1.2：模块拆分与依赖设计

按业务边界划分模块，输出**模块依赖图**：

```
Personnel 模块 ──┐
                 ├─→ Shared（Models/DbContext/Security）
Billing 模块 ────┤
                 ├─→ Api（Controllers/Services）
Dorm/Meter 模块 ─┘   └─→ Admin（Controllers/Views/Services）
```

### 步骤 1.3：数据模型设计

输出 **ER 图** + **字段表**（类型/约束/索引/外键）

### 步骤 1.4：接口契约定义

输出 **API 端点表**（路径/方法/请求/响应/错误码）+ **页面跳转矩阵**

### ✅ 阶段 ① 验收物

- [x] 架构选型决策文档
- [x] 模块依赖图
- [x] ER 图 + 字段表
- [x] API 端点表 + 跳转矩阵

---

## 3. 阶段 ② — 功能需求（20%）

### 步骤 2.1：梳理业务需求

模板：
```
【业务背景】为什么要做这个功能
【用户角色】谁会用
【核心场景】主要操作路径
【业务规则】关键约束与计算公式
【边界条件】异常 / 边界 / 性能要求
```

### 步骤 2.2：功能详细说明

按页面/接口拆解，每个单元包含：
- **输入**：字段、校验规则
- **处理**：业务逻辑伪代码
- **输出**：响应字段、跳转目标
- **权限**：需要哪些 Permission:Code

### 步骤 2.3：开发要求说明

- **代码规范**：命名约定 / 注释密度 / 异常处理
- **数据层要求**：索引 / 事务 / 并发
- **接口层要求**：HTTP 状态码 / 错误信封 / 鉴权
- **UI 要求**：Bootstrap 5 风格 / 响应式 / 中文界面

### 步骤 2.4：需求文档精简

需求文档应**只包含本次交付相关内容**：
- ❌ 删除未来规划章节
- ❌ 删除与本次模块无关的章节
- ❌ 删除未实现的扩展功能
- ✅ 保留字段表、API 契约、页面跳转图
- ✅ 保留业务规则、计算公式

### ✅ 阶段 ② 验收物

- [x] 功能需求规格文档（`XX-需求规格-vX.XX.md`）
- [x] 字段表（与原型字段一一对应）
- [x] API 契约定义
- [x] 业务规则清单

---

## 4. 阶段 ③ — 原型设计（15%）

### 步骤 3.1：原型设计原则

> **目标**：用最少代码验证 UI 与交互逻辑，无需后端即可演示

| 原则 | 说明 |
|------|------|
| 单文件 | 每个页面一个 .html，CDN 引用 Bootstrap |
| 静态数据 | 用 mock JSON / 硬编码数据填充 |
| 跳转用 # | 链接 target="_self" + href="#page-name" |
| 保留 Razor 字段 | input name/id 与后续 Razor 字段对齐 |
| 中文界面 | 与最终交付一致 |

### 步骤 3.2：页面清单规划

按本次需求规划页面结构：
```
prototype/
├── index.html              # 导航首页（功能入口卡片）
├── personnel/
│   ├── list.html           # 人员清单（筛选 + 分页 + 表格）
│   ├── create.html         # 新增人员表单
│   ├── edit.html           # 编辑人员表单
│   └── import.html         # 3 步导入向导
├── billing/
│   ├── standards.html      # 费用标准列表
│   ├── standard-form.html  # 新增/编辑费用标准
│   ├── dorm-bills.html     # 宿舍费用清单
│   └── employee-bills.html # 员工分摊费用
└── assets/
    └── mock-data.js        # Mock 数据（员工/账单）
```

### 步骤 3.3：跳转矩阵设计

| 源页面 | 操作 | 目标页面 |
|--------|------|---------|
| personnel/list | +新增 | personnel/create |
| personnel/list | 编辑 | personnel/edit?id={id} |
| personnel/list | 离职 | personnel/list（刷新） |
| personnel/list | 📥导入 | personnel/import |
| personnel/list | 📤导出 | 直接下载 Excel（mock） |

### 步骤 3.4：原型验收

由产品/业务方逐项核对：
- ✅ 字段是否齐全（与需求规格一致）
- ✅ 按钮动作是否齐全（导入/导出/发布/生成）
- ✅ 跳转逻辑是否正确（与跳转矩阵一致）
- ✅ 中文文案是否符合业务习惯
- ✅ 表格列顺序、状态颜色、Badge 样式

### 步骤 3.5：原型 JavaScript 验证（重要）

> ⚠️ **必须检查**：页面 JavaScript 是否与 `mock-data.js` 中的变量名冲突

| 检查项 | 操作 |
|--------|------|
| 1. 变量名是否以模块前缀开头？ | 如 `BK_`, `EMP_`, `DORM_` |
| 2. 是否避免了 `STATUS_MAP`, `STATUS_BADGE`？ | 改用 `BK_STATUS_TEXT` |
| 3. 是否避免了 `BOOKING_STATUSES`, `BOOKING_STATUS_BADGE`？ | 改用 `BK_STATUS_TEXT`, `BK_STATUS_CLASS` |
| 4. 页面初始化是否等待数据加载完成？ | 使用 `initPage()` 模式 |

**详见**：`25-统一UI设计规范v2.0.md` 第 20 章 JavaScript 变量命名规范

### ✅ 阶段 ③ 验收物

- [x] HTML 原型页面（`04-HTML原型/` 目录下所有页面）
- [x] 通过产品/业务方验收签字

---

## 5. 阶段 ④ — 编程开发（25%）

> **⚠️ 前置条件**：原型已通过验收！否则禁止进入编程开发阶段

### 步骤 4.1：分层实现顺序

```
Shared (Models → DbContext → DTOs → Service)
   ↓
Api (Controller → 路由注册)
   ↓
Admin (Controller → View → 静态资源)
   ↓
TrayApp / Bootstrapper（如需要）
```

### 步骤 4.2：代码规范

- **命名**：类 PascalCase / 方法 PascalCase / 私有字段 _camelCase / 局部变量 camelCase
- **注释**：公共方法必须有 XML 文档注释
- **异常**：业务异常用 InvalidOperationException，控制器捕获后返回 ApiResponse.Error(code, msg)
- **事务**：批量操作使用 db.Database.BeginTransactionAsync()
- **日志**：关键操作（生成账单/发布）输出 Console.WriteLine

### 步骤 4.3：基线对照开发

实现时**必须打开 HTML 原型对照**：
- View 的 input name/id 与原型 HTML 一致
- Controller 的 ActionResult 与跳转矩阵一致
- API 端点路径与需求规格一致

### 步骤 4.4：编译验证

```
dotnet build DormManage.sln -c Release    # 必须 0 错误
```

### ✅ 阶段 ④ 验收物

- [x] 4 个 EXE 全部编译通过（0 错误）
- [x] 原型与代码基线对照报告（`05-原型与代码基线对照.md`）

---

## 6. 阶段 ⑤ — 功能测试（10%）

### 步骤 5.1：冒烟测试

```
✅ Api 启动 → /health 返回 200
✅ Api 启动 → /api/v1/personnel/dictionaries 返回完整字典
✅ Api 启动 → /api/v1/billing/standards/active 返回当前标准
✅ Admin 启动 → /Account/Login 页面可访问
✅ Bootstrapper → 自动解压 + 启动 TrayApp
```

### 步骤 5.2：功能用例测试

按需求规格文档逐项验证：

| 用例 | 输入 | 预期输出 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| 新增人员 | 工号/姓名/部门/类型 | 创建成功，返回列表页 | | |
| 编辑人员 | 修改姓名 | 更新成功，列表显示新姓名 | | |
| 删除人员 | 点击离职 | Status=2，列表不再显示 | | |
| Excel导入 | 标准模板上传 | 批量创建，导入报告 | | |
| 生成账单 | 选择月份 | 创建宿舍账单记录 | | |

### 步骤 5.3：边界与异常测试

- ✅ 空字段提交 → 提示必填
- ✅ 重复工号 → 提示已存在
- ✅ 无效日期范围 → 提示校验失败
- ✅ 网络断开 → 提示重试
- ✅ 权限不足 → 提示无权限

### ✅ 阶段 ⑤ 验收物

- [x] 测试用例执行报告
- [x] 缺陷修复记录
- [x] 功能测试通过签字

---

## 7. 阶段 ⑥ — 交付部署（10%）

### 步骤 7.1：发布打包

```bash
dotnet publish Api    -c Release -r win-x64 --self-contained true -o publish-final/Api
dotnet publish Admin  -c Release -r win-x64 --self-contained true -o publish-final/Admin
dotnet publish TrayApp -c Release -r win-x64 --self-contained true -o publish-final/TrayApp
dotnet publish Bootstrapper -c Release -r win-x64 --self-contained true -o publish-final/Bootstrapper
```

### 步骤 7.2：交付包打包

- `Embedded.zip`（包含 Api/Admin/TrayApp）
- `DormManage.Bootstrapper.exe` 引导器
- `README.md` 部署说明

### 步骤 7.3：交付清单

```
publish-final/
├── DormManage.Bootstrapper.exe
├── Embedded.zip
├── Admin/      ← Web 后台 EXE
├── Api/        ← 后端 API EXE
├── TrayApp/    ← 托盘守护 EXE
├── Bootstrapper/
└── README.md   ← 部署说明
```

### ✅ 阶段 ⑥ 验收物

- [x] `publish-final/` 完整目录
- [x] `README.md` 部署说明
- [x] 用户操作手册（如需要）

---

## 8. SOP 流程总结卡

| 阶段 | 工作量 | 关键产出 | 阻塞条件 | 验收物 |
|------|--------|---------|---------|--------|
| ① 开发方案 | 20% | 架构图、ER图、API表 | — | 技术方案文档 |
| ② 功能需求 | 20% | 需求规格、字段表、规则 | ①未完成 | 需求规格文档 |
| ③ 原型设计 | 15% | HTML原型 | ②未完成 | 原型+验收签字 |
| ④ 编程开发 | 25% | 编译通过 | ③未验收 | 源代码+编译报告 |
| ⑤ 功能测试 | 10% | 测试通过 | ④未通过 | 测试报告 |
| ⑥ 交付部署 | 10% | 发布包 | ⑤未通过 | 部署包+说明 |

---

## 9. 文档命名规范

| 文档类型 | 命名格式 | 示例 |
|---------|---------|------|
| 需求规格 | `XX-需求规格-vX.XX.md` | `03-人员清单需求-v2.11.md` |
| 原型对照 | `05-原型与代码基线对照.md` | 同名 |
| 测试报告 | `XX-测试报告-vX.XX.md` | `03-人员清单测试报告-v2.11.md` |
| 部署说明 | `README.md` | 根目录 |

---

## 10. 版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v3.1 | 2026-07-12 | 新增原型 JavaScript 验证检查项，引用统一 UI 规范第 20 章 |
| v3.0 | 2026-07-11 | 更新为 6 阶段 SOP（开发方案→功能需求→原型设计→编程开发→功能测试→交付部署） |
| v2.10 | 2026-07-10 | 初始 5 阶段 SOP |
