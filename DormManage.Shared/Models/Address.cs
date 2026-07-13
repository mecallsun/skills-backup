namespace DormManage.Shared.Models;

/// <summary>
/// 地址
/// </summary>
public class Address : BaseEntity
{
    /// <summary>
    /// 地址描述
    /// </summary>
    public string AddressText { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
