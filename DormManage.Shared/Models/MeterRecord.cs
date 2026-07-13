using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 抄表记录状态枚举
/// </summary>
public enum MeterRecordStatus
{
    /// <summary>未完成（占位记录，三表全0）</summary>
    Incomplete = 0,

    /// <summary>正常（三表完整）</summary>
    Normal = 1,

    /// <summary>已修正（三表完整，已修正过）</summary>
    Corrected = 2
}

/// <summary>
/// 抄表记录
/// </summary>
[Table("MeterRecord")]
public class MeterRecord : BaseEntity
{
    /// <summary>
    /// 宿舍ID
    /// </summary>
    public int DormId { get; set; }

    /// <summary>
    /// 宿舍号
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string DormCode { get; set; } = string.Empty;

    /// <summary>
    /// 抄表月份（yyyy-MM）
    /// </summary>
    [Required]
    [MaxLength(7)]
    public string ReadMonth { get; set; } = string.Empty;

    /// <summary>
    /// 冷水表读数
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ColdMeter { get; set; }

    /// <summary>
    /// 热水表读数
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal HotMeter { get; set; }

    /// <summary>
    /// 电表读数
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ElectricMeter { get; set; }

    /// <summary>
    /// 操作员
    /// </summary>
    [MaxLength(50)]
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// 设备序列号
    /// </summary>
    [MaxLength(50)]
    public string? DeviceSn { get; set; }

    /// <summary>
    /// 客户端记录ID
    /// </summary>
    [MaxLength(50)]
    public string? ClientRecordId { get; set; }

    /// <summary>
    /// 客户端创建时间
    /// </summary>
    public DateTime? ClientCreatedAt { get; set; }

    /// <summary>
    /// 服务器创建时间
    /// </summary>
    public DateTime ServerCreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 状态：0=未完成 1=正常 2=已修正
    /// </summary>
    public byte Status { get; set; }

    /// <summary>
    /// 备注（包含历史覆盖快照）
    /// </summary>
    [MaxLength(1000)]
    public string? Remark { get; set; }

    /// <summary>
    /// 自动判定状态
    /// </summary>
    public static byte DetermineStatus(decimal cold, decimal hot, decimal electric)
    {
        int nonZeroCount = 0;
        if (cold > 0) nonZeroCount++;
        if (hot > 0) nonZeroCount++;
        if (electric > 0) nonZeroCount++;

        if (nonZeroCount == 0) return 0;  // 未完成
        if (nonZeroCount == 3) return 1;  // 正常
        return 0;  // 部分有值也算未完成
    }

    /// <summary>
    /// 获取状态名称
    /// </summary>
    public string GetStatusName()
    {
        return Status switch
        {
            0 => "未完成",
            1 => "正常",
            2 => "已修正",
            _ => "未知"
        };
    }
}
