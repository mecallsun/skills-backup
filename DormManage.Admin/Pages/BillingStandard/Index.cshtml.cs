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

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? IsActive { get; set; }

    /// <summary>v2.13.20 新增：适用类型筛选项（标准名称/适用类型/状态）</summary>
    [BindProperty(SupportsGet = true)]
    public string? ApplicableType { get; set; }

    public List<string> ApplicableTypes { get; set; } = new();

    public int Total => Result?.Total ?? 0;

    public async Task OnGetAsync()
    {
        ActiveStandard = await _service.GetActiveStandardAsync();
        ApplicableTypes = await _service.GetStandardApplicableTypesAsync();
        Result = await _service.GetStandardsAsync(Keyword, ApplicableType, IsActive, PageIndex, 20);
    }
}
