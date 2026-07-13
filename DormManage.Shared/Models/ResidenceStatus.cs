namespace DormManage.Shared.Models;

/// <summary>
/// 住宿状态
/// </summary>
public class ResidenceStatus : BaseEntity
{
    /// <summary>
    /// 状态编码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 状态名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 住宿状态枚举值
/// </summary>
public static class ResidenceStatusEnum
{
    public const string Lodged = "LODGED";           // 已住宿
    public const string NotLodged = "NOT_LODGED";   // 未住宿
    public const string Pending = "PENDING";         // 待入住
}
