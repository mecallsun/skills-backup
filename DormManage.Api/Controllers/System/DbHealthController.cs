using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Services;
using DormManage.Shared.Models;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 数据库深度验证 API（P1-10）
///
/// 端点：
/// - GET  /api/v1/system/dbhealth/quick   快速连接测试
/// - POST /api/v1/system/dbhealth/deep    深度验证（8 步）
/// </summary>
[ApiController]
[Route("api/v1/system/dbhealth")]
public class DbHealthController : ControllerBase
{
    private readonly IDatabaseHealthService _service;

    public DbHealthController(IDatabaseHealthService service)
    {
        _service = service;
    }

    [HttpGet("quick")]
    public async Task<ApiResponse<bool>> Quick()
    {
        var ok = await _service.QuickCheckAsync();
        return ApiResponse<bool>.Ok(ok, ok ? "数据库连接正常" : "连接失败");
    }

    [HttpPost("deep")]
    public async Task<ApiResponse<DatabaseHealthReport>> Deep()
    {
        var report = await _service.RunDeepCheckAsync();
        return ApiResponse<DatabaseHealthReport>.Ok(report,
            report.OverallPassed ? "深度验证通过" : "存在失败步骤，请查看详情");
    }
}