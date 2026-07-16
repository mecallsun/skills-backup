using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using DormManage.Shared.Data;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Personnel;

/// <summary>员工新增页面模型（项1）</summary>
public class CreateModel : PageModel
{
    private readonly IPersonnelService _svc;
    private readonly DormDbContext _db;

    public CreateModel(IPersonnelService svc, DormDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    [BindProperty]
    public PersonnelEditDto Input { get; set; } = new();

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> EmployeeTypes { get; set; } = new();
    public List<SelectListItem> Teams { get; set; } = new();
    public List<SelectListItem> AttendanceTypes { get; set; } = new();
    public List<SelectListItem> EmploymentStatuses { get; set; } = new();

    public async Task OnGetAsync() => await LoadDropdownsAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return Page();
        }
        var (ok, msg, _) = await _svc.CreateAsync(Input);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, msg);
            await LoadDropdownsAsync();
            return Page();
        }
        TempData["Success"] = msg;
        return RedirectToPage("/Personnel/Index");
    }

    private async Task LoadDropdownsAsync()
    {
        Departments = await _db.Departments.Where(x => x.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        EmployeeTypes = await _db.EmployeeTypes.Where(x => x.IsActive).OrderBy(x => x.Id)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Teams = await _db.Teams.Where(x => x.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        AttendanceTypes = await _db.AttendanceTypes.Where(x => x.IsActive).OrderBy(x => x.Id)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        EmploymentStatuses = await _db.EmploymentStatuses.Where(x => x.IsActive).OrderBy(x => x.Id)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
    }
}
