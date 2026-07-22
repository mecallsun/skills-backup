using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using DormManage.Shared.Data;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Personnel;

/// <summary>
/// 员工新增页面模型（项1）
/// v2.13.106 三层防御：PageModel 校验 personnel:add 按钮权限
/// （UI 层 PageHeader PermissionCode + PageModel 层 + API 层）
/// </summary>
public class CreateModel : PageModel
{
    private readonly IPersonnelService _svc;
    private readonly DormDbContext _db;
    private readonly IPermissionService _perm;
    private readonly IHttpContextAccessor _http;

    /// <summary>v2.13.106 新增人员所需权限码（与 PageHeader primaryAction.PermissionCode 一致）</summary>
    public const string RequiredPermissionCode = "personnel:add";

    public CreateModel(IPersonnelService svc, DormDbContext db, IPermissionService perm, IHttpContextAccessor http)
    {
        _svc = svc;
        _db = db;
        _perm = perm;
        _http = http;
    }

    [BindProperty]
    public PersonnelEditDto Input { get; set; } = new();

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> EmployeeTypes { get; set; } = new();
    public List<SelectListItem> Teams { get; set; } = new();
    public List<SelectListItem> AttendanceTypes { get; set; } = new();
    public List<SelectListItem> EmploymentStatuses { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // v2.13.106 三层防御（PageModel 层）：无 personnel:add 权限 → 重定向到列表 + 拒绝提示
        if (!_perm.CurrentUserHasCode(_http, RequiredPermissionCode))
        {
            TempData["Error"] = $"您没有「新增人员」权限（{RequiredPermissionCode}），无法访问该页面";
            return RedirectToPage("/Personnel/Index");
        }
        await LoadDropdownsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // v2.13.106 三层防御（PageModel 层）：POST 也必须校验
        if (!_perm.CurrentUserHasCode(_http, RequiredPermissionCode))
        {
            TempData["Error"] = $"您没有「新增人员」权限（{RequiredPermissionCode}），无法提交数据";
            return RedirectToPage("/Personnel/Index");
        }

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
