using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

public interface IDormService
{
    Task<PagedResult<Dorm>> GetListAsync(string? keyword, int? buildingId, int page, int pageSize);
    Task<Dorm?> GetByIdAsync(int id);
}

public class DormService : IDormService
{
    public Task<PagedResult<Dorm>> GetListAsync(string? keyword, int? buildingId, int page, int pageSize)
    {
        return Task.FromResult(new PagedResult<Dorm>
        {
            Items = new List<Dorm>(),
            Total = 0,
            PageIndex = page,
            PageSize = pageSize
        });
    }

    public Task<Dorm?> GetByIdAsync(int id) => Task.FromResult<Dorm?>(null);
}
