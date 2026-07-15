using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

public interface IPersonnelService
{
    Task<PagedResult<SysEmployee>> GetListAsync(string? keyword, string? department, int? employeeTypeId, int? employmentStatusId, int page, int pageSize);
}

public class PersonnelService : IPersonnelService
{
    public Task<PagedResult<SysEmployee>> GetListAsync(string? keyword, string? department, int? employeeTypeId, int? employmentStatusId, int page, int pageSize)
    {
        return Task.FromResult(new PagedResult<SysEmployee>
        {
            Items = new List<SysEmployee>(),
            Total = 0,
            PageIndex = page,
            PageSize = pageSize
        });
    }
}
