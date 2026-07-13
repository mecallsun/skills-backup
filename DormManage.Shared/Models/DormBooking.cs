using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 办理记录（入住/退房）
/// </summary>
[Table("DormBooking")]
public class DormBooking : BaseEntity
{
    /// <summary>
    /// 员工ID
    /// </summary>
    [Required]
    public int EmployeeId { get; set; }

    /// <summary>
    /// 员工工号（冗余）
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// 员工姓名（冗余）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// 手机号（冗余）
    /// </summary>
    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// 部门（冗余）
    /// </summary>
    [MaxLength(50)]
    public string? Department { get; set; }

    /// <summary>
    /// 宿舍代码
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string DormCode { get; set; } = string.Empty;

    /// <summary>
    /// 类型：1=入住 2=退房
    /// </summary>
    [Required]
    public int Type { get; set; }

    /// <summary>
    /// 入退日期
    /// </summary>
    [Required]
    public DateOnly BookingDate { get; set; }

    /// <summary>
    /// 状态：1=预约 2=在宿 3=已退房 4=已取消
    /// </summary>
    [Required]
    public int Status { get; set; }

    /// <summary>
    /// 原因（入职、调岗、离职等）
    /// </summary>
    [MaxLength(200)]
    public string? Reason { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 实际登记日期
    /// </summary>
    [Required]
    public DateTime RegistrationDate { get; set; }

    /// <summary>
    /// 登记人
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Registrar { get; set; } = string.Empty;
}

/// <summary>
/// 办理类型枚举
/// </summary>
public static class BookingType
{
    public const int CheckIn = 1;   // 入住
    public const int CheckOut = 2;  // 退房
}

/// <summary>
/// 办理状态枚举
/// </summary>
public static class BookingStatus
{
    public const int Reserved = 1;      // 预约
    public const int Staying = 2;      // 在宿
    public const int CheckedOut = 3;   // 已退房
    public const int Cancelled = 4;    // 已取消
}
