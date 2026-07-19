using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Services;
using DormManage.Shared.Models;
using System.Security.Claims;

namespace DormManage.Api.Controllers.User;

/// <summary>
/// 用户筛选条件云端缓存 API（v2.13.12）
///
/// 端点：
///   GET    /api/v1/user/filter-cache?module=personnel  — 读取指定模块的筛选缓存
///   GET    /api/v1/user/filter-cache/list              — 列出当前用户所有模块的缓存
///   POST   /api/v1/user/filter-cache/save              — 保存（body: {module, filter}）
///   DELETE /api/v1/user/filter-cache?module=xxx        — 清除指定模块缓存
///   DELETE /api/v1/user/filter-cache/all               — 清除当前用户所有模块缓存
/// </summary>
[ApiController]
[Route("api/v1/user/filter-cache")]
[Authorize]
public class FilterCacheController : ControllerBase
{
    private readonly ISysUserFilterCacheService _svc;
    private readonly IHttpContextAccessor _http;

    public FilterCacheController(ISysUserFilterCacheService svc, IHttpContextAccessor http)
    {
        _svc = svc;
        _http = http;
    }

    private int? GetUserId()
    {
        var ctx = _http.HttpContext;
        if (ctx == null) return null;
        var c = ctx.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (c != null && int.TryParse(c.Value, out var id) && id > 0) return id;
        return null;
    }

    /// <summary>读取指定模块的筛选缓存</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string module)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(ApiResponse.Fail("未登录", "401"));
        if (string.IsNullOrWhiteSpace(module))
            return BadRequest(ApiResponse.Fail("module 参数必填", "400"));

        var data = await _svc.GetCacheAsync(userId.Value, module);
        return Ok(ApiResponse<Dictionary<string, object>>.Ok(data));
    }

    /// <summary>列出所有模块的筛选缓存</summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListAll()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(ApiResponse.Fail("未登录", "401"));

        var list = await _svc.ListAllAsync(userId.Value);
        return Ok(ApiResponse<List<FilterCacheSummary>>.Ok(list));
    }

    /// <summary>保存筛选条件</summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveFilterCacheRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(ApiResponse.Fail("未登录", "401"));
        if (req == null || string.IsNullOrWhiteSpace(req.Module))
            return BadRequest(ApiResponse.Fail("请求体缺少 module 字段", "400"));

        await _svc.SaveCacheAsync(userId.Value, req.Module, req.Filter ?? new Dictionary<string, object>());
        return Ok(ApiResponse.Ok("保存成功"));
    }

    /// <summary>清除指定模块的筛选缓存</summary>
    [HttpDelete]
    public async Task<IActionResult> Reset([FromQuery] string module)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(ApiResponse.Fail("未登录", "401"));
        if (string.IsNullOrWhiteSpace(module))
            return BadRequest(ApiResponse.Fail("module 参数必填", "400"));

        await _svc.ResetAsync(userId.Value, module);
        return Ok(ApiResponse.Ok("已清除"));
    }

    /// <summary>清除所有模块的筛选缓存</summary>
    [HttpDelete("all")]
    public async Task<IActionResult> ResetAll()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(ApiResponse.Fail("未登录", "401"));

        await _svc.ResetAllAsync(userId.Value);
        return Ok(ApiResponse.Ok("已清除所有筛选缓存"));
    }
}

/// <summary>保存筛选请求体</summary>
public class SaveFilterCacheRequest
{
    /// <summary>模块标识</summary>
    public string Module { get; set; } = "";
    /// <summary>筛选条件字典（与前端 collectFromForm 输出格式一致）</summary>
    public Dictionary<string, object> Filter { get; set; } = new();
}