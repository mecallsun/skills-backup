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
    public int Gender { get; set; }

    /// <summary>
    /// 房间数
    /// </summary>
    public int RoomCount { get; set; } = 1;

    /// <summary>
    /// 床位号集合（P2-4 新增）
    /// 格式：逗号分隔字符串，例如 "1,2,3,4" 表示有 4 个床位，编号 1~4
    /// Booking 创建时必须从中选择；避免人工误选
    /// </summary>
    [MaxLength(100)]
    public string? BedNumbers { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public string? Remark { get; set; }
}
