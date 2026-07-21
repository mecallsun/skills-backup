using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.EmployeeBilling;

/// <summary>
/// 员工账单页面模型（人员分摊）
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBillingService _billing;

    public IndexModel(DormDbContext db, IBillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    /// <summary>
    /// 员工账单列表
    /// </summary>
    public PagedResult<EmployeeBillingDto>? Result { get; set; }

    /// <summary>
    /// 部门列表（用于筛选）
    /// </summary>
    public List<DepartmentDropdownItem> Departments { get; set; } = new();

    /// <summary>
    /// 员工类型列表（用于筛选）
    /// </summary>
    public List<EmployeeTypeDropdownItem> EmployeeTypes { get; set; } = new();

    /// <summary>
    /// 住宿状态列表（用于筛选）
    /// </summary>
    public List<ResidenceStatusDropdownItem> ResidenceStatuses { get; set; } = new();

    /// <summary>
    /// v2.13.44 新增：在职状态列表（用于筛选）
    /// </summary>
    public List<EmploymentStatusDropdownItem> EmploymentStatuses { get; set; } = new();

    /// <summary>
    /// 宿舍房号候选（datalist 自动完成用）
    /// </summary>
    public List<string> DormCodes { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    /// <summary>v2.13.74 BUG 修复：必须 BindProperty 才能从 ?pageSize=N URL 绑定</summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    [BindProperty(SupportsGet = true)]
    public string? BillingMonth { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EmpKeyword { get; set; }

    /// <summary>v2.13.20 新增：部门筛选</summary>
    [BindProperty(SupportsGet = true)]
    public int? DepartmentId { get; set; }

    /// <summary>v2.13.20 新增：员工类型筛选</summary>
    [BindProperty(SupportsGet = true)]
    public int? EmployeeTypeId { get; set; }

    /// <summary>v2.13.20 新增：住宿状态筛选</summary>
    [BindProperty(SupportsGet = true)]
    public int? ResidenceStatusId { get; set; }

    /// <summary>v2.13.44 新增：在职状态筛选</summary>
    [BindProperty(SupportsGet = true)]
    public int? EmploymentStatusId { get; set; }

    /// <summary>
    /// 分摊合计金额
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 分摊人数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>总数（供 PageHeader 组件使用）</summary>
    public int Total => Result?.TotalCount ?? 0;

    public async Task OnGetAsync()
    {
        // 默认月份为当前月
        if (string.IsNullOrEmpty(BillingMonth))
        {
            BillingMonth = DateTime.Now.ToString("yyyy-MM");
        }

        // 加载筛选下拉数据
        Departments = await _db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .Select(d => new DepartmentDropdownItem { Id = d.Id, Name = d.Name })
            .ToListAsync();

        EmployeeTypes = await _db.EmployeeTypes
            .Where(e => e.IsActive)
            .OrderBy(e => e.Id)
            .Select(e => new EmployeeTypeDropdownItem { Id = e.Id, Name = e.Name })
            .ToListAsync();

        ResidenceStatuses = await _db.ResidenceStatuses
            .Where(r => r.IsActive)
            .OrderBy(r => r.Id)
            .Select(r => new ResidenceStatusDropdownItem { Id = r.Id, Name = r.Name })
            .ToListAsync();

        // v2.13.44 新增：在职状态下拉数据
        EmploymentStatuses = await _db.EmploymentStatuses
            .Where(e => e.IsActive)
            .OrderBy(e => e.Id)
            .Select(e => new EmploymentStatusDropdownItem { Id = e.Id, Name = e.Name })
            .ToListAsync();

        DormCodes = await _db.Dorms
            .Where(d => d.IsActive)
            .OrderBy(d => d.DormCode)
            .Select(d => d.DormCode)
            .ToListAsync();

        // 使用真实服务查询（v2.13.44 扩展：新增在职状态筛选）
        var entities = await _billing.GetEmployeeBillsAsync(BillingMonth, DormCode, EmpKeyword, DepartmentId, EmployeeTypeId, ResidenceStatusId, EmploymentStatusId, PageIndex, PageSize);

        // v2.13.86 批量 JOIN SysEmployee 拿 Gender（避免 N+1）
        var empIds = entities.Items.Select(e => e.EmployeeId).Distinct().ToList();
        var genderMap = await _db.Employees
            .Where(emp => empIds.Contains(emp.Id))
            .Select(emp => new { emp.Id, emp.Gender })
            .ToDictionaryAsync(x => x.Id, x => x.Gender);

        Result = new PagedResult<EmployeeBillingDto>
        {
            Items = entities.Items.Select(e => new EmployeeBillingDto
            {
                Id = e.Id,
                EmployeeId = e.EmployeeId,
                EmployeeCode = e.EmployeeCode,
                EmployeeName = e.EmployeeName,
                Gender = genderMap.GetValueOrDefault(e.EmployeeId, 0),  // v2.13.86 JOIN SysEmployee 取实时性别
                Department = "",
                DormCode = e.DormCode ?? "",
                BillingMonth = e.BillingMonth,
                ShareAmount = e.TotalShareAmount,
                // v2.13.44 新增：4 个分摊明细字段
                ColdShareAmount = e.ColdShareAmount,
                HotShareAmount = e.HotShareAmount,
                ElectricityShareAmount = e.ElectricityShareAmount,
                ResidentCount = e.ResidentCount,
                ShareRatio = e.ShareRatio,
                IsPublished = e.IsPublished
            }).ToList(),
            TotalCount = entities.Total,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }
}

/// <summary>
/// 员工账单数据传输对象（人员分摊）
/// </summary>
public class EmployeeBillingDto
{
    public int Id { get; set; }
    /// <summary>v2.13.44 新增：员工 ID（用于详情跳转）</summary>
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    /// <summary>v2.13.86 性别（JOIN SysEmployee 取实时 Gender）</summary>
    public int Gender { get; set; }
    public string Department { get; set; } = "";
    public string DormCode { get; set; } = "";
    public string BillingMonth { get; set; } = "";
    public decimal ShareAmount { get; set; }
    /// <summary>v2.13.44 新增：4 个分摊明细字段</summary>
    public decimal ColdShareAmount { get; set; }
    public decimal HotShareAmount { get; set; }
    public decimal ElectricityShareAmount { get; set; }
    public int ResidentCount { get; set; }
    public decimal ShareRatio { get; set; }
    public bool IsPublished { get; set; }
}

/// <summary>
/// 部门下拉项
/// </summary>
public class DepartmentDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// 员工类型下拉项
/// </summary>
public class EmployeeTypeDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// 住宿状态下拉项
/// </summary>
public class ResidenceStatusDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// v2.13.44 新增：在职状态下拉项
/// </summary>
public class EmploymentStatusDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}