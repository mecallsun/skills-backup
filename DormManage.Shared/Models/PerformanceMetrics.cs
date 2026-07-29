namespace DormManage.Shared.Models;

/// <summary>
/// v2.13.172 性能调优指标快照
/// </summary>
public class PerformanceMetrics
{
    /// <summary>CPU 核数（启动时锁定）</summary>
    public int CpuCount { get; set; }

    /// <summary>ThreadPool 最小工作线程数（=max(N_cpu, N_cpu×1.5)）</summary>
    public int MinThreadPoolThreads { get; set; }

    /// <summary>ThreadPool 最大工作线程数（=N_cpu×50 上限）</summary>
    public int MaxThreadPoolThreads { get; set; }

    /// <summary>DbContext 连接池大小（=min(200, max(8, N_cpu×4))）</summary>
    public int DbContextPoolSize { get; set; }

    /// <summary>Kestrel 最大并发连接数（=min(10000, max(100, N_cpu×1000))）</summary>
    public int KestrelMaxConcurrentConnections { get; set; }

    /// <summary>Kestrel 升级连接（WebSocket）最大并发</summary>
    public int KestrelMaxConcurrentUpgradedConnections { get; set; }

    /// <summary>采样时的 IO 等待/CPU 比例（用于自适应决策）</summary>
    public double IoToCpuRatio { get; set; }

    /// <summary>最近一次调优时间</summary>
    public DateTime SampleAt { get; set; } = DateTime.UtcNow;

    /// <summary>当前进程 CPU 占用率（动态评估）</summary>
    public double CurrentCpuPercent { get; set; }

    /// <summary>当前托管内存 (MB)</summary>
    public long CurrentManagedMemoryMb { get; set; }
}
