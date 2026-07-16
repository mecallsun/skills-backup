using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.BillingStandard;

public class IndexModel : PageModel
{
    private readonly IBillingService _service;

    public IndexModel(IBillingService service) => _service = service;

    public global::DormManage.Shared.Models.BillingStandard? ActiveStandard { get; set; }
    public PagedResult<global::DormManage.Shared.Models.BillingStandard>? Result { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public int Total => Result?.Total ?? 0;

    public async Task OnGetAsync()
    {
        ActiveStandard = await _service.GetActiveStandardAsync();
        Result = await _service.GetStandardsAsync(PageIndex, 20);
    }
}
