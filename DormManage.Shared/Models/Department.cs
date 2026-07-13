namespace DormManage.Shared.Models;

/// <summary>
/// 部门
/// </summary>
public class Department : BaseEntity
{
    /// <summary>
    /// 部门编码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 部门名称
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
