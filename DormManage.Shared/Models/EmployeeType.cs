namespace DormManage.Shared.Models;

/// <summary>
/// 员工类型
/// </summary>
public class EmployeeType : BaseEntity
{
    /// <summary>
    /// 类型编码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 类型名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 员工类型枚举值
/// </summary>
public static class EmployeeTypeEnum
{
    public const string Contract = "CONTRACT";      // 合同工
    public const string Temporary = "TEMPORARY";    // 临时工
    public const string Outsource = "OUTSOURCE";     // 外包
    public const string Intern = "INTERN";           // 实习生
    public const string Onsite = "ONSITE";           // 驻场
}
