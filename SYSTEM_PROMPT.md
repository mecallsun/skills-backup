# SYSTEM CONSTRAINT: ZERO CREATIVE FREEDOM

> **生效日期**：2026-07-15  
> **适用版本**：v2.13.3  
> **数据源**：`init_schema.sql`（1:1 绝对真理源）

---

## 🛑 STRICT PROHIBITIONS

1. **DO NOT** add any extra fields, buttons, decorations, or routes not explicitly defined in `init_schema.sql` 或需求文档。
2. **DO NOT** modify any existing table structures, primary keys, foreign keys, or database constraints.
3. **DO NOT** compress, skip, or placeholder any logic. Code generation must be 100% complete. **No `// ... 保持原有代码不变`**.
4. If you notice any ambiguity, **STOP and ask**. Never hallucinate or assume.
5. All EF Core models must match `init_schema.sql` column names, types, lengths, and constraints **exactly**.

## 🧱 FIXED TECH STACK

| 层级 | 技术选型 |
|------|---------|
| 后端框架 | .NET 8 ASP.NET Core |
| 前端框架 | Razor Pages + Bootstrap 5 + jQuery |
| ORM | Entity Framework Core 8 |
| 数据库 | SQL Server（生产）/ SQLite（开发） |
| 部署 | EXE 自托管 + 托盘守护 |

## 📊 DATABASE TRUTH SOURCE

- **DDL 定义**：`init_schema.sql`（23 张表 + 1 视图）
- **EF Core 模型**：`DormManage.Shared/Data/DormDbContext.cs`
- **生产服务器**：`192.168.1.237 / WaterMeterDB`
- **连接串**：`Server=192.168.1.237;Database=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=True;`

## 📋 TABLE INVENTORY（23 Tables + 1 View）

| # | 表名 | 列数 | 行数 | 说明 |
|---|------|------|------|------|
| 1 | Address | 7 | 2 | 地址 |
| 2 | AttendanceType | 9 | 6 | 考勤班次 |
| 3 | BillingStandard | 11 | 3 | 费用标准 |
| 4 | Building | 7 | 2 | 楼栋 |
| 5 | Department | 8 | 8 | 部门 |
| 6 | Dorm | 24 | 140 | 宿舍档案 |
| 7 | DormBooking | 17 | 337 | 办理登记 |
| 8 | EmployeeType | 8 | 5 | 员工类型 |
| 9 | EmploymentStatus | 8 | 3 | 在职状态 |
| 10 | Floor | 7 | 6 | 楼层 |
| 11 | MeterImage | 11 | 0 | 图片附件 |
| 12 | MeterRecord | 20 | 0 | 抄表记录 |
| 13 | MeterUnit | 9 | 3 | 计量单位 |
| 14 | PdaDevice | 9 | 0 | PDA 设备 |
| 15 | ResidenceStatus | 8 | 3 | 住宿状态 |
| 16 | SysConfig | 6 | 5 | 系统配置 |
| 17 | SysEmployee | 22 | 906 | 员工档案 |
| 18 | SysOpLog | 8 | 0 | 操作日志 |
| 19 | SysRole | 6 | 3 | 角色 |
| 20 | SysUser | 13 | 3 | 系统用户 |
| 21 | SysUserRole | 2 | 3 | 用户角色关联 |
| 22 | Team | 8 | 11 | 班组 |
| 23 | (视图) v_MeterRecordDetail | — | — | 抄表详情视图 |

## 🔗 FOREIGN KEYS（4 条）

| 表 | 列 | 引用表 | 引用列 |
|---|---|---|---|
| MeterImage | RecordId | MeterRecord | RecordId (CASCADE DELETE) |
| MeterRecord | DormId | Dorm | DormId |
| SysUserRole | UserId | SysUser | UserId (CASCADE DELETE) |
| SysUserRole | RoleId | SysRole | RoleId (CASCADE DELETE) |
