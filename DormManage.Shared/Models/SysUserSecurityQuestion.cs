using System.ComponentModel.DataAnnotations;

namespace DormManage.Shared.Models;

/// <summary>
/// 用户安全问题（v2.13.26 密码找回）
///
/// 每位用户最多 2 个安全问题 + 答案密文
/// 答案使用 AES-256 加密存储（可解密比对，因为密码找回需要）
///
/// 字段：
/// - UserId：所属用户 FK
/// - QuestionIndex：序号 1 或 2
/// - Question：问题文本（如"您的出生城市是？"）
/// - AnswerHash：AES-256 加密后的答案密文
/// </summary>
public class SysUserSecurityQuestion : BaseEntity
{
    /// <summary>所属用户 ID（FK → SysUser.Id）</summary>
    public int UserId { get; set; }

    /// <summary>问题序号（1 或 2，每用户唯一）</summary>
    public int QuestionIndex { get; set; }

    /// <summary>问题文本（最长 200 字）</summary>
    [Required]
    [MaxLength(200)]
    public string Question { get; set; } = string.Empty;

    /// <summary>答案密文（AES-256 加密存储）</summary>
    [Required]
    [MaxLength(500)]
    public string AnswerHash { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public new DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public new DateTime? UpdatedAt { get; set; }
}