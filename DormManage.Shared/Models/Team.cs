namespace DormManage.Shared.Models;
public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    /// <summary>v2.13.161：DB Schema NOT NULL，需要 EF 模型有该字段以避免 NULL INSERT</summary>
    public DateTime UpdatedAt { get; set; }
}
