using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.BillingStandard;

/// <summary>
/// 费用标准新增页面模型
/// v2.13.110 三层防御：PageModel 校验 billingstandard:add 按钮权限
/// （UI 层 PageHeader PermissionCode + PageModel 层 + API 层）
/// </summary>
public class CreateModel : PageModel
{
    private readonly IBillingService _service;
    private readonly IPermissionService _perm;
    private readonly IHttpContextAccessor _http;

    /// <summary>v2.13.110 新增费用标准所需权限码（与 PageHeader primaryAction.PermissionCode 一致）</summary>
    public const string RequiredPermissionCode = "billingstandard:add";

    public CreateModel(IBillingService service, IPermissionService perm, IHttpContextAccessor http)
    {
        _service = service;
        _perm = perm;
        _http = http;
    }

    [BindProperty]
    public global::DormManage.Shared.Models.BillingStandard Input { get; set; } = new();

    /// <summary>v2.13.61 新增：员工类型字典（基础资料真源）— 用于适用员工类型下拉</summary>
    public List<EmployeeType> EmployeeTypes { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // v2.13.110 三层防御（PageModel 层）：无 billingstandard:add 权限 → 重定向到列表 + 拒绝提示
        if (!_perm.CurrentUserHasCode(_http, RequiredPermissionCode))
        {
            TempData["Error"] = $"您没有「新增费用标准」权限（{RequiredPermissionCode}），无法访问该页面";
            return RedirectToPage("/BillingStandard/Index");
        }
        // v2.13.61 默认勾选启用 + 加载员工类型字典
        Input.IsActive = true;
        EmployeeTypes = await _service.GetEmployeeTypesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // v2.13.110 三层防御（PageModel 层）：POST 也必须校验
        if (!_perm.CurrentUserHasCode(_http, RequiredPermissionCode))
        {
            TempData["Error"] = $"您没有「新增费用标准」权限（{RequiredPermissionCode}），无法提交数据";
            return RedirectToPage("/BillingStandard/Index");
        }

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