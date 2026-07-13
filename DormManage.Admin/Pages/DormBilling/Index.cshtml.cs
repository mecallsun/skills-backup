using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.DormBilling;

/// <summary>
/// 宿舍账单页面模型
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;

    public IndexModel(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 宿舍账单列表
    /// </summary>
    public PagedResult<DormBillingDto>? Result { get; set; }

    /// <summary>
    /// 楼栋列表（用于筛选）
    /// </summary>
    public List<BuildingDropdownItem> Buildings { get; set; } = new();

    /// <summary>
    /// 楼层列表（用于筛选）
    /// </summary>
    public List<FloorDropdownItem> Floors { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? BillingMonth { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? BuildingId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FloorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; } = 20;

    public async Task OnGetAsync()
    {
        // 默认月份为当前月
        if (string.IsNullOrEmpty(BillingMonth))
        {
            BillingMonth = DateTime.Now.ToString("yyyy-MM");
        }

        // 加载筛选下拉数据
        Buildings = await _db.Buildings
            .Where(b => b.IsActive)
            .OrderBy(b => b.Id)
            .Select(b => new BuildingDropdownItem { Id = b.Id, Name = b.Name })
            .ToListAsync();

        Floors = await _db.Floors
            .Where(f => f.IsActive)
            .OrderBy(f => f.FloorNo)
            .Select(f => new FloorDropdownItem { Id = f.Id, FloorNo = f.FloorNo })
            .ToListAsync();

        // 查询账单数据（使用模拟数据，实际应从 DormBilling 表读取）
        var query = _db.Dorms
            .Include(d => d.Building)
            .Include(d => d.Floor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(DormCode))
            query = query.Where(d => d.DormCode.Contains(DormCode));

        if (BuildingId.HasValue)
            query = query.Where(d => d.BuildingId == BuildingId.Value);

        if (FloorId.HasValue)
            query = query.Where(d => d.FloorId == FloorId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.DormCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .Select(d => new DormBillingDto
            {
                DormId = d.Id,
                DormCode = d.DormCode,
                BuildingName = d.BuildingName ?? "-",
                FloorName = d.FloorId.ToString(),
                ColdUsage = new Random().NextDecimal(80, 160),
                HotUsage = new Random().NextDecimal(50, 100),
                ElectricUsage = new Random().NextDecimal(200, 450),
                ColdAmount = new Random().NextDecimal(200, 400),
                HotAmount = new Random().NextDecimal(300, 500),
                ElectricAmount = new Random().NextDecimal(600, 900),
                TotalAmount = 0m,
                ResidentCount = new Random().Next(1, d.Capacity + 1),
                MaxCapacity = d.Capacity,
                IsPublished = new Random().Next(0, 3) != 0,
                ReadMonth = BillingMonth ?? DateTime.Now.ToString("yyyy-MM")
            })
            .ToListAsync();

        // 计算合计
        foreach (var item in items)
        {
            item.TotalAmount = item.ColdAmount + item.HotAmount + item.ElectricAmount;
        }

        Result = new PagedResult<DormBillingDto>
        {
            Items = items,
            TotalCount = total,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }

    public string GetQueryString(int page)
    {
        var qs = new System.Text.StringBuilder();
        qs.Append($"pageIndex={page}");
        if (!string.IsNullOrEmpty(BillingMonth))
            qs.Append($"&billingMonth={BillingMonth}");
        if (!string.IsNullOrEmpty(DormCode))
            qs.Append($"&dormCode={DormCode}");
        if (BuildingId.HasValue)
            qs.Append($"&buildingId={BuildingId}");
        if (FloorId.HasValue)
            qs.Append($"&floorId={FloorId}");
        return qs.ToString();
    }
}

/// <summary>
/// 宿舍账单数据传输对象
/// </summary>
public class DormBillingDto
{
    public int DormId { get; set; }
    public string DormCode { get; set; } = "";
    public string BuildingName { get; set; } = "";
    public string FloorName { get; set; } = "";
    public decimal ColdUsage { get; set; }
    public decimal HotUsage { get; set; }
    public decimal ElectricUsage { get; set; }
    public decimal ColdAmount { get; set; }
    public decimal HotAmount { get; set; }
    public decimal ElectricAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int ResidentCount { get; set; }
    public int MaxCapacity { get; set; }
    public bool IsPublished { get; set; }
    public string ReadMonth { get; set; } = "";
    public int RowIndex => 0; // 由视图计算
}

/// <summary>
/// 楼栋下拉项
/// </summary>
public class BuildingDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// 楼层下拉项
/// </summary>
public class FloorDropdownItem
{
    public int Id { get; set; }
    public int FloorNo { get; set; }
}
