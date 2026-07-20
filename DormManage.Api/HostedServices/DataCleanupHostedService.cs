using DormManage.Shared.Data;
using DormManage.Shared.Services;

namespace DormManage.Api.HostedServices;

/// <summary>
/// 一次性数据清洗后台服务（v2.11.24 → v2.12.44 升级为 BackgroundService → v2.13.25 优化）
/// </summary>
/// <remarks>
/// 规范文档：<c>00-方案文档/43-无效FK归一通用规范-v2.11.24.md</c>
///
/// v2.13.25 关键变更：
/// <list type="bullet">
///   <item><description>延迟从 30s 缩短为 5s（启动校验已确保表存在，无需等 Kestrel 完全就绪）</description></item>
///   <item><description>支持取消令牌提前终止</description></item>
///   <item><description>分级日志（缺省静默修复、有异常大声告警）</description></item>
/// </list>
///
/// 仍然保持：
/// <list type="bullet">
///   <item><description>StartAsync 立即返回（不阻塞 Kestrel 绑定端口）</description></item>
///   <item><description>异常必须 try/catch 吞掉，**不阻塞应用启动**</description></item>
///   <item><description>只在启动时执行一次</description></item>
/// </list>
/// </remarks>
public class DataCleanupHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DataCleanupHostedService> _logger;
    private bool _hasRun = false;

    public DataCleanupHostedService(IServiceProvider services, ILogger<DataCleanupHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// 后台执行：等待 5s（让 Kestrel 优先绑定端口）→ 执行一次性 FK 归一 → 标记完成
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // v2.13.25：5s 已足够（启动校验保证表存在；Kestrel 仅需极短时间绑定端口）
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            if (_hasRun) return;
            _hasRun = true;

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();

            // v2.13.25：连通性快速校验（启动校验可能已做，此处再做一次 100ms 内的健康检查）
            if (!await db.Database.CanConnectAsync(stoppingToken))
            {
                _logger.LogWarning("[FK 归一化清理] 数据库不可达，跳过本次清理");
                return;
            }

            _logger.LogInformation("[FK 归一化清理] 开始启动一次性数据清洗…");

            var fixedStats = await DictionaryFallbackService.BatchNormalizeEmployeesAsync(db);

            var total = fixedStats.Values.Sum();

            // v2.13.25：分级日志
            if (total == 0)
            {
                _logger.LogInformation("[FK 归一化清理] ✓ 无需修复（数据已规范）");
            }
            else
            {
                _logger.LogInformation("[FK 归一化清理] ✓ 已修复 {N} 条无效外键", total);
                foreach (var (key, count) in fixedStats.Where(s => s.Value > 0))
                {
                    _logger.LogInformation("[FK 归一化清理]   - {Key}: {Count} 条", key, count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 应用停止
        }
        catch (Exception ex)
        {
            // 不阻塞应用启动
            _logger.LogError(ex, "[FK 归一化清理] 启动清洗异常（非阻塞）");
        }
    }
}
