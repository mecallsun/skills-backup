namespace DormManage.Shared.Models;

/// <summary>
/// 基础实体类
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    // v2.13.161：DB Schema 要求 UpdatedAt NOT NULL（DEFAULT GETDATE()），强制非空避免 NULL INSERT
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
