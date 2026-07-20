using DormManage.Shared.Data;
using DormManage.Shared.Extensions;
using DormManage.Shared.Models;
using Microsoft.AspNetCore.Http;

namespace DormManage.Shared.Services;

/// <summary>
/// 统一操作日志服务（v2.13.29 新增）
/// 自动从 HttpContext 提取用户、IP、User-Agent 等上下文信息
/// </summary>
public interface IOperationLogService
{
    /// <summary>记录操作日志</summary>
    Task LogAsync(string action, string target, string detail = "");

    /// <summary>记录登录日志</summary>
    Task LogLoginAsync(string username, bool success, string detail = "");

    /// <summary>记录登出日志</summary>
    Task LogLogoutAsync(string username);

    /// <summary>记录配置变更</summary>
    Task LogConfigChangeAsync(string configKey, string oldValue, string newValue);

    /// <summary>记录数据变更（CRUD）</summary>
    Task LogDataChangeAsync(string entityType, string entityId, string operation, string detail = "");
}

public class OperationLogService : IOperationLogService
{
    private readonly DormDbContext _db;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public OperationLogService(DormDbContext db, IHttpContextAccessor? httpContextAccessor = null)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string target, string detail = "")
    {
        var (userId, username) = GetCurrentUser();
        var ip = GetClientIp();

        _db.SysOpLogs.Add(new SysOpLog
        {
            UserId = userId,
            Username = username,
            Action = action,
            Target = target,
            Detail = detail,
            Ip = ip,
            CreatedAt = DateTime.Now
        });

        try { await _db.SaveChangesAsync(); }
        catch { /* 日志写入失败不应阻塞主业务 */ }
    }

    public async Task LogLoginAsync(string username, bool success, string detail = "")
    {
        await LogAsync(
            success ? "LOGIN_SUCCESS" : "LOGIN_FAILED",
            username,
            detail);
    }

    public async Task LogLogoutAsync(string username)
    {
        await LogAsync("LOGOUT", username, "");
    }

    public async Task LogConfigChangeAsync(string configKey, string oldValue, string newValue)
    {
        await LogAsync(
            "CONFIG_CHANGED",
            configKey,
            $"old={Truncate(oldValue, 200)}; new={Truncate(newValue, 200)}");
    }

    public async Task LogDataChangeAsync(string entityType, string entityId, string operation, string detail = "")
    {
        await LogAsync(
            $"DATA_{operation.ToUpperInvariant()}",
            $"{entityType}#{entityId}",
            detail);
    }

    private (int UserId, string Username) GetCurrentUser()
    {
        var ctx = _httpContextAccessor?.HttpContext;
        if (ctx == null) return (0, "system");

        var userId = ctx.GetCurrentUserId();
        var username = ctx.GetCurrentUserName();
        return (userId, username);
    }

    private string GetClientIp()
    {
        var ctx = _httpContextAccessor?.HttpContext;
        if (ctx == null) return "";

        // 优先 X-Forwarded-For（反向代理场景）
        var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xff))
        {
            var firstIp = xff.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp)) return firstIp;
        }

        // X-Real-IP（Nginx 反向代理）
        var xri = ctx.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xri)) return xri;

        // 直接连接 IP
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLen ? value : value.Substring(0, maxLen) + "...";
    }
}