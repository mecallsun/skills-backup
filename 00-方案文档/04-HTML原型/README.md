# HTML 原型 — v2.12.3（共用页头 + Tab 页签切换 + 系统集成 + 筛选条件持久化缓存 + 7 张 KPI + 考勤班次 + 筛选优化 + 住宿状态 + 表头顺序 + 看板布局重构 + 高度拉伸 + 员工分摊精确计算 + 住宿账单筛选 + 数据看板月度选择 + 入住段拆分两列 + 入住率排名 TOP 15（%） + 抄表"未抄表"状态 + 抄表"未完成"状态 + **统一 UI 导航栏** + **修改办理登记数据关联可视化** + **KPI 入住人数/异常人员 数据逻辑修复 v2.11.6** + **v2.13.98 班次简称显示**）

> **本次 v2.12.3 重大升级（2026-07-12）**：
> - ✅ **架构精简**：所有 26 个原型页面统一升级为「共用页头 + Tab 页签切换」**三层架构**（v2.12.3 移除原 Tier 2 紧凑型图标导航条）
>   - **Tier 1 顶部品牌栏** (48px)：Logo + 品牌名 + 版本号 + 用户胶囊 + 退出
>   - **Tier 2 Tab 页签栏** (45px)：10 个固定菜单 Tab，自动根据 URL 推断激活，禁止关闭
>   - **Tier 3 页面内容区**：图标 + 标题 + 总数 + 操作按钮
> - ✅ **新建 `_shared/` 目录**：
>   - `layout.html` — 共用页头模板
>   - `layout-tab.css` — Tab / 品牌栏样式（共享样式库）
>   - `tab-bar.js` — Tab 页签栏渲染 + 状态管理（Tier 2）
>   - `storage-keys.js` — localStorage key 常量定义（与 §11 筛选缓存共用 userId 隔离）
>   - `migrate.py` / `cleanup.py` / `inject.py` — 批量迁移工具
> - ⚠️ **renderNav() 标记为废弃**：保留以兼容旧引用，新代码请使用 `mountTabBar()`
> - 📘 完整规范参见 [`00-方案文档/37-共用页头与Tab页签导航设计规范-v2.12.md`](../37-共用页头与Tab页签导航设计规范-v2.12.md)（v2.13.24 已删除原 §4 紧凑型图标导航条整章）

> **目的**：在编码前快速预览 UI 与交互逻辑；与最终 Razor 视图的字段、跳转、按钮动作保持一致。
> **本次更新（2026-07-12）**：
> - **v2.11.14 BUGFIX 列表筛选"房号"条件失效修复**：原型 `booking/index.html` 的 `getFiltered()` 函数存在 JS 运算符优先级 BUG——`!(a > -1)` 被解析为 `(!a) > -1`，当 `indexOf` 返回 0（命中首字符，如输入"D"匹配 `D-085`）时 `!0=true` 且 `true > -1=true`，导致条件恒真，所有记录被错误排除；修复方案：将 `!(b.dormCode || "").toLowerCase().indexOf(dc) > -1` 改写为 `(b.dormCode || "").toLowerCase().indexOf(dc) === -1`（显式"未找到"判断）；同步修复同文件中 `keyword` / `department` 字段相同 BUG 模式；需求文档新增 §14 BUGFIX 章节（根因分析 + 验收用例 + 防退化建议）。
> - **v2.11.15.c 样式优化 抄表记录列表用量单元格亮蓝色高亮 + 同风格同高度**：原型 `meter/index.html` 与 `DormManage.Admin/Pages/Meter/Index.cshtml` 中，对**抄表数为 0 或未抄表项**的用量单元格（冷水/热水/电），将原有的"**框线风格**"（橙色文字 + 浅橙背景 + 橙色边框）优化为"**文本背景风格**"（**深蓝文字 #084c61 + 亮蓝背景 #cfe2ff + 无边框**），**背景风格及高度与状态列 Badge 完全一致**（`padding 0.35em 0.65em` + `line-height 1` + `font-size 0.75em` + `border-radius 0.375rem`），与状态列 Badge 视觉上同风格同高度（仅颜色不同用以区分语义）；需求文档 §27 §10.6 + §11.7 完整记录变更对比与验收用例。
> - **v2.11.12 操作列"退房"按钮显示条件扩展**：当记录的 **Type = 2 退房 && Status = 1 预约** 时，在"修改"与"撤销预约"之间显示黄色 `btn-outline-warning` "**退房**"按钮（一键完成预约退房）；点击调用新接口 `POST /api/v1/bookings/{id}/confirm-checkout`，服务端将 Status 即时变更为 3 已退房，并同步更新 `Registrar=当前登录账号`、`RegistrationDate=now()`、`UpdatedAt=now()`，联动 `SysEmployee.DormCode=NULL`（员工变为未分配住宿）；保留 v2.11.7 中 Status=2 在宿时显示退房按钮的既有规则；最终操作列顺序：**修改 → 撤销退房(v2.11.10) → 入住 → 退房(v2.11.12) → 撤销预约(v2.11.11)**。
> - **v2.11.11 操作列最右侧新增"撤销"按钮（撤销预约）**：当记录的 **Status = 1 预约** 时，操作列**最右侧**新增红色 `btn-outline-danger` "**撤销**"按钮；点击调用新接口 `POST /api/v1/bookings/{id}/cancel-reservation`，服务端将 Status 即时变更为 4 已取消，并同步更新 `Registrar=当前登录账号`、`RegistrationDate=now()`、`UpdatedAt=now()`（**无需校验房间余量**，因为该记录从未入住）；与 v2.11.10 "撤销退房"按钮（同文字"撤销"但语义不同）通过状态/位置/颜色三个维度区分：v2.11.10 青色在中间位置针对已退房记录的"误操作回退"，v2.11.11 红色在最右侧针对预约记录的"主动取消"；最终操作列顺序：**修改 → 撤销退房(v2.11.10) → 入住 → 退房 → 撤销预约(v2.11.11)**，两个"撤销"按钮在不同状态下互斥显示。
> - **v2.11.10 操作列新增"撤销"按钮（撤销退房）**：当记录的 **入退日期 = 服务端当天日期**（`BookingDate == today()`）且 **Status = 3 已退房** 时，操作列新增青色 `btn-outline-info` "**撤销**"按钮，位置在"修改"按钮**右侧**；点击调用新接口 `POST /api/v1/bookings/{id}/undo-checkout`，**服务端先校验住宿房号余量**——**有余量**则 Status 即时变更为 2 在宿，同步更新 `Registrar=当前登录账号`、`RegistrationDate=now()`、`UpdatedAt=now()`，联动恢复 `SysEmployee.DormCode`；**无余量**则前端弹窗提示**"床位已满，撤销退房失败！"**且不修改任何字段；最终操作列顺序：**修改 → 撤销 → 入住 → 退房**。
> - **v2.11.9 操作列按钮位置调整**：将"入住"按钮位置由原"修改"**左侧**调整为"修改"**右侧**；最终操作列从左至右顺序：**修改 → 入住 → 退房**（"修改"贴近登记时间列，符合"先改后确认"的操作直觉；"入住"作为快速确认放在"修改"右侧，"退房"作为退出动作放在最右）。
> - **v2.11.8.b 入住按钮房间床位余量校验文案优化**：点击"入住"按钮后，后台先校验该预约的住宿房号余量——**有余量**则即时变更 Status 为在宿并同步登记人/登记时间；**无余量**则前端弹窗提示**"床位已满，请更换其他房间"**（v2.11.8 原"房间 {dormCode} 余量不足"措辞不够友好）；原型 `confirmCheckin()` 增加 mock 演示分支。
> - **v2.11.8 操作列新增"入住"快速确认按钮**：当记录的 **Type=1 入住 && Status=1 预约** 时，操作列新增绿色 `btn-outline-success` "**入住**"按钮，点击调用新接口 `POST /api/v1/bookings/{id}/confirm-checkin`，服务端即时将 Status 从 1 预约变更为 2 在宿，并同步更新 `Registrar=当前登录账号`、`RegistrationDate=now()`、`UpdatedAt=now()`，联动更新 `SysEmployee.DormCode`；其他状态（2 在宿 / 3 已退房 / 4 已取消）不显示该按钮；业务含义：将"未来预约入住"快速确认为"当前在宿"，省去重复走 `/Booking/CheckIn` 流程。
> - **v2.11.7.b 操作列权限规则优化**：将"修改"只读按钮的 `bi-lock-fill` 🔒 图标移除，**仅保留"修改"文本**（disabled 灰色 `btn-outline-secondary` + title="仅预约状态可修改"），符合「按钮即按钮，文本即文本」的极简风格；仅 Status=1 预约时显示可点击的蓝色 `btn-outline-primary` 修改按钮；其他状态（2/3/4）一律只读 disabled。
> - **v2.11.7 操作列权限规则**：① `booking/index.html` 列表操作列的"编辑"按钮文本更名为"**修改**"；② 新增"修改"按钮权限规则：**仅 Status=1 预约**时可点（蓝色 `btn-outline-primary`），其他状态（2 在宿 / 3 已退房 / 4 已取消）一律 **disabled 锁定**（灰色 `btn-outline-secondary` + 悬停 title="仅预约状态可修改"）；③ 同时确认既有规则——"退房"仅 Status=2 在宿时显示、"删除"仅 Status=1/4 时显示。
> - **v2.11.5.b 修改办理登记数据关联可视化样式规范化**：① 解决"乱码"显示问题——将表单重构为**双卡片布局**（"基础信息（只读）"卡片 + "可编辑字段"卡片）；② 只读字段采用 `info-item` 标准结构（label 110px + value flex:1），不再有徽章错乱；③ 数据源标识改为 **label 后的图标徽章 + 悬停 tooltip**（图标：`bi-database` DormBooking / `bi-link-45deg` 运行时 JOIN / `bi-bookmark-star` 字典翻译 / `bi-magic` 系统自动）；④ 修复 HTML 无效嵌套（`<small>` 内不再嵌 `<div>`）；⑤ 系统自动记录（登记日期/登记人）使用独立的 `.auto-record` 警告色样式块；⑥ 状态可编辑字段（仅 Status=1 预约时）单独出现在"可编辑字段"卡片底部，与只读状态显示解耦；⑦ 新增 `renderSourceHint(field)` / `renderInfoItem(field, value)` 辅助函数；⑧ `mock-data-rels.js` 字段矩阵 Tab 新增"字段"列（显示源字段名）。
> - **v2.11.5 修改办理登记数据关联可视化**：`booking/edit.html` 新增右侧"数据关联"面板（默认折叠，可一键展开），含 5 个 Tab —— **字段-数据源矩阵** / **ER 关系图（SVG 渲染 6 张表节点 + 5 条 FK 连线）** / **关联基础资料字典（9 项）** / **校验联动（V-EDIT-01~08）** / **数据写入流程（8 步）**；每个字段右上角新增数据源徽章（DormBooking / 运行时 JOIN / 字典翻译 / 系统自动）。新增 `mock-data-rels.js` 元数据脚本，与 `07-办理登记需求-v2.11.md` §13 字段数据来源与关系矩阵完全一致。
> - **v2.11.6**：系统设置新增"**系统集成**"子功能 — HR/K3ERP 双系统配置（服务器地址/账号/密码/启用状态开关/连接测试/保存参数），原型 settings/index.html 新增"系统集成"Tab
> - **v2.11.6 修正**：系统集成页面中系统名称/服务器地址/账号/密码全部可编辑修改；表头"连接测试"列改为"操作"，"操作"列改为"连接状态"；"保存参数"按钮按"修改"权限控制显隐
> - **v2.11.6 修正**：系统设置左侧导航宽度固定为 200px；基础资料子菜单由横向 Tab 导航改为纵向 pills 导航（与系统设置菜单风格一致）；**修复系统设置/基础资料左右区域上边错位问题（改用 flexbox 布局 `display: flex; align-items: flex-start; gap: 12px` 替代 Bootstrap row，确保菜单导航上端面与内容区顶端严格水平对齐）；基础资料右侧内容区增加 `.tab-content` 包裹层，修复 Bootstrap Tab 切换失效问题**；**抄表记录筛选区域参照人员清单风格：flex-nowrap 一行排列、所有筛选条件项随屏幕宽度自适应缩放、固定查询/重置按钮；操作按钮（手动补录/批量导入/导出Excel）移至页面标题右侧**
> - **v2.11.6 员工分摊（员工账单）筛选区重构**：`billing/employee-bills.html` 筛选区改用 `flex-nowrap` 一行排列 + 自适应宽度（参照人员清单）；**分摊合计**字段使用 `.filter-item-stats` 最小自适应（min-width: 90px, max-width: 140px）；业务按钮（**生成/重生成/导出/发布**）迁移到页面标题右侧 `btn-group`；查询/重置按钮固定 90×38px；移动端 ≤768px 自动 2 列布局。与人员清单筛选区样式和位置完全一致。
> - **v2.11.6 住宿账单详情弹窗"在住人数/最大容量数"**：`billing/dorm-bills.html` 详情弹窗信息卡从 3 列（住宿/计费月份/合计金额）改为 4 列（**住宿 / 在住人数/最大容量数 / 计费月份 / 合计金额**）；新增字段在"住宿"与"计费月份"之间，格式 `X/Y`（如 `3/4`）；数据来源 `DormBilling.residentCount`（账单快照，保证一致性） / `Dorm.capacity`（配置数据）；超额（>capacity）显示红色 `text-danger`；找不到 Dorm 档案显示 `-/-`。
> - **v2.11.7 住宿账单详情弹窗三项优化**：`billing/dorm-bills.html` ① **弹窗宽度 +300px**（`modal-lg` → `modal-xl + max-width: 1100px`）；② **信息卡列标题简化为"在住人数"**（与档案字段名一致），值显示风格 `X/Y` 不变；③ **员工分摊明细列表新增"考勤班次"列**（8 → 9 列），关联引用 `SysEmployee.AttendanceTypeId` → `AttendanceType.Name` FK（Badge 按 `ATTENDANCE_BADGE` 6 种颜色渲染）；合计行 `colspan="2"` 跨姓名+考勤班次两列。`SysEmployee.cs` 添加 `AttendanceType` 导航属性。
> - **v2.11.7.BUGFIX 修复 EMP-2026-228 考勤班次关联失效 + 全栈关联关系错误更正**：① `mock-data.js` PERSONNEL 数组中 619 条记录存在数据格式不一致（旧 `attendanceType: "MIDDLE"` vs 新 `attendanceTypeId: 3` FK），导致 FK 关联引用代码在 95% 员工记录上失效；② 修复方案：在 `mock-data.js` 末尾添加 `normalizeData()` IIFE 自动补全规范；③ 增强 `findEmployee()` 支持按 employeeCode 字符串查找；④ 清理 `EMPLOYEE_BILLS_202607` 中冗余缓存字段；⑤ `dorm-bills.html`/`dorms/details.html`/`booking/check-in.html`/`booking/check-out.html` 增加 fallback 兼容新旧格式。需求文档同步补充到 `15-考勤班次需求-v2.11.2.md` §9、`16-人员清单筛选条件-v2.11.2.md` §5.5、`33-基础资料模块-v2.11.4.md` §10、`34b-mock-data-id-mapping-v2.11.4u.md` §6、`01-技术架构与系统开发方案.md` §19.1 ER 图与索引。
> - **v2.11.7.B 人员清单员工类型 FK 关联补全展示效果**：① ~~EMPLOYEE_TYPES 字典从 5 种扩展到 10 种~~ → **v2.11.7.CORRECT 已回退至 5 种**；② 新增 `employeeTypeBadge()` 渲染助手 + `EMPLOYEE_TYPE_BADGE` 颜色映射（5 种）；③ `personnel/list.html` 列表员工类型列从纯文本改为按 FK 渲染 Badge；④ 筛选下拉 `value` 从字符串改为 ID；⑤ 筛选比较从字符串改为 FK ID。~~新增文档 `34c-mock-data-人员清单FK补全-v2.11.7b.md`~~（已修正为 v2.11.7.CORRECT）。
> - **v2.11.7.D PERSONNEL 深度 FK 关联数据补全（100% 覆盖）**：① 直接修改 `mock-data.js` PERSONNEL 数组 650 条记录的 FK 字段，**所有 650 条记录全部具备** `employeeTypeId` (FK) + `departmentId` (FK) + `attendanceTypeId` (FK) 三类关联引用（共 1950 个新字段值）；② ~~类型分布按部门业务语义智能分配（合同工 224 / 临时工 87 / 外包 76 / 实习生 66 / 顾问 38 / 技师 117 / 保安 42）~~ → **v2.11.7.CORRECT 已回退**：所有 `employeeTypeId` 值均在 1-5 范围内；③ 保留旧字段向后兼容；④ `normalizeData()` IIFE 新增第 2b 步校正无效 FK。~~新增文档 `34d-mock-data-PERSONNEL-FK深度补全-v2.11.7d.md`~~（已修正为 v2.11.7.CORRECT）。
> - **v2.11.7.C 业务术语统一：办理记录 → 办理登记**（UI 标签层面）：① 全项目文档与原型页面（31 个文件，100 处替换）"办理记录"更名为"办理登记"；② `07-办理记录需求-v2.11.md` 文件重命名为 `07-办理登记需求-v2.11.md`；③ `mock-data.js` `renderNav()` 菜单项中文同步更新（"办理登记"）；④ 所有跨文档引用自动同步；⑤ **代码标识符**（`BookingController` / `DormBooking` / `BOOKINGS` / `BookingService`）**保留不变**（这些是数据库表名 / API 路径 / C# 类名，重命名会破坏 SQL/Code）。
> - **v2.11.6 住宿详情"当前入住人员"员工类型列 FK 关联引用**：`dorms/details.html` 当前入住人员列表的"员工类型"列**强制**关联引用 `SysEmployee.EmployeeTypeId` → `EmployeeType.Name`（数据链路单一、实时联动）；不再使用冗余的 `emp.employeeType` 字符串字段；找不到字典显示 `-`；Badge 颜色按 `Code` 渲染（合同工-灰、临时工-橙、外包-青、实习生-绿、驻场-紫）。`SysEmployee.cs` 添加 `EmployeeType` 导航属性（v2.11.7.BUGFIX 后兼容新旧字段格式）。
> - **v2.11.7.CORRECT 员工类型 FK 关联修正**：基础资料-员工类型表仅定义 5 种类型。`mock-data.js` 中扩展至 10 种的顾问/技师/保安/司机/保洁 已回退；`normalizeData()` 新增第 2b 步校正 197 条 `employeeTypeId > 5` 的记录；人员清单页面 Badge 映射仅支持 5 种。
> - **v2.11.5**：新增"**筛选条件持久化缓存**"功能设计 — localStorage 本地存储 + 数据库云端持久化双模式；个人中心新增"存储筛选条件"开关与"清除"按钮；覆盖全部 7 个列表模块（办理登记/住宿管理/人员清单/抄表记录/费用标准/住宿账单/员工账单）
> - **v2.11.2 增补 (a)**：首页看板新增 2 张 KPI（**预约人员** + **异常人员**）
> - **v2.11.2 增补 (b)**：人员档案新增"**考勤班次**"字段，办理登记联动，住宿智能分配
> - **v2.11.2 增补 (c)**：人员清单删除"显示离职人员"选项，新增"姓名"筛选条件
> - **v2.11.2 增补 (d)**：人员清单新增"**住宿状态**"字段（基于 BOOKINGS 最后记录推断）
> - **v2.11.2 增补 (e)**：人员清单表头顺序调整（手机号提前、在职状态前置）
> - **v2.11.2 增补 (f)**：**首页看板图表布局重构**（左右分栏 + 大图跨行）
> - **v2.11.2 增补 (g)**：**住宿费用 TOP10 canvas 按所占区域大小拉伸**
> - **v2.11.2 增补 (h)**：**员工账单删除"本月合计"行**
> - **v2.11.2 增补 (i)**：**住宿费用清单新增 3 个筛选条件**（房号 / 楼栋 / 楼层）
> - **v2.11.2 增补 (j)**：**数据看板月度选择**（默认当前月，可切换历史月份）
> - **v2.11.2 增补 (k)**：**员工账单精确计算**（入住天数 × 日均费用 × 占比 + 住宿房号筛选 + 调宿分两段）
> - **v2.11.2 增补 (l)**：**员工账单拆分"住宿状态"与"时间段"为两列**
> - **v2.11.2 增补 (m)**：**住宿费用 TOP10 宽度缩小 + 新增入住率最低 TOP10**
> - **v2.11.2 增补 (n)**：**抄表记录增加"未抄表"状态**（每月每房号必须有 1 条记录）
> - **v2.11.2 增补 (o)**：**抄表记录增加"未完成"状态**（部分表项缺失 + 覆盖历史备注追加）
> - **v2.11.2 增补 (p)**：**统一 UI 导航栏**（所有页面使用同一 renderNav() 函数，菜单样式完全一致）
> - **v2.11.1**：将演示数据扩展至**设计背景最大记录数**（员工 600 / 房间 200）

## 🚀 打开方式

直接双击 `index.html` 即可在浏览器中预览（无需任何后端）。

> **提示**：浏览器打开时 mock-data.js (1.1 MB) 首次加载约需 200-500ms，请耐心等待。

## 📁 文件清单

```
04-HTML原型/
├── README.md                          ← 本文件
├── mock-data.js                       ← Mock 数据 + 公共函数（v2.13.24 移除 mock-data-rels.js）
├── index.html                         ← 首页/经营概览（7 KPI + 8 图表，v2.13.24 修正）
├── personnel/
│   ├── list.html                      ← 人员清单（筛选+分页+操作）
│   ├── create.html                    ← 新增人员表单
│   ├── edit.html                      ← 编辑人员表单（GET ?id=）
│   └── import.html                    ← 3 步导入向导
├── billing/
│   ├── standards.html                 ← 费用标准列表（当前生效卡片）
│   ├── standard-form.html             ← 新增/编辑费用标准（多选复选框）
│   ├── dorm-bills.html                ← 住宿月度账单（生成/发布/导出）
│   └── employee-bills.html            ← 员工账单（生成/发布/导出）
├── booking/
│   ├── index.html                     ← 办理登记列表（筛选+分页+操作）
│   ├── check-in.html                  ← ⚠️ 旧版独立页（v2.11.16 已迁入 list.html 弹窗，保留仅供历史参考）
│   ├── check-out.html                 ← ⚠️ 旧版独立页（v2.11.16 已迁入 list.html 弹窗，保留仅供历史参考）
│   └── edit.html                      ← 修改办理登记（v2.11.15.d 重构：标准表单布局）
├── dorms/
│   ├── list.html                      ← 住宿档案列表（筛选+分页+操作，含"性别"列）
│   ├── create.html                    ← ⚠️ 旧版独立页（v2.12.37 已迁入 list.html Modal，保留仅供历史参考）
│   ├── details.html                   ← 住宿详情（基本信息 + 当前入住 + 历史 + 操作）
│   ├── edit.html                      ← ⚠️ 旧版独立页（v2.12.38 已迁入 list.html Modal，保留仅供历史参考）
│   └── history.html                   ← 住宿历史时间线
├── meter/
│   ├── index.html                     ← 抄表记录列表（筛选+分页+操作）
│   ├── entry.html                     ← 手动补录抄表
│   ├── import.html                    ← 批量导入抄表
│   ├── detail.html                    ← 抄表详情
│   └── edit.html                      ← 修正抄表读数
├── basics/
│   └── index.html                     ← 基础资料（9 类字典：部门/楼栋/楼层/地址/员工类型/考勤班次/员工班组/计量单位/住宿状态/在职状态）
├── settings/
│   └── index.html                     ← 系统设置（8 个 Tab：服务与端口/数据库连接/PDA 版本/用户管理/角色与权限/备份与恢复/系统集成/关于系统）
└── _shared/                           ← 共享资源（详见 §_shared/ 目录结构）
    ├── layout.html
    ├── layout-tab.css
    ├── tab-bar.js
    ├── storage-keys.js
    ├── filter-persistence.js
    └── （v2.13.24 已移除：icon-rail.js / mock-data-rels.js）
```

## 🎨 共用页头 + Tab 页签导航（v2.12.3）

### 三层架构总览（v2.12.3 精简）

所有原型页面统一采用「共用页头 + Tab 页签切换」**三层架构**：

```
┌──────────────────────────────────────────────────────────────────────┐
│ Tier 1  顶部品牌栏 (48px)                                              │
│  [Logo + 品牌名 + 版本号]                          [用户胶囊 + 退出]   │
├──────────────────────────────────────────────────────────────────────┤
│ Tier 2  Tab 页签栏 (45px) — 直接作为菜单项切换使用                       │
│  [首页][办理登记][住宿管理][人员清单][费用标准][住宿账单]              │
│  [员工账单][抄表记录][基础资料][系统设置]                              │
├──────────────────────────────────────────────────────────────────────┤
│ Tier 3  页面内容区                                                      │
│   图标 + 标题 + 总数 + 操作按钮 + 筛选区 + 列表 + 分页                  │
└──────────────────────────────────────────────────────────────────────┘
```

> **v2.12.3 变更**：移除原 Tier 2 紧凑型图标导航条（功能与 Tab 栏重复），节省 56px 垂直空间。

### 调用方式（v2.12.3 起）

每个原型页面统一在 `</head>` 之前引入共享 CSS，并在 `</body>` 之前引入共享脚本：

```html
<!-- Head -->
<link rel="stylesheet" href="../_shared/layout-tab.css">

<!-- Body 末尾 -->
<script src="../_shared/storage-keys.js"></script>
<script src="../_shared/tab-bar.js"></script>
<script>
document.addEventListener('DOMContentLoaded', function() {
    mountTabBar({ basePath: '..', currentUrl: 'dorms/list.html' });
});
</script>
```

### _shared/ 目录结构

| 文件 | 作用 |
|------|------|
| `layout.html` | 共用页头模板（参考用） |
| `layout-tab.css` | Tab / 品牌栏样式 |
| `tab-bar.js` | Tier 2 Tab 页签栏 + TabManager |
| `storage-keys.js` | localStorage key 常量 |
| `migrate.py` | 批量迁移工具（v2.11 → v2.12） |
| `cleanup.py` | 清理遗留的旧导航标记 |
| `inject.py` | 注入缺失的初始化脚本 |

### ⚠️ renderNav() 已废弃

`mock-data.js` 中的 `renderNav()` 函数保留以兼容旧引用，**新代码请使用**：

```js
mountIconRail({ basePath: '..', currentModule: 'dorms' });  // Tier 2
mountTabBar({ basePath: '..', currentUrl: 'dorms/list.html' });  // Tier 3
TabManager.open({ title: '...', module: '...', icon: '...', url: '...' });  // 打开 Tab
```

### 菜单链接对照表（v2.13.24 修正：与文档 60/37 一致）

| # | 菜单项 | 首页链接 | 子页面链接 | 目标文件 | 状态 |
|---|--------|---------|-----------|---------|------|
| 1 | 首页 | `index.html` | `../index.html` | ✅ 存在 | 经营概览（7 KPI + 8 图表） |
| 2 | 办理登记 | `booking/index.html` | `../booking/index.html` | ✅ 存在 | 列表 + check-in.html + check-out.html + edit.html |
| 3 | 住宿管理 | `dorms/list.html` | `../dorms/list.html` | ✅ 存在 | 列表 + details.html + history.html（create/edit 已迁入 Modal） |
| 4 | 人员清单 | `personnel/list.html` | `../personnel/list.html` | ✅ 存在 | 列表 + create.html + edit.html + import.html |
| 5 | 费用标准 | `billing/standards.html` | `../billing/standards.html` | ✅ 存在 | 列表 + standard-form.html |
| 6 | 住宿账单 | `billing/dorm-bills.html` | `../billing/dorm-bills.html` | ✅ 存在 | 列表 + 详情弹窗 |
| 7 | 员工账单 | `billing/employee-bills.html` | `../billing/employee-bills.html` | ✅ 存在 | 列表 + 详情弹窗 |
| 8 | 抄表记录 | `meter/index.html` | `../meter/index.html` | ✅ 存在 | 列表 + entry.html + import.html + detail.html + edit.html |
| 9 | 基础资料 | `basics/index.html` | `../basics/index.html` | ✅ 存在 | 9 类字典管理 |
| 10 | 系统设置 | `settings/index.html` | `../settings/index.html` | ✅ 存在 | 8 个 Tab（含个人中心筛选缓存开关） |

### 菜单分类颜色

| 分类 | 激活色 | 模块 |
|------|--------|------|
| 首页/核心业务 | 蓝 `#1976d2` | 首页、办理登记、住宿管理、人员清单 |
| 费用管理 | 橙 `#e65100` | 费用标准、住宿账单、员工账单 |
| 数据采集 | 青 `#00838f` | 抄表记录 |
| 系统管理 | 灰 `#546e7a` | 系统设置 |
| 个人设置 | 紫 `#7b1fa2` | 个人中心（筛选条件存储开关） |

## 🔗 跳转矩阵（与需求规格 §7 一致）

| 源页面 | 操作 | 目标 |
|--------|------|------|
| `personnel/list.html` | +新增 | `personnel/create.html` |
| `personnel/list.html` | 编辑 | `personnel/edit.html?id={id}` |
| `personnel/list.html` | 离职 | 当前页（标记后刷新） |
| `personnel/list.html` | 📥导入 | `personnel/import.html` |
| `personnel/list.html` | 📤导出 | 直接下载（mock alert） |
| `billing/standards.html` | +新增 | `billing/standard-form.html` |
| `billing/standards.html` | 编辑 | `billing/standard-form.html?id={id}` |
| `billing/standards.html` | 删除 | 当前页（确认后删除） |
| `billing/dorm-bills.html` | 生成账单 | 当前页（alert 后刷新） |
| `billing/employee-bills.html` | 全部发布 | 当前页（alert 后刷新） |

## 📊 Mock 数据规模（v2.11.1 设计背景最大规模）

| # | 数据集 | 数量 | 说明 |
|---|--------|------|------|
| 1 | 人员（PERSONNEL） | **600** | 在职 500 + 待入职 30 + 已离职 70（v2.11.19 规范：与基础资料-在职状态表 EmploymentStatus 一致）；5 员工类型 × 8 部门 |
| 2 | 住宿（DORMS） | **200** | 启用 190 间；1人/2人/4人/6人/8人 5 种房型；床位合计 777 |
| 3 | 住宿记录（RESIDENCIES） | **550** | 在宿 500 + 历史 50 |
| 4 | 办理登记（BOOKINGS） | **630** | 在宿 500 + 退房 100（50人×2条）+ 预约 20 + 异常 10 |
| 5 | 抄表记录（METER_RECORDS） | **380** | 启用住宿 × 2 月（2026-06 + 2026-07），含 ~5% 已修正/已作废 |
| 6 | 住宿账单（DORM_BILLS_202607） | **181** | 2026-07 月份，覆盖 ~95% 启用住宿 |
| 7 | 员工账单（EMPLOYEE_BILLS_202607） | **479** | 在宿员工按 1/N 按类型差异化分摊 |
| 8 | 费用标准（BILLING_STANDARDS） | **7** | 5 种类型各 1 当前标准 + 2 历史标准 |
| 9 | 入住退房月统计（MONTHLY_MOVE_STATS） | 12 | 最近 12 个月 |
| 10 | 月度总费用（MONTHLY_COST_TREND） | 12 | 反映 190 间 × 平均 ¥350/间 ≈ ¥63000/月 |

### 看板预览效果（v2.11.6：6 张 KPI + 数据逻辑修复）

打开 `index.html` 即可看到：

| # | KPI | 当前值 | 颜色 | 副文 |
|---|-----|-------|------|------|
| 1 | **入住人数** | **dormCode 非空计数** | 蓝 | 共登记 600 人 |
| 2 | 住宿入住率 | 入住人数 / 总容量 | 绿 | 入住人数/总床位 |
| **3** | **预约人员** | **30** | **橙** | **人待入住** |
| **4** | **异常人员** | **A+B+C** | **红** | **需处理** |
| 5 | 本月抄表覆盖 | 188/190 | 蓝 | 未抄 D-XXX |
| 6 | 本月费用合计 | ¥63,355 | 红 | 约 181 间 |

> **KPI 1 入住人数**：PERSONNEL 中 DormCode 非空计数（无论 status：在职/待入职/已离职），非 status=1 计数（v2.11.6 修复）
>
> **KPI 4 异常人员 A 项**：`status === 3 && dormCode !== null`（已离职且房号不为空），非 status=2（v2.11.6 修复）
>
> **异常人员 78 构成**：A 已离职仍住 14 + B 未到入职提前住 54 + C 超期未办理 10
>
> **数量为 0 时显示样式**：绿色"正常"大号字体（1.8rem, #0ca30c, bold）

### 图表预览（v2.11.2 增补 f：左右分栏布局）

**主区域**（左右分栏）：
- **左半（col-lg-6）**：两个趋势图上下堆叠
  - 上：入住 / 退房 人数对比（12 个月分组柱状）
  - 下：每月总费用变化曲线（折线图，紫色）
- **右半（col-lg-6）**：住宿费用排名 TOP 10（**高度 = 左半两图之和**，重点突出）

**行 3**：分布图（col-lg-3 各，4 图并列）
- 部门分布（柱状）
- 费用类型占比（环形）
- 员工类型分布（环形）
- 抄表覆盖（环形）

### 班次（v2.11.2 新增，v2.13.98 简称显示）

人员档案 6 种班次：

| 取值 | 时段 | 颜色 |
|------|------|------|
| DEFAULT 默认 | 09:00-18:00 | 灰 |
| MORNING 早 | 06:00-14:00 | 橙 |
| MIDDLE 中 | 14:00-22:00 | 黄 |
| EVENING 晚 | 18:00-02:00 | 蓝 |
| NIGHT 夜 | 22:00-06:00 | 紫 |
| OTHER 其他 | 不定期 | 青 |

**当前数据**（在职 500 人）：
- 默认 172 / 早 122 / 中 84 / 晚 78 / 夜 25 / 其他 19

**智能分配效果**：启用住宿 129 间中 **126 间完全一致**（97.7%）—— 4 人间几乎都是相同班次作息

**办理登记联动**：列表新增"班次"列（Badge）；check-in.html / check-out.html 顶部员工信息卡片显示班次 Badge；edit.html 显示该员工班次

## ✅ 验收清单

打开每个页面后，逐项核对：

- [ ] **字段齐全**：与需求规格 §4 一致（无遗漏字段）
- [ ] **按钮齐全**：列表所有操作按钮（编辑、删除、导入、导出、生成、发布）均存在
- [ ] **跳转正确**：点击后跳转到目标页面（如 list → create → list）
- [ ] **筛选生效**：下拉筛选 + 关键词搜索实时刷新列表
- [ ] **分页生效**：8 条数据 / 每页 5 条，验证翻页
- [ ] **中文文案**：与业务习惯一致（如"标记离职"而非"软删除"）
- [ ] **样式统一**：Bootstrap 5 + Bootstrap Icons + 主色 #1976d2

## 🔧 与 Razor 视图的对照

每个 HTML 原型对应的真实 Razor 文件：

| HTML 原型 | Razor 视图 |
|-----------|-----------|
| `personnel/list.html` | `DormManage.Admin/Pages/Personnel/Index.cshtml` |
| `personnel/create.html` | `DormManage.Admin/Pages/Personnel/Create.cshtml` |
| `personnel/edit.html` | `DormManage.Admin/Pages/Personnel/Edit.cshtml` |
| `personnel/import.html` | `DormManage.Admin/Pages/Personnel/Import.cshtml` |
| `billing/standards.html` | `DormManage.Admin/Pages/BillingStandard/Index.cshtml` |
| `billing/standard-form.html` | `DormManage.Admin/Pages/BillingStandard/Create.cshtml`<br>`DormManage.Admin/Pages/BillingStandard/Edit.cshtml` |
| `billing/dorm-bills.html` | `DormManage.Admin/Pages/DormBilling/Index.cshtml` |
| `billing/employee-bills.html` | `DormManage.Admin/Pages/EmployeeBilling/Index.cshtml` |

> 对照原则：input 的 `name`/`id`、按钮的 `onclick`、`<a href>` 路径三者完全对齐。

## ⚠️ 原型 vs 真实页面的差异

| 维度 | 原型 | 真实页面 |
|------|------|---------|
| 数据源 | mock-data.js 静态数据 | 后端 API + EF Core |
| 提交反馈 | alert() 弹窗 | TempData 横幅 + 自动刷新 |
| 错误处理 | 简单校验 | 服务端验证 + ModelState |
| 权限拦截 | 无 | `[RequirePermission]` 特性 |
| Excel 导入/导出 | mock alert | 真实 ClosedXML 处理 |

---

> **SOP 提示**：HTML 原型验收通过后，才能进入代码实现阶段。本次 v2.10 的 Razor 视图已基于该原型实现，可作为后续 v2.11 的参照基线。