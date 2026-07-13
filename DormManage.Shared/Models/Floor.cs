namespace DormManage.Shared.Models;

/// <summary>
/// 楼层
/// </summary>
public class Floor : BaseEntity
{
    /// <summary>
    /// 楼层号
    /// </summary>
    public int FloorNo { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
