using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 数据库初始化器（v2.13.25 生产启动机制）
///
/// 职责：
/// 1. 测试数据库连通性（不抛异常，返回具体错误信息）
/// 2. 检测关键表是否存在（EF 模型 vs 实际数据库）
/// 3. SQLite 走 EnsureCreated；SQL Server 在缺失时打印告警提示运维手工执行 init_schema.sql
/// 4. 初始化基础字典数据（Department/Building/EmployeeType 等）
/// 5. 创建默认管理员账号 admin / admin123
///
/// 设计原则：
/// - 所有操作幂等（重复执行不报错、不重复插入）
/// - 任何步骤失败仅记录日志，不阻塞应用启动
/// - 在 Web/Api 启动时同步调用，避免后台任务的延迟
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>关键表清单（任一缺失视为 DB 未初始化）</summary>
    public static readonly string[] CriticalTables = new[]
    {
        "SysUser", "SysRole", "SysPermission", "SysRolePermission", "SysEmployee",
        "Dorm", "DormBooking", "MeterRecord", "SysFieldPermission",
        "Department", "Building", "Floor", "EmployeeType", "MeterUnit",
        "BillingStandard", "AppVersion", "SysConfig", "SysParameter"
    };

    /// <summary>
    /// 启动校验 + 初始化（应用启动时调用一次）
    /// </summary>
    /// <returns>启动报告（含连接、缺失表、种子统计）</returns>
    public static async Task<StartupReport> InitializeAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken ct = default)
    {
        var report = new StartupReport
        {
            StartedAt = DateTime.Now,
            Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown"
        };

        try
        {
            // 第 1 步：解析 DbContext
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();

            // 第 2 步：连通性探测 + Provider 识别
            report.Provider = db.Database.ProviderName ?? "Unknown";
            try
            {
                report.ConnectionOk = await db.Database.CanConnectAsync(ct);
                if (!report.ConnectionOk)
                {
                    logger.LogWarning("[Startup] ⚠ 数据库连接失败：CanConnectAsync 返回 false");
                    report.Warnings.Add("数据库连接失败");
                    return report;
                }
                logger.LogInformation("[Startup] ✓ 数据库连接成功（Provider: {Provider}）", report.Provider);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Startup] ✗ 数据库连接异常");
                report.Warnings.Add($"数据库连接异常：{ex.Message}");
                return report;
            }

            // 第 3 步：检测关键表
            var missingTables = await DetectMissingTablesAsync(db, logger, ct);
            report.MissingTables = missingTables;

            if (missingTables.Count > 0)
            {
                logger.LogWarning("[Startup] ⚠ 关键表缺失 {N} 张: {Tables}",
                    missingTables.Count, string.Join(", ", missingTables));
                logger.LogWarning("[Startup]    提示：请执行 01-Database/01_DDL_Schema.sql 与 02_Seed_Data.sql");
                // v2.13.109: SQLite 已移除，不再调用 EnsureCreatedAsync；SQL Server Schema 由 init_schema.sql 管理
            }
            else
            {
                logger.LogInformation("[Startup] ✓ {N} 张关键表全部存在", CriticalTables.Length);
            }

            // 第 4 步：种子基础字典
            report.SeedCounts = await SeedDictionariesAsync(db, logger, ct);

            // 第 5 步：种子默认管理员
            report.AdminSeeded = await SeedDefaultAdminAsync(db, logger, ct);

            // 第 6 步：v2.13.99 启动迁移 — 补建 SysFieldPermission 表 + 隐私字段权限种子
            // 原因：v2.13.92 引入的新表/新权限码，EF Core EnsureCreated() 对既有 DB 不生效，
            //       导致 SQLite 生产库至今缺失，Html.IsFieldHiddenAsync 链路全部短路返回 false，
            //       表现为「未勾选的隐私字段仍能显示」。
            report.FieldPermissionMigrated = (await MigrateFieldPermissionAsync(db, logger, ct)).AllSucceeded;

            // 第 7 步：写入 AppVersion 当前版本
            report.AppVersionSeeded = await SyncAppVersionAsync(db, report.Version, logger, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Startup] ✗ 初始化过程异常（非阻塞，应用将继续启动）");
            report.Warnings.Add($"初始化异常：{ex.Message}");
        }
        finally
        {
            report.FinishedAt = DateTime.Now;
            report.DurationMs = (long)(report.FinishedAt - report.StartedAt).TotalMilliseconds;
        }

        return report;
    }

    /// <summary>
    /// 检测关键表是否存在（v2.13.109 起仅 SQL Server）
    /// </summary>
    private static async Task<List<string>> DetectMissingTablesAsync(
        DormDbContext db, ILogger logger, CancellationToken ct)
    {
        var missing = new List<string>();

        try
        {
            var sql = $@"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
                         WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME IN ({string.Join(",", CriticalTables.Select(t => $"'{t}'"))})";
            var tables = await db.Database.SqlQueryRaw<string>(sql).ToListAsync(ct);
            foreach (var t in CriticalTables)
            {
                if (!tables.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    missing.Add(t);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Startup] ⚠ 关键表检测异常（视为全部存在，避免误报）");
        }

        return missing;
    }

    /// <summary>
    /// 种子基础字典（幂等 — 仅在任何记录为空时插入）
    /// </summary>
    private static async Task<Dictionary<string, int>> SeedDictionariesAsync(
        DormDbContext db, ILogger logger, CancellationToken ct)
    {
        var counts = new Dictionary<string, int>();

        // 部门（Department: Code/Name/SortOrder/Remark，无 IsActive）
        if (!await db.Departments.AnyAsync(ct))
        {
            db.Departments.AddRange(
                new Department { Code = "ADMIN", Name = "行政部", SortOrder = 1 },
                new Department { Code = "PROD", Name = "生产部", SortOrder = 2 },
                new Department { Code = "QC", Name = "品控部", SortOrder = 3 },
                new Department { Code = "LOG", Name = "物流部", SortOrder = 4 },
                new Department { Code = "FIN", Name = "财务部", SortOrder = 5 },
                new Department { Code = "RND", Name = "研发部", SortOrder = 6 }
            );
            counts["Department"] = 6;
        }

        // 楼栋（Building: Name/SortOrder/Remark）
        if (!await db.Buildings.AnyAsync(ct))
        {
            db.Buildings.AddRange(
                new Building { Name = "A栋", SortOrder = 1 },
                new Building { Name = "B栋", SortOrder = 2 },
                new Building { Name = "C栋", SortOrder = 3 }
            );
            counts["Building"] = 3;
        }

        // 楼层（Floor: FloorNo/Remark）
        if (!await db.Floors.AnyAsync(ct))
        {
            db.Floors.AddRange(
                new Floor { FloorNo = 1 },
                new Floor { FloorNo = 2 },
                new Floor { FloorNo = 3 }
            );
            counts["Floor"] = 3;
        }

        // 员工类型（EmployeeType 假设为通用字典结构）
        if (!await db.EmployeeTypes.AnyAsync(ct))
        {
            db.EmployeeTypes.AddRange(
                new EmployeeType { Code = "FULLTIME", Name = "正式工" },
                new EmployeeType { Code = "CONTRACT", Name = "合同工" },
                new EmployeeType { Code = "INTERN", Name = "实习生" },
                new EmployeeType { Code = "ONSITE", Name = "驻场" }
            );
            counts["EmployeeType"] = 4;
        }

        // 考勤班次
        if (!await db.AttendanceTypes.AnyAsync(ct))
        {
            db.AttendanceTypes.AddRange(
                new AttendanceType { Code = "DAY", Name = "白班" },
                new AttendanceType { Code = "NIGHT", Name = "夜班" },
                new AttendanceType { Code = "MIXED", Name = "综合班" }
            );
            counts["AttendanceType"] = 3;
        }

        // 仪表单位
        if (!await db.MeterUnits.AnyAsync(ct))
        {
            db.MeterUnits.AddRange(
                new MeterUnit { Code = "COLD", Name = "冷水" },
                new MeterUnit { Code = "HOT", Name = "热水" },
                new MeterUnit { Code = "ELEC", Name = "电" }
            );
            counts["MeterUnit"] = 3;
        }

        // 住宿状态（ResidenceStatus: Code/Name/Remark）
        if (!await db.ResidenceStatuses.AnyAsync(ct))
        {
            db.ResidenceStatuses.AddRange(
                new ResidenceStatus { Code = "IN", Name = "在宿" },
                new ResidenceStatus { Code = "OUT", Name = "退宿" },
                new ResidenceStatus { Code = "RESERVE", Name = "预约入住" }
            );
            counts["ResidenceStatus"] = 3;
        }

        // 在职状态（EmploymentStatus: Code/Name/Remark）
        if (!await db.EmploymentStatuses.AnyAsync(ct))
        {
            db.EmploymentStatuses.AddRange(
                new EmploymentStatus { Code = "ACTIVE", Name = "在职" },
                new EmploymentStatus { Code = "PROBATION", Name = "试用期" },
                new EmploymentStatus { Code = "LEFT", Name = "离职" }
            );
            counts["EmploymentStatus"] = 3;
        }

        // 班组（Team: Id/Name/Code/SortOrder/IsActive/CreatedAt，无 BaseEntity）
        if (!await db.Teams.AnyAsync(ct))
        {
            var now = DateTime.Now;
            db.Teams.AddRange(
                new Team { Code = "DEFAULT", Name = "默认班", SortOrder = 1, IsActive = true, CreatedAt = now },
                new Team { Code = "A", Name = "A班", SortOrder = 2, IsActive = true, CreatedAt = now },
                new Team { Code = "B", Name = "B班", SortOrder = 3, IsActive = true, CreatedAt = now },
                new Team { Code = "C", Name = "C班", SortOrder = 4, IsActive = true, CreatedAt = now },
                new Team { Code = "D", Name = "D班", SortOrder = 5, IsActive = true, CreatedAt = now }
            );
            counts["Team"] = 5;
        }

        // 地址（Address: AddressText/Remark）
        if (!await db.Addresses.AnyAsync(ct))
        {
            db.Addresses.Add(
                new Address { AddressText = "江苏省苏州市吴中区金戈新材料厂区" }
            );
            counts["Address"] = 1;
        }

        // 费用标准（默认）
        if (!await db.BillingStandards.AnyAsync(ct))
        {
            db.BillingStandards.Add(new BillingStandard
            {
                StandardName = "默认费用标准（2026）",
                ApplicableType = "ONSITE",
                ColdWaterUnitPrice = 3.50m,
                HotWaterUnitPrice = 18.00m,
                ElectricUnitPrice = 0.62m,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                EffectiveTo = new DateOnly(2026, 12, 31),
                IsActive = true
            });
            counts["BillingStandard"] = 1;
        }

        // v2.13.26 启动种子：默认 admin 的安全问题（仅当 SysUserSecurityQuestion 表为空时执行）
        var adminIdForSq = await db.SysUsers
            .Where(u => u.UserName == "admin")
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);
        if (adminIdForSq.HasValue && !await db.SysUserSecurityQuestions.AnyAsync(q => q.UserId == adminIdForSq.Value, ct))
        {
            db.SysUserSecurityQuestions.AddRange(
                new SysUserSecurityQuestion
                {
                    UserId = adminIdForSq.Value,
                    QuestionIndex = 1,
                    Question = "您出生的城市是？",
                    AnswerHash = AesEncryptor.Encrypt("suzhou"),   // 默认 admin 密码找回答案（小写）
                    CreatedAt = DateTime.Now
                },
                new SysUserSecurityQuestion
                {
                    UserId = adminIdForSq.Value,
                    QuestionIndex = 2,
                    Question = "您的工号末四位是？",
                    AnswerHash = AesEncryptor.Encrypt("0000"),
                    CreatedAt = DateTime.Now
                }
            );
            counts["SysUserSecurityQuestion"] = 2;
        }

        if (counts.Any())
        {
            try
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("[Startup] ✓ 已种子基础字典: {Names}",
                    string.Join(", ", counts.Select(c => $"{c.Key}={c.Value}")));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Startup] ✗ 字典种子保存失败");
            }
        }

        return counts;
    }

    /// <summary>
    /// 种子默认管理员（admin / admin123, SHA-256(Salt+Pwd)）
    /// 使用 DbSet 名 SysUsers/SysRoles/SysUserRoles
    /// </summary>
    private static async Task<bool> SeedDefaultAdminAsync(
        DormDbContext db, ILogger logger, CancellationToken ct)
    {
        try
        {
            if (await db.SysUsers.AnyAsync(u => u.UserName == "admin", ct))
                return false;

            // 计算 SHA256(salt + pwd) → 与 02_Seed_Data.sql 一致
            const string salt = "WaterMeter2026";
            const string pwd = "admin123";
            var hash = ComputeSha256(salt + pwd);

            db.SysUsers.Add(new SysUser
            {
                UserName = "admin",
                PasswordHash = hash,
                Salt = salt,
                DisplayName = "系统管理员",
                Phone = "13800000000",
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync(ct);

            // 分配 Admin 角色（先查，没有则创建）
            var adminUser = await db.SysUsers.FirstOrDefaultAsync(u => u.UserName == "admin", ct);
            var adminRole = await db.SysRoles.FirstOrDefaultAsync(r => r.RoleCode == "Admin", ct);

            if (adminRole == null)
            {
                db.SysRoles.Add(new SysRole
                {
                    RoleCode = "Admin",
                    RoleName = "系统管理员",
                    Description = "默认管理员（启动自动种子）",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
                await db.SaveChangesAsync(ct);
                adminRole = await db.SysRoles.FirstOrDefaultAsync(r => r.RoleCode == "Admin", ct);
            }

            if (adminUser != null && adminRole != null)
            {
                db.SysUserRoles.Add(new SysUserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id
                });
                await db.SaveChangesAsync(ct);
            }

            logger.LogInformation("[Startup] ✓ 已创建默认管理员账号 admin / admin123");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Startup] ⚠ 默认管理员种子失败（已有或异常）");
            return false;
        }
    }

    /// <summary>
    /// 同步当前 DLL 版本到 AppVersion 表（标记 IsLatest）
    /// </summary>
    private static async Task<bool> SyncAppVersionAsync(
        DormDbContext db, string version, ILogger logger, CancellationToken ct)
    {
        try
        {
            if (await db.AppVersions.AnyAsync(ct))
                return false;

            db.AppVersions.Add(new AppVersion
            {
                Version = version,
                IsLatest = true,
                IsEnabled = false,  // PDA 端不下载此版本（这是 server 端版本）
                ReleaseNotes = "Server-side build（启动时自动登记）",
                ReleaseDate = DateTime.Now,
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[Startup] ✓ AppVersion 已登记: {Ver}", version);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Startup] ⚠ AppVersion 登记失败");
            return false;
        }
    }

    /// <summary>
    /// v2.13.99 启动迁移：补建 SysFieldPermission 表 + v2.13.92 隐私字段权限种子
    ///
    /// 背景：
    ///   v2.13.92 引入 SysFieldPermission 新表 + 3 个权限码（settings:fields / fieldpermission:edit / privacy:field:enable）
    ///   + admin 3 条角色权限关联 + 5 个敏感字段种子。但 EF Core Database.EnsureCreated()
    ///   仅对不存在的 DB 完整建表，对既有 DB 立即 return，导致这些 schema 变更从未落到
    ///   SQLite 生产库（dorm.db / DormManage.Admin/dorm.db）。
    ///
    /// 后果：
    ///   Html.IsFieldHiddenAsync → HasPrivacyFieldEnabledAsync → GetUserPermissionCodesAsync
    ///   → 查不到 PermissionCode='privacy:field:enable'（SysPermission 表无 Id=39）
    ///   → 整条隐私判定链路短路返回 false → 所有字段始终可见。
    ///
    /// 修复：
    ///   启动时检测 SysFieldPermission 表是否存在，缺失则按 Provider (SQLite/SQL Server)
    ///   分别执行 DDL 补建 + 幂等 INSERT（WHERE NOT EXISTS 守卫）。
    /// </summary>
    public static async Task<SeedMigrationResult> MigrateFieldPermissionAsync(
        DormDbContext db, ILogger logger, CancellationToken ct)
    {
        var result = new SeedMigrationResult();
        try
        {
            // v2.13.109 起 SQLite 已移除；统一使用 SQL Server 语法

            // 0. v2.13.120 检测 DormMeter 表是否存在（设备档案 — 与 Dorm 1:1）
            //    若不存在则自动创建（无需手动执行 init_schema.sql，避免部署遗漏）
            try
            {
                var dormMeterTableExists = await db.Database.SqlQueryRaw<string>(
                    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DormMeter'")
                    .ToListAsync(ct);
                if (dormMeterTableExists.Count == 0)
                {
                    logger.LogInformation("[v2.13.120 Migrate] DormMeter 表缺失，开始创建...");
                    var createDormMeterSql = @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DormMeter')
                            BEGIN
                                CREATE TABLE [dbo].[DormMeter] (
                                    [DormMeterId] INT IDENTITY(1,1) NOT NULL,
                                    [DormId] INT NOT NULL,
                                    [ElectricMeterId] NVARCHAR(64) NULL,
                                    [ColdWaterMeterId] NVARCHAR(64) NULL,
                                    [HotWaterMeterId] NVARCHAR(64) NULL,
                                    [Remark] NVARCHAR(500) NULL,
                                    [IsActive] BIT NOT NULL DEFAULT 1,
                                    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
                                    [UpdatedAt] DATETIME NULL DEFAULT GETDATE(),
                                    CONSTRAINT [PK_DormMeter] PRIMARY KEY CLUSTERED ([DormMeterId]),
                                    CONSTRAINT [FK_DormMeter_Dorm] FOREIGN KEY ([DormId])
                                        REFERENCES [dbo].[Dorm]([DormId]) ON DELETE CASCADE,
                                    CONSTRAINT [UX_DormMeter_DormId] UNIQUE ([DormId])
                                );
                            END";
                    await db.Database.ExecuteSqlRawAsync(createDormMeterSql, ct);
                    logger.LogInformation("[v2.13.120 Migrate] DormMeter 表已创建（含 FK + UNIQUE INDEX）");
                }
            }
            catch (Exception exDormMeter)
            {
                logger.LogWarning(exDormMeter, "[v2.13.120 Migrate] DormMeter 表检测/创建异常（继续后续迁移）");
            }

            // 0.6 v2.13.130 检测 EquipmentReading 表是否存在（设备读数日志 — 与 DormMeter 配置层 + MeterRecord 聚合层解耦）
            //    独立日志表，不 FK 到 DormMeter（PDA 原始上传流水可能没经过设备档案配置）
            //    若不存在则自动创建（无需手动执行 init_schema.sql，避免部署遗漏）
            try
            {
                var eqReadingTableExists = await db.Database.SqlQueryRaw<string>(
                    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EquipmentReading'")
                    .ToListAsync(ct);
                if (eqReadingTableExists.Count == 0)
                {
                    logger.LogInformation("[v2.13.130 Migrate] EquipmentReading 表缺失，开始创建...");
                    var createEqReadingSql = @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EquipmentReading')
                            BEGIN
                                CREATE TABLE [dbo].[EquipmentReading] (
                                    [ReadingId]       INT             IDENTITY(1,1) NOT NULL,
                                    [EquipmentId]     NVARCHAR(64)    NOT NULL,
                                    [EquipmentType]   TINYINT         NOT NULL,
                                    [Reading]         DECIMAL(12,2)   NOT NULL DEFAULT 0,
                                    [ReadTime]        DATETIME        NOT NULL,
                                    [Remark]          NVARCHAR(500)   NULL,
                                    [CreatedBy]       NVARCHAR(64)    NULL,
                                    [CreatedAt]       DATETIME        NOT NULL DEFAULT GETDATE(),
                                    [UpdatedAt]       DATETIME        NULL DEFAULT GETDATE(),
                                    CONSTRAINT [PK_EquipmentReading] PRIMARY KEY CLUSTERED ([ReadingId]),
                                    CONSTRAINT [CK_EquipmentReading_Type] CHECK ([EquipmentType] BETWEEN 1 AND 3)
                                );
                                CREATE NONCLUSTERED INDEX [IX_EquipmentReading_EquipmentId] ON [dbo].[EquipmentReading] ([EquipmentId]);
                                CREATE NONCLUSTERED INDEX [IX_EquipmentReading_ReadTime]    ON [dbo].[EquipmentReading] ([ReadTime]);
                                CREATE NONCLUSTERED INDEX [IX_EquipmentReading_Type_Time]   ON [dbo].[EquipmentReading] ([EquipmentType], [ReadTime]);
                            END";
                    await db.Database.ExecuteSqlRawAsync(createEqReadingSql, ct);
                    logger.LogInformation("[v2.13.130 Migrate] EquipmentReading 表已创建（含 3 索引 + CHECK 约束）");
                }
            }
            catch (Exception exEqReading)
            {
                logger.LogWarning(exEqReading, "[v2.13.130 Migrate] EquipmentReading 表检测/创建异常（继续后续迁移）");
            }

            // 0.5 v2.13.120-hotfix 主菜单重命名兜底：生产 DB 中 SysPermission.PermissionName 字段
            //     仍是 v2.13.96 之前的旧值「抄表记录」，EF Core HasData() 对已存在记录不生效。
            //     此处 UPDATE 强制改为「智能抄表」保证主菜单实时刷新（无需手动 SQL）。
            try
            {
                var renameMeterSql = @"-- v2.13.120 主菜单重命名兜底（v2.13.96/v2.13.118 遗漏的生产 DB PermissionName UPDATE）
                            UPDATE [dbo].[SysPermission] SET [PermissionName] = N'智能抄表' WHERE [PermissionCode] = N'meter:view' AND [PermissionName] <> N'智能抄表';
                            UPDATE [dbo].[SysPermission] SET [PermissionName] = N'新增智能抄表' WHERE [PermissionCode] = N'meter:create' AND [PermissionName] NOT LIKE N'%智能抄表%';
                            UPDATE [dbo].[SysPermission] SET [PermissionName] = N'修正智能抄表' WHERE [PermissionCode] = N'meter:edit' AND [PermissionName] NOT LIKE N'%智能抄表%';
                            UPDATE [dbo].[SysPermission] SET [PermissionName] = N'删除智能抄表' WHERE [PermissionCode] = N'meter:delete' AND [PermissionName] NOT LIKE N'%智能抄表%';
                            UPDATE [dbo].[SysPermission] SET [PermissionName] = N'导出智能抄表' WHERE [PermissionCode] = N'meter:export' AND [PermissionName] NOT LIKE N'%智能抄表%';";

                var affected = await db.Database.ExecuteSqlRawAsync(renameMeterSql, ct);
                if (affected > 0)
                    logger.LogInformation("[v2.13.120-hotfix Rename] SysPermission.PermissionName 已 UPDATE {N} 行：抄表记录 → 智能抄表", affected);
            }
            catch (Exception exRename)
            {
                logger.LogWarning(exRename, "[v2.13.120-hotfix Rename] 主菜单重命名兜底异常（不影响后续）");
            }

            // 1. 检测 SysFieldPermission 表是否存在
            bool tableExists;
            try
            {
                var rows = await db.Database.SqlQueryRaw<string>(
                    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SysFieldPermission'")
                    .ToListAsync(ct);
                tableExists = rows.Count > 0;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[v2.13.99 Migrate] SysFieldPermission 表检测异常，跳过迁移");
                return new SeedMigrationResult { FatalError = ex.Message, AllSucceeded = false };
            }

            if (!tableExists)
            {
                logger.LogWarning("[v2.13.99 Migrate] SysFieldPermission 表缺失，开始补建...");
                result.CreatedFieldPermissionTable = true;

                var createSql = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SysFieldPermission]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [dbo].[SysFieldPermission] (
                                [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                [FieldKey] NVARCHAR(64) NOT NULL,
                                [Module] NVARCHAR(32) NOT NULL,
                                [FieldName] NVARCHAR(64) NOT NULL,
                                [FieldType] NVARCHAR(16) NULL,
                                [SensitivityLevel] INT NOT NULL DEFAULT 1,
                                [SortOrder] INT NOT NULL DEFAULT 0,
                                [IsActive] BIT NOT NULL DEFAULT 0,
                                [Description] NVARCHAR(200) NULL,
                                [CreatedAt] DATETIME2 NOT NULL,
                                [UpdatedAt] DATETIME2 NULL,
                                [UpdatedBy] NVARCHAR(64) NULL
                            );
                            CREATE UNIQUE INDEX [IX_SysFieldPermission_FieldKey] ON [dbo].[SysFieldPermission] ([FieldKey]);
                        END";

                await db.Database.ExecuteSqlRawAsync(createSql, ct);
                logger.LogInformation("[v2.13.99 Migrate] SysFieldPermission 表已创建");
            }

            // 2. 插入 5 字段种子（idempotent via IF NOT EXISTS）
            var fieldsSql = @"IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 1)
                    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
                    VALUES (1, N'employee.realname', N'Personnel', N'姓名', N'string', 1, 1, 1, N'员工真实姓名（高 PII）', '2026-07-22');
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 2)
                    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
                    VALUES (2, N'employee.phone', N'Personnel', N'手机号', N'string', 1, 2, 1, N'联系电话（高 PII）', '2026-07-22');
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 3)
                    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
                    VALUES (3, N'employee.employeecode', N'Personnel', N'工号', N'string', 2, 3, 1, N'公司内唯一标识', '2026-07-22');
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 4)
                    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
                    VALUES (4, N'employee.dormcode', N'Personnel', N'宿舍房号', N'string', 2, 4, 1, N'当前入住房号（隐私住址）', '2026-07-22');
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[SysFieldPermission] WHERE Id = 5)
                    INSERT INTO [dbo].[SysFieldPermission] ([Id],[FieldKey],[Module],[FieldName],[FieldType],[SensitivityLevel],[SortOrder],[IsActive],[Description],[CreatedAt])
                    VALUES (5, N'employee.remark', N'Personnel', N'备注', N'string', 2, 5, 1, N'自由文本备注（可能含敏感信息）', '2026-07-22');";

            await db.Database.ExecuteSqlRawAsync(fieldsSql, ct);

            // 3. 插入 v2.13.92 3 个权限码种子（Id 37/38/39）+ v2.13.97 1 个补充（Id 40）
            //    v2.13.99 P0 BUG 修复：原 MigrateFieldPermissionAsync 漏写 Id=40，导致 personnel:add 按钮权限失效
            //    v2.13.100 修订：扩展为补齐所有缺失的 seed（包括 v2.13.97 personnel:add）
            //    v2.13.103 终极修复：拆分为单条 INSERT + 独立 try/catch，让单条失败不影响其他 + 记录到 result.PermSteps
            //    v2.13.108 P0 终极修复：SQL Server IDENTITY_INSERT — SysPermission.Id 是 IDENTITY(1,1) 列，
            //      必须先 SET IDENTITY_INSERT ON 才能显式 INSERT 指定 Id，否则报"Cannot insert explicit value
            //      for identity column"错误，所有 Id=37/38/39/40 一直未落地！按钮永久不显示。
            //    v2.13.109 起移除 SQLite 分支；保留 SQL Server 语法不变
            var permInserts = new[]
            {
                // v2.13.108 SQL Server：IDENTITY_INSERT 必须显式开启
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 37)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
                  VALUES (37, N'settings:fields', N'字段权限', 1, 18, N'/Settings?tab=fields', N'bi-shield-check', 28, 1, 1, N'管理敏感字段清单', '2026-07-22');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 38)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
                  VALUES (38, N'fieldpermission:edit', N'编辑字段权限', 2, 37, N'', N'', 29, 1, 1, N'勾选/取消勾选敏感字段', '2026-07-22');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 39)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[Description],[CreatedAt])
                  VALUES (39, N'privacy:field:enable', N'启用隐私字段保护', 3, 0, N'', N'', 30, 1, 1, N'勾选此权限的角色将看不到所有 SysFieldPermission 清单中的字段', '2026-07-22');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                // v2.13.97 P0 BUG 修复：personnel:add
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 40)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (40, N'personnel:add', N'新增人员', 2, 9, N'/Personnel/Create', N'bi-plus-lg', 7, 1, 0, '2026-07-22');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                // v2.13.110 P0 BUG 修复：billingstandard:add（费用标准「新增标准」按钮权限独立）
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 41)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (41, N'billingstandard:add', N'新增费用标准', 2, 11, N'/BillingStandard/Create', N'bi-plus-lg', 5, 1, 0, '2026-07-22');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                // v2.13.120 新增：设备档案（基础资料二级菜单：device:view + 3 个子权限）
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 42)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (42, N'device:view', N'查看设备档案', 1, 10, N'/Basics?tab=device', N'bi-cpu', 31, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 43)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (43, N'device:create', N'新增设备档案', 2, 42, N'', N'', 32, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 44)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (44, N'device:edit', N'修改设备档案', 2, 42, N'', N'', 33, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 45)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (45, N'device:delete', N'删除设备档案', 2, 42, N'', N'', 34, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                // v2.13.130 新增：设备记录（基础资料二级菜单：equipment-reading:view + 4 个子权限：create/edit/delete/batch-delete）
                // 三层数据模型：设备档案(DormMeter v2.13.120) → 设备读数日志(EquipmentReading v2.13.130) → 智能抄表(MeterRecord)
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 46)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (46, N'equipment-reading:view', N'查看设备记录', 1, 10, N'/Basics?tab=equipmentreading', N'bi-journal-text', 41, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 47)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (47, N'equipment-reading:create', N'新增设备记录', 2, 46, N'', N'', 42, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 48)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (48, N'equipment-reading:edit', N'修改设备记录', 2, 46, N'', N'', 43, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 49)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (49, N'equipment-reading:delete', N'删除设备记录', 2, 46, N'', N'', 44, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;",
                @"SET IDENTITY_INSERT [dbo].[SysPermission] ON;
                  IF NOT EXISTS (SELECT 1 FROM [dbo].[SysPermission] WHERE Id = 50)
                  INSERT INTO [dbo].[SysPermission] ([Id],[PermissionCode],[PermissionName],[PermissionType],[ParentId],[Route],[Icon],[SortOrder],[IsActive],[IsSystem],[CreatedAt])
                  VALUES (50, N'equipment-reading:batch-delete', N'批量删除设备记录', 2, 46, N'', N'', 45, 1, 0, '2026-07-23');
                  SET IDENTITY_INSERT [dbo].[SysPermission] OFF;"
            };

            var permTargetIds = new[] { 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50 };
            for (int i = 0; i < permInserts.Length; i++)
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(permInserts[i], ct);
                    result.PermSteps.Add($"Id={permTargetIds[i]} ✓");
                }
                catch (Exception ex)
                {
                    result.PermSteps.Add($"Id={permTargetIds[i]} ✗ {ex.GetType().Name}: {ex.Message}");
                    logger.LogWarning(ex, "[v2.13.103] SysPermission Id={Id} INSERT 失败（继续执行）", permTargetIds[i]);
                    result.AllSucceeded = false;
                }
            }

            // 4. 插入 admin (RoleId=1) 的 SysRolePermission 关联（v2.13.92/97/110 共 5 条）
            //    v2.13.103 拆分单条 INSERT + 独立 try/catch
            //    v2.13.108 P0 修复 SysPermission.Id INSERT（IDENTITY_INSERT）
            //    v2.13.114 P0 终极修复 SysRolePermission：SysRolePermission.Id 是 IDENTITY(1,1) 列，
            //      生产 DB 已累积到 Id=184，硬编码 Id=58/59/60/61/62 中至少 Id=62 已被占用（访客 RoleId=9 占）
            //      → 旧 SQL `INSERT VALUES (62, 1, 41)` 因 PK 冲突被 try/catch 静默吞掉，admin 永远拿不到 billingstandard:add 权限
            //    修复方案：去掉硬编码 Id 列（让 IDENTITY 自动分配），去掉 SET IDENTITY_INSERT，
            //      按 (RoleId, PermissionCode) JOIN SysPermission 唯一性判断（替代 Id 唯一性）
            //    幂等性保证：多次执行不会重复插入（已有 admin→billingstandard:add 关联则跳过）
            var rpInserts = new[]
            {
                // v2.13.114：按 (RoleId, PermissionCode) 唯一性判断，不指定 Id（IDENTITY 自动分配）
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'settings:fields'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-22' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'settings:fields';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'fieldpermission:edit'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-22' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'fieldpermission:edit';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'privacy:field:enable'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-22' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'privacy:field:enable';",
                // v2.13.97 P0 BUG 修复：admin → personnel:add
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'personnel:add'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-22' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'personnel:add';",
                // v2.13.110 P0 BUG 修复：admin → billingstandard:add（v2.13.114 终极修复：去掉硬编码 Id）
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'billingstandard:add'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'billingstandard:add';",
                // v2.13.120 新增：admin → device:view / device:create / device:edit / device:delete（4 个权限码幂等授权）
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'device:view'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'device:view';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'device:create'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'device:create';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'device:edit'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'device:edit';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'device:delete'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'device:delete';",
                // v2.13.130 新增：admin → equipment-reading:* 5 个权限码幂等授权
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'equipment-reading:view'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'equipment-reading:view';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'equipment-reading:create'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'equipment-reading:create';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'equipment-reading:edit'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'equipment-reading:edit';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'equipment-reading:delete'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'equipment-reading:delete';",
                @"IF NOT EXISTS (
                      SELECT 1 FROM [dbo].[SysRolePermission] rp
                      INNER JOIN [dbo].[SysPermission] sp ON rp.PermissionId = sp.Id
                      WHERE rp.RoleId = 1 AND sp.PermissionCode = N'equipment-reading:batch-delete'
                  )
                  INSERT INTO [dbo].[SysRolePermission] ([RoleId],[PermissionId],[CreatedAt])
                  SELECT 1, Id, '2026-07-23' FROM [dbo].[SysPermission]
                  WHERE PermissionCode = N'equipment-reading:batch-delete';"
            };

            // v2.13.114：日志标识改为 PermissionCode（更直观）；v2.13.120 新增 4 个 device 权限码；v2.13.130 新增 5 个 equipment-reading 权限码
            var rpTargetCodes = new[] { "settings:fields", "fieldpermission:edit", "privacy:field:enable", "personnel:add", "billingstandard:add", "device:view", "device:create", "device:edit", "device:delete", "equipment-reading:view", "equipment-reading:create", "equipment-reading:edit", "equipment-reading:delete", "equipment-reading:batch-delete" };
            for (int i = 0; i < rpInserts.Length; i++)
            {
                try
                {
                    var affected = await db.Database.ExecuteSqlRawAsync(rpInserts[i], ct);
                    result.RolePermSteps.Add($"{rpTargetCodes[i]} ({(affected > 0 ? "新插入" : "已存在")})");
                }
                catch (Exception ex)
                {
                    result.RolePermSteps.Add($"{rpTargetCodes[i]} ✗ {ex.GetType().Name}: {ex.Message}");
                    logger.LogWarning(ex, "[v2.13.114] SysRolePermission {Code} INSERT 失败（继续执行）", rpTargetCodes[i]);
                    result.AllSucceeded = false;
                }
            }

            // 5. v2.13.101 验证迁移完整性 — 列出关键 PermissionCode 的实际状态
            // v2.13.114 P0 修订：原按硬编码 Id 验证（Id 38/39/40/41/58/59/60/61/62）已因 IDENTITY 列占用不可靠，
            //    改为按 PermissionCode JOIN SysPermission 验证 admin 用户是否拥有 5 个权限码
            try
            {
                var requiredPermCodes = new[] { "settings:fields", "fieldpermission:edit", "privacy:field:enable", "personnel:add", "billingstandard:add", "device:view", "device:create", "device:edit", "device:delete", "equipment-reading:view", "equipment-reading:create", "equipment-reading:edit", "equipment-reading:delete", "equipment-reading:batch-delete" };

                var presentPerms = await db.Database.SqlQueryRaw<string>(
                    $"SELECT PermissionCode FROM SysPermission WHERE PermissionCode IN ({string.Join(",", requiredPermCodes.Select(c => $"N'{c}'"))})")
                    .ToListAsync(ct);

                var presentAdminCodes = await db.Database.SqlQueryRaw<string>(
                    @"SELECT DISTINCT sp.PermissionCode
                      FROM SysUserRole ur
                      INNER JOIN SysRolePermission rp ON ur.RoleId = rp.RoleId
                      INNER JOIN SysPermission sp ON rp.PermissionId = sp.Id
                      WHERE ur.UserId = 1")
                    .ToListAsync(ct);

                var fieldPermCount = await db.SysFieldPermissions.CountAsync(ct);

                var missingPerms = requiredPermCodes.Except(presentPerms).ToList();
                var missingAdminPerms = requiredPermCodes.Except(presentAdminCodes).ToList();

                if (missingPerms.Count == 0 && missingAdminPerms.Count == 0 && fieldPermCount >= 5)
                {
                    logger.LogInformation("[v2.13.130 Verify] 隐私字段权限迁移完整性检查通过：admin 拥有 {N}/14 权限码（含 v2.13.120 device 4 个 + v2.13.130 equipment-reading 5 个），SYS FieldPermission {N}/5", presentAdminCodes.Count(c => requiredPermCodes.Contains(c)), fieldPermCount);
                }
                else
                {
                    if (missingPerms.Count > 0)
                        logger.LogWarning("[v2.13.114 Verify] SysPermission 缺失 {Codes}", string.Join(",", missingPerms));
                    if (missingAdminPerms.Count > 0)
                        logger.LogWarning("[v2.13.114 Verify] admin 用户缺失 {Codes}（v2.13.97 personnel:add / v2.13.110 billingstandard:add 等）", string.Join(",", missingAdminPerms));
                    if (fieldPermCount < 5)
                        logger.LogWarning("[v2.13.114 Verify] SysFieldPermission 仅 {N}/5 行，字段权限清单不完整", fieldPermCount);
                    logger.LogWarning("[v2.13.114 Verify] ⚠ 迁移不完整！请检查：(1) Admin 是否已重启触发 DatabaseInitializer.InitializeAsync；(2) 数据库连接字符串是否指向预期文件；(3) 启动日志是否有错误");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[v2.13.114 Verify] 完整性验证异常（不影响迁移主流程）");
            }

            logger.LogInformation("[v2.13.130 Migrate] 隐私字段权限迁移完成（SysFieldPermission 表 + 5 字段种子 + 14 权限码 + 14 角色关联，含 v2.13.97 personnel:add / v2.13.110 billingstandard:add / v2.13.120 device:* / v2.13.130 equipment-reading:* 修复）。结果：{Result}", string.Join("; ", result.PermSteps) + " | " + string.Join("; ", result.RolePermSteps));
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[v2.13.99 Migrate] 迁移失败（非阻塞，应用继续启动）");
            result.FatalError = ex.GetType().Name + ": " + ex.Message;
            return result;
        }
    }

    private static string ComputeSha256(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// v2.13.102 新增：DB seed 完整性查询（供 UI 展示）。
    /// 复用 v2.13.114 验证 SQL（按 PermissionCode JOIN），但不写日志——返回对象供 PageModel 直接序列化到 UI。
    ///
    /// 检测维度：
    /// - SysPermission 关键 PermissionCode（v2.13.92: settings:fields/fieldpermission:edit/privacy:field:enable；
    ///   v2.13.97: personnel:add；v2.13.110: billingstandard:add）
    /// - SysRolePermission admin (UserId=1) 关联以上 5 个 PermissionCode
    /// - SysFieldPermission 行数 ≥ 5（v2.13.92 隐私字段种子）
    ///
    /// v2.13.114 修订：原按硬编码 Id 列表验证已不可靠（SysRolePermission.Id 已被占用至 184+），
    ///    改为按 PermissionCode 列表 + JOIN SysPermission 派生验证。
    ///
    /// 设计原则：失败不抛异常——UI banner 不能因为检查失败而崩溃；返回 null/空集合让 UI 显示「未运行」。
    /// </summary>
    public static async Task<SeedIntegrityReport> CheckSeedIntegrityAsync(
        DormDbContext db, CancellationToken ct = default)
    {
        var requiredPermCodes = new[] { "settings:fields", "fieldpermission:edit", "privacy:field:enable", "personnel:add", "billingstandard:add" };
        const int expectedFieldPermCount = 5;

        var presentPerms = new List<string>();
        var presentAdminPerms = new List<string>();
        int fieldPermCount = 0;

        try
        {
            var permCodesLiteral = string.Join(",", requiredPermCodes.Select(c => $"N'{c}'"));
            presentPerms = await db.Database.SqlQueryRaw<string>(
                $"SELECT PermissionCode FROM SysPermission WHERE PermissionCode IN ({permCodesLiteral})").ToListAsync(ct);
        }
        catch { /* 表/视图不存在时返回空集合 */ }

        try
        {
            presentAdminPerms = await db.Database.SqlQueryRaw<string>(
                @"SELECT DISTINCT sp.PermissionCode
                  FROM SysUserRole ur
                  INNER JOIN SysRolePermission rp ON ur.RoleId = rp.RoleId
                  INNER JOIN SysPermission sp ON rp.PermissionId = sp.Id
                  WHERE ur.UserId = 1").ToListAsync(ct);
        }
        catch { /* 同上 */ }

        try
        {
            fieldPermCount = await db.SysFieldPermissions.CountAsync(ct);
        }
        catch { /* 同上 */ }

        var missingPerms = requiredPermCodes.Except(presentPerms).ToList();
        var missingAdminPerms = requiredPermCodes.Except(presentAdminPerms).ToList();
        var ok = missingPerms.Count == 0 && missingAdminPerms.Count == 0 && fieldPermCount >= expectedFieldPermCount;

        return new SeedIntegrityReport
        {
            Ok = ok,
            RequiredPermissionCodes = requiredPermCodes.ToList(),
            MissingPermissionCodes = missingPerms,
            RequiredAdminPermissionCodes = requiredPermCodes.ToList(),
            MissingAdminPermissionCodes = missingAdminPerms,
            FieldPermissionCount = fieldPermCount,
            ExpectedFieldPermissionCount = expectedFieldPermCount,
            CheckedAt = DateTime.Now,
            Version = "v2.13.110"
        };
    }
}

/// <summary>
/// v2.13.103 新增：seed 迁移结果（结构化替代原 bool）。
///
/// 背景：
///   v2.13.102 MigrateFieldPermissionAsync 返回 bool，但 permSql 整段任一 INSERT 失败被 try/catch 吞掉，
///   返回 false 后 OnPostRoleSeedRepairAsync 又走"report.Ok"分支返回 success=true，
///   用户看到 success toast 但实际没写入——这是 v2.13.102 隐藏 BUG。
///
/// 修复：
///   - PermSteps / RolePermSteps 记录每条 INSERT 的成功/失败详情（供 UI 直接展示）
///   - AllSucceeded 单一标志：true = 全部成功；false = 有失败
///   - FatalError：整段被 try/catch 吞掉的致命异常
/// </summary>
public class SeedMigrationResult
{
    public List<string> PermSteps { get; set; } = new();
    public List<string> RolePermSteps { get; set; } = new();
    public bool CreatedFieldPermissionTable { get; set; }
    public bool AllSucceeded { get; set; } = true;
    public string? FatalError { get; set; }

    /// <summary>UI 一句话摘要</summary>
    public string Summary
    {
        get
        {
            if (!string.IsNullOrEmpty(FatalError))
                return $"致命异常：{FatalError}";
            var ok = PermSteps.Count(p => p.Contains("✓")) + RolePermSteps.Count(p => p.Contains("✓"));
            var fail = PermSteps.Count(p => p.Contains("✗")) + RolePermSteps.Count(p => p.Contains("✗"));
            return fail == 0 ? $"全部成功（{ok} 条）" : $"{ok} 成功 / {fail} 失败";
        }
    }
}

/// <summary>
/// v2.13.102 新增：seed 完整性报告（供 UI 序列化）。
/// v2.13.114 修订：原按 SysPermission.Id/SysRolePermission.Id 验证（IDENTITY 列不可靠）改为按 PermissionCode 验证。
/// 字段命名遵守 PascalCase，由 Razor `@Model.SeedIntegrity.Ok` 直接渲染。
/// </summary>
public class SeedIntegrityReport
{
    public bool Ok { get; set; }
    /// <summary>v2.13.114：SysPermission 5 个关键 PermissionCode（按 PermissionCode 而非 Id 验证）</summary>
    public List<string> RequiredPermissionCodes { get; set; } = new();
    /// <summary>v2.13.114：SysPermission 缺失的 PermissionCode</summary>
    public List<string> MissingPermissionCodes { get; set; } = new();
    /// <summary>v2.13.114：admin 用户应拥有的 5 个 PermissionCode</summary>
    public List<string> RequiredAdminPermissionCodes { get; set; } = new();
    /// <summary>v2.13.114：admin 用户缺失的 PermissionCode</summary>
    public List<string> MissingAdminPermissionCodes { get; set; } = new();
    public int FieldPermissionCount { get; set; }
    public int ExpectedFieldPermissionCount { get; set; }
    public DateTime CheckedAt { get; set; }
    public string Version { get; set; } = "v2.13.114";

    /// <summary>v2.13.114：SysPermission 缺失项的中文标签（PermissionCode 形式）</summary>
    public List<string> MissingPermissionLabels => MissingPermissionCodes
        .Select(code => code switch
        {
            "settings:fields" => "settings:fields（字段权限菜单）",
            "fieldpermission:edit" => "fieldpermission:edit（编辑字段权限）",
            "privacy:field:enable" => "privacy:field:enable（启用隐私字段保护）",
            "personnel:add" => "personnel:add（新增人员按钮）",
            "billingstandard:add" => "billingstandard:add（新增费用标准按钮）",
            _ => code
        }).ToList();

    /// <summary>v2.13.114：admin 缺失项的中文标签</summary>
    public List<string> MissingRolePermissionLabels => MissingAdminPermissionCodes
        .Select(code => code switch
        {
            "settings:fields" => "admin → settings:fields（缺失）",
            "fieldpermission:edit" => "admin → fieldpermission:edit（缺失）",
            "privacy:field:enable" => "admin → privacy:field:enable（缺失）",
            "personnel:add" => "admin → personnel:add（缺失）",
            "billingstandard:add" => "admin → billingstandard:add（缺失）",
            _ => $"admin → {code}（缺失）"
        }).ToList();

    /// <summary>v2.13.114：UI 顶部一句话摘要（按 PermissionCode 5/5 计算）</summary>
    public string Summary => Ok
        ? $"SysPermission {PresentPermCount}/5 · SysRolePermission(admin) {PresentRpCount}/5 · SysFieldPermission {FieldPermissionCount}/5"
        : $"缺失 {MissingPermissionCodes.Count + MissingAdminPermissionCodes.Count} 项";

    public int PresentPermCount => RequiredPermissionCodes.Count - MissingPermissionCodes.Count;
    public int PresentRpCount => RequiredAdminPermissionCodes.Count - MissingAdminPermissionCodes.Count;
}

/// <summary>
/// 启动报告（包含初始化阶段的所有结果）
/// </summary>
public class StartupReport
{
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public long DurationMs { get; set; }
    public string Version { get; set; } = "";
    public string Provider { get; set; } = "";
    public bool ConnectionOk { get; set; }
    public List<string> MissingTables { get; set; } = new();
    public bool TablesAutoCreated { get; set; }
    public Dictionary<string, int> SeedCounts { get; set; } = new();
    public bool AdminSeeded { get; set; }
    public bool AppVersionSeeded { get; set; }
    public bool FieldPermissionMigrated { get; set; }
    public List<string> Warnings { get; set; } = new();

    public bool Healthy => ConnectionOk && MissingTables.Count == 0 && Warnings.Count == 0;

    public string ToBanner()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║         金戈宿舍管理系统 · 启动机制 v2.13.25                  ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════╣");
        var v = $"Build: {Version} | DB: {Provider} | {ConnectionOk switch { true => "✓ OK", false => "✗ FAIL" }}";
        var vStr = v.Length > 60 ? v.Substring(0, 60) : v.PadRight(60);
        sb.AppendLine($"║ {vStr} ║");
        var tStr = $"Tables: {(MissingTables.Count == 0 ? $"✓ all {DatabaseInitializer.CriticalTables.Length}" : $"✗ miss {MissingTables.Count}")} | Seed: {(SeedCounts.Any() ? string.Join("/", SeedCounts.Select(c => $"{c.Key}={c.Value}")) : "none")}";
        var tStr60 = tStr.Length > 60 ? tStr.Substring(0, 60) : tStr.PadRight(60);
        sb.AppendLine($"║ {tStr60} ║");
        var dStr = $"Duration: {DurationMs} ms{(AdminSeeded ? " | Admin: new" : " | Admin: exists")}";
        var dStr60 = dStr.Length > 60 ? dStr.Substring(0, 60) : dStr.PadRight(60);
        sb.AppendLine($"║ {dStr60} ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }
}
