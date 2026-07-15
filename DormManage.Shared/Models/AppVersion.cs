using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormManage.Shared.Models;

/// <summary>
/// PDA App 版本（P2-7）
/// </summary>
[Table("AppVersion")]
public class AppVersion : BaseEntity
{
    /// <summary>版本号（如 1.0.3）</summary>
    [Required, MaxLength(20)]
    public string Version { get; set; } = "";

    /// <summary>APK 文件名（含扩展名）</summary>
    [MaxLength(200)]
    public string? FileName { get; set; }

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>发布说明</summary>
    [MaxLength(1000)]
    public string? ReleaseNotes { get; set; }

    /// <summary>是否为最新版本</summary>
    public bool IsLatest { get; set; }

    /// <summary>是否启用（PDA 可下载）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>强制升级（低于此版本必须升级）</summary>
    public bool IsForceUpdate { get; set; }

    /// <summary>最低兼容版本（低于此版本不可用）</summary>
    [MaxLength(20)]
    public string? MinCompatibleVersion { get; set; }

    /// <summary>MD5 校验值</summary>
    [MaxLength(64)]
    public string? Md5 { get; set; }

    /// <summary>发布时间</summary>
    public DateTime ReleaseDate { get; set; } = DateTime.Now;
}