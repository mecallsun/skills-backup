using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// 抄表图片附件（v2.13.24 P0-6 修复：与 init_schema.sql [MeterImage] 表 1:1 对齐）
///
/// SQL 主键为 BIGINT ImageId，FK RecordId 引用 MeterRecord.RecordId。
/// </summary>
[Table("MeterImage")]
public class MeterImage
{
    /// <summary>图片ID（PK BIGINT）</summary>
    [Key]
    [Column("ImageId")]
    public long Id { get; set; }

    /// <summary>智能抄表ID（FK → MeterRecord.RecordId，BIGINT）</summary>
    public long RecordId { get; set; }

    /// <summary>表类型（cold/hot/electric）</summary>
    [Required]
    [MaxLength(16)]
    public string MeterType { get; set; } = string.Empty;

    /// <summary>相对路径（NVARCHAR(512)）</summary>
    [Required]
    [MaxLength(512)]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>绝对路径（NVARCHAR(512)）</summary>
    [MaxLength(512)]
    public string? AbsolutePath { get; set; }

    /// <summary>文件名（NVARCHAR(128)）</summary>
    [Required]
    [MaxLength(128)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>文件大小（字节）</summary>
    public int FileSize { get; set; }

    /// <summary>文件哈希（NVARCHAR(64)）</summary>
    [Required]
    [MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    /// <summary>图片宽度</summary>
    public int Width { get; set; }

    /// <summary>图片高度</summary>
    public int Height { get; set; }

    /// <summary>上传时间</summary>
    public DateTime UploadedAt { get; set; } = DateTime.Now;

    // 导航属性
    public MeterRecord? Record { get; set; }
}