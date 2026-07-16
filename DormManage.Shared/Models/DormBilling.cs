using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 宿舍月度费用账单
/// </summary>
[Table("DormBilling")]
public class DormBilling
{
    /// <summary>
    /// 主键
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 宿舍号
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string DormCode { get; set; } = string.Empty;

    /// <summary>
    /// 宿舍ID（冗余）
    /// </summary>
    public int DormId { get; set; }

    /// <summary>
    /// 计费月份 yyyy-MM
    /// </summary>
    [Required]
    [MaxLength(7)]
    public string BillingMonth { get; set; } = string.Empty;

    /// <summary>
    /// 冷水用量 (m³)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ColdUsage { get; set; }

    /// <summary>
    /// 热水用量 (m³)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal HotUsage { get; set; }

    /// <summary>
    /// 用电用量 (kWh)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ElectricityUsage { get; set; }

    /// <summary>
    /// 冷水费
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ColdAmount { get; set; }

    /// <summary>
    /// 热水费
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal HotAmount { get; set; }

    /// <summary>
    /// 电费
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ElectricityAmount { get; set; }

    /// <summary>
    /// 合计金额
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 在住人数
    /// </summary>
    public int ResidentCount { get; set; }

    /// <summary>
    /// 费用标准ID
    /// </summary>
    [ForeignKey(nameof(BillingStandard))]
    public int BillingStandardId { get; set; }
    public BillingStandard? BillingStandard { get; set; }

    /// <summary>
    /// 是否已发布到员工
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// 生成时间
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 生成人
    /// </summary>
    [MaxLength(50)]
    public string? GeneratedBy { get; set; }
}
