namespace DormManage.TrayApp.Models;

/// <summary>
/// 子服务状态枚举（用于托盘图标与 SettingsForm 状态展示）
/// </summary>
public enum ServiceState
{
    /// <summary>已停止（未启动）</summary>
    Stopped = 0,

    /// <summary>启动中（已下发启动命令，等待 HTTP 探测成功）</summary>
    Starting = 1,

    /// <summary>运行中</summary>
    Running = 2,

    /// <summary>异常（HTTP 探测失败或进程崩溃）</summary>
    Crashed = 3,

    /// <summary>正在停止</summary>
    Stopping = 4
}

/// <summary>
/// 健康检查结果
/// </summary>
public record ServiceHealth(
    string ServiceName,
    bool IsHealthy,
    int Port,
    string? Detail = null,
    DateTime CheckedAt = default!)
{
    public DateTime CheckedAt { get; init; } = CheckedAt == default ? DateTime.Now : CheckedAt;
}