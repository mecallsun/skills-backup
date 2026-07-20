using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 数据库连接配置 API 控制器（v2.13.19 双 UI 双向同步）
/// 替代原 Settings/Index Post-handler 的 TODO stub
/// </summary>
[ApiController]
[Route("api/v1/system/dbconfig")]
public class DbConfigController : ControllerBase
{
    /// <summary>
    /// 获取当前数据库连接配置（密码脱敏显示）
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<DatabaseConfigDto>> GetConfig()
    {
        var config = await AppConfigManager.Instance.LoadAsync(() =>
        {
            // 默认回退：从 appsettings.json 读取当前环境配置
            return new DatabaseConfigDto
            {
                DbServer = "192.168.1.237",
                DbPort = 1433,
                DbName = "WaterMeterDB",
                DbUser = "__DB_USER__",
                DbPassword = "__DB_PASSWORD__",
                Provider = "SqlServer",
                SqlitePath = ""
            };
        });

        if (config == null)
            return ApiResponse<DatabaseConfigDto>.Fail("NOT_FOUND", "数据库配置不存在");

        // 密码脱敏返回
        if (!string.IsNullOrEmpty(config.DbPassword))
            config.DbPassword = "******";

        return ApiResponse<DatabaseConfigDto>.Ok(config);
    }

    /// <summary>
    /// 测试数据库连接（不写入）
    /// </summary>
    [HttpPost("test")]
    public async Task<ApiResponse> TestConnection([FromBody] DatabaseConfigDto config)
    {
        var (ok, msg) = await AppConfigManager.Instance.TestDbConnectionAsync(config);
        return ok
            ? ApiResponse.Ok(msg)
            : ApiResponse.Fail("TEST_FAILED", msg);
    }

    /// <summary>
    /// 保存数据库连接配置（写入文件 + DB + 广播）
    /// </summary>
    [HttpPost("save")]
    public async Task<ApiResponse> SaveConfig([FromBody] DatabaseConfigDto config)
    {
        // v2.13.19：前端可能发送 "unchanged" 密码哨兵，已由 AppConfigManager 处理

        // 安全卡口：必须先测试连接（防止保存无效参数）
        var (ok, msg) = await AppConfigManager.Instance.TestDbConnectionAsync(config);
        if (!ok)
            return ApiResponse.Fail("CONN_FAILED", $"连通性校验失败：{msg}");

        // 保存配置（双擎持久化）
        var (saveOk, saveMsg) = await AppConfigManager.Instance.SaveConfigurationAsync(config);
        if (!saveOk)
            return ApiResponse.Fail("SAVE_FAILED", saveMsg);

        // v2.13.19：兜底同步，主动通过 IPC 通知 TrayApp
        _ = Task.Run(async () =>
        {
            try
            {
                var ipc = new IpcClient();
                var resp = await ipc.SendAsync(new ServiceIpc.IpcCommand
                {
                    Command = "dbconfig.updated",
                    Payload = new Dictionary<string, object?>
                    {
                        ["provider"] = config.Provider,
                        ["dbServer"] = config.DbServer,
                        ["dbPort"] = config.DbPort,
                        ["dbName"] = config.DbName,
                        ["dbUser"] = config.DbUser,
                        ["dbPassword"] = config.DbPassword ?? "",
                        ["sqlitePath"] = config.SqlitePath ?? ""
                    }
                }, 5000);
                // 仅记录，不阻塞 HTTP 响应
                Console.WriteLine($"[DbConfigController] IPC dbconfig.updated: success={resp.Success}, msg={resp.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbConfigController] IPC dbconfig.updated 失败: {ex.Message}");
            }
        });

        return ApiResponse.Ok(saveMsg);
    }

    /// <summary>
    /// 8 步深度验证（同 DashboardService 类似）
    /// </summary>
    [HttpPost("deep-test")]
    public async Task<ApiResponse<DatabaseHealthReport>> DeepTest([FromBody] DatabaseConfigDto config)
    {
        try
        {
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DormManage.Shared.Data.DormDbContext>()
                .UseSqlServer(config.BuildConnectionString())
                .Options;
            var healthService = new DatabaseHealthService(new DormManage.Shared.Data.DormDbContext(options));
            var report = await healthService.RunDeepCheckAsync();
            return ApiResponse<DatabaseHealthReport>.Ok(report);
        }
        catch (Exception ex)
        {
            return ApiResponse<DatabaseHealthReport>.Fail("DEEP_TEST_FAILED", ex.Message);
        }
    }
}
