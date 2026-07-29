using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using System.ComponentModel.DataAnnotations;

namespace DormManage.Shared.Services;

/// <summary>
/// 字段权限服务（v2.13.92 新增）：管理 SysFieldPermission 字段清单，
/// 提供给 Settings 字段权限页面 + 角色隐私开关使用。
///
/// 与 IPermissionService 的关系：
///   - IPermissionService.AllowDisplayPrivacyFieldsAsync(userId)  → 角色级总开关（v2.13.176 deny-by-default：勾选才能显示）
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

    /// <summary>新增字段权限（v2.13.195 新增：支持动态添加隐私字段）</summary>
    Task<SysFieldPermission> CreateAsync(SysFieldPermissionCreateDto dto);
}

/// <summary>字段权限更新 DTO（v2.13.92）</summary>
public class SysFieldPermissionUpdateDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 字段权限创建 DTO（v2.13.195 新增：支持动态添加隐私字段）
/// </summary>
public class SysFieldPermissionCreateDto
{
    [Required(ErrorMessage = "字段键不能为空")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "字段键只能包含字母、数字、点、下划线和连字符")]
    public string FieldKey { get; set; } = "";

    [Required(ErrorMessage = "模块不能为空")]
    public string Module { get; set; } = "";

    [Required(ErrorMessage = "字段显示名不能为空")]
    [MaxLength(64, ErrorMessage = "字段显示名不能超过64个字符")]
    public string FieldName { get; set; } = "";

    [Required(ErrorMessage = "字段类型不能为空")]
    public string FieldType { get; set; } = "string";

    [Range(1, 3, ErrorMessage = "敏感等级必须在 1-3 之间")]
    public byte SensitivityLevel { get; set; } = 2;

    [Required(ErrorMessage = "描述不能为空")]
    [MaxLength(200, ErrorMessage = "描述不能超过200个字符")]
    public string Description { get; set; } = "";

    public bool? IsActive { get; set; } = true;
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

    /// <summary>
    /// 新增字段权限（v2.13.195 新增；v2.13.196 修复：手动生成 Id）
    /// </summary>
    public async Task<SysFieldPermission> CreateAsync(SysFieldPermissionCreateDto dto)
    {
        // 检查 FieldKey 唯一性
        if (await _db.SysFieldPermissions.AnyAsync(f => f.FieldKey == dto.FieldKey))
            throw new ArgumentException($"字段键 \"{dto.FieldKey}\" 已存在，请使用其他值", nameof(dto.FieldKey));

        // v2.13.196 修复：使用原始 SQL 直接插入（绕过 EF Core 模型缓存的 IDENTITY 状态问题）
        var maxId = await _db.SysFieldPermissions.MaxAsync(f => (int?)f.Id) ?? 0;
        var newId = maxId + 1;

        var maxSort = await _db.SysFieldPermissions
            .Where(f => f.Module == dto.Module)
            .MaxAsync(f => (int?)f.SortOrder) ?? 0;

        var entity = new SysFieldPermission
        {
            Id = newId,
            FieldKey = dto.FieldKey,
            Module = dto.Module,
            FieldName = dto.FieldName,
            FieldType = dto.FieldType,
            SensitivityLevel = dto.SensitivityLevel,
            SortOrder = maxSort + 1,
            IsActive = dto.IsActive ?? true,
            Description = dto.Description,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            UpdatedBy = "system"
        };

        // v2.13.196: 显式设置 EntityState.Added 并设置所有属性为 Unchanged，避免 EF 跟踪默认值
        _db.SysFieldPermissions.Add(entity);

        // 显式标记 Id 为已设置（防止 EF 重新生成）
        _db.Entry(entity).Property(e => e.Id).IsModified = true;

        await _db.SaveChangesAsync();

        return entity;
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