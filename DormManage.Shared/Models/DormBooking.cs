using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 办理记录（入住/退房）— v2.13.24 P75 业务深度字段补全
///
/// 完整字段映射与 init_schema.sql [DormBooking] 1:1 对齐 + 业务深度字段。
/// </summary>
[Table("DormBooking")]
public class DormBooking : BaseEntity
{
    // ========== 核心 FK 与冗余字段 ==========

    /// <summary>员工ID（FK → SysEmployee.Id）</summary>
    [Required]
    public int EmployeeId { get; set; }

    /// <summary>员工工号（冗余，便于查询）</summary>
    [Required]
    [MaxLength(64)]
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>员工姓名（冗余）</summary>
    [Required]
    [MaxLength(128)]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>手机号（冗余）</summary>
    [MaxLength(32)]
    public string? Phone { get; set; }

    /// <summary>部门（冗余）</summary>
    [MaxLength(128)]
    public string? Department { get; set; }

    /// <summary>考勤班次 ID（FK → AttendanceType，v2.13.24 新增，列表展示）</summary>
    public int? AttendanceTypeId { get; set; }

    // ========== v2.13.24 P75 业务深度字段（1-4） ==========

    /// <summary>
    /// 床位号（v2.13.24 P75 新增）— 文档 36 §R-BED-006 / v2.12.40
    /// 入住操作时按 activeCount+1 计算，调宿/容量变更时自动重新分配
    /// </summary>
    public int? BedNo { get; set; }

    /// <summary>
    /// 调宿来源房号（v2.13.24 P75 新增）— 调岗场景记录"由 X 调宿至 Y"
    /// 与 Remark 中的"由 D-055 调宿至 D-088"语义一致但作为独立字段便于查询统计
    /// </summary>
    [MaxLength(32)]
    public string? MoveFromDormCode { get; set; }

    /// <summary>
    /// 实际入住日期（v2.13.24 P75 新增）— 区别于预约日期 BookingDate
    /// 当 Status 由 1 预约 → 2 在宿时记录（ConfirmCheckIn 操作时填充）
    /// </summary>
    public DateOnly? ActualCheckInDate { get; set; }

    /// <summary>
    /// 实际退房日期（v2.13.24 P75 新增）— 区别于入退日期 BookingDate
    /// 当 Status 由 2 在宿 → 3 已退房时记录
    /// </summary>
    public DateOnly? ActualCheckOutDate { get; set; }

    // ========== 住宿信息 ==========

    /// <summary>宿舍代码（FK → Dorm.DormCode）</summary>
    [Required]
    [MaxLength(64)]
    public string DormCode { get; set; } = string.Empty;

    /// <summary>类型：1=入住 2=退房</summary>
    [Required]
    public int Type { get; set; }

    /// <summary>入退日期（预约日期或实际日期）</summary>
    [Required]
    public DateOnly BookingDate { get; set; }

    /// <summary>状态：1=预约 2=在宿 3=已退房 4=已取消</summary>
    [Required]
    public int Status { get; set; }

    /// <summary>原因（入职、调岗、离职等）</summary>
    [MaxLength(512)]
    public string? Reason { get; set; }

    /// <summary>
    /// 取消原因（v2.13.24 P75 新增）— 专门记录 Status=4 已取消的原因
    /// 与 Reason 区分：Reason 是入住/退房的原因；CancellationReason 是取消的原因
    /// </summary>
    [MaxLength(512)]
    public string? CancellationReason { get; set; }

    /// <summary>备注</summary>
    [MaxLength(1024)]
    public string? Remark { get; set; }

    // ========== 系统记录 ==========

    /// <summary>实际登记日期（默认 now()）</summary>
    [Required]
    public DateTime RegistrationDate { get; set; }

    /// <summary>登记人（创建记录时 = 当前登录用户名）</summary>
    [Required]
    [MaxLength(64)]
    public string Registrar { get; set; } = string.Empty;

    /// <summary>
    /// 入住确认操作人（v2.13.24 P75 新增）— Status 由 1→2 时记录
    /// 与 Registrar 区分：Registrar 是创建人，CheckInOperator 是确认入住的人
    /// </summary>
    [MaxLength(64)]
    public string? CheckInOperator { get; set; }

    /// <summary>
    /// 退房确认操作人（v2.13.24 P75 新增）— Status 由 2→3 时记录
    /// </summary>
    [MaxLength(64)]
    public string? CheckOutOperator { get; set; }

    // ========== 计算字段（[NotMapped]，运行时计算） ==========

    /// <summary>
    /// 入住天数（v2.13.24 P75 新增）— 仅在 Status=3 已退房时有值
    /// 计算：ActualCheckOutDate - ActualCheckInDate + 1；用于员工分摊费用计算
    /// </summary>
    [NotMapped]
    public int? Days
    {
        get
        {
            if (ActualCheckInDate.HasValue && ActualCheckOutDate.HasValue)
                return ActualCheckOutDate.Value.DayNumber - ActualCheckInDate.Value.DayNumber + 1;
            return null;
        }
    }
}

/// <summary>办理类型枚举</summary>
public static class BookingType
{
    public const int CheckIn = 1;
    public const int CheckOut = 2;
}

/// <summary>办理状态枚举</summary>
public static class BookingStatus
{
    public const int Reserved = 1;
    public const int Staying = 2;
    public const int CheckedOut = 3;
    public const int Cancelled = 4;
}