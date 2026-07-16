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

    public async Task OnGetAsync() { }

    public async Task<IActionResult> OnPostAsync()
    {
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
