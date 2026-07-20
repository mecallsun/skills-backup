using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 系统操作日志（v2.13.24 P0-7 修复：与 init_schema.sql [SysOpLog] 表 1:1 对齐）
///
/// SQL 主键为 BIGINT LogId，EF 实体通过 HasColumnName("LogId") 映射。
/// </summary>
[Table("SysOpLog")]
public class SysOpLog
{
    /// <summary>日志ID（PK BIGINT）</summary>
    [Key]
    [Column("LogId")]
    public long Id { get; set; }

    /// <summary>操作用户ID</summary>
    public int UserId { get; set; }

    /// <summary>用户名（NVARCHAR(64)）</summary>
    [Required]
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    /// <summary>操作类型（NVARCHAR(128)）</summary>
    [Required]
    [MaxLength(128)]
    public string Action { get; set; } = string.Empty;

    /// <summary>操作目标（NVARCHAR(512)）</summary>
    [Required]
    [MaxLength(512)]
    public string Target { get; set; } = string.Empty;

    /// <summary>操作详情（NVARCHAR(MAX)）</summary>
    [Required]
    public string Detail { get; set; } = string.Empty;

    /// <summary>客户端IP（NVARCHAR(128)）</summary>
    [Required]
    [MaxLength(128)]
    public string Ip { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}