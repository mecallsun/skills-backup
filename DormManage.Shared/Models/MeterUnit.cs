namespace DormManage.Shared.Models;

/// <summary>
/// 计量单位
/// </summary>
public class MeterUnit : BaseEntity
{
    /// <summary>
    /// 单位编码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 单位名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示单位
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 计量单位枚举值
/// </summary>
public static class MeterUnitEnum
{
    public const string ColdWater = "COLD_WATER";      // 冷水 m³
    public const string HotWater = "HOT_WATER";        // 热水 m³
    public const string Electricity = "ELECTRICITY";    // 电 度
}
