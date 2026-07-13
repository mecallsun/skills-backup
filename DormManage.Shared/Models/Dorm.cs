using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 宿舍档案
/// </summary>
[Table("Dorm")]
public class Dorm : BaseEntity
{
    /// <summary>
    /// 宿舍代码
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string DormCode { get; set; } = string.Empty;

    /// <summary>
    /// 楼栋ID
    /// </summary>
    public int BuildingId { get; set; }

    /// <summary>
    /// 楼栋名称（冗余）
    /// </summary>
    [MaxLength(50)]
    public string? BuildingName { get; set; }

    /// <summary>
    /// 楼层ID
    /// </summary>
    public int FloorId { get; set; }

    /// <summary>
    /// 地址ID
    /// </summary>
    public int AddressId { get; set; }

    /// <summary>
    /// 地址（冗余）
    /// </summary>
    [MaxLength(200)]
    public string? AddressText { get; set; }

    /// <summary>
    /// 宿舍容量
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// 性别：1=男 2=女
    /// </summary>
    public int Gender { get; set; } = 1;

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public string? Remark { get; set; }
}
