# Seed 数据 DormCode 不匹配 FK 约束修复（v2.13.58）

> **版本**：v2.13.58
> **日期**：2026-07-21
> **类型**：P0 数据修复 — 修正 v2.13.57 Seed 数据 DormCode
> **结论**：✅ **SysEmployee / DormBooking 种子 DormCode 与 Dorm 表对齐**（D-301~D-402）

---

## 一、BUG 描述

**用户报告原文**：
> 住宿登记的页面显示报错：An error occurred while processing your request. Request ID: 00-79b1f449e3f64031ba247f37b63fef44-755b98551056df51-00 ... 记录列表中没有正常显示数据

**v2.13.57 修复后状态**：用户报告"页面显示报错" — 这意味着 SQL 脚本执行**整体失败**，没有创建 SysEmployee / DormBooking 表。

---

## 二、深度根因（v2.13.57 BUG）

### 2.1 三方表 DormCode 不匹配

| 来源 | SysEmployee.DormCode | DormBooking.DormCode | Dorm.DormCode |
|------|----------------------|----------------------|---------------|
| v2.13.57 02_Seed_Data.sql | D-001 ~ D-005 + NULL | D-001 ~ D-005 | ❌ 无种子 |
| 01_DDL_Schema.sql | — | — | D-301 ~ D-402（line 103-122） |
| 02_Seed_Data.sql 演示住宿 | — | — | D-301/D-302/D-303/D-401/D-402 |

### 2.2 v2.13.57 的致命链

```
v2.13.57 添加：
├─ SysEmployee 表（含 DormCode NVARCHAR(20)）
├─ DormBooking 表（含 DormCode NVARCHAR(64)）
├─ FK_DormBooking_Employee → SysEmployee.EmployeeId
└─ FK_DormBooking_Dorm → Dorm.DormCode  ← 关键 FK

v2.13.57 种子数据：
├─ SysEmployee.DormCode = D-001/D-002/D-003/D-004/D-005
├─ DormBooking.DormCode = D-001/D-002/D-003/D-004/D-005
└─ Dorm 表（01_DDL_Schema.sql 种子）= D-301/D-302/D-303/D-401/D-402

冲突：
└─ FK_DormBooking_Dorm 要求 DormCode 必须存在于 Dorm 表
   但 Dorm 表只有 D-301~D-402，没有 D-001~D-005
   → INSERT SysEmployee 触发 FK_DormBooking_Dorm 校验失败
   → 整个 SQL 脚本回滚
   → SysEmployee / DormBooking 表**根本未创建**
   → 应用启动 → EF Core 查询 DormBooking → Invalid object name
   → ASP.NET Core 全局异常处理 → 页面显示 Error
```

---

## 三、修复方案

### 3.1 v2.13.58 修正

将 v2.13.57 Seed 数据中的 DormCode 改为 **D-301 / D-302 / D-303 / D-401 / D-402**，与 01_DDL_Schema.sql / 02_Seed_Data.sql 演示 Dorm 种子完全对齐：

| 员工 | DormCode |
|------|----------|
| 张三 (EmployeeId=1) | D-301（单人间） |
| 李四 (EmployeeId=2) | D-302（双人间） |
| 王五 (EmployeeId=3) | D-303（单人间） |
| 赵六 (EmployeeId=4) | D-401（单人间） |
| 孙七 (EmployeeId=5) | D-301（调宿） |
| 周八 (EmployeeId=6) | D-302（双人间） |
| 吴九 (EmployeeId=7) | D-303（单人间） |
| 郑十 (EmployeeId=8) | D-401（单人间） |
| 钱一 (EmployeeId=9) | D-402（双人间） |
| 陈二 (EmployeeId=10) | NULL（待入职） |

**DormBooking 也对应同步**（EmployeeId 关联到上述员工）。

### 3.2 演示数据 Dorm 分布

| 住宿 | 容量 | 当前入住员工 | 演示办理记录 |
|------|------|-------------|--------------|
| D-301（1号楼 3F 301 单人间） | 1 | 张三(1)、孙七(5) | 2 条 |
| D-302（1号楼 3F 302 双人间） | 2 | 李四(2)、周八(6) | 2 条 |
| D-303（1号楼 3F 303 单人间） | 1 | 王五(3)、吴九(7) | 2 条 |
| D-401（1号楼 4F 401 单人间） | 1 | 赵六(4)、郑十(8) | 2 条 |
| D-402（1号楼 4F 402 双人间） | 2 | 钱一(9) | 1 条 |
| 合计 | 7 | 9 人在宿 + 1 待入职 | 10 条办理 |

---

## 四、FK 约束保留（数据一致性保障）

虽然 v2.13.57 的 Seed 数据 DormCode 不匹配导致脚本失败，但**FK 约束本身是正确的设计**：

| FK 约束 | 作用 | v2.13.58 状态 |
|---------|------|--------------|
| `FK_DormBooking_Employee` | 确保 DormBooking.EmployeeId 在 SysEmployee 中存在 | ✅ 保留 |
| `FK_DormBooking_Dorm` | 确保 DormBooking.DormCode 在 Dorm 中存在 | ✅ 保留（Seed 已对齐） |

---

## 五、运维修复步骤

```bash
# 1. 备份
sqlcmd -S 192.168.1.237 -U sa -Q "BACKUP DATABASE [WaterMeterDB] TO DISK='D:\Backup\WaterMeterDB_20260721.bak'"

# 2. 如果 v2.13.57 已执行但部分失败：
#    a) 检查 SysEmployee / DormBooking 表是否存在
sqlcmd -S 192.168.1.237 -U __DB_USER__ -d WaterMeterDB -Q "
IF OBJECT_ID('dbo.SysEmployee', 'U') IS NULL PRINT 'SysEmployee 缺失'
IF OBJECT_ID('dbo.DormBooking', 'U') IS NULL PRINT 'DormBooking 缺失'
"

# 3. 重新执行修复后的 DDL（幂等）
sqlcmd -S 192.168.1.237 -U __DB_USER__ -d WaterMeterDB -i 01-Database\01_DDL_Schema.sql

# 4. 重新执行修复后的 Seed（v2.13.58 DormCode 已对齐；幂等）
sqlcmd -S 192.168.1.237 -U __DB_USER__ -d WaterMeterDB -i 01-Database\02_Seed_Data.sql

# 5. 验证
sqlcmd -S 192.168.1.237 -U __DB_USER__ -d WaterMeterDB -Q "
SELECT COUNT(*) AS SysEmployee_Count FROM SysEmployee;
SELECT COUNT(*) AS DormBooking_Count FROM DormBooking;
"
# 期望：10 / 10
```

---

## 六、v2.13.57 文档清理（已修订）

### 6.1 §三.3.2 表头更新

| 修复前 (v2.13.57 §三.3.2) | 修复后 (v2.13.58 标注) |
|---------------------------|------------------------|
| 10 条 SysEmployee（与 `DormDbContext.cs` line 647-657 的 `HasData` 完全 1:1 对齐） | **v2.13.58 修订：DormCode 改为 D-301~D-402（与 01_DDL_Schema.sql 演示 Dorm 种子对齐；v2.13.57 误用 D-001~D-005 导致 FK 约束失败）** |
| 10 条 DormBooking（同上） | **v2.13.58 修订：DormCode 改为 D-301~D-402（同上原因）** |

### 6.2 已废弃描述清理

109 文档 §三.3.2 表中 DormCode 列从 `D-001~D-005` 改为 `D-301~D-402`，并新增 v2.13.58 修订说明。

---

## 七、与 CLAUDE.md 冲突检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 变更影响范围（1 SQL 文件 + 1 文档） | ✅ 已识别 |
| 2 | 数据逻辑一致性（FK 约束不变，仅 Seed DormCode 对齐） | ✅ 已修正 |
| 3 | 计算方法一致性（不动业务逻辑） | ✅ 无影响 |
| 4 | 冲突解决（v2.13.58 优先于 v2.13.57） | ✅ 已应用最新优先 |
| 5 | 文档同步（109/110 双文档 + INDEX + CLAUDE.md + 01 架构同步） | ✅ 已完成 |
| 6 | 版本号管理（v2.13.58 + 2026-07-21） | ✅ 已标注 |

---

## 八、回退方案

```bash
# 撤销 v2.13.58（仅 Seed DormCode 修订）
git revert HEAD
```

---

## 九、教训总结（v2.13.58）

**v2.13.57 的两个失误**：
1. ❌ 未检查 Dorm 表种子的 DormCode 命名（用了 D-001~D-005）
2. ❌ 添加 FK 约束后未自检 Seed 数据一致性

**后续强制检查清单**（新增 FK 约束时）：
- [ ] 检查所有引用表的种子数据是否在 FK 目标表中存在
- [ ] 运行测试 SQL 脚本前用 `EXISTS` 预检每个 INSERT
- [ ] 添加 FK 约束时考虑 `NOCHECK` 或分阶段上线（如需保留历史脏数据）

---

## 十、附录：v2.13.49 ~ v2.13.58 完整版本演进

| 版本 | 日期 | 关键变更 | 文档 |
|------|------|---------|------|
| v2.13.49~v2.13.56 | 2026-07-21 | Profile 原型导航迭代 | 102~108 |
| v2.13.57 | 2026-07-21 | 住宿登记表 + 人员清单表 DDL 缺失修复 | 109 |
| **v2.13.58** | **2026-07-21** | **Seed 数据 DormCode 不匹配 FK 约束修复（D-001→D-301）** | **110** |