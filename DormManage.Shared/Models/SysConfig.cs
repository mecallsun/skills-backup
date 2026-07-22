using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 系统配置（v2.13.24 P0-4 修复：与 init_schema.sql [SysConfig] 表 1:1 对齐）
///
/// SQL 表主键为 NVARCHAR(64) ConfigKey（不是 int Id），与一般 EF 实体模式不同。
/// EF 实体通过 HasColumnName("ConfigKey") 映射。
/// </summary>
[Table("SysConfig")]
public class SysConfig
{
    /// <summary>配置键（PK NVARCHAR(64)）</summary>
    [Key]
    [Required]
    [MaxLength(64)]
    [Column("ConfigKey")]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值（NVARCHAR(MAX)）</summary>
    [Required]
    [Column("ConfigValue")]
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>配置分组（NVARCHAR(32)）</summary>
    [Required]
    [MaxLength(32)]
    [Column("ConfigGroup")]
    public string ConfigGroup { get; set; } = string.Empty;

    /// <summary>配置描述（NVARCHAR(512)）</summary>
    [Required]
    [MaxLength(512)]
    [Column("Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>更新时间（DATETIME DEFAULT GETDATE）</summary>
    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>更新人（NVARCHAR(64)，v2.13.89 改为冗余字段，真源为 UpdatedByUserId FK）</summary>
    [Required]
    [MaxLength(64)]
    [Column("UpdatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// 更新人 UserId FK（v2.13.89 新增：物理真理源 — 关联账号主键）
    /// 页面渲染通过 JOIN SysUser 取 DisplayName 显示
    /// </summary>
    public int? UpdatedByUserId { get; set; }

    /// <summary>更新人姓名（v2.13.89 JOIN 派生：SysUser.DisplayName）</summary>
    [NotMapped]
    public string? UpdatedByDisplayName { get; set; }
}