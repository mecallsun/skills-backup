using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 住宿档案（v2.13.24 P0-2 修复：补齐 init_schema.sql 的 9 列 PDA 扫码抄表关键字段）
///
/// 与 init_schema.sql 的 [Dorm] 表 1:1 对齐（主键 DormId + 25 列）。
/// </summary>
[Table("Dorm")]
public class Dorm : BaseEntity
{
    /// <summary>住宿代码</summary>
    [Required]
    [MaxLength(32)]
    public string DormCode { get; set; } = string.Empty;

    // ========== v2.13.24 P0-2 新增：9 列 PDA 扫码抄表关键字段 ==========

    /// <summary>楼栋（NVARCHAR(32)）— PDA 扫码展示</summary>
    [MaxLength(32)]
    public string Building { get; set; } = string.Empty;

    /// <summary>楼层（NVARCHAR(16)）— PDA 扫码展示</summary>
    [MaxLength(16)]
    public string Floor { get; set; } = string.Empty;

    /// <summary>房间号（NVARCHAR(16)）— PDA 扫码展示</summary>
    [MaxLength(16)]
    public string RoomNo { get; set; } = string.Empty;

    /// <summary>住宿地址（NVARCHAR(128)）— PDA 扫码展示完整地址</summary>
    [MaxLength(128)]
    public string DormAddress { get; set; } = string.Empty;

    /// <summary>住宿类型（NVARCHAR(16)）— 单人/双人/多人</summary>
    [MaxLength(16)]
    public string DormType { get; set; } = string.Empty;

    /// <summary>是否冷水表 BIT DEFAULT 1 — 抄表时判定是否需要冷水读数</summary>
    public bool HasColdMeter { get; set; } = true;

    /// <summary>是否热水表 BIT DEFAULT 1</summary>
    public bool HasHotMeter { get; set; } = true;

    /// <summary>是否电表 BIT DEFAULT 1</summary>
    public bool HasElectricMeter { get; set; } = true;

    /// <summary>条形码（NVARCHAR(64) UNIQUE）— PDA 扫码唯一标识</summary>
    [Required]
    [MaxLength(64)]
    public string Barcode { get; set; } = string.Empty;

    /// <summary>启用标志 BIT DEFAULT 1</summary>
    public bool IsActive { get; set; } = true;

    // ========== 原 v2.13.10 已实现字段（保留） ==========

    /// <summary>楼栋ID（FK → Building）</summary>
    public int BuildingId { get; set; }

    /// <summary>楼栋名称（冗余）</summary>
    [MaxLength(50)]
    public string? BuildingName { get; set; }

    /// <summary>楼层ID（FK → Floor）</summary>
    public int FloorId { get; set; }

    /// <summary>地址ID（FK → Address）</summary>
    public int AddressId { get; set; }

    /// <summary>地址（冗余）</summary>
    [MaxLength(200)]
    public string? AddressText { get; set; }

    /// <summary>住宿容量</summary>
    public int Capacity { get; set; }

    /// <summary>性别：1=男 2=女 0=不限</summary>
    public int Gender { get; set; }

    /// <summary>房间数</summary>
    public int RoomCount { get; set; } = 1;

    /// <summary>
    /// 床位号集合（P2-4 新增）
    /// 格式：逗号分隔字符串，例如 "1,2,3,4" 表示有 4 个床位，编号 1~4
    /// Booking 创建时必须从中选择；避免人工误选
    /// </summary>
    [MaxLength(1000)]
    public string? BedNumbers { get; set; }

    // ========== v2.13.24 P77 抄表相关冗余字段 ==========

    /// <summary>最近抄表月份（yyyy-MM）— 上次抄表月份缓存</summary>
    [MaxLength(7)]
    public string? LastReadMonth { get; set; }

    /// <summary>最近冷水表读数（缓存上月读数）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal? LastColdMeter { get; set; }

    /// <summary>最近热水表读数（缓存上月读数）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal? LastHotMeter { get; set; }

    /// <summary>最近电表读数（缓存上月读数）</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal? LastElectricMeter { get; set; }

    /// <summary>最近抄表时间（PDA 扫码缓存）</summary>
    public DateTime? LastReadAt { get; set; }

    /// <summary>备注</summary>
    [MaxLength(256)]
    public string? Remark { get; set; }
}