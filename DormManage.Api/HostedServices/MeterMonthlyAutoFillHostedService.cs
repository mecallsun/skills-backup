using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Api.HostedServices;

/// <summary>
/// 智能抄表每日占位记录自动补全后台服务（v2.13.128）
/// </summary>
/// <remarks>
/// 业务规则（用户原话）：
/// "对智能抄表页面的数据表，进行每天0:01检查智能抄表列表的数据表记录中，
///  是否每个房号（依据宿舍档案）至少有一条当前月份（服务运行主机时间为参照）
///  的记录数据，如果有当月的数据记录则不处理，如果该记号当前月份没有当前月份的
///  数据记录，则新增一条当前日期有该房号的空数据记录（各表项数据为0）"
///
/// 设计决策（最专业）：
/// <list type="bullet">
///   <item><description>进程：DormManage.Api（不是 TrayApp）— Api 已具备 EF Core DbContext + DI 容器 + 现有 HostedService 模式（<see cref="DataCleanupHostedService"/>）；TrayApp 是 WinForms 不便集成 EF</description></item>
///   <item><description>触发：本地服务器时间（DateTime.Now）— 与业务时间一致（中国大陆无跨时区）</description></item>
///   <item><description>范围：Dorm.IsActive=true（v2.13.82 / v2.13.117 锁定约束延伸 — 停用房间无需抄表）</description></item>
///   <item><description>「有当月记录」定义：任意 Status (0/1/2/3)，**排除** Status=4 (Voided) — 用户主动作废的算"没有"，下一天被新占位覆盖</description></item>
///   <item><description>占位记录字段：Cold/Hot/Electric Meter=上月最后一条读数（继承，避免补录时 Usage 凭空增加）；Usage=0；Previous*=上月最后一条读数；Status=0 (Incomplete)；ReadMode=4 (AutoGenerate)；ReadDate=今天；ReadMonth=yyyy-MM</description></item>
///   <item><description>失败处理：try/catch 吞异常 + 日志告警，明天 0:01 自动重试（不阻塞 Api 启动）</description></item>
///   <item><description>幂等性：同一天多次运行不会产生重复记录（每日唯一性通过 DormCode + ReadMonth + 0:01 顺序执行保证）</description></item>
/// </list>
///
/// 详细实施文档：00-方案文档/177-智能抄表每日占位自动补全-v2.13.128.md
/// </remarks>
public class MeterMonthlyAutoFillHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MeterMonthlyAutoFillHostedService> _logger;

    /// <summary>本地时区（业务时间基准 — 与 BillingService / DashboardService 一致）</summary>
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.Local;

    public MeterMonthlyAutoFillHostedService(
        IServiceProvider services,
        ILogger<MeterMonthlyAutoFillHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 启动后立即跑一次（避免重启当天 0:01 已过则等到明天 — 等不到补全）
            //   用 5s 延迟让 Kestrel/数据库就绪（参照 DataCleanupHostedService 模式）
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            await SafeRunOnceAsync(stoppingToken);

            // 进入每天 0:01 循环：先算到下次 0:01 的延迟，再循环触发
            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunDelay = ComputeDelayUntilNext0001(DateTimeOffset.Now);
                _logger.LogInformation(
                    "[Meter占位自动补全] 下次执行 {NextRunAt:yyyy-MM-dd HH:mm:ss}（{DelayMinutes:N1} 分钟后）",
                    DateTimeOffset.Now.Add(nextRunDelay), nextRunDelay.TotalMinutes);

                await Task.Delay(nextRunDelay, stoppingToken);
                if (stoppingToken.IsCancellationRequested) break;

                await SafeRunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 应用停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Meter占位自动补全] 后台循环异常（非阻塞）");
        }
    }

    /// <summary>
    /// 计算从 now 到下一个 0:01 的延迟
    /// </summary>
    private static TimeSpan ComputeDelayUntilNext0001(DateTimeOffset nowLocal)
    {
        var next = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 1, 0, 0);
        if (next <= nowLocal) next = next.AddDays(1);
        return next - nowLocal;
    }

    /// <summary>
    /// 单次执行入口：异常吞掉不阻塞 Api 启动（参照 DataCleanupHostedService 模式）
    /// </summary>
    private async Task SafeRunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Meter占位自动补全] 单次执行异常（已吞掉，明天 0:01 重试）");
        }
    }

    /// <summary>
    /// 核心业务：扫描 Dorm + 当月 MeterRecord，差集新增占位（公开 static，
    ///   供 MeterController.TriggerMonthlyAutoFill 手动调用，无需重启服务）
    /// </summary>
    public static async Task<MonthlyAutoFillResult> RunOnceAsync(
        DormDbContext db,
        ILogger logger,
        CancellationToken stoppingToken)
    {
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, BusinessTimeZone);
        var todayLocal = nowLocal.Date;
        var readMonth = todayLocal.ToString("yyyy-MM");

        logger.LogInformation(
            "[Meter占位自动补全] 开始：目标月份={ReadMonth}，执行时间={Now:yyyy-MM-dd HH:mm:ss}",
            readMonth, nowLocal);

        // 启动校验：数据库不可达时跳过本次
        if (!await db.Database.CanConnectAsync(stoppingToken))
        {
            logger.LogWarning("[Meter占位自动补全] 数据库不可达，跳过本次扫描");
            return new MonthlyAutoFillResult { ReadMonth = readMonth, Success = false, Error = "数据库不可达" };
        }

        // Step 1: 取所有启用的宿舍房号（Dorm.IsActive=true — 锁定约束延伸）
        //   DormId 列映射 BaseEntity.Id，所以 C# 属性是 .Id（不能直接 .DormId）
        var activeDorms = await db.Dorms
            .AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => new { DormInternalId = d.Id, d.DormCode })
            .ToListAsync(stoppingToken);

        if (activeDorms.Count == 0)
        {
            logger.LogInformation("[Meter占位自动补全] 无启用宿舍，跳过");
            return new MonthlyAutoFillResult { ReadMonth = readMonth, Success = true, ActiveDormCount = 0, InsertedCount = 0 };
        }

        // Step 2: 取当月所有非作废记录（Status != Voided(4)）
        //   - 排除 Voided(4)：用户主动作废的算"没有"
        //   - 包含 Incomplete(0)/Normal(1)/Corrected(2)/Unfinished(3)
        var existingDormCodes = await db.MeterRecords
            .AsNoTracking()
            .Where(m => m.ReadMonth == readMonth && m.Status != (byte)MeterRecordStatus.Voided)
            .Select(m => m.DormCode)
            .Distinct()
            .ToListAsync(stoppingToken);

        var existingSet = new HashSet<string>(existingDormCodes, StringComparer.OrdinalIgnoreCase);

        // Step 3: 差集 = 启用宿舍 - 已存在当月记录
        var missingDorms = activeDorms
            .Where(d => !existingSet.Contains(d.DormCode))
            .ToList();

        if (missingDorms.Count == 0)
        {
            logger.LogInformation(
                "[Meter占位自动补全] ✓ {ReadMonth} 全部 {Total} 间启用宿舍已有记录，无需补全",
                readMonth, activeDorms.Count);
            return new MonthlyAutoFillResult
            {
                ReadMonth = readMonth,
                Success = true,
                ActiveDormCount = activeDorms.Count,
                ExistingDormCount = existingDormCodes.Count,
                MissingDormCount = 0,
                InsertedCount = 0,
            };
        }

        // Step 4: 为每个缺失的房号查上月最后一条记录（用于继承读数）
        var lastMonth = todayLocal.AddMonths(-1);
        var prevMonthStr = lastMonth.ToString("yyyy-MM");
        var missingDormCodes = missingDorms.Select(d => d.DormCode).ToList();
        var previousReadingsGrouped = await db.MeterRecords
            .AsNoTracking()
            .Where(m => m.ReadMonth == prevMonthStr
                     && m.Status != (byte)MeterRecordStatus.Voided
                     && missingDormCodes.Contains(m.DormCode))
            .GroupBy(m => m.DormCode)
            .Select(g => new
            {
                DormCode = g.Key,
                // 上月最后一条 = ServerCreatedAt DESC 取首条
                ColdMeter = g.OrderByDescending(x => x.ServerCreatedAt).First().ColdMeter,
                HotMeter = g.OrderByDescending(x => x.ServerCreatedAt).First().HotMeter,
                ElectricMeter = g.OrderByDescending(x => x.ServerCreatedAt).First().ElectricMeter,
            })
            .ToListAsync(stoppingToken);

        var previousReadings = previousReadingsGrouped
            .ToDictionary(x => x.DormCode, StringComparer.OrdinalIgnoreCase);

        var now = DateTime.Now;
        var newRecords = new List<MeterRecord>();
        foreach (var dorm in missingDorms)
        {
            previousReadings.TryGetValue(dorm.DormCode, out var prev);
            var prevCold = prev?.ColdMeter ?? 0m;
            var prevHot = prev?.HotMeter ?? 0m;
            var prevElectric = prev?.ElectricMeter ?? 0m;

            // v2.13.128 占位记录：
            //   - 三表读数 = 上月最后一条（继承 — 避免补录时 Usage 凭空增加）
            //   - 三表用量 = 0（占位）
            //   - 上月读数 = 上月最后一条
            //   - Status = 0 (Incomplete) 占位
            //   - ReadMode = 4 (AutoGenerate)
            //   - DeviceSn / ClientRecordId = AUTO-{yyyyMM}-{DormCode}-{Guid} 防 UNIQUE 冲突
            newRecords.Add(new MeterRecord
            {
                DormId = dorm.DormInternalId,
                DormCode = dorm.DormCode,
                ReadMonth = readMonth,
                ColdMeter = prevCold,
                HotMeter = prevHot,
                ElectricMeter = prevElectric,
                ColdUsage = 0m,
                HotUsage = 0m,
                ElectricUsage = 0m,
                PreviousColdReading = prevCold,
                PreviousHotReading = prevHot,
                PreviousElectricReading = prevElectric,
                ReadDate = DateOnly.FromDateTime(todayLocal),
                ReadMode = (byte)MeterReadMode.AutoGenerate,
                Status = (byte)MeterRecordStatus.Incomplete,
                Operator = "系统自动",
                DeviceSn = $"AUTO-{todayLocal:yyyyMM}-{dorm.DormCode}",
                ClientRecordId = $"AUTO-{todayLocal:yyyyMMdd}-{dorm.DormCode}-{Guid.NewGuid():N}".Substring(0, 32),
                ServerCreatedAt = now,
                Remark = "v2.13.128 系统每日 0:01 占位自动补全",
            });
        }

        // Step 5: 批量插入（性能：单次 SaveChanges 而非 N 次）
        await db.MeterRecords.AddRangeAsync(newRecords, stoppingToken);
        await db.SaveChangesAsync(stoppingToken);

        logger.LogInformation(
            "[Meter占位自动补全] ✓ {ReadMonth} 补全 {Inserted} 条占位记录（启用 {Active} 间，已有 {Existing} 间，缺失 {Missing} 间）",
            readMonth, newRecords.Count, activeDorms.Count, existingDormCodes.Count, missingDorms.Count);

        return new MonthlyAutoFillResult
        {
            ReadMonth = readMonth,
            Success = true,
            ActiveDormCount = activeDorms.Count,
            ExistingDormCount = existingDormCodes.Count,
            MissingDormCount = missingDorms.Count,
            InsertedCount = newRecords.Count,
            InsertedDormCodes = newRecords.Select(r => r.DormCode).ToList(),
        };
    }

    /// <summary>
    /// 私有实例调用包装（HostedService 入口）
    /// </summary>
    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();
        try
        {
            await RunOnceAsync(db, _logger, stoppingToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Meter占位自动补全] 单次执行异常（已吞掉，明天 0:01 重试）");
        }
    }
}

/// <summary>
/// 手动触发一次的执行结果（v2.13.128 — API 返回用）
/// </summary>
public class MonthlyAutoFillResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string ReadMonth { get; set; } = string.Empty;
    public int ActiveDormCount { get; set; }
    public int ExistingDormCount { get; set; }
    public int MissingDormCount { get; set; }
    public int InsertedCount { get; set; }
    public List<string> InsertedDormCodes { get; set; } = new();
}
