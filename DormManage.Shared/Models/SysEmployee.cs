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
    /// - 住宿详情页"当前入住人员"列表（v2.11.6 强制数据源）
    /// - 办理记录列表（员工类型筛选）
    /// - 任何其他需要展示"员工类型中文名"的页面
    /// 单一数据源，避免在多处冗余存储。
    /// </remarks>
    [ForeignKey(nameof(EmployeeTypeId))]
    public EmployeeType? EmployeeType { get; set; }

    /// <summary>员工类型名称（冗余，对应真实表 EmployeeType nvarchar 列，NOT NULL）</summary>
    public string? EmployeeTypeText { get; set; }

    /// <summary>班组ID（对应真实表 TeamId，可为空）</summary>
    public int? TeamId { get; set; }

    /// <summary>
    /// 班组导航属性（v2.13.78 新增，对应基础资料-班组表 Team）
    /// </summary>
    /// <remarks>
    /// EF Core [ForeignKey] 关联 → Team.Id
    /// 引用本字段的业务场景：
    /// - 人员清单列表（班组列显示 Team.Name，按 TeamId FK 关联）
    /// - 人员清单筛选条件（班组下拉框）
    /// - 任何其他需要展示"班组中文名"的页面
    ///
    /// v2.13.78 BUG 修复：原代码 DTO 直接读 SysEmployee.Team 字符串字段，
    /// 但 DB 实际只有 TeamId（FK）+ DormDbContext 显式 Ignore(e.Team)，
    /// 导致列表班组列永远显示 "-" 或字符串。
    /// 修复：① 添加本导航属性；② DormDbContext 移除 Ignore；
    ///      ③ Personnel/Index.cshtml.cs 加 .Include(e => e.Team) + DTO 读 FK 名称。
    /// </remarks>
    [ForeignKey(nameof(TeamId))]
    public Team? Team { get; set; }

    /// <summary>性别（1=男 2=女，对应真实表 Gender，默认 1）</summary>
    public int Gender { get; set; } = 1;

    /// <summary>
    /// 手机号
    /// </summary>
    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// 身份证号（v2.13.180 + v2.13.208 修复字段名一致性：DB 列名是 IdNumber，C# 属性保持 IdNumber）
    /// </summary>
    /// <remarks>
    /// 18 位中国大陆居民身份证号（GB 11643-1999）。
    /// 属于高敏感个人信息（高 PII），UI 显示受「字段权限」控制；
    /// 默认 deny-by-default ——未勾选"允许显示隐私字段"角色的视图自动隐藏该列。
    /// 单一数据源：SysEmployee.IdNumber 列（v2.13.180 已添加），不再有冗余字段。
    /// </remarks>
    [MaxLength(18)]
    public string? IdNumber { get; set; }

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
    /// 当前住宿代码（v2.11.18 起作为"入住人数"统计的单一数据源）
    /// </summary>
    /// <remarks>
    /// 业务含义：人员当前是否入住住宿的最终事实。
    /// 入住人数 = count(PERSONNEL where DormCode != null and DormCode != '')
    /// 维护规则（v2.11.18 强制）：
    /// - 办理登记操作（CheckIn/ConfirmCheckIn/CancelToday）→ 同步更新 DormCode = 记录 DormCode
    /// - 办理登记操作（CheckOut/ConfirmCheckOutCreate/UndoCheckOut）→ 同步清空 DormCode = NULL
    /// - 撤销预约（CANCELLED） → 不变更 DormCode（未生效过）
    /// 数据一致性：人员清单 dormCode 列 / 首页 KPI 1 入住人数 / 人员清单住宿列 共享此字段。
    /// </remarks>
    [MaxLength(20)]
    public int? BedNo { get; set; }
    public string? DormCode { get; set; }

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
    /// - 住宿详情"当前入住人员"列表（v2.11.6 已建立关联引用规则）
    /// - 住宿账单明细"员工分摊明细"列表（v2.11.7 新增考勤班次列）
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
