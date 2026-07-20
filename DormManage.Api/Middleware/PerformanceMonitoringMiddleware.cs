using System.Diagnostics;

namespace DormManage.Api.Middleware;

/// <summary>
/// API 性能监控中间件（v2.13.29 新增）
/// - 记录所有 API 请求的响应时间
/// - 慢请求（> 3s）记录到警告日志
/// - 错误请求（5xx）记录到错误日志
/// - 在响应头中返回耗时（X-Response-Time-Ms）
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _log;

    /// <summary>慢请求阈值（毫秒）</summary>
    private const int SlowRequestThresholdMs = 3000;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMonitoringMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 仅监控 /api/ 开头的请求
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        Exception? caughtException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            caughtException = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;
            var path = context.Request.Path.Value ?? "";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "";

            // 响应头：返回耗时（便于前端排查）
            if (!context.Response.HasStarted)
            {
                context.Response.Headers["X-Response-Time-Ms"] = elapsedMs.ToString();
            }

            // 慢请求：警告日志
            if (elapsedMs > SlowRequestThresholdMs)
            {
                _log.LogWarning(
                    "[SLOW-API] {Method} {Path} → {StatusCode} ({ElapsedMs}ms) from {Ip}",
                    method, path, statusCode, elapsedMs, ip);
            }
            else
            {
                _log.LogInformation(
                    "[API] {Method} {Path} → {StatusCode} ({ElapsedMs}ms)",
                    method, path, statusCode, elapsedMs);
            }

            // 错误请求：错误日志
            if (statusCode >= 500 || caughtException != null)
            {
                _log.LogError(caughtException,
                    "[API-ERROR] {Method} {Path} → {StatusCode} ({ElapsedMs}ms)",
                    method, path, statusCode, elapsedMs);
            }
        }
    }
}