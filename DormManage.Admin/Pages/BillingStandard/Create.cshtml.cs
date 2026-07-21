using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.BillingStandard;

public class CreateModel : PageModel
{
    private readonly IBillingService _service;

    public CreateModel(IBillingService service) => _service = service;

    [BindProperty]
    public global::DormManage.Shared.Models.BillingStandard Input { get; set; } = new();

    /// <summary>v2.13.61 新增：员工类型字典（基础资料真源）— 用于适用员工类型下拉</summary>
    public List<EmployeeType> EmployeeTypes { get; set; } = new();

    public async Task OnGetAsync()
    {
        // v2.13.61 默认勾选启用 + 加载员工类型字典
        Input.IsActive = true;
        EmployeeTypes = await _service.GetEmployeeTypesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        EmployeeTypes = await _service.GetEmployeeTypesAsync();
        if (!ModelState.IsValid) return Page();
        Input.Id = 0;
        var (ok, msg) = await _service.SaveStandardAsync(Input);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, msg);
            return Page();
        }
        TempData["Success"] = msg;
        return RedirectToPage("/BillingStandard/Index");
    }
}
