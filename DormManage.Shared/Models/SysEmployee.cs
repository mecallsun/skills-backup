using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 员工档案
/// </summary>
[Table("SysEmployee")]
public class SysEmployee : BaseEntity
{
    /// <summary>
    /// 工号
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// 姓名
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string RealName { get; set; } = string.Empty;

    /// <summary>
    /// 部门ID
    /// </summary>
    public int DepartmentId { get; set; }

    /// <summary>
    /// 部门名称（冗余）
    /// </summary>
    [MaxLength(50)]
    public string? Department { get; set; }

    /// <summary>
    /// 员工类型ID
    /// </summary>
    public int EmployeeTypeId { get; set; }

    /// <summary>
    /// 员工类型（v2.11.6 新增导航属性）
    /// </summary>
    /// <remarks>
    /// EF Core [ForeignKey] 关联 → EmployeeType.Id
    /// 引用本字段的业务场景：
    /// - 人员清单列表 / 详情（员工类型 Badge + 中文名）
    /// - 宿舍详情页"当前入住人员"列表（v2.11.6 强制数据源）
    /// - 办理记录列表（员工类型筛选）
    /// - 任何其他需要展示"员工类型中文名"的页面
    /// 单一数据源，避免在多处冗余存储。
    /// </remarks>
    [ForeignKey(nameof(EmployeeTypeId))]
    public EmployeeType? EmployeeType { get; set; }

    /// <summary>员工类型名称（冗余，对应真实表 EmployeeType nvarchar 列，NOT NULL）</summary>
    public string? EmployeeTypeText { get; set; }

    /// <summary>班组ID（对应真实表 TeamId，NOT NULL）</summary>
    public int TeamId { get; set; }

    /// <summary>性别（1=男 2=女，对应真实表 Gender，默认 1）</summary>
    public int Gender { get; set; } = 1;

    /// <summary>
    /// 手机号
    /// </summary>
    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// 在职状态ID（v2.11.18 新增 FK 字段，引用基础资料-在职状态表 EmploymentStatus.Id）
    /// </summary>
    /// <remarks>
    /// 外键关联到 EmploymentStatus.Id（1=在职 / 2=待入职 / 3=已离职）
    /// 所有"在职状态"展示必须通过 FK 关联引用 EmploymentStatus 表的 Name 显示，
    /// 不得在业务表/前端硬编码枚举值。
    /// 单一数据源：基础资料-在职状态表（EmploymentStatus）。
    /// </remarks>
    public int EmploymentStatusId { get; set; } = EmployeeStatus.Active;

    /// <summary>
    /// 在职状态（v2.11.18 起标记为冗余字段，与 EmploymentStatusId FK 保持同步）
    /// </summary>
    /// <remarks>
    /// 保留此 int 字段仅为向后兼容旧代码与种子数据；新代码必须使用 EmploymentStatusId FK 字段。
    /// </remarks>
    [Obsolete("请使用 EmploymentStatusId + EmploymentStatus 导航属性，引用基础资料-在职状态表")]
    public int Status { get; set; } = 1;

    /// <summary>
    /// 在职状态导航属性（v2.11.18 新增）
    /// </summary>
    /// <remarks>
    /// EF Core [ForeignKey] 关联 → EmploymentStatus.Id
    /// 引用本字段的业务场景：
    /// - 人员清单列表（在职状态 Badge）
    /// - 首页数据看板 KPI 4 异常人员统计
    /// - 任何其他需要展示"在职状态中文名"的页面
    /// 单一数据源，避免在多处冗余存储字典值。
    /// </remarks>
    [ForeignKey(nameof(EmploymentStatusId))]
    public EmploymentStatus? EmploymentStatus { get; set; }

    /// <summary>
    /// 入职日期
    /// </summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// 离职日期
    /// </summary>
    public DateOnly? LeaveDate { get; set; }

    /// <summary>
    /// 当前宿舍代码（v2.11.18 起作为"入住人数"统计的单一数据源）
    /// </summary>
    /// <remarks>
    /// 业务含义：人员当前是否入住宿舍的最终事实。
    /// 入住人数 = count(PERSONNEL where DormCode != null and DormCode != '')
    /// 维护规则（v2.11.18 强制）：
    /// - 办理登记操作（CheckIn/ConfirmCheckIn/CancelToday）→ 同步更新 DormCode = 记录 DormCode
    /// - 办理登记操作（CheckOut/ConfirmCheckOutCreate/UndoCheckOut）→ 同步清空 DormCode = NULL
    /// - 撤销预约（CANCELLED） → 不变更 DormCode（未生效过）
    /// 数据一致性：人员清单 dormCode 列 / 首页 KPI 1 入住人数 / 人员清单宿舍列 共享此字段。
    /// </remarks>
    [MaxLength(20)]
    public int? BedNo { get; set; }
    public string? DormCode { get; set; }

    /// <summary>
    /// 班组（v2.13.3 补充字段，对应基础资料-班组表 Team 字符串）
    /// </summary>
    [MaxLength(20)]
    public string? Team { get; set; }

    /// <summary>
    /// 住宿状态ID（v2.11.20 新增 FK 字段，引用基础资料-住宿状态表 ResidenceStatus.Id）
    /// </summary>
    /// <remarks>
    /// 外键关联到 ResidenceStatus.Id（1=已住宿 / 2=未住宿 / 3=待入住）
    /// 概念区分（v2.11.20 深度统一）：
    /// - 住宿状态（本字段，ResidenceStatus）：人员维度的派生属性，表示整体住宿情况
    /// - 办理登记流水状态（BOOKINGS.Status）：单条办理记录的状态（1=预约 / 2=在宿 / 3=已退房 / 4=已取消）
    /// - 住宿记录状态（已弃用）：旧版住宿记录表
    /// 单一数据源：所有"住宿状态"展示必须通过 FK 关联引用 ResidenceStatus 表的 Name 显示。
    /// </remarks>
    public int ResidenceStatusId { get; set; } = 2; // 默认未住宿

    /// <summary>
    /// 住宿状态导航属性（v2.11.20 新增）
    /// </summary>
    /// <remarks>
    /// EF Core [ForeignKey] 关联 → ResidenceStatus.Id
    /// </remarks>
    [ForeignKey(nameof(ResidenceStatusId))]
    public ResidenceStatus? ResidenceStatus { get; set; }

    /// <summary>
    /// 考勤班次ID（v2.11.7 新增字段）
    /// </summary>
    /// <remarks>
    /// 外键关联到 AttendanceType.Id
    /// </remarks>
    public int? AttendanceTypeId { get; set; }

    /// <summary>
    /// 考勤班次导航属性（v2.11.7 新增）
    /// </summary>
    /// <remarks>
    /// EF Core [ForeignKey] 关联 → AttendanceType.Id
    /// 引用本字段的业务场景：
    /// - 人员清单列表（考勤班次 Badge）
    /// - 宿舍详情"当前入住人员"列表（v2.11.6 已建立关联引用规则）
    /// - 宿舍账单明细"员工分摊明细"列表（v2.11.7 新增考勤班次列）
    /// - 任何其他需要展示"考勤班次中文名+颜色"的页面
    /// 单一数据源，避免在多处冗余存储。
    /// </remarks>
    [ForeignKey(nameof(AttendanceTypeId))]
    public AttendanceType? AttendanceType { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public string? Remark { get; set; }
}

/// <summary>
/// 在职状态枚举
/// </summary>
public static class EmployeeStatus
{
    public const int Active = 1;       // 在职
    public const int Onboarding = 2;   // 待入职
    public const int Left = 3;        // 已离职
}
