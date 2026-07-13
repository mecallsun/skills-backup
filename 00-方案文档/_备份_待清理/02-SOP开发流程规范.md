# 金戈宿舍管理系统 — 开发 SOP 流程规范

> **版本**：v2.10 附录  
> **生效日期**：2026-07-10  
> **目的**：将开发流程固化为可复用的标准动作，确保「方案 → 原型 → 代码 → 交付」四阶段无遗漏。

---

## 1. SOP 全景（5 阶段 12 步骤）

```
┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐
│ ① 方案设计 │ →  │ ② 需求规格 │ →  │ ③ HTML原型 │ →  │ ④ 代码实现 │ →  │ ⑤ 编译交付 │
│   顶层架构 │    │   详细字段 │    │   快速体验 │    │   业务落地 │    │   测试打包 │
└────────────┘    └────────────┘    └────────────┘    └────────────┘    └────────────┘
   架构师           产品经理          UI/前端           后端/全栈         运维/QA
```

---

## 2. 阶段 ① — 方案设计（30%）

### 步骤 1.1：架构选型

| 维度 | 决策 | 备注 |
|------|------|------|
| 技术栈 | .NET 8 + EF Core + Razor + Bootstrap 5 | 已固化 |
| 数据库 | SQLite（开发）/ SQL Server（生产） | 双驱动 |
| 部署 | EXE 自托管 + 托盘守护 | 零 IIS 依赖 |
| 鉴权 | JWT（API）+ Cookie（Web） | 双轨制 |

### 步骤 1.2：模块拆分

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
- `01-技术架构与系统开发方案.md` 第 N 章（本次仅涉及 19/20 章）

---

## 3. 阶段 ② — 需求规格（25%）

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

### 步骤 2.4：删除无关信息

需求文档应**只包含本次交付相关内容**：
- ❌ 删除未来规划章节
- ❌ 删除与本次模块无关的章节
- ❌ 删除未实现的扩展功能
- ✅ 保留字段表、API 契约、页面跳转图
- ✅ 保留业务规则、计算公式

### ✅ 阶段 ② 验收物
- `03-需求规格-v2.10.md`（本次交付）

---

## 4. 阶段 ③ — HTML 原型（20%）

### 步骤 3.1：原型设计原则

> **目标**：用最少代码验证 UI 与交互逻辑，无需后端即可演示

| 原则 | 说明 |
|------|------|
| 单文件 | 每个页面一个 .html，CDN 引用 Bootstrap |
| 静态数据 | 用 mock JSON / 硬编码数据填充 |
| 跳转用 # | 链接 target="_self" + href="#page-name" |
| 保留 Razor 字段 | input name/id 与后续 Razor 字段对齐 |
| 中文界面 | 与最终交付一致 |

### 步骤 3.2：页面清单（按本次需求）

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

### 步骤 3.3：跳转矩阵

| 源页面 | 操作 | 目标页面 |
|--------|------|---------|
| personnel/list | +新增 | personnel/create |
| personnel/list | 编辑 | personnel/edit?id={id} |
| personnel/list | 离职 | personnel/list（刷新） |
| personnel/list | 📥导入 | personnel/import |
| personnel/list | 📤导出 | 直接下载 Excel（mock） |
| billing/standards | +新增 | billing/standard-form |
| billing/standards | 编辑 | billing/standard-form?id={id} |
| billing/dorm-bills | 生成账单 | billing/dorm-bills（提示刷新） |
| billing/dorm-bills | 导出 | 直接下载 |
| billing/employee-bills | 生成分摊 | billing/employee-bills（提示刷新） |

### 步骤 3.4：原型验收

由产品/业务方逐项核对：
- ✅ 字段是否齐全（与需求规格 2.2 节一致）
- ✅ 按钮动作是否齐全（导入/导出/发布/生成）
- ✅ 跳转逻辑是否正确（与跳转矩阵一致）
- ✅ 中文文案是否符合业务习惯
- ✅ 表格列顺序、状态颜色、Badge 样式

### ✅ 阶段 ③ 验收物
- `04-HTML原型/` 目录下所有页面
- 通过产品/业务方验收签字

---

## 5. 阶段 ④ — 代码实现（15%）

> **前置条件**：原型已通过验收！否则禁止进入代码实现阶段

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

### 步骤 4.3：基线对照

实现时**打开 HTML 原型对照**：
- View 的 input name/id 与原型 HTML 一致
- Controller 的 ActionResult 与跳转矩阵一致
- API 端点路径与需求规格 2.2 节一致

### ✅ 阶段 ④ 验收物
- 4 个 EXE 全部编译通过（0 错误）

---

## 6. 阶段 ⑤ — 编译交付（10%）

### 步骤 5.1：编译

```bash
dotnet build DormManage.Service.sln -c Release    # 0 错误
dotnet publish Api    -c Release -r win-x64 --self-contained true -o publish-final/Api
dotnet publish Admin  -c Release -r win-x64 --self-contained true -o publish-final/Admin
dotnet publish TrayApp -c Release -r win-x64 --self-contained true -o publish-final/TrayApp
dotnet publish Bootstrapper -c Release -r win-x64 --self-contained true -o publish-final/Bootstrapper
```

### 步骤 5.2：冒烟测试

```
✅ Api 启动 → /health 返回 200
✅ Api 启动 → /api/v1/personnel/dictionaries 返回完整字典
✅ Api 启动 → /api/v1/billing/standards/active 返回当前标准
✅ Admin 启动 → /Account/Login 页面可访问
✅ Bootstrapper → 自动解压 + 启动 TrayApp
```

### 步骤 5.3：交付包打包

- `Embedded.zip`（包含 Api/Admin/TrayApp）
- `DormManage.Bootstrapper.exe` 引导器
- `README.md` 部署说明

### 步骤 5.4：交付清单

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

### ✅ 阶段 ⑤ 验收物
- publish-final/ 完整目录
- README.md 部署说明

---

## 7. SOP 流程总结卡

| 阶段 | 工作量 | 关键产出 | 阻塞条件 |
|------|--------|---------|---------|
| ① 方案设计 | 30% | 架构图、ER 图、API 表 | — |
| ② 需求规格 | 25% | 业务规则、字段表、跳转矩阵 | ① 未完成 |
| ③ HTML 原型 | 20% | 静态页面 | ② 未完成 |
| ④ 代码实现 | 15% | 编译通过 | ③ 未验收 |
| ⑤ 编译交付 | 10% | 发布包 + README | ④ 未通过 |

---

## 8. 与本次交付的对照

| 阶段 | 状态 | 证据 |
|------|------|------|
| ① 方案设计 | ✅ | 7000 行方案文档第 19/20 章 |
| ② 需求规格 | 🔄 正在执行 | 见 `03-需求规格-v2.10.md` |
| ③ HTML 原型 | 🔄 正在执行 | 见 `04-HTML原型/` |
| ④ 代码实现 | ✅ | 已落地（Personnel + Billing 全套） |
| ⑤ 编译交付 | ✅ | `publish-final/` 部署包 + README |

> 注：本 SOP 改进将在 **v2.11 及以后版本**严格执行；本次 v2.10 已在阶段 ④⑤ 落地，阶段 ②③ 补齐文档。