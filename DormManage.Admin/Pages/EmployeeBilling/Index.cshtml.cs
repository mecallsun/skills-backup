using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.EmployeeBilling;

/// <summary>
/// 员工账单页面模型（人员分摊）
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;

    public IndexModel(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 员工账单列表
    /// </summary>
    public PagedResult<EmployeeBillingDto>? Result { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 计费月份
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? BillingMonth { get; set; }

    /// <summary>
    /// 宿舍号
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    /// <summary>
    /// 员工（工号/姓名）
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? EmpKeyword { get; set; }

    /// <summary>
    /// 分摊合计金额
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 分摊人数
    /// </summary>
    public int TotalCount { get; set; }

    public async Task OnGetAsync()
    {
        // 默认月份为当前月
        if (string.IsNullOrEmpty(BillingMonth))
        {
            BillingMonth = DateTime.Now.ToString("yyyy-MM");
        }

        // 这里使用 SysEmployee 表作为演示数据
        // 实际项目应该用 EmployeeBilling 表
        var query = _db.Employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(EmpKeyword))
            query = query.Where(e =>
                e.EmployeeCode.Contains(EmpKeyword) ||
                e.RealName.Contains(EmpKeyword));

        // 统计数据
        TotalCount = await query.CountAsync();

        // 分页数据
        var items = await query
            .OrderBy(e => e.EmployeeCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .Select(e => new EmployeeBillingDto
            {
                Id = e.Id,
                EmployeeCode = e.EmployeeCode,
                EmployeeName = e.RealName,
                Department = e.Department ?? "",
                DormCode = e.DormCode ?? "",
                BillingMonth = BillingMonth,
                ShareAmount = 569.50m,
                IsPublished = false
            })
            .ToListAsync();

        Result = new PagedResult<EmployeeBillingDto>
        {
            Items = items,
            TotalCount = TotalCount,
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
    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string Department { get; set; } = "";
    public string DormCode { get; set; } = "";
    public string BillingMonth { get; set; } = "";
    public decimal ShareAmount { get; set; }
    public bool IsPublished { get; set; }
}