# 130 — 抄表记录 BIGINT 类型对齐修复（v2.13.79）

> **结论**：✅ **v2.13.79 修复 /Meter 列表 Error BUG**：根因是 `MeterRecord.RecordId` 在 SQL Server 是 `BIGINT IDENTITY(1,1)`，但 EF Core 模型继承 `BaseEntity.Id`（`int`）导致 EF 物化时 `Int64 → Int32` cast 失败。**v2.13.6** 用 `HasColumnName("RecordId")` + `取值在 int 范围内安全` 的错误假设规避；**v2.13.68** 用 `HasColumnType("int")` 仅影响 DDL 不解决读回 cast；**v2.13.79** 用 `new long Id` + `[Column("RecordId")]` 彻底对齐。

## 一、用户反馈（2026-07-21）

> *"抄表记录中没有显示数据列表，提示错误：Error. An error occurred while processing your request. Request ID: 00-464832cca2225dcd018499ebb14ffd40-97a570699e7c1185-00"*

## 二、BUG 根因审计（5 层问题叠加 + 3 次错误尝试）

### 2.1 数据真相源（DB Schema）

**`01-Database/01_DDL_Schema.sql`** [MeterRecord] 表定义：

```sql
CREATE TABLE dbo.MeterRecord (
    RecordId         BIGINT         IDENTITY(1,1) NOT NULL,  -- ← BIGINT PK
    DormId           INT            NOT NULL,
    DormCode         NVARCHAR(32)   NOT NULL,
    ...
    CONSTRAINT PK_MeterRecord PRIMARY KEY (RecordId),
    CONSTRAINT FK_MeterRecord_Dorm FOREIGN KEY (DormId) REFERENCES dbo.Dorm(DormId)
);
```

**结论**：MeterRecord 主键是 **BIGINT**（不是 INT）。同步检查 SQL Server 表：

```sql
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'MeterRecord' AND COLUMN_NAME = 'RecordId'
-- RecordId | bigint
```

### 2.2 EF Core 模型层（继承 BaseEntity）

**`DormManage.Shared/Models/BaseEntity.cs`**：

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }   // ← int (Int32)
}
```

**`DormManage.Shared/Models/MeterRecord.cs`** 继承 BaseEntity → Id = int。

**根因 #1**：EF 模型 `MeterRecord.Id` 是 `int (Int32)`，但 DB 是 `BIGINT (Int64)`，类型不匹配。

### 2.3 EF Core 映射层（3 次错误尝试）

| 版本 | 尝试 | 代码 | 结果 |
|------|------|------|------|
| **v2.13.6** | 「取值在 int 范围内安全」 | `entity.Property(e => e.Id).HasColumnName("RecordId");` | ❌ 假设错误：BIGINT 读回 Int32 cast 失败与值范围无关 |
| **v2.13.68** | `HasColumnType("int")` 强制 DDL | `entity.Property(e => e.Id).HasColumnName("RecordId").HasColumnType("int");` | ❌ 仅影响 CREATE TABLE DDL；EF 物化时仍然 cast 失败 |
| **v2.13.79** | **`new long Id` + `[Column("RecordId")]`** | 覆盖 BaseEntity int → long + 属性标记列名 | ✅ 真正解决类型对齐 |

### 2.4 关联一致性（API/前端/导航已正确）

| 位置 | 类型 | 状态 |
|------|------|------|
| `MeterController` 路由参数 | `long id`（12 个端点）| ✅ 已 long |
| `MeterRecordDto.Id` | `long` | ✅ 已 long |
| `MeterImage.RecordId` | `long` | ✅ 已 long |
| `SysOpLog.Id` | `long` | ✅ 已 long |
| **`MeterRecord.Id`（EF 模型）** | **`int`（继承 BaseEntity）** | ❌ **本次修复** |

**根因 #2**：EF 模型是唯一未对齐 BIGINT 的位置。Controller/DTO/导航属性/Seed 全部已 long，唯独 EF 模型 int。

### 2.5 错误抛出点（EF 物化）

`DormManage.Shared/Data/Interceptors/DatabaseOperationInterceptor.cs` 监控所有 SQL，错误日志显示：

```
SELECT [m].[RecordId], [m].[ClientCreatedAt], ..., [m].[UpdatedAt]
FROM [MeterRecord] AS [m]
ORDER BY [m].[ReadMonth] DESC, [m].[DormCode]
OFFSET @__p_0 ROWS FETCH NEXT @__p_1 ROWS ONLY
-- ↑ SQL 执行成功（200ms 内）
-- ↓ EF Materializer 失败（Int64 → Int32 cast）
```

**根因 #3**：EF Core 在 DbDataReader → CLR object 转换阶段失败：
- `SqlDataReader.GetInt32(0)` 对 BIGINT 列抛出 `InvalidCastException: Int64 → Int32`

## 三、修复方案（v2.13.79）

### 3.1 模型层：覆盖基类 Id 为 long

**`DormManage.Shared/Models/MeterRecord.cs`**：

```csharp
[Table("MeterRecord")]
public class MeterRecord : BaseEntity
{
    /// <summary>
    /// 抄表记录ID（v2.13.79 修复：SQL BIGINT ↔ EF long 类型对齐）
    /// 原 BaseEntity.Id 为 int，但 init_schema.sql [MeterRecord].[RecordId] 为 BIGINT IDENTITY(1,1)，
    /// EF 物化时 Int64 → Int32 cast 失败 → /Meter 列表 Error。
    /// 覆盖基类 int Id 为 long Id 并显式映射到 RecordId 列。
    /// </summary>
    [Column("RecordId")]
    public new long Id { get; set; }

    // ... 其他字段保持不变
}
```

**关键技术细节**：
- `new` 关键字：遮蔽（shadow）基类 `int Id`，引入新 `long Id`
- `[Column("RecordId")]`：属性级别映射到 RecordId 列（替代 DbContext HasColumnName）
- `long` 类型：与 SQL `BIGINT` 完全对齐

### 3.2 DbContext 层：移除无效的 HasColumnType("int")

**`DormManage.Shared/Data/DormDbContext.cs`** MeterRecord 配置块：

```csharp
// 之前（v2.13.68 BUG）：HasColumnType("int") 只影响 DDL，不解决读回 cast
- entity.Property(e => e.Id).HasColumnName("RecordId").HasColumnType("int");

// 之后（v2.13.79）：属性上的 [Column("RecordId")] + long 类型自动处理映射
+ // v2.13.79 修复：MeterRecord.RecordId 是 SQL BIGINT，EF 模型改 long Id + [Column("RecordId")] 映射
+ // 不再需要 HasColumnName("RecordId").HasColumnType("int")，属性上的 [Column("RecordId")] 自动处理
```

### 3.3 兼容性确认（7 处引用审计）

| 引用位置 | 类型 | 状态 |
|---------|------|------|
| `DormDbContext` Seed data `Id = 1, 2, ...` | int 字面量 → long 隐式转换 | ✅ 自动兼容 |
| `MeterController` 路由 `[HttpGet("records/{id}")]` `long id` | long | ✅ 无需改 |
| `MeterRecordDto.Id` | long | ✅ 无需改 |
| `Meter/Index.cshtml.cs` `Id = r.Id`（DTO 赋值）| long → long | ✅ 无需改 |
| `Meter/Detail.cshtml.cs` `id` 参数 | long | ✅ 无需改 |
| `BillingService._db.MeterRecords` LINQ | 自动推断 | ✅ 无需改 |
| `DashboardService._db.MeterRecords` LINQ | 自动推断 | ✅ 无需改 |

**结论**：零业务代码改动，仅修改 EF 模型 + DbContext 配置。

## 四、规则逻辑文档更新

### 4.1 EF 模型 ↔ DB 类型对齐硬规则（v2.13.79 新增 P0 规则）

| DB 列类型 | EF C# 类型 | 关系 |
|----------|-----------|------|
| `INT IDENTITY` | `int` | ✅ 1:1 |
| `BIGINT IDENTITY` | **`long`** | ✅ **v2.13.79 起强制** |
| `DECIMAL(p,s)` | `decimal` | ✅ 1:1 |
| `NVARCHAR(n)` | `string` | ✅ 1:1 |
| `DATETIME2` | `DateTime` | ✅ 1:1 |
| `BIT` | `bool` | ✅ 1:1 |
| `TINYINT` | `byte` | ✅ 1:1 |

**核心铁律**：EF 模型 ID 类型必须严格匹配 DB 列类型；**禁止**用 `HasColumnType` / `HasColumnName` 「假装」类型兼容。

### 4.2 修复失败模式（v2.13.6 / v2.13.68 的错误）

| 错误模式 | 表现 | 后果 |
|---------|------|------|
| **「取值在 int 范围内安全」假设**（v2.13.6）| 注释里写「取值在 int 范围内安全」| ❌ **BIGINT 读回 Int32 cast 失败与值范围无关**——EF 在 DbDataReader.GetInt32(0) 阶段就失败，根本不到值范围判断 |
| **`HasColumnType("int")` 强制 DDL**（v2.13.68）| `entity.Property(e => e.Id).HasColumnType("int");` | ❌ **只影响 CREATE TABLE DDL 生成**；EF 物化阶段仍然 cast 失败 |
| **「实体 int + 列名 RecordId」侥幸**（v2.13.6 → v2.13.68）| 长期运行 13 个版本侥幸未触发 | ❌ **生产 DB RecordId 一旦 ≥ 2^31 就立即崩溃**；当前生产 < 2^31 仍能跑但 /Meter 列表 SQL → EF 物化时 100% 抛 InvalidCastException |

### 4.3 同类风险表（其他 BIGINT 表已对齐）

| 表 | 主键类型 | EF 模型 | 当前状态 |
|---|---------|--------|---------|
| MeterRecord | BIGINT | `new long Id` | ✅ **v2.13.79 修复** |
| MeterImage | BIGINT | `long Id` | ✅ 已对齐（v2.13.24 P0-6） |
| SysOpLog | BIGINT | `long Id` | ✅ 已对齐 |
| DormBooking | INT | `int Id` | ✅ 一致 |
| SysEmployee | INT | `int Id` | ✅ 一致 |
| Dorm | INT | `int Id` | ✅ 一致 |

## 五、端到端验证

### 5.1 修复前（v2.13.78）

```bash
GET /Meter → HTTP 302 redirect
Location: /Error?code=INTERNAL_ERROR&message=Unable%20to%20cast%20object%20of%20type%20%27System.Int64%27%20to%20type%20%27System.Int32%27.
```

**根因日志**（Development 环境开启后）：
```
fail: DormManage.Admin.Filters.GlobalExceptionFilter[0]
[未处理异常] Unable to cast object of type 'System.Int64' to type 'System.Int32'.
   at Microsoft.EntityFrameworkCore.Query.Internal.EntityFrameworkCoreQueryable`1.GetEnumerator()
   at DormManage.Admin.Pages.Meter.IndexModel.OnGetAsync()
```

### 5.2 修复后（v2.13.79）

```bash
GET /Meter → HTTP 200 OK, 58628 bytes
GET /Meter/Detail?id=3 → HTTP 200 OK, 27495 bytes
GET /api/meter/records?pageSize=20 → HTTP 200, items=[{id:3, dormCode:"A101", ...}]
GET /api/meter/records/3 → HTTP 200, data.id=3
```

**表格渲染验证**：
```html
<tbody>
  <tr>
    <td>1</td><td>A101</td><td>2026-08</td>
    <td>130.50</td><td>24.80</td><td>580.50</td>
    <td>0.00</td><td>0.00</td><td>0.00</td>
    <td>admin（后台补录）</td><td>-</td>
    <td>2026-07-21 08:57</td>
    <td><span class="badge bg-success">正常</span></td>
    <td>详情</td>
  </tr>
</tbody>
```

**14 列全部正确渲染**（序号 / 房号 / 月份 / 冷水 / 热水 / 电表 / 冷水用量 / 热水用量 / 电用量 / 抄表员 / 设备 / 抄表时间 / 状态 / 操作）。

### 5.3 覆盖率统计验证

```html
<div class="alert alert-danger ...">
  <i class="bi bi-pie-chart-fill"></i>
  <strong>2026-07</strong> 抄表进度：
  已抄 <strong>0</strong> / <strong>140</strong> 间（<strong>0%</strong>）
  · 未完成 <strong>140</strong> 间
</div>
```

覆盖率 SQL 查询（`MeterRecord.Status IN (1, 2)`）正常执行 → 覆盖率统计正确。

### 5.4 编译验证

```
dotnet build DormManage.sln -c Release
→ 0 错误
```

## 六、关键决策与权衡

| 决策 | 理由 |
|------|------|
| **`new long Id` 覆盖而非泛型 BaseEntity** | 影响最小（只改 MeterRecord），不破坏其他 30 个实体；BaseEntity 改为泛型需要全面重构 |
| **`[Column("RecordId")]` 属性而非 DbContext HasColumnName** | 属性级别标注更清晰；与 MeterImage.cs 已有模式一致 |
| **保留 Seed data `Id = 1, 2, ...` int 字面量** | C# int → long 隐式转换自动兼容；零代码改动 |
| **不修改 SQL Server 表 DDL** | 数据兼容 + 零维护成本；其他 2 张 BIGINT 表（MeterImage / SysOpLog）也是 EF long ↔ SQL BIGINT 模式 |
| **不发数据库迁移脚本** | DB 物理结构无变更；纯 EF 模型层修正 |

## 七、版本历史

| 版本 | 内容 | 状态 |
|------|------|------|
| v2.13.6 | EF 模型对齐修复：`HasColumnName("RecordId")` + 注释「取值在 int 范围内安全」 | ❌ 错误假设 |
| v2.13.68 | `HasColumnType("int")` 强制 DDL | ❌ 仅 DDL 层 |
| **v2.13.79** | **`new long Id` + `[Column("RecordId")]`** | ✅ **本次彻底修复** |

## 八、关联历史

| # | 文档 | 关系 |
|---|------|------|
| 1 | `00-方案文档/65-EF实体与真实Schema对齐修复报告-v2.13.6.md` | 「取值在 int 范围内安全」错误假设来源 |
| 2 | `00-方案文档/125-每页条数分页BUG修复-v2.13.74.md` | 备注 v2.13.68 已追踪 Meter 列表 BIGINT cast 问题 |
| 3 | `00-方案文档/76-入住记录与抄表记录业务深度文档-v2.13.24.md` | MeterRecord 业务字段深度文档 |
| 4 | `00-方案文档/75-数据库Schema与代码映射文档-v2.13.24.md` | 数据库 Schema 与代码映射 |
| 5 | `00-方案文档/08-抄表记录需求-v2.11.md` | 抄表记录原始需求（FK→MeterRecord.Id 标注） |
| 6 | `00-方案文档/129-人员清单班组FK关联修复-v2.13.78.md` | 前置版本（班组 FK） |

## 九、核心教训

1. **「类型不匹配」是 EF Core 物化阶段的隐性 BUG 重灾区**：EF 在 DbDataReader.GetXxx() 阶段就抛 cast 异常，根本不到 LINQ 投影层
2. **`HasColumnType("int")` 是 DDL-only 指令**：只影响 EF 生成 CREATE TABLE 的列类型；不解决 EF 读回阶段的 cast
3. **「取值在 int 范围内安全」是错误的安全感**：BIGINT → Int32 cast 失败与值大小无关；只要类型不匹配，100% 失败
4. **「属后续增强」注释是定时炸弹（重演 v2.13.78）**：v2.13.6 注释埋下「取值在 int 范围内安全」—— 这个错误假设跨越 13 个版本才被 v2.13.79 彻底解决。修复原则：**遇到类型不匹配 + 「暂且」注释，立即修**
5. **关联一致性审计**：Controller/DTO/导航属性/Seed 全部已 long，唯独 EF 模型 int——**逐层审计才能发现遗漏**。修复原则：**类型相关变更必须全链路审计**
6. **运行时错误日志的「隐身」特性**：`HasColumnType("int")` 静默成功，运行时才报错；测试环境 + Dev environment 是发现此类 BUG 的唯一路径

---

**版本**：v2.13.79（2026-07-21）
**作者**：Claude Sonnet 4.6 + 用户反馈驱动
**Commit**：pending