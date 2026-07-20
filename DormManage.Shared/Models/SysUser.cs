using System.ComponentModel.DataAnnotations;

namespace DormManage.Shared.Models;
public class SysUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LastLoginTime { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ===== v2.13.26 个人中心与账号安全 =====
    /// <summary>微信 OpenID（企业内部应用绑定唯一标识）</summary>
    [MaxLength(64)]
    public string? WeChatOpenId { get; set; }

    /// <summary>微信绑定时间</summary>
    public DateTime? WeChatBindAt { get; set; }

    /// <summary>密码找回令牌（密码找回流程使用，30 分钟过期）</summary>
    [MaxLength(128)]
    public string? PasswordResetToken { get; set; }

    /// <summary>密码找回令牌过期时间</summary>
    public DateTime? PasswordResetTokenExpiry { get; set; }

    /// <summary>密码找回失败次数（连续 3 次锁定 15 分钟）</summary>
    public int PasswordResetFailedCount { get; set; }

    /// <summary>密码找回临时锁定到期时间</summary>
    public DateTime? PasswordResetLockedUntil { get; set; }
}

public class SysRole
{
    public int Id { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class SysUserRole
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
}