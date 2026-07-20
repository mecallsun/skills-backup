using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.Shared.Extensions;

namespace DormManage.Api.Controllers.Account;

/// <summary>
/// 用户自助服务 API（v2.13.26 个人中心与账号安全）
///
/// 路由：/api/v1/account
///
/// 端点分类：
/// - 已登录用户（从 Claims 读取 userId）：
///   GET/PUT /profile
///   POST /change-password
///   GET/POST /security-questions
///   POST /wechat/bind, /wechat/unbind
/// - 公开端点（密码找回流程）：
///   POST /forgot/get-questions
///   POST /forgot/verify
///   POST /forgot/reset
/// </summary>
[ApiController]
[Route("api/v1/account")]
public class SelfController : ControllerBase
{
    private readonly ISysUserSelfService _service;

    public SelfController(ISysUserSelfService service)
    {
        _service = service;
    }

    // ============================================================
    // 已登录用户端点
    // ============================================================

    [HttpGet("profile")]
    public async Task<ApiResponse<SysUserProfileDto>> GetProfile()
    {
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return ApiResponse<SysUserProfileDto>.Fail("UNAUTHORIZED", "请先登录");
        var p = await _service.GetProfileAsync(userId);
        return ApiResponse<SysUserProfileDto>.Ok(p);
    }

    [HttpPut("profile")]
    public async Task<ApiResponse> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return ApiResponse.Fail("UNAUTHORIZED", "请先登录");
        return await _service.UpdateProfileAsync(userId, req);
    }

    [HttpPost("change-password")]
    public async Task<ApiResponse> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return ApiResponse.Fail("UNAUTHORIZED", "请先登录");
        return await _service.ChangePasswordAsync(userId, req);
    }

    [HttpGet("security-questions")]
    public async Task<ApiResponse<List<SecurityQuestionDto>>> GetSecurityQuestions()
    {
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return ApiResponse<List<SecurityQuestionDto>>.Fail("UNAUTHORIZED", "请先登录");
        var qs = await _service.GetMySecurityQuestionsAsync(userId);
        return ApiResponse<List<SecurityQuestionDto>>.Ok(qs);
    }

    [HttpPost("security-questions")]
    public async Task<ApiResponse> SetSecurityQuestions([FromBody] SetSecurityQuestionsRequest req)
    {
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return ApiResponse.Fail("UNAUTHORIZED", "请先登录");
        return await _service.SetSecurityQuestionsAsync(userId, req);
    }

    [HttpPost("wechat/bind")]
    public async Task<ApiResponse> BindWeChat([FromBody] BindWeChatRequest req)
    {
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return ApiResponse.Fail("UNAUTHORIZED", "请先登录");
        return await _service.BindWeChatAsync(userId, req);
    }

    [HttpPost("wechat/unbind")]
    public async Task<ApiResponse> UnbindWeChat([FromBody] UnbindWeChatRequest req)
    {
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return ApiResponse.Fail("UNAUTHORIZED", "请先登录");
        return await _service.UnbindWeChatAsync(userId, req.CurrentPassword ?? "");
    }

    // ============================================================
    // 公开端点（密码找回）
    // ============================================================

    [HttpPost("forgot/get-questions")]
    public async Task<ApiResponse<GetQuestionsResult>> GetQuestionsForReset([FromBody] ForgotGetQuestionsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.UserName))
            return ApiResponse<GetQuestionsResult>.Fail("EMPTY_USERNAME", "用户名不能为空");
        return await _service.GetSecurityQuestionsForResetAsync(req.UserName.Trim());
    }

    [HttpPost("forgot/verify")]
    public async Task<ApiResponse<VerifyQuestionsResult>> VerifyQuestions([FromBody] VerifyQuestionsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.UserName) || req.Answers == null || req.Answers.Count == 0)
            return ApiResponse<VerifyQuestionsResult>.Fail("INVALID_INPUT", "请求参数不完整");
        return await _service.VerifySecurityQuestionsAsync(req);
    }

    [HttpPost("forgot/reset")]
    public async Task<ApiResponse> ResetPassword([FromBody] ResetPasswordByTokenRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Token))
            return ApiResponse.Fail("EMPTY_TOKEN", "令牌不能为空");
        return await _service.ResetPasswordByTokenAsync(req);
    }
}

public class UnbindWeChatRequest
{
    public string CurrentPassword { get; set; } = "";
}

public class ForgotGetQuestionsRequest
{
    public string UserName { get; set; } = "";
}