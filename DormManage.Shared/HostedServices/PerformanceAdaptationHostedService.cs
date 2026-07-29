using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DormManage.Shared.Services;

namespace DormManage.Shared.HostedServices;

/// <summary>
/// v2.13.172 性能自适应评估 HostedService
/// 启动时执行基线调优 + 60s 周期评估
/// </summary>
public class PerformanceAdaptationHostedService : BackgroundService
{
    private readonly ILogger<PerformanceAdaptationHostedService> _logger;
    private readonly PerformanceOptimizer _optimizer;
    private const int AdaptationIntervalSeconds = 60;

    public PerformanceAdaptationHostedService(ILogger<PerformanceAdaptationHostedService> logger, PerformanceOptimizer optimizer)
    {
        _logger = logger;
        _optimizer = optimizer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动时立即执行基线调优
        try
        {
            _optimizer.ApplyBaseline();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PERF] 启动期基线调优失败（不影响服务）");
        }

        // 周期自适应评估
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(AdaptationIntervalSeconds), stoppingToken);
                _optimizer.EvaluateAndAdapt();
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PERF] 自适应评估异常（继续运行）");
            }
        }
    }
}
