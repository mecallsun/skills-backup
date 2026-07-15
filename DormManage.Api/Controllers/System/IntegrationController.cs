using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 系统集成 API（P2-6）
///
/// 端点：
/// - GET    /api/v1/system/integration/list          集成配置列表
/// - GET    /api/v1/system/integration/{id}          详情
/// - POST   /api/v1/system/integration               创建
/// - PUT    /api/v1/system/integration/{id}          更新
/// - DELETE /api/v1/system/integration/{id}          删除
/// - POST   /api/v1/system/integration/{id}/test     测试连接
/// - POST   /api/v1/system/integration/{id}/sync     触发同步
/// </summary>
[ApiController]
[Route("api/v1/system/integration")]
public class IntegrationController : ControllerBase
{
    private readonly DormDbContext _db;
    private readonly IHttpClientFactory _httpFactory;

    public IntegrationController(DormDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
    }

    [HttpGet("list")]
    public async Task<ApiResponse<List<SysIntegration>>> List()
    {
        var list = await _db.SysIntegrations.OrderBy(i => i.SystemCode).ToListAsync();
        // 隐藏密码字段
        list.ForEach(i => i.Password = MaskPassword(i.Password));
        return ApiResponse<List<SysIntegration>>.Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ApiResponse<SysIntegration>> GetById(int id)
    {
        var entity = await _db.SysIntegrations.FindAsync(id);
        if (entity is null) return ApiResponse<SysIntegration>.Fail("NOT_FOUND", "集成配置不存在");
        entity.Password = MaskPassword(entity.Password);
        return ApiResponse<SysIntegration>.Ok(entity);
    }

    [HttpPost]
    public async Task<ApiResponse<SysIntegration>> Create([FromBody] SysIntegration model)
    {
        if (string.IsNullOrWhiteSpace(model.SystemCode) || string.IsNullOrWhiteSpace(model.SystemName))
            return ApiResponse<SysIntegration>.Fail("INVALID_INPUT", "系统编码和名称必填");
        if (await _db.SysIntegrations.AnyAsync(i => i.SystemCode == model.SystemCode))
            return ApiResponse<SysIntegration>.Fail("DUPLICATE_CODE", $"系统编码 {model.SystemCode} 已存在");

        model.CreatedAt = DateTime.Now;
        _db.SysIntegrations.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<SysIntegration>.Ok(model, "创建成功");
    }

    [HttpPut("{id:int}")]
    public async Task<ApiResponse<SysIntegration>> Update(int id, [FromBody] SysIntegration model)
    {
        var entity = await _db.SysIntegrations.FindAsync(id);
        if (entity is null) return ApiResponse<SysIntegration>.Fail("NOT_FOUND", "集成配置不存在");

        entity.SystemName = model.SystemName ?? entity.SystemName;
        entity.ServerAddress = model.ServerAddress ?? entity.ServerAddress;
        entity.Account = model.Account ?? entity.Account;
        // 密码更新：传入 ****/null/empty 表示不修改
        if (!string.IsNullOrEmpty(model.Password) && !model.Password.StartsWith("***"))
            entity.Password = model.Password;
        entity.ApiKey = model.ApiKey ?? entity.ApiKey;
        entity.IsEnabled = model.IsEnabled;
        entity.SyncIntervalMinutes = model.SyncIntervalMinutes;
        entity.ExtraConfigJson = model.ExtraConfigJson ?? entity.ExtraConfigJson;
        entity.Remark = model.Remark ?? entity.Remark;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        entity.Password = MaskPassword(entity.Password);
        return ApiResponse<SysIntegration>.Ok(entity, "更新成功");
    }

    [HttpDelete("{id:int}")]
    public async Task<ApiResponse> Delete(int id)
    {
        var entity = await _db.SysIntegrations.FindAsync(id);
        if (entity is null) return ApiResponse.Fail("NOT_FOUND", "集成配置不存在");
        _db.SysIntegrations.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    [HttpPost("{id:int}/test")]
    public async Task<ApiResponse<IntegrationTestResult>> TestConnection(int id)
    {
        var entity = await _db.SysIntegrations.FindAsync(id);
        if (entity is null) return ApiResponse<IntegrationTestResult>.Fail("NOT_FOUND", "集成配置不存在");
        if (string.IsNullOrEmpty(entity.ServerAddress))
            return ApiResponse<IntegrationTestResult>.Fail("NO_SERVER", "服务器地址为空");

        var result = new IntegrationTestResult { SystemCode = entity.SystemCode };
        var sw = global::System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var request = new HttpRequestMessage(HttpMethod.Get, entity.ServerAddress);
            if (!string.IsNullOrEmpty(entity.ApiKey))
                request.Headers.Add("Authorization", $"Bearer {entity.ApiKey}");
            var resp = await client.SendAsync(request);
            sw.Stop();
            result.HttpStatus = (int)resp.StatusCode;
            result.Success = resp.IsSuccessStatusCode;
            result.Message = $"HTTP {(int)resp.StatusCode} ({sw.ElapsedMilliseconds}ms)";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Success = false;
            result.Message = ex.Message;
        }

        entity.LastTestTime = DateTime.Now;
        entity.LastTestResult = result.Success;
        await _db.SaveChangesAsync();

        return ApiResponse<IntegrationTestResult>.Ok(result,
            result.Success ? "连接成功" : $"连接失败：{result.Message}");
    }

    [HttpPost("{id:int}/sync")]
    public async Task<ApiResponse> TriggerSync(int id)
    {
        var entity = await _db.SysIntegrations.FindAsync(id);
        if (entity is null) return ApiResponse.Fail("NOT_FOUND", "集成配置不存在");
        if (!entity.IsEnabled) return ApiResponse.Fail("DISABLED", "该集成未启用");

        // 简化：记录同步时间，详细同步逻辑由具体业务模块实现
        entity.LastSyncTime = DateTime.Now;
        entity.LastSyncResult = true;
        entity.LastSyncMessage = "已触发同步（具体业务由对应 Worker 处理）";
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("同步已触发");
    }

    private static string? MaskPassword(string? pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return pwd;
        if (pwd.Length <= 2) return "**";
        return pwd.Substring(0, 1) + "***" + pwd.Substring(pwd.Length - 1);
    }
}

public class IntegrationTestResult
{
    public string SystemCode { get; set; } = "";
    public bool Success { get; set; }
    public int HttpStatus { get; set; }
    public string Message { get; set; } = "";
}