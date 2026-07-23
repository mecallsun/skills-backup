using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 设备读数日志 — v2.13.130 新增
///
/// 三层递进抽象关系：
/// - 设备档案（DormMeter v2.13.120）：静态配置（房号 ↔ 表 ID 1:1）
/// - **设备读数日志（本类 v2.13.130）**：动态日志（设备某时间点的一次读数流水）
/// - 智能抄表（MeterRecord v2.13.24）：月度聚合统计结果
///
/// 与 DormMeter 解耦：本表不 FK 到 DormMeter，因为设备读数可能是 PDA 直接上传的
/// 原始数据流（不经过 DormMeter 配置层），例如 PDA 抄表自动入库、PDA 离线模式
/// 数据同步、手工补录到日志等场景。设备类型 + 设备 ID 是字段值，不强制外键。
/// </summary>
[Table("EquipmentReading")]
public class EquipmentReading : BaseEntity
{
    /// <summary>主键</summary>
    [Column("ReadingId")]
    public new int Id { get; set; }

    /// <summary>设备 ID（电表/冷水/热水表编号，NVARCHAR(64)）</summary>
    [Required]
    [StringLength(64)]
    [Column("EquipmentId")]
    public string EquipmentId { get; set; } = string.Empty;

    /// <summary>
    /// 设备类型（1=电表 2=冷水表 3=热水表，byte 节省空间）
    /// </summary>
    [Column("EquipmentType")]
    public byte EquipmentType { get; set; }

    /// <summary>读数（DECIMAL(12,2)，精度 2 位小数）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal Reading { get; set; }

    /// <summary>读取时间（DATETIME，业务读取时刻）</summary>
    [Required]
    [Column("ReadTime")]
    public DateTime ReadTime { get; set; }

    /// <summary>备注（NVARCHAR(500)）</summary>
    [StringLength(500)]
    [Column("Remark")]
    public string? Remark { get; set; }

    /// <summary>记录创建人（SysUser FK，冗余字段，便于审计）</summary>
    [StringLength(64)]
    [Column("CreatedBy")]
    public string? CreatedBy { get; set; }
}

/// <summary>设备类型枚举（v2.13.130 新增，与 EquipmentReading.EquipmentType 配套）</summary>
public static class EquipmentType
{
    /// <summary>电表</summary>
    public const byte Electric = 1;
    /// <summary>冷水表</summary>
    public const byte ColdWater = 2;
    /// <summary>热水表</summary>
    public const byte HotWater = 3;

    public static string GetDisplayName(byte type) => type switch
    {
        Electric => "电表",
        ColdWater => "冷水",
        HotWater => "热水",
        _ => "未知",
    };

    public static string GetBadgeClass(byte type) => type switch
    {
        Electric => "bg-warning text-dark",
        ColdWater => "bg-info text-dark",
        HotWater => "bg-danger",
        _ => "bg-secondary",
    };

    public static string GetIcon(byte type) => type switch
    {
        Electric => "bi-lightning-charge-fill",
        ColdWater => "bi-droplet-fill",
        HotWater => "bi-droplet-half",
        _ => "bi-question-circle",
    };

    public static bool IsValid(byte type) => type >= Electric && type <= HotWater;
}
