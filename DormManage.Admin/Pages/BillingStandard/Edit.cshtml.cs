using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;

namespace DormManage.Admin.Pages.BillingStandard;

public class EditModel : PageModel
{
    private readonly IBillingService _service;
    private readonly DormDbContext _db;

    public EditModel(IBillingService service, DormDbContext db)
    {
        _service = service;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public global::DormManage.Shared.Models.BillingStandard Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var standard = await _db.BillingStandards.FindAsync(Id);
        if (standard == null)
        {
            TempData["ErrorMessage"] = "费用标准不存在";
            return RedirectToPage("/BillingStandard/Index");
        }
        Input = standard;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
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
