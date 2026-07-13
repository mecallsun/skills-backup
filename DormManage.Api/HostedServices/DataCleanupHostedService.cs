using DormManage.Shared.Data;
using DormManage.Shared.Services;

namespace DormManage.Api.HostedServices;

/// <summary>
/// 一次性数据清洗后台服务（v2.11.24）
/// </summary>
/// <remarks>
/// 规范文档：<c>00-方案文档/43-无效FK归一通用规范-v2.11.24.md</c>
///
/// 启动流程：
/// <list type="number">
///   <item><description>应用启动时由 <see cref="IHostedService"/> 调度 <c>StartAsync</c></description></item>
///   <item><description>通过 <see cref="IServiceProvider"/> 创建 Scope 拿 <see cref="DormDbContext"/></description></item>
///   <item><description>调用 <see cref="DictionaryFallbackService.BatchNormalizeEmployeesAsync"/> 一次性归一存量数据</description></item>
///   <item><description>输出 v2.11.24 标准审计日志（ILogger）</description></item>
/// </list>
///
/// 关键约束：
/// <list type="bullet">
///   <item><description>异常必须 try/catch 吞掉，**不阻塞应用启动**</description></item>
///   <item><description>只在启动时执行一次（HostedService 默认行为）</description></item>
///   <item><description>数据量小（800 人），使用批量归一方法足够</description></item>
/// </list>
/// </remarks>
public class DataCleanupHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DataCleanupHostedService> _logger;

    public DataCleanupHostedService(IServiceProvider services, ILogger<DataCleanupHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// 启动钩子：执行 v2.11.24 一次性 FK 归一。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
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
        catch (Exception ex)
        {
            // 不阻塞应用启动
            _logger.LogError(ex, "[v2.11.24 FK 归一] 启动清洗异常（非阻塞）");
        }
    }

    /// <summary>
    /// 停止钩子：本服务无后台轮询，空实现即可。
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
