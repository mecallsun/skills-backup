using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using DormManage.Shared.Data;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Personnel;

/// <summary>员工编辑页面模型（项1，纯 @@page + ?id= 路由）</summary>
public class EditModel : PageModel
{
    private readonly IPersonnelService _svc;
    private readonly DormDbContext _db;

    public EditModel(IPersonnelService svc, DormDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public PersonnelEditDto Input { get; set; } = new();

    public string EmployeeCode { get; set; } = "";

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> EmployeeTypes { get; set; } = new();
    public List<SelectListItem> Teams { get; set; } = new();
    public List<SelectListItem> AttendanceTypes { get; set; } = new();
    public List<SelectListItem> EmploymentStatuses { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var emp = await _svc.GetByIdAsync(Id);
        if (emp == null)
        {
            TempData["ErrorMessage"] = "员工不存在";
            return RedirectToPage("/Personnel/Index");
        }
        EmployeeCode = emp.EmployeeCode;
        Input = new PersonnelEditDto
        {
            EmployeeCode = emp.EmployeeCode,
            RealName = emp.RealName,
            DepartmentId = emp.DepartmentId,
            EmployeeTypeId = emp.EmployeeTypeId,
            TeamId = emp.TeamId,
            Gender = emp.Gender,
            Phone = emp.Phone,
            HireDate = emp.HireDate,
            AttendanceTypeId = emp.AttendanceTypeId ?? 0,
            EmploymentStatusId = emp.EmploymentStatusId,
            DormCode = emp.DormCode,
            BedNo = emp.BedNo,
            Remark = emp.Remark
        };
        await LoadDropdownsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return Page();
        }
        var (ok, msg) = await _svc.UpdateAsync(Id, Input);
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
