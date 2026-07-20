using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using DormManage.Shared.Exceptions;

namespace DormManage.Admin.Filters;

/// <summary>
/// Razor Pages 全局异常过滤器（v2.13.29 新增）
/// 捕获所有未处理异常，统一重定向到错误页或返回友好提示
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _log;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> log, IWebHostEnvironment env)
    {
        _log = log;
        _env = env;
    }

    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case BusinessException bex:
                _log.LogWarning($"[业务异常] {bex.ErrorCode}: {bex.Message}");
                context.Result = new RedirectToPageResult("/Error", new
                {
                    code = bex.ErrorCode,
                    message = bex.Message
                });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException uex:
                _log.LogWarning($"[权限异常] {uex.Message}");
                context.Result = new RedirectToPageResult("/Account/Login");
                context.ExceptionHandled = true;
                break;

            case Microsoft.EntityFrameworkCore.DbUpdateException dex:
                _log.LogError(dex, "[数据库异常]");
                var dbMsg = _env.IsDevelopment()
                    ? (dex.InnerException?.Message ?? dex.Message)
                    : "数据库操作失败";
                context.Result = new RedirectToPageResult("/Error", new
                {
                    code = "DB_ERROR",
                    message = dbMsg
                });
                context.ExceptionHandled = true;
                break;

            default:
                _log.LogError(context.Exception, "[未处理异常]");
                var msg = _env.IsDevelopment()
                    ? context.Exception.Message
                    : "页面执行出错，请稍后重试";
                context.Result = new RedirectToPageResult("/Error", new
                {
                    code = "INTERNAL_ERROR",
                    message = msg
                });
                context.ExceptionHandled = true;
                break;
        }
    }
}