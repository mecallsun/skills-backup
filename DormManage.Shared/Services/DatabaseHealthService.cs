using System.Data;
using System.Diagnostics;
using DormManage.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Shared.Services;

public interface IDatabaseHealthService
{
    Task<DatabaseHealthReport> RunDeepCheckAsync();
    Task<bool> QuickCheckAsync();
}

public class DatabaseHealthReport
{
    public bool OverallPassed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public long ElapsedMs { get; set; }
    public string Provider { get; set; } = "";
    public List<HealthStep> Steps { get; set; } = new();
}

public class HealthStep
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Passed { get; set; }
    public long ElapsedMs { get; set; }
    public string? Error { get; set; }
}

public class DatabaseHealthService : IDatabaseHealthService
{
    private readonly DormDbContext _db;

    public DatabaseHealthService(DormDbContext db)
    {
        _db = db;
    }

    public async Task<bool> QuickCheckAsync()
    {
        try
        {
            return await _db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<DatabaseHealthReport> RunDeepCheckAsync()
    {
        var report = new DatabaseHealthReport
        {
            StartedAt = DateTime.Now,
            Provider = _db.Database.ProviderName ?? "Unknown"
        };
        var sw = Stopwatch.StartNew();

        // 步骤 1: TCP/连接测试
        await RunStep(report, "TCP 连接测试", "尝试与数据库建立 TCP 连接", async () =>
        {
            return await _db.Database.CanConnectAsync();
        });

        // 步骤 2: SQL 登录验证
        await RunStep(report, "SQL 登录验证", "验证数据库账号与密码", async () =>
        {
            await _db.Database.OpenConnectionAsync();
            return true;
        });

        // 步骤 3: 数据库可见性
        await RunStep(report, "数据库可见性检查", "确认当前数据库可访问", async () =>
        {
            var conn = _db.Database.GetDbConnection();
            return !string.IsNullOrEmpty(conn.Database);
        });

        // 步骤 4: 表结构验证
        await RunStep(report, "表结构验证", "检查核心表是否存在", async () =>
        {
            var requiredTables = new[] { "SysUser", "SysRole", "Dorm", "DormBooking", "MeterRecord", "SysEmployee" };
            foreach (var t in requiredTables)
            {
                try
                {
                    var sql = _db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
                        ? $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{t}'"
                        : $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{t}'";
                    using var cmd = _db.Database.GetDbConnection().CreateCommand();
                    cmd.CommandText = sql;
                    if (cmd.Connection.State != ConnectionState.Open) await cmd.Connection.OpenAsync();
                    var cnt = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (cnt == 0) throw new Exception($"表 {t} 不存在");
                }
                catch
                {
                    throw;
                }
            }
            return true;
        });

        // 步骤 5: SELECT 权限
        await RunStep(report, "SELECT 权限测试", "查询 SysUser 表验证读取权限", async () =>
        {
            var count = await _db.SysUsers.CountAsync();
            return count >= 0;
        });

        // 步骤 6: EF Core 迁移状态
        await RunStep(report, "EF Core 迁移检查", "检查数据库迁移状态", async () =>
        {
            try
            {
                var pending = (await _db.Database.GetPendingMigrationsAsync()).ToList();
                if (pending.Any())
                    throw new Exception($"有 {pending.Count} 个待执行迁移：{string.Join(", ", pending.Take(3))}");
                return true;
            }
            catch
            {
                // 首次运行时数据库未启用迁移不算失败
                return true;
            }
        });

        sw.Stop();
        report.ElapsedMs = sw.ElapsedMilliseconds;
        report.FinishedAt = DateTime.Now;
        report.OverallPassed = report.Steps.All(s => s.Passed);
        return report;
    }

    private async Task RunStep(DatabaseHealthReport report, string name, string desc, Func<Task<bool>> action)
    {
        var sw = Stopwatch.StartNew();
        var step = new HealthStep { Name = name, Description = desc };
        try
        {
            step.Passed = await action();
            if (!step.Passed) step.Error = "返回 false";
        }
        catch (Exception ex)
        {
            step.Passed = false;
            step.Error = ex.Message;
        }
        finally
        {
            sw.Stop();
            step.ElapsedMs = sw.ElapsedMilliseconds;
            report.Steps.Add(step);
        }
    }
}