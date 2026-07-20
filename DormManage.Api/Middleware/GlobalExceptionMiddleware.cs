using System.Net;
using System.Text.Json;
using DormManage.Shared.Exceptions;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Api.Middleware;

/// <summary>
/// 全局异常处理中间件（v2.13.29 新增）
/// - 捕获所有未处理异常
/// - 区分业务异常 vs 系统异常
/// - 返回统一的 ApiResponse 格式
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _log;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> log,
        IWebHostEnvironment env)
    {
        _next = next;
        _log = log;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException bex)
        {
            // 业务异常：返回 200 + 错误码（前端可正常处理）
            _log.LogWarning($"[业务异常] {bex.ErrorCode}: {bex.Message}");
            await WriteBusinessError(context, bex);
        }
        catch (UnauthorizedAccessException uex)
        {
            _log.LogWarning($"[权限异常] {uex.Message}");
            await WriteError(context, HttpStatusCode.Unauthorized, "UNAUTHORIZED", uex.Message);
        }
        catch (KeyNotFoundException kex)
        {
            _log.LogWarning($"[资源不存在] {kex.Message}");
            await WriteError(context, HttpStatusCode.NotFound, "NOT_FOUND", kex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dex)
        {
            _log.LogError(dex, "[数据库异常]");
            await WriteError(context, HttpStatusCode.InternalServerError, "DB_ERROR",
                _env.IsDevelopment() ? (dex.InnerException?.Message ?? dex.Message) : "数据库操作失败");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[未处理异常]");
            await WriteError(context, HttpStatusCode.InternalServerError, "INTERNAL_ERROR",
                _env.IsDevelopment() ? ex.Message : "服务器内部错误，请稍后重试");
        }
    }

    private static async Task WriteBusinessError(HttpContext ctx, BusinessException bex)
    {
        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
        ctx.Response.ContentType = "application/json; charset=utf-8";

        var response = ApiResponse.Fail(bex.ErrorCode, bex.Message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await ctx.Response.WriteAsync(json);
    }

    private static async Task WriteError(HttpContext ctx, HttpStatusCode status, string code, string message)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json; charset=utf-8";

        var response = ApiResponse.Fail(code, message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await ctx.Response.WriteAsync(json);
    }
}