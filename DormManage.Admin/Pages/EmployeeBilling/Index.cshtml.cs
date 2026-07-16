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

        // 使用真实服务查询
        var entities = await _billing.GetEmployeeBillsAsync(BillingMonth, DormCode, EmpKeyword, PageIndex, PageSize);
        Result = new PagedResult<EmployeeBillingDto>
        {
            Items = entities.Items.Select(e => new EmployeeBillingDto
            {
                Id = e.Id,
                EmployeeCode = e.EmployeeCode,
                EmployeeName = e.EmployeeName,
                Department = "",
                DormCode = e.DormCode ?? "",
                BillingMonth = e.BillingMonth,
                ShareAmount = e.TotalShareAmount,
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
    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string Department { get; set; } = "";
    public string DormCode { get; set; } = "";
    public string BillingMonth { get; set; } = "";
    public decimal ShareAmount { get; set; }
    public bool IsPublished { get; set; }
}