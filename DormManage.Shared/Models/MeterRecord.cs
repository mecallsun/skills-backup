using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>智能抄表状态枚举（v2.13.24 5 项标准）</summary>
public enum MeterRecordStatus
{
    /// <summary>未完成（占位记录，三表全 0）</summary>
    Incomplete = 0,

    /// <summary>正常（三表完整且读数递增）</summary>
    Normal = 1,

    /// <summary>已修正（三表完整，已修正过）</summary>
    Corrected = 2,

    /// <summary>未完成(PDA 占位)（v2.13.3 新增）</summary>
    Unfinished = 3,

    /// <summary>已作废（v2.13.3 新增）</summary>
    Voided = 4
}

/// <summary>抄表方式枚举（v2.13.24 P76 新增）</summary>
public static class MeterReadMode
{
    public const int PDA = 1;          // PDA 端上传
    public const int Manual = 2;        // 人工手动补录
    public const int Import = 3;        // Excel 批量导入
    public const int AutoGenerate = 4;  // 系统自动生成占位
}

/// <summary>
/// 抄表状态显示名映射（中文）— v2.13.24 5 项枚举
/// </summary>
public static class MeterStatusExtensions
{
    public static string GetDisplayName(this MeterRecordStatus status) => status switch
    {
        MeterRecordStatus.Incomplete => "未完成",
        MeterRecordStatus.Normal => "正常",
        MeterRecordStatus.Corrected => "已修正",
        MeterRecordStatus.Unfinished => "未完成(PDA)",
        MeterRecordStatus.Voided => "已作废",
        _ => "未知"
    };

    public static string GetBadgeClass(this MeterRecordStatus status) => status switch
    {
        MeterRecordStatus.Incomplete => "bg-warning text-dark",
        MeterRecordStatus.Normal => "bg-success",
        MeterRecordStatus.Corrected => "bg-info",
        MeterRecordStatus.Unfinished => "bg-warning",
        MeterRecordStatus.Voided => "bg-secondary text-decoration-line-through",
        _ => "bg-light"
    };

    public static bool IsEffective(this MeterRecordStatus status) => status == MeterRecordStatus.Normal || status == MeterRecordStatus.Corrected;
}

/// <summary>
/// 智能抄表 — v2.13.24 P76 业务深度字段补全
///
/// 与 init_schema.sql [MeterRecord] 表 1:1 对齐 + 业务深度字段：
/// - ColdUsage/HotUsage/ElectricUsage 用量字段（SQL NOT NULL，EF 完全缺失）
/// - PreviousColdReading 等上月读数参考
/// - ReadDate 业务抄表日期（区别于 ServerCreatedAt 入库时间）
/// - ReadMode 抄表方式（PDA/手动/导入/自动）
/// - CorrectionReason/CorrectedBy/CorrectedAt 修正追踪
/// - ConfirmedAt PDA 确认时间
/// </summary>
[Table("MeterRecord")]
public class MeterRecord : BaseEntity
{
    /// <summary>
    /// 智能抄表ID（v2.13.79 修复：SQL BIGINT ↔ EF long 类型对齐）
    /// 原 BaseEntity.Id 为 int，但 init_schema.sql [MeterRecord].[RecordId] 为 BIGINT IDENTITY(1,1)，
    /// EF 物化时 Int64 → Int32 cast 失败 → /Meter 列表 Error。
    /// 覆盖基类 int Id 为 long Id 并显式映射到 RecordId 列。
    /// </summary>
    [Column("RecordId")]
    public new long Id { get; set; }

    /// <summary>宿舍ID（FK → Dorm.DormId，SQL 有外键）</summary>
    public int DormId { get; set; }

    /// <summary>宿舍号（冗余）</summary>
    [Required]
    [MaxLength(32)]
    public string DormCode { get; set; } = string.Empty;

    /// <summary>抄表月份（yyyy-MM）</summary>
    [Required]
    [MaxLength(7)]
    public string ReadMonth { get; set; } = string.Empty;

    // ========== 三表读数（SQL DECIMAL(12,2) NOT NULL） ==========

    /// <summary>冷水表读数（DECIMAL(12,2)）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ColdMeter { get; set; }

    /// <summary>热水表读数（DECIMAL(12,2)）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal HotMeter { get; set; }

    /// <summary>电表读数（DECIMAL(12,2)）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ElectricMeter { get; set; }

    // ========== v2.13.24 P76 关键补全：三表用量（SQL NOT NULL） ==========

    /// <summary>冷水用量（DECIMAL(12,2) NOT NULL）— 当月读数 - 上月读数</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ColdUsage { get; set; }

    /// <summary>热水用量（DECIMAL(12,2) NOT NULL）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal HotUsage { get; set; }

    /// <summary>电用量（DECIMAL(12,2) NOT NULL）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ElectricUsage { get; set; }

    // ========== v2.13.24 P76 新增：上月读数参考 ==========

    /// <summary>上月冷水读数（v2.13.24 P76 新增）— 手动补录页"上月读数参考卡片"</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal PreviousColdReading { get; set; }

    /// <summary>上月热水读数</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal PreviousHotReading { get; set; }

    /// <summary>上月电读数</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal PreviousElectricReading { get; set; }

    // ========== 抄表信息 ==========

    /// <summary>抄表员 / PDA 操作员（NVARCHAR(64)，v2.13.89 改为冗余字段，真源为 OperatorUserId FK）</summary>
    [MaxLength(64)]
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// 抄表员 UserId FK（v2.13.89 新增：物理真理源 — 关联账号主键）
    /// 页面渲染通过 JOIN SysUser 取 DisplayName 显示
    /// </summary>
    public int? OperatorUserId { get; set; }

    /// <summary>抄表员姓名（v2.13.89 JOIN 派生：SysUser.DisplayName）</summary>
    [NotMapped]
    public string? OperatorDisplayName { get; set; }

    /// <summary>设备序列号（NVARCHAR(64)，SQL NOT NULL）— v2.13.80 修正 DB 一致性</summary>
    [MaxLength(64)]
    public string? DeviceSn { get; set; }

    /// <summary>客户端记录ID（NVARCHAR(64) PDA 唯一键，SQL NOT NULL）— v2.13.80 修正 DB 一致性</summary>
    [MaxLength(64)]
    public string? ClientRecordId { get; set; }

    /// <summary>客户端创建时间（PDA 端抄表时间）</summary>
    public DateTime? ClientCreatedAt { get; set; }

    /// <summary>服务器创建时间（入库时间）</summary>
    public DateTime ServerCreatedAt { get; set; } = DateTime.Now;

    /// <summary>状态（5 项枚举：0/1/2/3/4）</summary>
    public byte Status { get; set; }

    /// <summary>备注（含历史覆盖快照）</summary>
    [MaxLength(512)]
    public string? Remark { get; set; }

    // ========== v2.13.24 P76 业务深度字段 ==========

    /// <summary>业务抄表日期（v2.13.24 P76 新增）— 区别于 ServerCreatedAt（入库时间）</summary>
    public DateOnly? ReadDate { get; set; }

    /// <summary>抄表方式（v2.13.24 P76 新增）— 1=PDA 2=手动 3=导入 4=自动</summary>
    public byte ReadMode { get; set; } = MeterReadMode.Manual;

    /// <summary>修正原因（v2.13.24 P76 新增）— Status 由 1→2 必填</summary>
    [MaxLength(512)]
    public string? CorrectionReason { get; set; }

    /// <summary>修正人（v2.13.24 P76 新增）</summary>
    [MaxLength(64)]
    public string? CorrectedBy { get; set; }

    /// <summary>修正时间（v2.13.24 P76 新增）</summary>
    public DateTime? CorrectedAt { get; set; }

    /// <summary>PDA 确认时间（v2.13.24 P76 新增）— PDA 端用户点击"提交"的时间</summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// 自动判定状态（v2.13.24 重构：根据三表用量值判定）
    /// - 0：未完成（任一表用量=0 或缺读数）
    /// - 1：正常（三表读数齐全且递增）
    /// - 2：已修正（需手动触发）
    /// </summary>
    public static byte DetermineStatus(decimal cold, decimal hot, decimal electric)
    {
        int nonZeroCount = 0;
        if (cold > 0) nonZeroCount++;
        if (hot > 0) nonZeroCount++;
        if (electric > 0) nonZeroCount++;

        if (nonZeroCount == 0) return (byte)MeterRecordStatus.Incomplete;  // 全 0：占位
        if (nonZeroCount == 3) return (byte)MeterRecordStatus.Normal;      // 3 表齐全
        return (byte)MeterRecordStatus.Incomplete;  // 部分：未完成
    }

    /// <summary>获取状态名称（v2.13.24 5 项）</summary>
    public string GetStatusName() => ((MeterRecordStatus)Status).GetDisplayName();
}