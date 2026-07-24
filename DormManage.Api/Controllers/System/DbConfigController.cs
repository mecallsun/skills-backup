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
                DbServer = "172.16.0.100",        // v2.13.145
                DbPort = 1433,
                DbName = "WaterMeterDB",
                DbUser = "user",                  // v2.13.145 - SQL 保留关键字
                DbPassword = "1234",              // v2.13.145 - 部署前请改强密码
                Provider = "SqlServer"
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
        // v2.13.109: SQLite Provider 已移除，硬拒绝
        if (!string.Equals(config.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            return ApiResponse.Fail("UNSUPPORTED_PROVIDER", "当前版本仅支持 SQL Server（SQLite 已于 v2.13.109 移除）");

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
        // v2.13.109: SQLite Provider 已移除，硬拒绝
        if (!string.Equals(config.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            return ApiResponse.Fail("UNSUPPORTED_PROVIDER", "当前版本仅支持 SQL Server，请设置 provider=SqlServer");

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
                        ["provider"] = "SqlServer",  // v2.13.109: 强制单 provider
                        ["dbServer"] = config.DbServer,
                        ["dbPort"] = config.DbPort,
                        ["dbName"] = config.DbName,
                        ["dbUser"] = config.DbUser,
                        ["dbPassword"] = config.DbPassword ?? ""
                        // v2.13.109: 移除 sqlitePath IPC 字段
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

    /// <summary>
    /// v2.13.32 运行时重载（手动触发，从文件 + DB 重新加载最新配置）
    /// 通常由 SaveConfig 内部自动调用（AppConfigManager.ApplyExternalConfiguration）；
    /// 本接口用于运维手动热切换场景。
    /// </summary>
    [HttpPost("runtime-reload")]
    public ApiResponse Reload()
    {
        try
        {
            AppConfigRuntime.Instance.Reload();
            return ApiResponse.Ok("运行时配置已重载");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail("RELOAD_FAILED", ex.Message);
        }
    }

    /// <summary>
    /// v2.13.32 运行时连接信息（页头连接健康徽章轮询用）
    /// </summary>
    [HttpGet("runtime-info")]
    public ApiResponse<object> RuntimeInfo()
    {
        try
        {
            var cfg = AppConfigRuntime.Instance.GetCurrent();
            return ApiResponse<object>.Ok(new
            {
                provider = cfg.Provider,
                server = cfg.DbServer,
                database = cfg.DbName,
                // 安全：仅暴露非敏感信息（user/password 永远不返回）
                lastReloadedAt = AppConfigRuntime.Instance.LastReloadedAt
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail("INFO_FAILED", ex.Message);
        }
    }
}
