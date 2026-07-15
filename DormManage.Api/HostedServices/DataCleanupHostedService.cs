using DormManage.Shared.Data;
using DormManage.Shared.Services;

namespace DormManage.Api.HostedServices;

/// <summary>
/// 一次性数据清洗后台服务（v2.11.24 → v2.12.44 升级为 BackgroundService）
/// </summary>
/// <remarks>
/// 规范文档：<c>00-方案文档/43-无效FK归一通用规范-v2.11.24.md</c>
///
/// v2.12.44 重大变更：
/// <list type="bullet">
///   <item><description>从 <see cref="IHostedService"/> 改为 <see cref="BackgroundService"/></description></item>
///   <item><description>StartAsync 立即返回（不阻塞 Kestrel 绑定端口）</description></item>
///   <item><description>真正工作在 ExecuteAsync 异步执行，添加 30s 启动延迟确保 Kestrel 先就绪</description></item>
///   <item><description>异常必须 try/catch 吞掉，**不阻塞应用启动**</description></item>
///   <item><description>只在启动时执行一次（BackgroundService 内部循环检测标志）</description></item>
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
    /// 后台执行：等待 30s（让 Kestrel 先绑定端口）→ 执行一次性 FK 归一 → 标记完成
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 等待 Kestrel 启动并绑定端口
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            if (_hasRun) return;
            _hasRun = true;

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DormDbContext>();

            _logger.LogInformation("[v2.11.24 FK 归一] 开始启动一次性数据清洗…");

            var fixedStats = await DictionaryFallbackService.BatchNormalizeEmployeesAsync(db);

            _logger.LogInformation("[v2.11.24 FK 归一] EmployeeType 修复: {N} 条", fixedStats["EmployeeType"]);
            _logger.LogInformation("[v2.11.24 FK 归一] AttendanceType 修复: {N} 条", fixedStats["AttendanceType"]);
            _logger.LogInformation("[v2.11.24 FK 归一] Department 修复: {N} 条", fixedStats["Department"]);
            _logger.LogInformation("[v2.11.24 FK 归一] EmploymentStatus 修复: {N} 条", fixedStats["EmploymentStatus"]);
            _logger.LogInformation("[v2.11.24 FK 归一] ResidenceStatus 修复: {N} 条", fixedStats["ResidenceStatus"]);

            var total = fixedStats.Values.Sum();
            _logger.LogInformation("[v2.11.24 FK 归一] 合计: {N} 条", total);
        }
        catch (OperationCanceledException)
        {
            // 应用停止
        }
        catch (Exception ex)
        {
            // 不阻塞应用启动
            _logger.LogError(ex, "[v2.11.24 FK 归一] 启动清洗异常（非阻塞）");
        }
    }
}
