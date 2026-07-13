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

    public int PageSize { get; } = 20;

    public async Task OnGetAsync()
    {
        Result = await _service.GetListAsync(Keyword, Department, DormCode, Type, Status, DateFrom, DateTo, PageIndex, PageSize);
    }
}
