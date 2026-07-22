using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 字段权限服务（v2.13.92 新增）：管理 SysFieldPermission 字段清单，
/// 提供给 Settings 字段权限页面 + 角色隐私开关使用。
///
/// 与 IPermissionService 的关系：
///   - IPermissionService.HasPrivacyFieldEnabledAsync(userId)  → 角色级总开关（隐私保护是否启用）
///   - IPermissionService.GetHiddenFieldKeysAsync(userId)      → 拉取该用户应隐藏的字段清单
///   - ISysFieldPermissionService.GetAllAsync/UpdateAsync      → 配置字段清单本身
/// </summary>
public interface ISysFieldPermissionService
{
    /// <summary>获取所有字段权限记录（含 IsActive=false），按 SortOrder 排序</summary>
    Task<List<SysFieldPermission>> GetAllAsync();

    /// <summary>获取启用的字段权限记录（IsActive=true），按 SortOrder 排序</summary>
    Task<List<SysFieldPermission>> GetActiveAsync();

    /// <summary>获取所有启用的字段键（FieldKey 列表）— 用于运行时隐藏判定缓存</summary>
    Task<List<string>> GetActiveFieldKeysAsync();

    /// <summary>
    /// 批量更新字段权限（设置 IsActive + SortOrder + UpdatedAt/UpdatedBy）。
    /// 不存在新增/删除入口（5 个字段内置 seed 覆盖 95% 场景）。
    /// </summary>
    Task<ApiResponse> UpdateAsync(List<SysFieldPermissionUpdateDto> updates, string? updatedBy);
}

/// <summary>字段权限更新 DTO（v2.13.92）</summary>
public class SysFieldPermissionUpdateDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class SysFieldPermissionService : ISysFieldPermissionService
{
    private readonly DormDbContext _db;

    public SysFieldPermissionService(DormDbContext db) => _db = db;

    public async Task<List<SysFieldPermission>> GetAllAsync()
    {
        return await _db.SysFieldPermissions
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();
    }

    public async Task<List<SysFieldPermission>> GetActiveAsync()
    {
        return await _db.SysFieldPermissions
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();
    }

    public async Task<List<string>> GetActiveFieldKeysAsync()
    {
        return await _db.SysFieldPermissions
            .Where(p => p.IsActive)
            .Select(p => p.FieldKey)
            .ToListAsync();
    }

    public async Task<ApiResponse> UpdateAsync(List<SysFieldPermissionUpdateDto> updates, string? updatedBy)
    {
        if (updates == null || updates.Count == 0)
            return ApiResponse.Ok("无变更");

        var ids = updates.Select(u => u.Id).ToList();
        var existing = await _db.SysFieldPermissions
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        if (existing.Count == 0)
            return ApiResponse.Fail("FIELD_NOT_FOUND", "字段不存在");

        var now = DateTime.Now;
        foreach (var dto in updates)
        {
            var entity = existing.FirstOrDefault(p => p.Id == dto.Id);
            if (entity == null) continue;
            entity.IsActive = dto.IsActive;
            entity.SortOrder = dto.SortOrder;
            entity.UpdatedAt = now;
            entity.UpdatedBy = updatedBy ?? "system";
        }

        await _db.SaveChangesAsync();
        return ApiResponse.Ok($"已保存 {updates.Count} 个字段配置");
    }
}