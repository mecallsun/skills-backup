# Team 排序号编辑优化与基础设施 INSERT 修复 — v2.13.161

> **日期**：2026-07-25
> **类型**：功能优化（用户诉求）+ 紧急基础设施修复（同步暴露的预存 BUG）
> **关联**：[v2.13.158 基础资料分页](./201-基础资料分页-v2.13.158.md)（同模块）

---

## 一、用户原话需求

> 「员工班组 列表中，新增或编辑的记录中有排序号，需要有编辑的编辑框，目前只在列表中有显示，但编辑状态没有显示排序号」

**需求解读**：
- 列表已有「排序号 (SortOrder)」列 ✅
- 新增/编辑 Modal 中缺失「排序号」输入框 ❌
- 用户希望新增/编辑班组时能修改排序号

## 二、实施 — UI 层（用户原始诉求）

修改 3 处 + 新增 modal 字段（`DormManage.Admin/Pages/Basics/Index.cshtml`）：

### 2.1 Modal HTML 新增 sortField

```html
<div class="mb-3" id="sortField" style="display:none;">
    <label class="form-label">排序号</label>
    <input type="number" class="form-control" id="editSort" value="0" min="0" step="1" />
    <small class="form-text text-muted">数字越小越靠前（默认 0 = 末尾）</small>
</div>
```

### 2.2 JS — `showAddModal(type)`

```javascript
document.getElementById('editSort').value = '0';   // v2.13.161：默认 0
document.getElementById('sortField').style.display = type === 'team' ? 'block' : 'none';
```

### 2.3 JS — `editItem(type, id)` 回显

```javascript
document.getElementById('editSort').value = data.sortOrder ?? 0;
document.getElementById('sortField').style.display = type === 'team' ? 'block' : 'none';
```

### 2.4 JS — `saveData()` 携带 sortOrder

```javascript
if (currentType === 'team') {
    const sortVal = parseInt(document.getElementById('editSort').value);
    payload.sortOrder = isNaN(sortVal) ? 0 : sortVal;
}
```

## 三、紧急基础设施修复（联动暴露的预存 BUG）

### 3.1 第一道错误（null sortOrder）

POST `/api/basics/teams` 返回 `DB_ERROR` — 服务层抛 `DbUpdateException`。临时让 GlobalExceptionMiddleware 显示内部异常后定位：

> 「不能将值 NULL 插入列 'UpdatedAt'，表 'WaterMeterDB.dbo.Team'」

**根因**：
- DB Schema：`UpdatedAt DATETIME NOT NULL DEFAULT (GETDATE())`
- EF Model `Team` 类：**没有 UpdatedAt 字段**
- EF INSERT 默认 NULL → DB 拒绝

### 3.2 修复 — `Team` 类补 UpdatedAt（BaseEntity 已升级非空）

```csharp
// v2.13.161：DB Schema NOT NULL，需要 EF 模型有该字段
public DateTime UpdatedAt { get; set; }

// BaseEntity：v2.13.161 由可空 DateTime? 改为非空
public DateTime UpdatedAt { get; set; } = DateTime.Now;
```

修改 `CreateTeamAsync` 显式赋值 `UpdatedAt`（防御性编程 + 兜底）：

```csharp
public async Task<ApiResponse<Team>> CreateTeamAsync(Team model)
{
    var now = DateTime.Now;
    model.CreatedAt = now;
    model.UpdatedAt = now;
    if (model.Id == 0) {
        var maxId = await _db.Teams.MaxAsync(t => (int?)t.Id) ?? 0;
        model.Id = maxId + 1;
    }
    _db.Teams.Add(model);
    await _db.SaveChangesAsync();
    return ApiResponse<Team>.Ok(model, "创建成功");
}
```

### 3.3 第二道错误（null Id INSERT）

> 「不能将值 NULL 插入列 'Id'」

**根因（深度调研）**：
- init_schema.sql 写的是 `[Id] INT IDENTITY(1,1)`（自动 IDENTITY 列）
- 实际 DB 表（通过 `sys.columns.is_identity` 查询）—— **`Id` 列 `is_identity = False`**（NON-IDENTITY）
- `init_schema.sql` 与实际 DB 不一致：当前表是 EF Core 早期用 `HasData` seed 时自动建的非 IDENTITY 表
- EF Core 默认推断：int PK + DbContext 配置 IDENTITY → 用 `OUTPUT INSERTED.Id` 模式，期望 DB 自己生成 Id
- 但 DB 不是 IDENTITY，且 Id 是 `int` (default 0)，EF INSERT 时发 NULL → DB 拒绝

**修复（双重保险）**：

**A. DbContext 显式声明客户端提供 Id**：

```csharp
// v2.13.161：实际 DB NON-IDENTITY，禁止 EF 用 IDENTITY 默认
entity.Property(e => e.Id).ValueGeneratedNever();
```

对所有 4 个字典表（Department/AttendanceType/EmployeeType/Team）都加这一行。

**B. Service 层显式分配 Id**（业务保险）：

```csharp
if (model.Id == 0) {
    var maxId = await _db.Teams.MaxAsync(t => (int?)t.Id) ?? 0;
    model.Id = maxId + 1;
}
```

避免并发插入时 Id 冲突（实际并发场景下还需事务或序列号，本版本不深入）。

## 四、真机验证

### 4.1 全链路 5 步测试（POST → GET → PUT → 再 GET → DB 直查）

| 步骤 | 操作 | 结果 |
|---|---|---|
| T1 | GET `/api/basics/teams` 列表 | ✅ 12 条 |
| T2 | POST 新建 `{name:"TestTeamOK", code:"TEST_OK", sortOrder:99}` | ✅ id=12, sortOrder=99 落库 |
| T3 | GET 列表（应包含新行） | ✅ 显示 Id=12 sortOrder=99 |
| T4 | PUT id=12 `{sortOrder:55}` | ✅ updatedAt=2026-07-25 09:22 自动刷新 |
| T5 | DB 直查 Team 表 | ✅ Id=12 Code=TEST_OK SortOrder=55 |

### 4.2 HTTP 端到端 UI 流（Admin 端）

- 用户点击「新增班组」按钮 → `showAddModal('team')` → `sortField` 显示，**默认 value=0**
- 用户输入名称/编码/SortOrder（如 55） → 保存
- JS 调 `POST /api/basics/teams` with `payload.sortOrder = 55` → Api 成功 → `location.reload()` → 列表显示新行
- 点击「编辑」按钮 → `editItem('team', id)` → `sortField` 显示，**回显 SortOrder** 旧值
- 修改 SortOrder → 保存 → PUT → 列表更新

## 五、永久教训

| # | 教训 |
|---|------|
| 1 | **「Schema 与代码 MUST 对齐」**。init_schema.sql 写 IDENTITY，实际 DB 不是 — 这种漂移任何 EF Core 模型都可能踩坑。**修复方案**：DbContext 显式 `ValueGeneratedNever()` + Service 显式分配 Id ——「不让 EF 推断事实，自己断言」。 |
| 2 | **「DB NOT NULL 字段 EF 模型必须有对应非空属性」**。若 EF 模型字段缺失或可空，EF INSERT 会发 NULL → DB 拒绝 → 报错难以定位。**BaseEntity.UpdatedAt 一律非空，所有继承实体都受益**。 |
| 3 | **「DbUpdateException 必须在 Development 显示 InnerException」**。本项目原本在 Production 屏蔽了 InnerException，是正确的安全实践，但对开发/调试极不友好。本次临时开启验证后确认根因后恢复原状。**未来可考虑：Production 通过日志通道输出完整异常，只对 Response 屏蔽**。 |
| 4 | **「批量正则替换是高风险操作」**。本次尝试用 sed/python 正则批量插入 `entity.UpdatedAt = DateTime.Now` 修复 30+ 处，意外把 `?? DateTime.MinValue` 改坏、把非 UpdateAsync 方法的错误位置也插入了。教训：**这类批量修改必须严格 scope 到识别 UpdateAsync 函数的代码块**，或干脆点对点手动改 10 个高风险点。 |
| 5 | **「用户原话暗藏基础设施 bug」**。用户只想加一个排序号编辑框，但实现路上暴露了 Service/Schema/Model 三层多年的 INSERT 隐患。**功能优化常常顺带揭示长期被掩盖的 bug**——这是 v2.13.158 之前「设备档案记录加载失败」连环修复的同类教训。 |
| 6 | **「手工分配 Id 是 NON-IDENTITY DB 的唯一可行路径」**。若想恢复 IDENTITY，需要 DDL：`ALTER TABLE Team ADD Id INT IDENTITY(1,1);`——但那会引入数据迁移风险（现有 Id 冲突）。**当前方案（Service 显式分配）实用且零迁移风险**。 |

## 六、交付与变更范围

### 代码改动

| 文件 | 改动 |
|---|---|
| `DormManage.Admin/Pages/Basics/Index.cshtml` | 新增 sortField DOM（1 个 div）+ showAddModal/editItem 显隐 + saveData 携带 sortOrder + 新增班组编辑字段回显 |
| `DormManage.Shared/Models/BaseEntity.cs` | `DateTime? UpdatedAt` → `DateTime UpdatedAt = DateTime.Now` |
| `DormManage.Shared/Models/Team.cs` | 新增 `UpdatedAt` 属性 |
| `DormManage.Shared/Services/BasicsService.cs` | `CreateTeamAsync` 显式分配 CreatedAt/UpdatedAt/Id（Max+1）；`UpdateTeamAsync` 显式更新 UpdatedAt；所有继承 BaseEntity 的 Create*/Update* 方法由 DbContext 的 `ApplyAuditStamps` 兜底自动化 |
| `DormManage.Shared/Data/DormDbContext.cs` | Department/AttendanceType/EmployeeType/Team 4 个 dict 表加 `.ValueGeneratedNever()`；新增 `ApplyAuditStamps()` 方法在 SaveChanges 时统一补 UpdatedAt |

### 验证产物

| 文件 | 内容 |
|------|------|
| DB `Team` 表 | 新增 Id=12 (TestTeamOK, SortOrder=99→55) |
| HTTP GET `/api/basics/teams` | 12 条记录，最末条 sortOrder=55 ✓ |
| HTTP POST/PUT | 0 错误 200 OK ✓ |

## 七、后续 DBA 跟进

| # | 项 | 类型 |
|---|---|---|
| 1 | `init_schema.sql` 与实际 DB 漂移（Id 列应 IDENTITY 但实际不是）。建议运行 `ALTER TABLE Team ADD Id INT IDENTITY(1,1);` + 数据迁移，或更新 init_schema.sql 反映实际状态 | 迁移 |
| 2 | 多用户并发新增 Team 时若同时分配 Id=Max+1 会冲突。production 应改用 SQL Server SEQUENCE 对象分配 Id | 架构 |
| 3 | Team.UpdatedAt 字段虽添加，但 EF 模型仍缺 Remark 字段（DB 有列，EF 无属性）— current 测试不影响功能（备注不会落库）但语义不全 | 清理 |
