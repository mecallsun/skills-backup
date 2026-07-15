using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Services;
using DormManage.Shared.Models;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 系统管理 API（P1-11 服务启停）
///
/// 端点：
/// - GET  /api/v1/system/services/status  查询 Api/Admin 状态（通过托盘 IPC）
/// - POST /api/v1/system/services/{name}/{action}  启停重启（action: start/stop/restart）
/// </summary>
[ApiController]
[Route("api/v1/system")]
public class SystemController : ControllerBase
{
    private readonly IpcClient _ipc;
    private readonly ILogger<SystemController> _logger;

    public SystemController(ILogger<SystemController> logger)
    {
        _ipc = new IpcClient();
        _logger = logger;
    }

    [HttpGet("services/status")]
    public async Task<ApiResponse<object>> GetServicesStatus()
    {
        try
        {
            var resp = await _ipc.SendAsync(new ServiceIpc.IpcCommand { Command = "status" }, timeoutMs: 3000);
            return ApiResponse<object>.Ok(resp.Data ?? new { });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "托盘 IPC 不可达");
            return ApiResponse<object>.Fail("IPC_UNREACHABLE", $"托盘不可达：{ex.Message}");
        }
    }

    [HttpPost("services/{name}/{action}")]
    public async Task<ApiResponse> ControlService(string name, string action)
    {
        if (action != "start" && action != "stop" && action != "restart")
            return ApiResponse.Fail("INVALID_ACTION", "action 必须是 start/stop/restart");
        if (name != "api" && name != "admin" && name != "all")
            return ApiResponse.Fail("INVALID_NAME", "name 必须是 api/admin/all");

        try
        {
            var resp = await _ipc.SendAsync(new ServiceIpc.IpcCommand
            {
                Command = action,
                Service = name
            }, timeoutMs: 30000); // 启停可能耗时
            return resp.Success ? ApiResponse.Ok(resp.Message ?? "操作成功") : ApiResponse.Fail("IPC_FAIL", resp.Message ?? "托盘返回失败");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "托盘 IPC 调用失败");
            return ApiResponse.Fail("IPC_UNREACHABLE", $"托盘不可达：{ex.Message}");
        }
    }

    [HttpPost("services/ping")]
    public async Task<ApiResponse> PingTray()
    {
        try
        {
            var resp = await _ipc.SendAsync(new ServiceIpc.IpcCommand { Command = "ping" }, timeoutMs: 2000);
            return ApiResponse.Ok(resp.Message ?? "pong");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail("IPC_UNREACHABLE", $"托盘不可达：{ex.Message}");
        }
    }
}