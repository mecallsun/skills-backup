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

    /// <summary>v2.13.61 新增：员工类型字典（基础资料真源）— 用于适用员工类型下拉</summary>
    public List<EmployeeType> EmployeeTypes { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var standard = await _db.BillingStandards.FindAsync(Id);
        if (standard == null)
        {
            TempData["ErrorMessage"] = "费用标准不存在";
            return RedirectToPage("/BillingStandard/Index");
        }
        // v2.13.61 修复：优先使用 ApplicableTypeId，兼容历史 ApplicableType Name
        if (standard.ApplicableTypeId <= 0 && !string.IsNullOrEmpty(standard.ApplicableType))
        {
            var matchedType = await _db.EmployeeTypes.FirstOrDefaultAsync(t => t.Name == standard.ApplicableType);
            if (matchedType != null) standard.ApplicableTypeId = matchedType.Id;
        }
        Input = standard;
        EmployeeTypes = await _service.GetEmployeeTypesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        EmployeeTypes = await _service.GetEmployeeTypesAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }
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
