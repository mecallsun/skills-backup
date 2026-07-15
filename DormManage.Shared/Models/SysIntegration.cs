using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 系统集成配置（P2-6）
/// 与外部系统（HR / K3ERP / 钉钉等）的连接参数
/// </summary>
[Table("SysIntegration")]
public class SysIntegration : BaseEntity
{
    /// <summary>系统编码（唯一，如 HR/K3ERP/DINGTALK）</summary>
    [Required, MaxLength(50)]
    public string SystemCode { get; set; } = "";

    /// <summary>系统名称</summary>
    [Required, MaxLength(100)]
    public string SystemName { get; set; } = "";

    /// <summary>服务器地址（如 http://hr.company.com/api）</summary>
    [MaxLength(500)]
    public string? ServerAddress { get; set; }

    /// <summary>账号</summary>
    [MaxLength(100)]
    public string? Account { get; set; }

    /// <summary>密码（建议加密存储，v2.13.3 暂明文）</summary>
    [MaxLength(200)]
    public string? Password { get; set; }

    /// <summary>API Key / Token</summary>
    [MaxLength(500)]
    public string? ApiKey { get; set; }

    /// <summary>启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>同步周期（分钟），0 表示手动</summary>
    public int SyncIntervalMinutes { get; set; }

    /// <summary>最近一次同步时间</summary>
    public DateTime? LastSyncTime { get; set; }

    /// <summary>最近一次同步结果</summary>
    public bool? LastSyncResult { get; set; }

    /// <summary>最近一次同步消息</summary>
    [MaxLength(1000)]
    public string? LastSyncMessage { get; set; }

    /// <summary>最近测试连接时间</summary>
    public DateTime? LastTestTime { get; set; }

    /// <summary>最近测试连接结果</summary>
    public bool? LastTestResult { get; set; }

    /// <summary>JSON 配置扩展（如同步规则、字段映射）</summary>
    [MaxLength(2000)]
    public string? ExtraConfigJson { get; set; }

    /// <summary>备注</summary>
    [MaxLength(500)]
    public string? Remark { get; set; }
}