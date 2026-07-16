using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 员工分摊费用账单
/// </summary>
[Table("EmployeeBilling")]
public class EmployeeBilling
{
    /// <summary>
    /// 主键
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 员工ID
    /// </summary>
    [Required]
    public int EmployeeId { get; set; }

    /// <summary>
    /// 工号（冗余，便于查询）
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// 姓名（冗余）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// 宿舍号（冗余）
    /// </summary>
    [MaxLength(20)]
    public string? DormCode { get; set; }

    /// <summary>
    /// 计费月份
    /// </summary>
    [Required]
    [MaxLength(7)]
    public string BillingMonth { get; set; } = string.Empty;

    /// <summary>
    /// 分摊比例 1/N
    /// </summary>
    [Column(TypeName = "decimal(5,4)")]
    public decimal ShareRatio { get; set; }

    /// <summary>
    /// 同住人数
    /// </summary>
    public int ResidentCount { get; set; }

    /// <summary>
    /// 分摊冷水费
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ColdShareAmount { get; set; }

    /// <summary>
    /// 分摊热水费
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal HotShareAmount { get; set; }

    /// <summary>
    /// 分摊电费
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ElectricityShareAmount { get; set; }

    /// <summary>
    /// 分摊合计
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalShareAmount { get; set; }

    /// <summary>
    /// 关联的宿舍账单ID
    /// </summary>
    [ForeignKey(nameof(DormBilling))]
    public int DormBillId { get; set; }
    public DormBilling? DormBilling { get; set; }

    /// <summary>
    /// 是否已发布
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// 生成时间
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}
