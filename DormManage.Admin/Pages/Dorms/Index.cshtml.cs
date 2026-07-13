using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 宿舍管理页面模型
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBasicsService _basicsService;

    public IndexModel(DormDbContext db, IBasicsService basicsService)
    {
        _db = db;
        _basicsService = basicsService;
    }

    /// <summary>
    /// 宿舍列表
    /// </summary>
    public PagedResult<DormDto>? Result { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 宿舍号
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    /// <summary>
    /// 楼栋ID
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? BuildingId { get; set; }

    /// <summary>
    /// 楼层ID
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? FloorId { get; set; }

    /// <summary>
    /// 地址ID
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? AddressId { get; set; }

    /// <summary>
    /// 楼栋列表
    /// </summary>
    public List<Building> Buildings { get; set; } = new();

    /// <summary>
    /// 楼层列表
    /// </summary>
    public List<Floor> Floors { get; set; } = new();

    /// <summary>
    /// 地址列表
    /// </summary>
    public List<Address> Addresses { get; set; } = new();

    public async Task OnGetAsync()
    {
        // 加载基础资料
        var buildings = await _basicsService.GetBuildingsAsync(null, 1, 100);
        var floors = await _basicsService.GetFloorsAsync(null, 1, 100);
        var addresses = await _basicsService.GetAddressesAsync(null, 1, 100);

        Buildings = buildings.Items.ToList();
        Floors = floors.Items.ToList();
        Addresses = addresses.Items.ToList();

        // 查询宿舍列表
        var query = _db.Dorms.AsQueryable();

        if (!string.IsNullOrWhiteSpace(DormCode))
            query = query.Where(d => d.DormCode.Contains(DormCode));

        if (BuildingId.HasValue && BuildingId.Value > 0)
            query = query.Where(d => d.BuildingId == BuildingId.Value);

        if (FloorId.HasValue && FloorId.Value > 0)
            query = query.Where(d => d.FloorId == FloorId.Value);

        if (AddressId.HasValue && AddressId.Value > 0)
            query = query.Where(d => d.AddressId == AddressId.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.DormCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .Select(d => new DormDto
            {
                Id = d.Id,
                DormCode = d.DormCode,
                BuildingName = d.BuildingName ?? "",
                FloorNo = d.FloorId,
                AddressText = d.AddressText ?? "",
                Capacity = d.Capacity,
                Gender = d.Gender,
                CurrentCount = _db.DormBookings.Count(b => b.DormCode == d.DormCode && b.Status == 2),
                IsActive = d.IsActive
            })
            .ToListAsync();

        Result = new PagedResult<DormDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }
}

/// <summary>
/// 宿舍数据传输对象
/// </summary>
public class DormDto
{
    public int Id { get; set; }
    public string DormCode { get; set; } = "";
    public string BuildingName { get; set; } = "";
    public int FloorNo { get; set; }
    public string AddressText { get; set; } = "";
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public int CurrentCount { get; set; }
    public bool IsActive { get; set; }
}
