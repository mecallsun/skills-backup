using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.Booking;

public class IndexModel : PageModel
{
    private readonly IBookingService _service;

    public IndexModel(IBookingService service)
    {
        _service = service;
    }

    public PagedResult<DormBooking>? Result { get; set; }

    /// <summary>总数（供 PageHeader 组件使用）</summary>
    public int Total => Result?.TotalCount ?? 0;

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Department { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    /// <summary>v2.13.74 BUG 修复：必须可写 + BindProperty 才能从 ?pageSize=N URL 绑定</summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    public List<string> DormCodes { get; set; } = new();

    public async Task OnGetAsync()
    {
        // v2.13.20 为房号筛选提供 datalist 候选
        DormCodes = await _service.GetAllDormCodesAsync();

        Result = await _service.GetListAsync(Keyword, Department, DormCode, Type, Status, DateFrom, DateTo, PageIndex, PageSize);
    }
}
