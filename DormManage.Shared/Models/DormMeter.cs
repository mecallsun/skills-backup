using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 设备档案 — v2.13.120 新增
///
/// 与 Dorm 1:1 关系（DormId FK UNIQUE）：
/// - 每间宿舍只有一套水/电表（电表 1 个 + 冷水表 1 个 + 热水表 1 个）
/// - Dorm 删除时级联清理 DormMeter
///
/// 业务用途：
/// - 抄表记录关联：本表保存表 ID，MeterRecord 保存读数
/// - 设备盘点：定期核对表 ID 与现场一致
/// </summary>
[Table("DormMeter")]
public class DormMeter : BaseEntity
{
    /// <summary>主键</summary>
    [Column("DormMeterId")]
    public new int Id { get; set; }

    /// <summary>房号 FK → Dorm.DormId（UNIQUE 1:1）</summary>
    [Required]
    [Column("DormId")]
    public int DormId { get; set; }

    /// <summary>电表 ID/编号（现场标识，可空）</summary>
    [StringLength(64)]
    [Column("ElectricMeterId")]
    public string? ElectricMeterId { get; set; }

    /// <summary>冷水表 ID/编号（现场标识，可空）</summary>
    [StringLength(64)]
    [Column("ColdWaterMeterId")]
    public string? ColdWaterMeterId { get; set; }

    /// <summary>热水表 ID/编号（现场标识，可空）</summary>
    [StringLength(64)]
    [Column("HotWaterMeterId")]
    public string? HotWaterMeterId { get; set; }

    /// <summary>备注</summary>
    [StringLength(500)]
    [Column("Remark")]
    public string? Remark { get; set; }

    /// <summary>导航属性：关联的宿舍档案</summary>
    [ForeignKey(nameof(DormId))]
    public Dorm? Dorm { get; set; }
}