using System;
using System.Text.Json;
using System.Threading.Tasks;
using DormManage.Shared.Security;
using Microsoft.AspNetCore.Http;

namespace DormManage.Api.Middleware;

/// <summary>
/// v2.13.136 全局只读中间件（Api 端）
///
/// 业务规则：注册失败/过期时，所有 POST/PUT/DELETE/PATCH 请求返回 403 JSON。
///
/// 与 Admin 端的差异：
/// 1. 所有响应统一 JSON（无 Razor Page POST → 重定向场景）
/// 2. PDA 端点（/api/v1/pda/*）放行 —— PdaController.Upload 自带 CheckReg 校验
///    （v2.13.135 引入，避免双层拦截让 PDA 无法上传）
/// 3. 健康检查（/api/v1/system/dbhealth/*）放行 —— 托盘需要轮询
/// 4. Swagger UI 放行 —— 运维诊断需要
/// </summary>
public class LicenseReadOnlyMiddleware
{
    private readonly RequestDelegate _next;

    public LicenseReadOnlyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // 1) 检测注册状态
        var isReadOnly = LicenseGuard.IsReadOnly();
        context.Items["__LICENSE_READONLY__"] = isReadOnly;

        if (!isReadOnly)
        {
            // 注册有效 → 放行
            await _next(context);
            return;
        }

        // 2) 放行 GET / HEAD
        var method = context.Request.Method;
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 3) 放行白名单路径
        var path = context.Request.Path.Value ?? "";
        if (IsApiWhitelisted(path))
        {
            await _next(context);
            return;
        }

        // 4) 拒绝写入请求 → 统一 403 JSON
        await RejectWrite(context);
    }

    /// <summary>
    /// 拒绝写入请求 —— 统一 403 JSON
    /// </summary>
    private static async Task RejectWrite(HttpContext context)
    {
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            success = false,
            code = "LICENSE_READONLY",
            message = "软件未注册或注册已过期，所有修改类操作已禁用。请联系信息科进行注册。"
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// Api 端白名单路径判定
    /// </summary>
    private static bool IsApiWhitelisted(string path)
    {
        // PDA 端点 —— PdaController.Upload 自带 CheckReg 校验
        // v2.13.135 设计原则：PDA 端点必须独立可验证（不能因为 Web 没注册就阻断 PDA）
        if (path.StartsWith("/api/v1/pda/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 健康检查（托盘 30s 轮询）
        if (path.StartsWith("/api/v1/system/dbhealth/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/system/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Swagger UI / OpenAPI 文档（运维诊断）
        if (path.Equals("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".swagger.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // ApiVersion 元数据
        if (path.StartsWith("/api/v1/appversion", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}