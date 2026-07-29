using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// v2.13.172 性能监控端点
/// GET /api/v1/system/performance 返回当前自适应调优指标 + 进程资源
/// </summary>
[ApiController]
[Route("api/v1/system")]
public class PerformanceController : ControllerBase
{
    private readonly PerformanceOptimizer _optimizer;

    public PerformanceController(PerformanceOptimizer optimizer)
    {
        _optimizer = optimizer;
    }

    [HttpGet("performance")]
    public IActionResult Get()
    {
        ThreadPool.GetMinThreads(out int minWorker, out int minIOCP);
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIOCP);
        ThreadPool.GetAvailableThreads(out int availWorker, out int availIOCP);
        int activeThreads = maxWorker - availWorker;
        double activeRatio = maxWorker > 0 ? (double)activeThreads / maxWorker * 100 : 0;

        var metrics = _optimizer.CurrentMetrics;
        return Ok(ApiResponse<object>.Ok(new
        {
            cpu = new { count = _optimizer.CpuCount },
            threadPool = new
            {
                minWorker,
                maxWorker,
                minIocp = minIOCP,
                maxIocp = maxIOCP,
                activeWorker = activeThreads,
                availableWorker = availWorker,
                activeRatio = $"{activeRatio:F1}%",
                iocpAvailable = availIOCP
            },
            tuning = new
            {
                minThreads = metrics.MinThreadPoolThreads,
                maxThreads = metrics.MaxThreadPoolThreads,
                dbContextPool = metrics.DbContextPoolSize,
                kestrelMax = metrics.KestrelMaxConcurrentConnections,
                kestrelUpgraded = metrics.KestrelMaxConcurrentUpgradedConnections,
                ioToCpuRatio = metrics.IoToCpuRatio,
                sampleAt = metrics.SampleAt
            },
            process = new
            {
                managedMemoryMb = GC.GetTotalMemory(false) / 1024 / 1024,
                workingSetMb = Environment.WorkingSet / 1024 / 1024
            },
            timestamp = DateTime.UtcNow
        }));
    }
}
