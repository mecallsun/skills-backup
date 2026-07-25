# 基础资料列表加载 BUG 全链路终极汇总 — v2.13.154（唯一权威源）

> ✅ **本文档为「基础资料列表空白 / 加载失败」问题域的唯一权威源**，整合 v2.13.151 → v2.13.154 共 4 个版本迭代。历史文档 [195-v2.13.151](./195-设备档案记录加载失败防回归-v2.13.151.md)、[196-v2.13.152](./196-基础资料模块空记录防回归-v2.13.152.md) 均已加 banner 指向本文，其结论以本文为准。

> **日期**：2026-07-25
> **类型**：P0 BUG 修复（终极根因）+ 全链路梳理
> **改动文件**：`DormManage.Admin/Pages/Basics/Index.cshtml`（v2.13.153 删除 1 行重复声明 + v2.13.154 setActiveTab 分派 3 处）
> **生产包**：`publish-final/DormManage-v2.13.154_*.zip`（Release + Obfuscar 混淆 + R2R，已冒烟测试通过）

---

## 〇、版本演进时间线（一次读懂全过程）

| 版本 | 现象 | 归因 | 结论 |
|------|------|------|------|
| v2.13.151 | 设备档案/设备记录「加载失败，请刷新重试」 | `list-pagination.js` 未就绪时裸调 `listPager.update` 抛 TypeError → catch 吞真因 | 加 `safeUpdate`/`safeElement` 守卫 + catch 显示 `err.message`（**有效加固，仍有效**）|
| v2.13.152 | 基础资料**全部**二级菜单列表空白 | ❌ **误诊**为 `loadData`/`renderTable` 静默失败 | 加错误处理，但**这些代码从未执行**（脚本没解析成功）→ 用户反馈「仍有BUG」|
| **v2.13.153** | 同上（真因） | ✅ `let deviceDormOptions` **重复声明** → 整块 `<script>` SyntaxError → 块内所有函数/监听器失效 | 删除重复声明，10 个字典类 tab 恢复 |
| **v2.13.154** | 设备档案/设备记录点击 tab 不加载 | ✅ `setActiveTab` 只调 `loadData`，而 `typeToApi`/`getTableId` 不含这两类 → 提前 return | `setActiveTab` 按类型分派到 `loadDeviceMeters`/`loadEquipmentReadings` |

**一句话**：v2.13.152 修错了层级（症状在下游渲染层，真因在上游脚本解析层）；v2.13.153 消除 SyntaxError 让整块脚本复活；v2.13.154 补齐设备档案/设备记录的加载分派。至此基础资料 **12 个二级菜单全部正常**。

---

## 一、用户报告

> 「仍有BUG，查明原因：在基础资料 中的 部门管理 列表 没有显示数据表的记录，存在BUG，之前版本中是有显示列表记录的，并且 人员清单 有记录（以此渲染方法为开发规范参照 统一设计开发规范）证明数据库连接正确的；请查明原因后，再修复BUG，形成优化功能完善的文档，清理旧版过时的干扰描述。」

> 追加：「再将 部门管理 的同级菜单都进行同样的修复，包括 设备档案、设备记录 都需要修复。」

**关键线索**：
- 「**之前版本中是有显示列表记录的**」→ 是回归 BUG，某次改动引入。
- 「**人员清单有记录**」→ 数据库连接正常，后端 API 正常。
- 「部门管理」是基础资料的**默认 tab**，用户首先看到它空白。


---

## 二、根因：JavaScript `let` 变量重复声明导致整段脚本 SyntaxError

### 2.1 缺陷代码

`Basics/Index.cshtml` 的 `<script>` 块内出现了**重复的 `let` 声明**（v2.13.151 编辑时误粘贴）：

```javascript
// ========== v2.13.120 设备档案 ==========
let deviceDormOptions = [];

// ========== v2.13.120 设备档案 ==========
let deviceDormOptions = [];   // ← 第二次声明，SyntaxError！
```

### 2.2 为什么导致「所有列表空白」

1. ECMAScript 规范规定：**同一词法作用域内用 `let`（或 `const`）重复声明同名变量，抛 `SyntaxError: Identifier 'deviceDormOptions' has already been declared`。**
2. 该错误发生在**脚本解析（parse）阶段，早于任何语句执行**。浏览器会**丢弃整个 `<script>` 块**。
3. 因此块内定义的**所有函数全部不存在**：`loadData`、`renderTable`、`setActiveTab`、`showAddModal`、`editItem`、`deleteItem`、`saveData`……
4. 第 757 行的 `document.addEventListener('DOMContentLoaded', …)` 也在这个块内 → **监听器从未注册** → 页面加载时 `loadData('dept')` 从未被调用。
5. 结果：`<tbody id="deptTable">` 始终保持初始空白，**基础资料所有 12 个二级菜单列表全部空白**（不仅部门）。

### 2.3 为什么人员清单不受影响

`Personnel/Index.cshtml` 是**完全独立的 Razor 页面**，拥有自己独立的 `<script>` 块，与 Basics 的脚本互不干扰。所以人员清单正常显示 —— 这恰好证明「数据库连接正常、后端正常」，与用户判断一致。

### 2.4 后端验证（全部正常 ✅）

`curl` 实测 12 个端点全部返回正确数据（与 v2.13.152 一致）：

| 端点 | totalCount | 端点 | totalCount |
|------|-----------|------|-----------|
| `/api/basics/departments` | 8 | `/api/basics/attendance-types` | 6 |
| `/api/basics/buildings` | 2 | `/api/basics/meter-units` | 3 |
| `/api/basics/floors` | 6 | `/api/basics/residence-statuses` | 3 |
| `/api/basics/addresses` | 2 | `/api/basics/employment-statuses` | 3 |
| `/api/basics/employee-types` | 5 | `/api/basics/teams` | 11 |
| `/api/basics/device-meters` | 1 | `/api/basics/equipment-readings` | 3281 |

**结论**：BUG 100% 在前端脚本解析层，与后端/数据库无关。

---

## 三、v2.13.152 为何未能修复（误诊纠正）

v2.13.152（文档 196）针对同一现象，给 `loadData` / `renderTable` **添加了完整的错误处理**（HTTP 状态码检查、`success=false` 分支、`items` null check、异常显示 `err.message`、未知 type 兜底模板）。

这些加固**本身是正确的防御式编程**，但**无法修复本 BUG**，因为：

> **`loadData` / `renderTable` 所在的整个 `<script>` 块因 SyntaxError 从未被解析，块内代码一行都没执行过。** 给一段永远不会运行的代码加错误处理，自然看不到任何变化。

这是一次典型的**「在错误的层级修复」**：症状（列表空白）被观察到，但修复动作作用在**下游**（渲染逻辑），而真因在**上游**（脚本解析）。用户实测后反馈「仍有BUG」正是此原因。

> ⚠️ v2.13.152 的错误处理增强**予以保留**（它们对未来的运行时错误仍有价值），但其「已修复空记录 BUG」的结论作废，以本文档为准。

---

## 四、修复方案（v2.13.153）

删除重复的 `let deviceDormOptions = [];` 声明，只保留一处：

```javascript
// ========== v2.13.120 设备档案 ==========
// v2.13.153 修复：删除此处重复的 `let deviceDormOptions = [];` 声明。
// 同一作用域内 let 重复声明会抛 SyntaxError，导致整个 <script> 块解析失败，
// 块内所有函数（loadData/renderTable/setActiveTab...）与 DOMContentLoaded 监听器全部失效，
// 表现为「基础资料所有二级菜单列表空白」。
let deviceDormOptions = [];
```

**改动量**：1 个文件、删除 1 行重复代码（+ 说明注释）。

---

## 五、验证

### 5.1 静态扫描（防重复声明）

```bash
# 实际可执行的 let deviceDormOptions 声明数 = 1（另一处命中在注释里）
grep -n "let deviceDormOptions" DormManage.Admin/Pages/Basics/Index.cshtml
# 1155: // ...注释...
# 1159: let deviceDormOptions = [];   ← 唯一执行声明

# 全脚本重复顶层 let/const 声明扫描 = 0（跨函数同名局部变量属合法）
# 重复 function 定义扫描 = 0
```

### 5.2 运行时预期

1. 打开 `/Basics`（默认 tab=dept）→ 部门列表显示 8 条记录。
2. 切换其余 11 个二级菜单 → 各自列表正常显示。
3. 浏览器 F12 Console **无 `SyntaxError: Identifier 'deviceDormOptions' has already been declared`**。

---

## 六、永久教训

| # | 教训 |
|---|------|
| 1 | **`let`/`const` 重复声明是「整块脚本级」故障**，不是「单函数级」——一处重复声明会让同一 `<script>` 块内**所有**函数与事件监听器失效，症状是「整页 JS 不工作」而非「某个功能报错」。 |
| 2 | **修复前先确认代码是否真的在运行**。给 `loadData`/`renderTable` 加错误处理前，应先在 Console 确认这些函数已定义、`DOMContentLoaded` 已触发。若整块脚本因 SyntaxError 未加载，任何下游加固都是空转（v2.13.152 教训）。 |
| 3 | **「症状在下游，真因常在上游」**。列表空白（渲染层症状）的真因是脚本解析层。排查异步 UI 问题必须先看 Console 首个报错，通常上游的 SyntaxError 才是根，后续「函数未定义」都是它的次生现象。 |
| 4 | **复制粘贴代码块后必须删除模板残留**。本次重复的 `// ========== 设备档案 ========== / let deviceDormOptions = [];` 正是粘贴时连同上一段落一起复制未清理所致。Razor 内联 `<script>` 不经过 JS 打包器 / linter，**C# 编译 0 error 也无法发现 JS SyntaxError**——内联脚本应尽量精简，大段逻辑迁移到 `wwwroot/js/*.js` 便于工具校验。 |

---

## 七、附：与人员清单渲染规范的一致性（用户诉求「以此为统一设计开发规范参照」）

人员清单（`Personnel/Index.cshtml`）之所以稳健，是因其列表渲染由**服务端 Razor + 独立脚本**驱动，脚本块保持精简、无重复声明。基础资料修复后已恢复同等稳健度。后续统一开发规范建议：

- 内联 `<script>` 块顶层变量声明**唯一化**，避免复制粘贴引入重复 `let`/`const`。
- 列表加载逻辑保留 v2.13.152 的完整错误处理（HTTP/`success`/null/异常四类分支 + F12 指引）。
- 大段前端逻辑（如设备档案/设备记录 CRUD）优先抽到 `wwwroot/js/` 独立文件，借助工具链在构建期捕获 SyntaxError。

---

## 八、v2.13.154 增量 — 设备档案 / 设备记录 tab 点击不加载的连带修复

### 8.1 背景

v2.13.153 消除 SyntaxError 后，10 个字典类 tab（部门/楼栋/楼层/地址/员工类型/班次/员工班组/计量单位/住宿状态/在职状态）全部经 `loadData` 恢复。但对**设备档案（device）/ 设备记录（equipmentreading）**做全量真机测试时发现二者存在**独立的连带 BUG**。

### 8.2 根因

`setActiveTab(type)` 结尾统一调用 `loadData(type)`，而 `typeToApi` / `getTableId` 两个映射表**都不包含** `device` / `equipmentreading`：

```javascript
const typeToApi = { dept:'departments', ..., employment:'employment-statuses' }; // 无 device/equipmentreading
function getTableId(type){ const map={ dept:'deptTable', ..., employment:'employmentTable' }; return map[type]||''; } // 同上
```

因此 `loadData('device')` 会在开头因 `apiType`/`tableId` 无效直接 `return`。设备档案/设备记录的真实加载器是独立的 `loadDeviceMeters()` / `loadEquipmentReadings()`，**点击这两个 tab（`onclick="setActiveTab('device')"`）时从未被调用** → 列表空白。此前仅在带 `?tab=device` 整页刷新进入时，靠一段独立的 `DOMContentLoaded` 监听器加载，掩盖了点击路径的缺陷。

### 8.3 修复（`Basics/Index.cshtml`）

1. **`setActiveTab` 按类型分派加载器**：
   ```javascript
   if (type === 'device') { loadDeviceMeters(); }
   else if (type === 'equipmentreading') { loadEquipmentReadings(); }
   else { loadData(type); }
   ```
2. **首个 `DOMContentLoaded` 监听器简化**为仅 `setActiveTab(type)`（分派逻辑已内聚，移除原 if/else 中对 `loadData` 的重复调用，避免同一 tab 加载两次）。
3. **移除末尾第二个 `DOMContentLoaded` 监听器**中对 `loadDeviceMeters`/`loadEquipmentReadings` 的重复自动加载（初始加载已由 `setActiveTab` 统一分派）。

### 8.4 全量真机测试（jsdom 真 DOM 引擎 + 逐 tab 模拟点击）

服务端渲染页面 → jsdom 执行完整脚本 → 逐一 `setActiveTab(key)` 模拟点击 → 轮询等待渲染（远程 DB 响应 2–5s）→ 读实际数据行：

| 二级菜单 | 数据行 | 二级菜单 | 数据行 |
|---------|-------|---------|-------|
| 部门管理 | 8 ✅ | 计量单位 | 3 ✅ |
| 楼栋管理 | 2 ✅ | 住宿状态 | 3 ✅ |
| 楼层管理 | 6 ✅ | 在职状态 | 3 ✅ |
| 地址管理 | 2 ✅ | **设备档案** | 1 ✅ |
| 员工类型 | 5 ✅ | **设备记录** | 10（3281 条/页 10）✅ |
| 班次 | 6 ✅ | 员工班组 | 10（11 条/页 10，首页 10 行）✅ |

**12/12 全部正常渲染。** 员工班组首页 10 行、设备记录首页 10 行均为**分页正确行为**（每页 10 条），非缺陷。

### 8.5 追加教训

| # | 教训 |
|---|------|
| 5 | **「统一入口」必须覆盖所有分支**。`setActiveTab` 作为唯一 tab 切换入口，却把两个特殊类型漏给了只认字典类型的 `loadData`——统一分发器新增类型时，必须同步在分发处补分支，否则新类型静默失效。 |
| 6 | **初始加载路径 ≠ 点击加载路径**。设备档案靠 `?tab=device` 刷新时的独立监听器能加载，掩盖了点击 `setActiveTab` 不加载的缺陷。测试必须覆盖「点击切换」而不仅是「带参进入」。 |
| 7 | **真机测试要适配慢依赖**。远程 DB（172.16.0.100）单查询 2–5s，固定 `sleep` 会误判为空；应改为**轮询等待数据出现**，并把「分页首页行数」与「总记录数」区分清楚，避免误报。 |

---

## 九、真机测试与生产发布（v2.13.154 收官）

### 9.1 全量真机测试（12 个二级菜单）

用 jsdom 真 DOM 引擎加载服务端实际渲染的 `/Basics` 页面 → 执行完整脚本 → 逐一 `setActiveTab(key)` 模拟点击 → 轮询等待渲染，读实际数据行：

| 二级菜单 | 数据行 | 二级菜单 | 数据行 |
|---------|-------|---------|-------|
| 部门管理 | 8 ✅ | 计量单位 | 3 ✅ |
| 楼栋管理 | 2 ✅ | 住宿状态 | 3 ✅ |
| 楼层管理 | 6 ✅ | 在职状态 | 3 ✅ |
| 地址管理 | 2 ✅ | 设备档案 | 1 ✅ |
| 员工类型 | 5 ✅ | 设备记录 | 首页 10（共 3420，分页）✅ |
| 班次 | 6 ✅ | 员工班组 | 首页 10（共 11，分页）✅ |

→ **12/12 全部正常渲染**（员工班组/设备记录首页 10 行为正确分页，非缺陷）。

### 9.2 生产发布包与冒烟测试

- **构建**：`dotnet publish -c Release -r win-x64 --self-contained`，`Directory.Build.props` 自动应用 Obfuscar 混淆 + R2R 预编译；3 项目退出码 0。
- **产物**：`publish-final/DormManage-v2.13.154_*.zip`（190 MB，含 Admin/Api/TrayApp + CLAUDE.md + 本文）。
- **冒烟测试（运行正式 exe 而非 Debug）**：DB 连接正常 + admin 登录 302 + **正式产物渲染的内联脚本经 V8 解析 3 段全通过（0 SyntaxError / 0 重复声明）** + `/api/basics/departments` totalCount=8 → 确认修复经受住混淆、已包含在生产包。

---

## 十、统一列表渲染开发规范（落地用户诉求「以此为统一设计开发规范参照」）

> 以下规范同步固化到 `35-列表页面统一UI设计规范` §（AJAX 内联脚本列表渲染），为后续所有列表页开发的强制基线。

1. **内联 `<script>` 顶层声明唯一化**：`let`/`const`/`function` 严禁在同一脚本块内重复声明（一次重复 = 整块脚本 SyntaxError = 整页 JS 失效）。复制粘贴代码块后必须清理模板残留。
2. **统一切换入口必须覆盖所有类型分支**：如 `setActiveTab(type)` 这类唯一入口，新增数据类型时必须在分发处（`typeToApi`/`getTableId`/if-else）同步补齐分支，否则新类型静默失效。
3. **异步加载 UI 必须自带错误可见性**：`loadXxx` 的 catch 块既 `console.error(真因)` 又更新 DOM 显示 `err.message` + 「请查看浏览器控制台(F12)」；禁止吞异常、禁止用通用「加载失败」掩盖 TypeError/ReferenceError（保留 v2.13.151/152 的四类分支：HTTP 状态 / `success=false` / `items` null / 异常）。
4. **全局对象与 DOM 元素访问必须有守卫**：`typeof window.listPager !== 'undefined'`、`getElementById` 结果 null check（沿用 v2.13.151 `safeUpdate`/`safeElement`）。
5. **大段前端逻辑优先抽到 `wwwroot/js/*.js`**：Razor 内联 `<script>` 不经 JS 打包器/linter，**C# 编译 0 error 也发现不了 JS SyntaxError**；独立 js 文件可借工具链在构建期捕获。
6. **测试必须覆盖「点击切换」而非仅「带参进入」**，且适配慢依赖用轮询等待，区分分页首页行数与总记录数。

**参照实现**：人员清单（`Personnel/Index.cshtml`）——服务端 Razor + 独立精简脚本，无重复声明。基础资料修复后已达同等稳健度。
