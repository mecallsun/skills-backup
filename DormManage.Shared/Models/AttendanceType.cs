namespace DormManage.Shared.Models;

/// <summary>
/// 考勤班次
/// </summary>
public class AttendanceType : BaseEntity
{
    /// <summary>
    /// 班次编码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 班次名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工作时段
    /// </summary>
    public string? WorkHours { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 考勤班次枚举值
/// </summary>
public static class AttendanceTypeEnum
{
    public const string Default = "DEFAULT";        // 默认 09:00-18:00
    public const string Morning = "MORNING";        // 早班 06:00-14:00
    public const string Middle = "MIDDLE";          // 中班 14:00-22:00
    public const string Evening = "EVENING";        // 晚班 18:00-02:00
    public const string Night = "NIGHT";            // 夜班 22:00-06:00
    public const string Other = "OTHER";             // 其他 不定期
}
