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
        "SysUser", "SysRole", "SysEmployee", "Dorm", "DormBooking", "MeterRecord",
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

                // SQLite 路径：自动 EnsureCreated（仅当数据库文件不存在）
                if (report.Provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await db.Database.EnsureCreatedAsync(ct);
                        report.TablesAutoCreated = true;
                        logger.LogInformation("[Startup] ✓ SQLite 自动建库完成（EnsureCreatedAsync）");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[Startup] ✗ SQLite EnsureCreated 失败");
                    }
                }
            }
            else
            {
                logger.LogInformation("[Startup] ✓ {N} 张关键表全部存在", CriticalTables.Length);
            }

            // 第 4 步：种子基础字典
            report.SeedCounts = await SeedDictionariesAsync(db, logger, ct);

            // 第 5 步：种子默认管理员
            report.AdminSeeded = await SeedDefaultAdminAsync(db, logger, ct);

            // 第 6 步：写入 AppVersion 当前版本
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
    /// 检测关键表是否存在
    /// </summary>
    private static async Task<List<string>> DetectMissingTablesAsync(
        DormDbContext db, ILogger logger, CancellationToken ct)
    {
        var missing = new List<string>();

        try
        {
            if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            {
                var tables = await db.Database.SqlQueryRaw<string>(
                    "SELECT name FROM sqlite_master WHERE type='table'").ToListAsync(ct);
                foreach (var t in CriticalTables)
                {
                    if (!tables.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase)))
                        missing.Add(t);
                }
            }
            else  // SQL Server
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

    private static string ComputeSha256(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
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
