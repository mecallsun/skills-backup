---
name: framework-migration
description: 框架升级迁移技能 - 用于未来任何项目或模块的框架全自动迁移任务。任务涉及"迁移、按原型1:1、升级"相类似的指令意思时自动触发。按三源对照流程（旧实现+HTML原型+当前实现），100% 功能完整复刻，不留占位/简化/TODO。
metadata:
  type: skill
  category: 12-Claude原厂
  triggers:
    - 迁移
    - 框架升级
    - 按原型1:1
    - 升级 Vue
    - 重写前端
    - 对齐原型
    - 完整复刻
    - prototype alignment
  priority: high
  complexity: high
---

# 框架升级迁移技能

> **触发条件**：用户消息包含「迁移 / 框架升级 / 按原型1:1 / 升级 X / 重写前端 / 对齐原型 / 完整复刻」等指令时自动执行。
>
> **核心目标**：将旧技术栈实现 1:1 完整迁移到新框架，**功能完整度 100%**，不留占位/简化/TODO/alert('待实现')。

---

## 🌀 永久性属性

| 属性 | 值 |
|------|------|
| **状态** | ⚡ PERMANENT / DEFAULT-ON / AUTO-APPLY |
| **适用范围** | 任何前端/全栈项目的框架迁移任务（Vue/Razor/React/Angular/小程序 等） |
| **执行方式** | 单 Agent 直接执行（中等任务）/ Workflow 并行执行（大型项目 ≥ 15 页面） |
| **优先级** | P0（与"原型功能完整性强制规则"同级） |
| **版本** | v1.0 — 2026-08-14（ZEEHUA v3.3.6 PC 端 15 页面迁移沉淀） |

---

## 📐 4 步核心流程

### Step 1：三源对照（必走）

```
对每个目标页面/模块执行：

1. 读 HTML 原型（设计稿/UI 草图）
   → 项目根 / 00-方案文档 / 04-HTML原型 / **/*.html

2. 读旧实现（旧技术栈代码）
   → 归档目录 / .Deleted.* / .Archived.* / _v1_legacy/ 等

3. 读当前实现（新框架，但功能可能简化）
   → 当前 src/views/ 或 src/pages/

4. 列差异清单（Diff List）：
   - 缺失功能（按钮/筛选/列/弹窗/步骤）
   - 简化实现（用 alert 兜底/字段缺失/默认值）
   - 占位（TODO/placeholder/演示）
   - 错误（数据/字段名/逻辑）

⚠ 不允许「我觉得差不多了」「这个不重要」
```

### Step 2：补全 API 层

```typescript
// 在 src/api/modules.ts 中新增/补全缺失 API
// ⚠️ 复用同一命名规范：xxx + Verbs (list/get/create/update/delete/...)
// ⚠️ 复用同一 baseURL：request.baseURL 已含 /api/v2

// 反例：分散命名 export const fetchBookings / loadBookings / getAllBookings
// 正例：统一 export const getBookings / getBooking / createBooking / ...
```

### Step 3：1:1 完整实现（不留占位）

```
⚠️ P0 规则：
1. 每个按钮必须有真实 onclick/API 调用（禁止 alert('待实现')）
2. 每个筛选必须有真实 query 联动
3. 每个弹窗必须有完整字段 + 校验 + 提交
4. 每个派生 Badge 必须用 SCSS 类名（m1-m5/s1-s5/a1-a5）保持视觉一致
5. 每个分页器必须用共享组件 _PaginationPartial 等价（Vue 端：computed startIdx/endIdx/totalPages）
6. 每个 toast/confirm 必须有引导信息（不只"成功"二字）
```

### Step 4：编译 + 同步 + 文档

```bash
# 1. 编译（必须 0 error）
cd <project> && npm run build  # 或 dotnet build / pnpm build

# 2. 同步到发布目录（P0 教训：双目录必须同步）
powershell -NoProfile -Command "
  Remove-Item -Recurse -Force <dist_target> -ErrorAction SilentlyContinue;
  Copy-Item -Path '<dist>' -Destination <dist_target> -Recurse -Force
"

# 3. 验证：grep 占位清零
grep -rn "alert.*待实现\|placeholder\|TODO\|演示" src/

# 4. 文档同步：CLAUDE.md 版本号 + CHANGELOG + 交付报告
```

---

## 📐 关键模式（Vue 3 + Element Plus 沉淀）

### 模式 1：页头 + 筛选条 + 表格卡片 + 分页器

```vue
<template>
  <div class="page-container">
    <!-- 页头：图标 + 标题 + 总数 Badge + 操作按钮 -->
    <div class="page-header">
      <div class="header-left">
        <i class="bi bi-xxx header-icon"></i>
        <span class="title">标题</span>
        <span class="count-badge">共 {{ total }} 条</span>
        <!-- 可选：金额合计 Badge -->
        <span class="total-amount-badge">¥ {{ grandTotal.toFixed(2) }}</span>
      </div>
      <div class="header-actions">
        <el-button type="primary">主操作</el-button>
      </div>
    </div>

    <!-- 筛选条 -->
    <div class="filter-bar">
      <el-input v-model="filters.x" placeholder="..." clearable />
      <el-select v-model="filters.y" placeholder="..." clearable>...</el-select>
      <el-button type="primary" :icon="Search" @click="onFilterChange">查询</el-button>
      <el-button @click="resetFilters">重置</el-button>
    </div>

    <!-- 表格卡片 -->
    <div class="card-container">
      <el-table :data="list" v-loading="loading" stripe border class="zeehua-table">
        <el-table-column type="index" :index="indexCalc" label="序号" width="60" />
        ...
      </el-table>
      <div class="zeehua-pagination">
        <span class="pagination-info">
          共 {{ total }} 条 · 第 {{ startIdx }}-{{ endIdx }} 条 · 共 {{ totalPages }} 页
        </span>
        <el-pagination ... />
      </div>
    </div>
  </div>
</template>
```

### 模式 2：派生 Badge（5 色旋转）

```vue
<style scoped>
/* 班组 Badge 5 色：m1-m5 */
.team-badge.m1 { background: rgba(0, 131, 143, 0.12); color: #00838f; }
.team-badge.m2 { background: rgba(85, 139, 47, 0.12); color: #558b2f; }
.team-badge.m3 { background: rgba(230, 81, 0, 0.12); color: #e65100; }
.team-badge.m4 { background: rgba(106, 27, 154, 0.12); color: #6a1b9a; }
.team-badge.m5 { background: rgba(198, 40, 40, 0.12); color: #c62828; }
</style>

<script setup lang="ts">
const teamClass = (id: number) => ['m1', 'm2', 'm3', 'm4', 'm5'][(id || 0) % 5]
</script>

<el-table-column label="班组">
  <template #default="{ row }">
    <span class="team-badge" :class="teamClass(row.teamId)">{{ row.teamName }}</span>
  </template>
</el-table-column>
```

### 模式 3：4 步表单（含校验）

```vue
<BaseDialog v-model="visible" :loading="loading" @confirm="submit">
  <el-form :model="form" :rules="rules" ref="formRef" label-width="100px">
    <el-form-item label="名称" prop="name">
      <el-input v-model="form.name" />
    </el-form-item>
  </el-form>
</BaseDialog>

<script setup lang="ts">
const submit = async () => {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  loading.value = true
  try {
    await api(form)
    ElMessage.success('操作成功')
    visible.value = false
    loadData()
  } catch (e: any) { ElMessage.error(e?.message || '操作失败') }
  finally { loading.value = false }
}
</script>
```

### 模式 4：3 步骤流程（含一进一出校验）

```vue
<el-steps :active="step" simple>
  <el-step title="选择" />
  <el-step title="确认" />
  <el-step title="完成" />
</el-steps>

<div v-if="step === 0">...选择 UI...</div>
<div v-else-if="step === 1">...确认 UI + 校验...</div>
<el-result v-else icon="success" title="完成" />
```

### 模式 5：导入前必走的全局检查

```bash
# 1. 检查命名空间冲突（v3.2.0 P0 教训）
grep -rn "DormManage.Shared" src/

# 2. 检查占位
grep -rn "ElMessage.info.*待实现\|ElMessage.info.*占位\|alert.*演示" src/views/

# 3. 检查未使用导入（清零）
grep -rn "^import.*from\s*['\"]@/api/modules['\"]" src/views/*.vue | awk -F: '{print $3}' | sort -u
```

---

## 📐 11 主菜单覆盖检查表（ZEEHUA 案例）

> 不同项目的菜单顺序不同，但**数据库 SysPermission SortOrder 永远为权威**。

```sql
SELECT PermissionCode, Name, ParentId, SortOrder
FROM SysPermission
WHERE ParentId = 0  -- 仅顶级菜单
ORDER BY SortOrder
```

| # | Vue Route | 名称 | 必须实现模块 |
|---|-----------|------|------------|
| 1 | /dashboard | 首页看板 | 7 KPI + 8 图 |
| 2 | /booking | 办理登记 | 6 筛选 + 14 列 + 3 撤销 + 3 步骤退房 |
| 3 | /dorms | 住宿管理 | 5 筛选 + 14 列 + 使用率 + 派生 Badge |
| 4 | /personnel | 人员清单 | 7 筛选 + 重置密码 Modal + 11 列导入 |
| 5 | /billing-standard | 费用标准 | 1 条 = 1 套标准 + 4 态状态 |
| 6 | /dorm-billing | 住宿账单 | 3 大按钮 + 在住人员子页 |
| 7 | /employee-billing | 员工账单 | 3 大按钮 + 发布状态 |
| 8 | /meter | 智能抄表 | 6 筛选 + 4 区块手动补录 Modal |
| 9 | /basics | 基础资料 | 11 子 Tab CRUD（替代 ElMessage.info） |
| 10 | /settings | 系统设置 | 11 Tab + 字段权限 |
| 11 | /vehicle | 车辆管理 | 6 子页面 + 真实 CRUD |

---

## 📐 强制规则（迁移任务不可绕过）

### 1. 不留占位（CLAUDE.md P0）

```bash
# 全局搜索占位/简化/TODO
grep -rn "alert.*待实现\|ElMessage.info.*占位\|ElMessage.info.*演示\|TODO\|FIXME\|// 实际项目调用\|placeholder\|XXXXX" src/

# 命中数必须 = 0
```

### 2. 三源对照（每次新增/修改前必走）

```
Step 1: 读 HTML 原型
Step 2: 读旧实现（WinForms/Razor/旧 Vue）
Step 3: 读当前实现
Step 4: 列差异清单 → 1:1 实现
```

### 3. 双目录同步（P0 教训：v3.1.0 / v3.3.1）

```bash
# Vue 端 dist 必须同步到两个目录（Kestrel SPA 双胞胎）
Remove-Item -Recurse -Force publish/latest/VueAdmin -ErrorAction SilentlyContinue
Copy-Item -Path dist/* -Destination publish/latest/VueAdmin -Recurse -Force
Copy-Item -Path publish/latest/VueAdmin/* -Destination publish/latest/Admin/wwwroot/ -Recurse -Force
```

### 4. API 一致性（避免构建失败）

```typescript
// 所有 API 集中导出在 src/api/modules.ts
// 新增前先 grep 该方法是否已存在
grep "^export const xxx" src/api/modules.ts

// ❌ 错误：在 vue 文件中 inline import
import { correctMeter } from '@/api/some-specific-file'

// ✅ 正确：统一从 modules.ts 导出
import { correctMeter } from '@/api/modules'
```

### 5. 字段权限（v2.13.176 deny-by-default）

```
- 默认 deny：未授权字段一律脱敏或隐藏
- 启用授权：SysFieldPermission.IsAllowed = true
- 前端守卫：hasFieldPermission() 返回 false → 隐藏该列
- 不能因为"暂时没人用"就跳过字段权限
```

### 6. 派生 Badge 系统

```
- 5 色旋转：m1-m5/s1-s5/a1-a5（班组/班次/类型）
- 状态 Badge：active/pending/expired/inactive（值驱动）
- 严禁用 Element Plus 默认 type 替代（辨识度低）
```

### 7. 关键按钮不能少

```
每个列表页必有：导出 / 查询 / 重置 / 分页
每个账单页必有：生成分摊 / 重新生成 / 全部发布 / 导出
每个 Modal 必有：取消 + 主操作 + loading 状态
```

---

## 📐 验证清单（每页完成必走）

```
□ npm run build 0 错误
□ grep 占位 = 0 命中
□ 三源对照差异清单全部覆盖
□ 字段、按钮、CheckBox、弹窗全部真实实现
□ 派生 Badge 用 SCSS 类名（非 inline style）
□ 同步 publish/latest/VueAdmin/ + Admin/wwwroot/
□ 文档：CLAUDE.md 版本号 + 任务状态 completed
□ 永久教训：写入 skill MD（本文档）+ 案例库
```

---

## 📐 反模式（绝对禁止）

| ❌ 反模式 | 后果 |
|----------|------|
| 留 ElMessage.info('待实现') | 永久 P0 缺失项 |
| 用 mock 数据兜底（A001/A002） | 车位/班次等关键数据永远造假 |
| 直接复制当前代码不动 | 失去迁移意义 |
| 不读 HTML 原型就改 | 偏离设计意图 |
| 不读旧实现就改 | 丢失历史功能 |
| 单独修改发布目录 | 双胞胎不同步 → 登录 404 |
| 把 Modal 改为独立路由 | 破坏用户体验 |
| 留 TODO / FIXME | 永久技术债 |
| 简化业务流程（如 3 步改 1 步） | 违反业务规则 |
| 跳过校验 | P0 安全风险 |

---

## 📐 工作量评估（Vue 3 + Element Plus 沉淀）

| 页面复杂度 | 行数 | 工作量 | 周期 |
|-----------|------|--------|------|
| **简单**（如 ForgotPassword） | 200-400 | 0.5h | 0.5 天 |
| **中等**（如 Dorms/Personnel） | 500-800 | 1-2h | 1 天 |
| **复杂**（如 Booking/Meter） | 800-1200 | 2-3h | 1.5 天 |
| **极复杂**（如 Dashboard 8 图） | 1200-2000 | 3-4h | 2 天 |
| **企业级**（如 Settings 11 Tab） | 1500+ | 4-6h | 2-3 天 |

**15 页面完整迁移总计**：~30 工时（≈ 5 个工作日）

---

## 📐 落地模板

### TaskCreate 任务列表（推荐结构）

```python
# 1. 创建 umbrella 任务（15 页面迁移）
# 2. 创建子任务（每页面 1 个）
# 3. 完成后立即标记 completed
# 4. 每 5 个子任务汇报一次进度（阶段梳理）

tasks = [
    TaskCreate("Layout 主菜单对齐", subject="1. Layout.vue 主菜单对齐"),
    TaskCreate("Login 登录页 1:1", subject="2. Login.vue 登录页 1:1"),
    ...
    TaskCreate("生成'框架升级迁移技能'MD", subject="生成 skill MD")
]
```

### 单页面 SOP

```
1. Read 当前页面 → 找出缺失功能
2. Read HTML 原型 → 对照 UI
3. Read 旧实现 → 找回业务逻辑
4. 列差异清单（写入 plan 文档）
5. Write 新页面（1:1）
6. Edit modules.ts 新增 API（如缺）
7. npm run build → 0 error
8. 同步 publish/latest/VueAdmin/ + Admin/wwwroot/
9. Mark task completed
```

---

## 📐 永久教训（ZEEHUA v3.3.6 案例沉淀）

1. **三源对照是 1:1 的前提**：跳过任何一源 = 必出 P0
2. **ElMessage.info 占位清零**：grep 必须 = 0 命中
3. **publish/latest 双目录同步**：v3.1.0 / v3.3.1 两次 P0 教训
4. **API 集中 modules.ts**：Vite/Rollup 静态分析找未导出方法
5. **派生 Badge 5 色系统**：避免 Element Plus 默认 type
6. **字段权限 deny-by-default**：v2.13.176 永久规则
7. **基础资料 11 Tab 用配置驱动**：避免 11 套模板重复
8. **车辆车位真实 CRUD**：替代 mock A001/A002
9. **派生 Modal 而非独立路由**：保留用户体验
10. **每个 Modal 必须有 loading + 校验 + 错误处理**

---

## 📌 自动触发关键词（同义识别）

| 用户输入 | 标准化解释 |
|---------|-----------|
| 「迁移」 | 启动三源对照流程 |
| 「框架升级」 | 启动三源对照流程 |
| 「按原型 1:1」 | 强制功能完整度 100% |
| 「升级 Vue」 | Vue 2→3 或 Razor→Vue |
| 「重写前端」 | 启动三源对照流程 |
| 「对齐原型」 | 强制功能完整度 100% |
| 「完整复刻」 | 强制功能完整度 100% |
| 「prototype alignment」 | 强制功能完整度 100% |
| 「不要简化」 | 强制功能完整度 100% |

---

## 📌 与 CLAUDE.md 永久规则的关系

| 永久规则 | 与本 skill 的关系 |
|---------|------------------|
| **原型功能完整性强制规则** | 本 skill 是其落地执行 SOP |
| **技术栈原则强制流程** | 本 skill 适用于前端层（Vue/React/Razor 迁移） |
| **发布与编译准则** | 双目录同步 + 编译 0 错误是本 skill 的强制步骤 |
| **大需求任务处理机制** | 15 页面迁移属大需求，按 TaskCreate 分阶段执行 |
| **苹果终端禁止规则** | 迁移任务禁止触碰 iOS/IPA/macOS |

---

## 📦 历史决策记录

- **2026-08-14 v1.0**：**新增章节**（ZEEHUA v3.3.6 PC 端 15 页面（Booking/BillingStandard/EmployeeBilling/DormBilling/Vehicle/Basics/Settings/Profile/ForgotPassword）1:1 迁移完成 + Layout/Login/Dashboard/Personnel/Dorms/Meter 共 15 个 Vue 3 页面功能完整度从 60% 提升到 100%）沉淀为永久 skill；PERMANENT / DEFAULT-ON，适用所有框架迁移类任务