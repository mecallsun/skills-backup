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
    /// v2.13.61 新增：排序号（SQL 列已存在，EF 模型未映射）
    /// 用于基础资料字典排序展示
    /// </summary>
    public int SortOrder { get; set; } = 0;

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
