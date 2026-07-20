using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.DormBilling;

/// <summary>
/// 宿舍账单页面模型
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBillingService _billing;

    public IndexModel(DormDbContext db, IBillingService billing)
    {
        _db = db;
        _billing = billing;
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

    /// <summary>
    /// 宿舍房号候选（datalist 自动完成用）
    /// </summary>
    public List<string> DormCodes { get; set; } = new();

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

    // 分页摘要（视图使用）
    public int TotalCount => Result?.TotalCount ?? 0;
    public int TotalPages => TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public int CurrentPage => PageIndex;
    public int StartIndex => (PageIndex - 1) * PageSize + 1;

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

        // v2.13.20 为房号筛选提供 datalist 候选
        DormCodes = await _db.Dorms
            .Where(d => d.IsActive)
            .OrderBy(d => d.DormCode)
            .Select(d => d.DormCode)
            .ToListAsync();

        // 查询账单数据（使用真实服务）
        var entities = await _billing.GetDormBillsAsync(BillingMonth, DormCode, PageIndex, PageSize);
        Result = new PagedResult<DormBillingDto>
        {
            Items = entities.Items.Select(e => new DormBillingDto
            {
                DormId = e.Id,
                DormCode = e.DormCode,
                BuildingName = e.DormCode,
                FloorName = e.DormCode,
                ColdUsage = e.ColdUsage,
                HotUsage = e.HotUsage,
                ElectricUsage = e.ElectricityUsage,
                ColdAmount = e.ColdAmount,
                HotAmount = e.HotAmount,
                ElectricAmount = e.ElectricityAmount,
                TotalAmount = e.TotalAmount,
                ResidentCount = e.ResidentCount,
                MaxCapacity = e.ResidentCount,
                IsPublished = e.IsPublished,
                ReadMonth = e.BillingMonth
            }).ToList(),
            TotalCount = entities.Total,
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
