namespace DormManage.Shared.Models;

/// <summary>
/// 在职状态
/// </summary>
public class EmploymentStatus : BaseEntity
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
/// 在职状态枚举值
/// </summary>
public static class EmploymentStatusEnum
{
    public const string Active = "ACTIVE";           // 在职
    public const string Onboarding = "ONBOARDING";   // 待入职
    public const string Left = "LEFT";               // 已离职
}
