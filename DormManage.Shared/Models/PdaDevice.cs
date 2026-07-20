using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// PDA 设备档案（v2.13.24 P0-5 修复：与 init_schema.sql [PdaDevice] 表 1:1 对齐）
///
/// SQL 主键为 DeviceId（不是 Id），9 列全部对齐。
/// </summary>
[Table("PdaDevice")]
public class PdaDevice
{
    /// <summary>设备ID（PK）</summary>
    [Key]
    [Column("DeviceId")]
    public int Id { get; set; }

    /// <summary>设备序列号（NVARCHAR(64) UNIQUE）</summary>
    [Required]
    [MaxLength(64)]
    public string DeviceSn { get; set; } = string.Empty;

    /// <summary>设备型号（NVARCHAR(64)）</summary>
    [MaxLength(64)]
    public string? DeviceModel { get; set; }

    /// <summary>绑定用户ID</summary>
    public int BoundUserId { get; set; }

    /// <summary>最后登录时间</summary>
    public DateTime LastLoginAt { get; set; }

    /// <summary>最后登录IP（NVARCHAR(128)）</summary>
    [Required]
    [MaxLength(128)]
    public string LastLoginIp { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>备注（NVARCHAR(512)）</summary>
    [MaxLength(512)]
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}