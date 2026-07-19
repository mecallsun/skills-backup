using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DormManage.Shared.Services;

/// <summary>
/// 用户筛选条件云端缓存服务（v2.13.12）
///
/// 用途：
///   - 跨设备同步用户在 6 大列表模块的筛选条件
///   - 与 localStorage 配合：localStorage 实时 + 服务端跨设备
///   - 退登录时若未勾选"存储筛选条件"，前端清空 localStorage；服务端缓存需调用 ResetAsync
///
/// 使用：
///   var cache = await svc.GetCacheAsync(userId, "personnel");
///   await svc.SaveCacheAsync(userId, "personnel", filterDict);
///   await svc.ResetAsync(userId, "personnel"); // 单模块
///   await svc.ResetAllAsync(userId); // 全部模块
/// </summary>
public interface ISysUserFilterCacheService
{
    /// <summary>读取指定用户+模块的筛选缓存（不存在返回空字典）</summary>
    Task<Dictionary<string, object>> GetCacheAsync(int userId, string moduleName);

    /// <summary>保存指定用户+模块的筛选缓存</summary>
    Task SaveCacheAsync(int userId, string moduleName, Dictionary<string, object> filter);

    /// <summary>清除指定用户+模块的缓存</summary>
    Task ResetAsync(int userId, string moduleName);

    /// <summary>清除指定用户的所有模块缓存</summary>
    Task ResetAllAsync(int userId);

    /// <summary>获取指定用户的所有模块缓存列表（用于个人中心展示）</summary>
    Task<List<FilterCacheSummary>> ListAllAsync(int userId);
}

/// <summary>筛选缓存摘要（个人中心展示用）</summary>
public class FilterCacheSummary
{
    public string ModuleName { get; set; } = "";
    public string FilterJson { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 实现：v2.13.12 新增
/// </summary>
public class SysUserFilterCacheService : ISysUserFilterCacheService
{
    private readonly DormDbContext _db;

    public SysUserFilterCacheService(DormDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, object>> GetCacheAsync(int userId, string moduleName)
    {
        var entity = await _db.SysUserFilterCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ModuleName == moduleName);

        if (entity == null || string.IsNullOrEmpty(entity.FilterJson))
            return new Dictionary<string, object>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(entity.FilterJson)
                ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    public async Task SaveCacheAsync(int userId, string moduleName, Dictionary<string, object> filter)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(moduleName))
            return;

        var json = JsonSerializer.Serialize(filter ?? new Dictionary<string, object>());

        var entity = await _db.SysUserFilterCaches
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ModuleName == moduleName);

        if (entity == null)
        {
            entity = new SysUserFilterCache
            {
                UserId = userId,
                ModuleName = moduleName,
                FilterJson = json,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _db.SysUserFilterCaches.Add(entity);
        }
        else
        {
            entity.FilterJson = json;
            entity.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task ResetAsync(int userId, string moduleName)
    {
        var entity = await _db.SysUserFilterCaches
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ModuleName == moduleName);
        if (entity != null)
        {
            _db.SysUserFilterCaches.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ResetAllAsync(int userId)
    {
        var entities = await _db.SysUserFilterCaches
            .Where(c => c.UserId == userId)
            .ToListAsync();
        if (entities.Any())
        {
            _db.SysUserFilterCaches.RemoveRange(entities);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<FilterCacheSummary>> ListAllAsync(int userId)
    {
        return await _db.SysUserFilterCaches
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.ModuleName)
            .Select(c => new FilterCacheSummary
            {
                ModuleName = c.ModuleName,
                FilterJson = c.FilterJson,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();
    }
}