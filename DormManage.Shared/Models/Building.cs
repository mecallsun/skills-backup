namespace DormManage.Shared.Models;

/// <summary>
/// 楼栋
/// </summary>
public class Building : BaseEntity
{
    /// <summary>
    /// 楼栋名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 排序号
    /// </summary>
    public int SortOrder { get; set; }
}
