using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Security;
using DormManage.Shared.Models;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// v2.13.169 注册状态查询 API（供前端 license-status-badge.js 30s 轮询）
///
/// 端点：
/// - GET /api/v1/system/license-status  返回 { status, regInt, regDate, ltdName, isReadOnly, isTrial, level, message }
///   status 取值：-1=未注册 1=有效 2=已过期 3=校验失败（对应 RegStatus 枚举）
///   level 取值：success / info / warning / danger（用于徽章样式）
///
/// 注意：本端点不放在只读中间件拦截列表中（GET 永远放行 + IsApiWhitelisted 自动放行只读场景）。
/// </summary>
[ApiController]
[Route("api/v1/system")]
public class LicenseStatusController : ControllerBase
{
    /// <summary>
    /// 获取当前注册状态摘要（无授权中间件强制）
    /// </summary>
    [HttpGet("license-status")]
    public IActionResult GetLicenseStatus()
    {
        // v2.13.169：从 LicenseGuard 提取状态（IPC 30s 缓存）
        var state = LicenseGuard.GetCachedState();
        var (code, message, level) = LicenseGuard.GetLicenseBanner();

        object payload = state is null
            ? new
            {
                status = -2,  // 自定义：授权服务不可用
                regInt = 0,
                regDate = (DateTime?)null,
                ltdName = "",
                isReadOnly = true,
                isTrial = false,
                code,
                message,
                level
            }
            : new
            {
                status = state.RegStatus,    // -1/1/2/3
                regInt = state.RegInt,        // 兼容（-1/0/1）
                regDate = state.RegDate,
                ltdName = state.LTDName,
                isReadOnly = LicenseGuard.IsReadOnly(),
                isTrial = LicenseGuard.IsTrialMode(),
                code,
                message,
                level
            };

        return Ok(ApiResponse<object>.Ok(payload));
    }
}
