using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 字段权限 API（v2.13.92 新增）：SysFieldPermission CRUD
///
/// 端点：
/// - GET  /api/v1/system/field-permissions       全表列表（按 SortOrder 排序）
/// - PUT  /api/v1/system/field-permissions       批量更新（设置 IsActive + SortOrder）
///
/// 说明：
///   - 无新增/删除接口：5 个字段内置 seed 已覆盖 95% 业务场景
///   - PermissionType=3（privacy:field:enable）的检查通过现有 IPermissionService 标准权限流自动加载
/// </summary>
[ApiController]
[Route("api/v1/system/field-permissions")]
public class FieldPermissionController : ControllerBase
{
    private readonly ISysFieldPermissionService _svc;
    private readonly IOperationLogService _opLog;

    public FieldPermissionController(ISysFieldPermissionService svc, IOperationLogService opLog)
    {
        _svc = svc;
        _opLog = opLog;
    }

    /// <summary>列出全部字段权限记录（含 IsActive=false）</summary>
    [HttpGet]
    public async Task<ApiResponse<List<SysFieldPermission>>> List()
    {
        var list = await _svc.GetAllAsync();
        return ApiResponse<List<SysFieldPermission>>.Ok(list);
    }

    /// <summary>批量更新字段权限（IsActive + SortOrder）</summary>
    [HttpPut]
    public async Task<ApiResponse> Update([FromBody] SysFieldPermissionUpdateRequest body)
    {
        if (body?.Updates == null || body.Updates.Count == 0)
            return ApiResponse.Ok("无变更");

        var updatedBy = User.Identity?.Name ?? "system";
        var result = await _svc.UpdateAsync(body.Updates, updatedBy);

        if (result.Success)
        {
            await _opLog.LogAsync("FieldPermission", "批量更新", $"更新字段数：{body.Updates.Count}（启用：{body.Updates.Count(u => u.IsActive)}）");
        }

        return result;
    }
}

/// <summary>批量更新请求体</summary>
public class SysFieldPermissionUpdateRequest
{
    public List<SysFieldPermissionUpdateDto> Updates { get; set; } = new();
}