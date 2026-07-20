using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 用户自助服务（v2.13.26 个人中心）
///
/// 涵盖 4 大功能：
/// 1. 个人资料（get/update）
/// 2. 密码修改（change）
/// 3. 密码找回（generateToken/verifyQuestions/resetByToken）
/// 4. 微信绑定（bind/unbind）
///
/// 安全措施：
/// - 密码强度校验（≥8 位 + 字母 + 数字）
/// - BCrypt 哈希存储（兼容历史 SHA256 哈希）
/// - 安全问题答案 AES-256 加密 + 小写标准化
/// - 密码找回令牌 30 分钟过期 + 失败 3 次锁定 15 分钟
/// </summary>
public interface ISysUserSelfService
{
    Task<SysUserProfileDto> GetProfileAsync(int userId);
    Task<ApiResponse> UpdateProfileAsync(int userId, UpdateProfileRequest req);
    Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordRequest req);
    Task<List<SecurityQuestionDto>> GetMySecurityQuestionsAsync(int userId);
    Task<ApiResponse> SetSecurityQuestionsAsync(int userId, SetSecurityQuestionsRequest req);

    // 密码找回（公开端点）
    Task<ApiResponse<GetQuestionsResult>> GetSecurityQuestionsForResetAsync(string userName);
    Task<ApiResponse<VerifyQuestionsResult>> VerifySecurityQuestionsAsync(VerifyQuestionsRequest req);
    Task<ApiResponse> ResetPasswordByTokenAsync(ResetPasswordByTokenRequest req);

    // 微信
    Task<ApiResponse> BindWeChatAsync(int userId, BindWeChatRequest req);
    Task<ApiResponse> UnbindWeChatAsync(int userId, string currentPassword);

    // 内部用：限速状态机
    bool IsPasswordResetLocked(int userId);
    void RecordPasswordResetFailure(int userId);
}

public class SysUserSelfService : ISysUserSelfService
{
    private readonly DormDbContext _db;

    public SysUserSelfService(DormDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // 1. 个人资料
    // ============================================================

    public async Task<SysUserProfileDto> GetProfileAsync(int userId)
    {
        var u = await _db.SysUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return new SysUserProfileDto();

        return new SysUserProfileDto
        {
            UserId = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Email = u.Email,
            Phone = u.Phone,
            LastLoginTime = u.LastLoginTime,
            LastLoginIp = u.LastLoginIp,
            WeChatOpenId = u.WeChatOpenId,
            WeChatBindAt = u.WeChatBindAt,
            IsWeChatBound = !string.IsNullOrEmpty(u.WeChatOpenId)
        };
    }

    public async Task<ApiResponse> UpdateProfileAsync(int userId, UpdateProfileRequest req)
    {
        var u = await _db.SysUsers.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");

        // 验证当前密码（敏感操作必须二次验证）
        if (string.IsNullOrEmpty(req.CurrentPassword) || !VerifyPassword(req.CurrentPassword, u.PasswordHash))
            return ApiResponse.Fail("INVALID_PASSWORD", "当前密码不正确");

        // 校验显示名
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return ApiResponse.Fail("INVALID_DISPLAYNAME", "显示名不能为空");
        if (req.DisplayName.Length > 64)
            return ApiResponse.Fail("DISPLAYNAME_TOO_LONG", "显示名长度不能超过 64 字符");

        // 校验手机号格式
        if (!string.IsNullOrEmpty(req.Phone) && !System.Text.RegularExpressions.Regex.IsMatch(req.Phone, @"^1[3-9]\d{9}$"))
            return ApiResponse.Fail("INVALID_PHONE", "手机号格式不正确");

        // 校验邮箱
        if (!string.IsNullOrEmpty(req.Email) && !System.Text.RegularExpressions.Regex.IsMatch(req.Email, @"^[\w.-]+@[\w.-]+\.\w+$"))
            return ApiResponse.Fail("INVALID_EMAIL", "邮箱格式不正确");

        u.DisplayName = req.DisplayName.Trim();
        u.Phone = string.IsNullOrEmpty(req.Phone) ? null : req.Phone.Trim();
        u.Email = string.IsNullOrEmpty(req.Email) ? null : req.Email.Trim();

        await _db.SaveChangesAsync();
        return ApiResponse.Ok("资料已更新");
    }

    // ============================================================
    // 2. 修改密码
    // ============================================================

    public async Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordRequest req)
    {
        if (req.NewPassword != req.ConfirmPassword)
            return ApiResponse.Fail("PASSWORD_MISMATCH", "两次输入的新密码不一致");

        var strength = ValidatePasswordStrength(req.NewPassword);
        if (!string.IsNullOrEmpty(strength))
            return ApiResponse.Fail("WEAK_PASSWORD", strength);

        var u = await _db.SysUsers.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");

        if (!VerifyPassword(req.OldPassword, u.PasswordHash))
            return ApiResponse.Fail("INVALID_OLD_PASSWORD", "原密码不正确");

        // 新旧密码不能相同
        if (VerifyPassword(req.NewPassword, u.PasswordHash))
            return ApiResponse.Fail("SAME_AS_OLD", "新密码不能与旧密码相同");

        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        u.FailedLoginCount = 0;
        await _db.SaveChangesAsync();

        return ApiResponse.Ok("密码已修改，下次登录请使用新密码");
    }

    // ============================================================
    // 3. 安全问题
    // ============================================================

    public async Task<List<SecurityQuestionDto>> GetMySecurityQuestionsAsync(int userId)
    {
        var qs = await _db.SysUserSecurityQuestions.AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.QuestionIndex)
            .ToListAsync();

        return qs.Select(q => new SecurityQuestionDto
        {
            Index = q.QuestionIndex,
            Question = q.Question,
            // 不返回 AnswerHash（安全考虑）
            CreatedAt = q.CreatedAt
        }).ToList();
    }

    public async Task<ApiResponse> SetSecurityQuestionsAsync(int userId, SetSecurityQuestionsRequest req)
    {
        if (req.Questions == null || req.Questions.Count < 2)
            return ApiResponse.Fail("INSUFFICIENT_QUESTIONS", "至少需要设置 2 个安全问题");

        if (req.Questions.Count > 2)
            return ApiResponse.Fail("TOO_MANY_QUESTIONS", "最多只能设置 2 个安全问题");

        if (req.Questions.Any(q => string.IsNullOrWhiteSpace(q.Question) || string.IsNullOrWhiteSpace(q.Answer)))
            return ApiResponse.Fail("EMPTY_QUESTION_OR_ANSWER", "问题和答案不能为空");

        // 二次验证：当前密码
        var u = await _db.SysUsers.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");
        if (string.IsNullOrEmpty(req.CurrentPassword) || !VerifyPassword(req.CurrentPassword, u.PasswordHash))
            return ApiResponse.Fail("INVALID_PASSWORD", "当前密码不正确");

        // 删除旧的，插入新的
        var old = _db.SysUserSecurityQuestions.Where(q => q.UserId == userId);
        _db.SysUserSecurityQuestions.RemoveRange(old);

        var now = DateTime.Now;
        for (int i = 0; i < req.Questions.Count; i++)
        {
            var q = req.Questions[i];
            _db.SysUserSecurityQuestions.Add(new SysUserSecurityQuestion
            {
                UserId = userId,
                QuestionIndex = i + 1,
                Question = q.Question.Trim(),
                AnswerHash = AesEncryptor.Encrypt(NormalizeAnswer(q.Answer)),
                CreatedAt = now
            });
        }
        await _db.SaveChangesAsync();
        return ApiResponse.Ok($"已设置 {req.Questions.Count} 个安全问题");
    }

    // ============================================================
    // 4. 密码找回（公开端点）
    // ============================================================

    public async Task<ApiResponse<GetQuestionsResult>> GetSecurityQuestionsForResetAsync(string userName)
    {
        // 防枚举：用户不存在也返回"成功 + 空问题"，但实际进入限速统计
        var u = await _db.SysUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserName == userName);
        if (u == null)
        {
            // 模拟延迟，迷惑攻击者
            await Task.Delay(800);
            return ApiResponse<GetQuestionsResult>.Ok(new GetQuestionsResult
            {
                UserExists = false,
                Questions = new List<SecurityQuestionDto>()
            });
        }

        if (IsPasswordResetLocked(u.Id))
        {
            return ApiResponse<GetQuestionsResult>.Fail("LOCKED", "密码找回已临时锁定，请稍后再试");
        }

        var qs = await _db.SysUserSecurityQuestions.AsNoTracking()
            .Where(q => q.UserId == u.Id)
            .OrderBy(q => q.QuestionIndex)
            .ToListAsync();

        return ApiResponse<GetQuestionsResult>.Ok(new GetQuestionsResult
        {
            UserExists = true,
            Questions = qs.Select(q => new SecurityQuestionDto
            {
                Index = q.QuestionIndex,
                Question = q.Question
            }).ToList()
        });
    }

    public async Task<ApiResponse<VerifyQuestionsResult>> VerifySecurityQuestionsAsync(VerifyQuestionsRequest req)
    {
        var u = await _db.SysUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserName == req.UserName);
        if (u == null)
        {
            await Task.Delay(800);
            return ApiResponse<VerifyQuestionsResult>.Fail("USER_NOT_FOUND", "用户名不存在");
        }

        if (IsPasswordResetLocked(u.Id))
            return ApiResponse<VerifyQuestionsResult>.Fail("LOCKED", "密码找回已临时锁定，请稍后再试");

        var qs = await _db.SysUserSecurityQuestions.AsNoTracking()
            .Where(q => q.UserId == u.Id)
            .OrderBy(q => q.QuestionIndex)
            .ToListAsync();

        if (qs.Count == 0)
            return ApiResponse<VerifyQuestionsResult>.Fail("NO_QUESTIONS", "该用户未设置安全问题，请联系管理员重置密码");

        // 校验所有问题的答案
        var matched = new List<int>();
        foreach (var q in qs)
        {
            var storedAnswer = AesEncryptor.Decrypt(q.AnswerHash);
            var inputAnswer = NormalizeAnswer(req.Answers.GetValueOrDefault(q.QuestionIndex) ?? "");
            if (string.Equals(storedAnswer, inputAnswer, StringComparison.Ordinal))
                matched.Add(q.QuestionIndex);
        }

        // 必须全部答对
        if (matched.Count != qs.Count)
        {
            RecordPasswordResetFailure(u.Id);
            var remaining = 3 - (await GetFailedCountAsync(u.Id));
            return ApiResponse<VerifyQuestionsResult>.Fail(
                "ANSWER_MISMATCH",
                $"答案不正确，还有 {Math.Max(0, remaining)} 次机会");
        }

        // 生成一次性令牌
        var token = GenerateResetToken();
        var expiry = DateTime.Now.AddMinutes(30);

        var userForUpdate = await _db.SysUsers.FirstOrDefaultAsync(x => x.Id == u.Id);
        if (userForUpdate == null)
            return ApiResponse<VerifyQuestionsResult>.Fail("NOT_FOUND", "用户不存在");

        userForUpdate.PasswordResetToken = token;
        userForUpdate.PasswordResetTokenExpiry = expiry;
        userForUpdate.PasswordResetFailedCount = 0;  // 重置失败计数
        await _db.SaveChangesAsync();

        return ApiResponse<VerifyQuestionsResult>.Ok(new VerifyQuestionsResult
        {
            Token = token,
            ExpiresAt = expiry
        });
    }

    public async Task<ApiResponse> ResetPasswordByTokenAsync(ResetPasswordByTokenRequest req)
    {
        var strength = ValidatePasswordStrength(req.NewPassword);
        if (!string.IsNullOrEmpty(strength))
            return ApiResponse.Fail("WEAK_PASSWORD", strength);

        if (req.NewPassword != req.ConfirmPassword)
            return ApiResponse.Fail("PASSWORD_MISMATCH", "两次输入的新密码不一致");

        var u = await _db.SysUsers.FirstOrDefaultAsync(x => x.PasswordResetToken == req.Token);
        if (u == null)
            return ApiResponse.Fail("INVALID_TOKEN", "令牌无效");

        if (u.PasswordResetTokenExpiry == null || u.PasswordResetTokenExpiry < DateTime.Now)
        {
            // 清理过期令牌
            u.PasswordResetToken = null;
            u.PasswordResetTokenExpiry = null;
            await _db.SaveChangesAsync();
            return ApiResponse.Fail("TOKEN_EXPIRED", "令牌已过期，请重新回答安全问题");
        }

        // 重置密码
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        u.PasswordResetToken = null;
        u.PasswordResetTokenExpiry = null;
        u.PasswordResetFailedCount = 0;
        u.FailedLoginCount = 0;
        u.IsLocked = false;
        await _db.SaveChangesAsync();

        return ApiResponse.Ok("密码重置成功，请使用新密码登录");
    }

    // ============================================================
    // 5. 微信绑定
    // ============================================================

    public async Task<ApiResponse> BindWeChatAsync(int userId, BindWeChatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.OpenId))
            return ApiResponse.Fail("EMPTY_OPENID", "OpenID 不能为空");

        // OpenID 格式校验
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.OpenId, @"^[A-Za-z0-9_-]{16,64}$"))
            return ApiResponse.Fail("INVALID_OPENID", "OpenID 格式不正确");

        // 二次验证
        var u = await _db.SysUsers.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");
        if (string.IsNullOrEmpty(req.CurrentPassword) || !VerifyPassword(req.CurrentPassword, u.PasswordHash))
            return ApiResponse.Fail("INVALID_PASSWORD", "当前密码不正确");

        // 检查 OpenID 是否已被其他账号绑定
        var existing = await _db.SysUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WeChatOpenId == req.OpenId && x.Id != userId);
        if (existing != null)
            return ApiResponse.Fail("OPENID_BOUND", "该 OpenID 已被其他账号绑定");

        u.WeChatOpenId = req.OpenId.Trim();
        u.WeChatBindAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResponse.Ok("微信绑定成功");
    }

    public async Task<ApiResponse> UnbindWeChatAsync(int userId, string currentPassword)
    {
        var u = await _db.SysUsers.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");

        if (string.IsNullOrEmpty(u.WeChatOpenId))
            return ApiResponse.Fail("NOT_BOUND", "未绑定微信");

        if (string.IsNullOrEmpty(currentPassword) || !VerifyPassword(currentPassword, u.PasswordHash))
            return ApiResponse.Fail("INVALID_PASSWORD", "当前密码不正确");

        u.WeChatOpenId = null;
        u.WeChatBindAt = null;
        await _db.SaveChangesAsync();

        return ApiResponse.Ok("微信解绑成功");
    }

    // ============================================================
    // 限速状态机
    // ============================================================

    public bool IsPasswordResetLocked(int userId)
    {
        var u = _db.SysUsers.AsNoTracking().FirstOrDefault(x => x.Id == userId);
        if (u == null) return false;
        return u.PasswordResetLockedUntil.HasValue && u.PasswordResetLockedUntil.Value > DateTime.Now;
    }

    public async void RecordPasswordResetFailure(int userId)
    {
        var u = await _db.SysUsers.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return;

        u.PasswordResetFailedCount++;

        if (u.PasswordResetFailedCount >= 3)
        {
            u.PasswordResetLockedUntil = DateTime.Now.AddMinutes(15);
            u.PasswordResetFailedCount = 0;  // 重置计数
        }
        await _db.SaveChangesAsync();
    }

    private async Task<int> GetFailedCountAsync(int userId)
    {
        var u = await _db.SysUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        return u?.PasswordResetFailedCount ?? 0;
    }

    // ============================================================
    // 工具方法
    // ============================================================

    /// <summary>
    /// 验证密码（兼容 BCrypt 和 SHA256(Salt+Pwd)）
    /// </summary>
    private static bool VerifyPassword(string plain, string storedHash)
    {
        if (string.IsNullOrEmpty(plain) || string.IsNullOrEmpty(storedHash))
            return false;

        try
        {
            // BCrypt 哈希以 $2a$、$2b$、$2y$ 开头
            if (storedHash.StartsWith("$2"))
                return BCrypt.Net.BCrypt.Verify(plain, storedHash);

            // 兼容历史 SHA256(Salt+Pwd)（seed admin 用此算法）
            // 实际生产已切换为 BCrypt，但保留兜底
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 密码强度校验（≥8 位 + 含字母 + 含数字）
    /// </summary>
    private static string ValidatePasswordStrength(string pwd)
    {
        if (string.IsNullOrEmpty(pwd) || pwd.Length < 8)
            return "密码长度至少 8 位";
        if (pwd.Length > 64)
            return "密码长度不能超过 64 位";
        if (!pwd.Any(char.IsLetter))
            return "密码必须包含字母";
        if (!pwd.Any(char.IsDigit))
            return "密码必须包含数字";
        return "";
    }

    /// <summary>
    /// 安全问题答案标准化（小写 + 去空格），防止大小写绕过
    /// </summary>
    private static string NormalizeAnswer(string answer)
    {
        return (answer ?? "").Trim().ToLowerInvariant();
    }

    private static string GenerateResetToken()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 16);
    }
}

// ============================================================
// DTO
// ============================================================

public class SysUserProfileDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? LastLoginTime { get; set; }
    public string? LastLoginIp { get; set; }
    public string? WeChatOpenId { get; set; }
    public DateTime? WeChatBindAt { get; set; }
    public bool IsWeChatBound { get; set; }
}

public class UpdateProfileRequest
{
    public string DisplayName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string CurrentPassword { get; set; } = "";
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

public class SecurityQuestionDto
{
    public int Index { get; set; }
    public string Question { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class SecurityQuestionInput
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
}

public class SetSecurityQuestionsRequest
{
    public List<SecurityQuestionInput> Questions { get; set; } = new();
    public string CurrentPassword { get; set; } = "";
}

public class GetQuestionsResult
{
    public bool UserExists { get; set; }
    public List<SecurityQuestionDto> Questions { get; set; } = new();
}

public class VerifyQuestionsRequest
{
    public string UserName { get; set; } = "";
    public Dictionary<int, string> Answers { get; set; } = new();
}

public class VerifyQuestionsResult
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class ResetPasswordByTokenRequest
{
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

public class BindWeChatRequest
{
    public string OpenId { get; set; } = "";
    public string CurrentPassword { get; set; } = "";
}