using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;
using DormManage.Admin.Pages.Shared;

namespace DormManage.Admin.Pages.Booking;

public class IndexModel : PaginatedPageModel
{
    private readonly IBookingService _service;
    private readonly DormDbContext _db;

    public IndexModel(IBookingService service, DormDbContext db)
    {
        _service = service;
        _db = db;
    }

    public PagedResult<DormBooking>? Result { get; set; }

    /// <summary>v2.13.86 性别映射：EmployeeId → Gender（批量 JOIN SysEmployee 取实时）</summary>
    public Dictionary<int, int> GenderMap { get; set; } = new();

    /// <summary>总数（供 PageHeader 组件使用）</summary>
    public int Total => Result?.TotalCount ?? 0;

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Department { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    // PageIndex / PageSize 继承自 v2.13.104 PaginatedPageModel 基类（含白名单校验）

    public List<string> DormCodes { get; set; } = new();

    public async Task OnGetAsync()
    {
        EnsureValidPagination();  // v2.13.104
        // v2.13.20 为房号筛选提供 datalist 候选
        DormCodes = await _service.GetAllDormCodesAsync();

        Result = await _service.GetListAsync(Keyword, Department, DormCode, Type, Status, DateFrom, DateTo, PageIndex, PageSize);

        // v2.13.86 批量 JOIN SysEmployee 拿 Gender（避免 N+1）
        if (Result?.Items != null && Result.Items.Any())
        {
            var empIds = Result.Items.Select(b => b.EmployeeId).Distinct().Where(id => id > 0).ToList();
            GenderMap = await _db.Employees
                .Where(emp => empIds.Contains(emp.Id))
                .Select(emp => new { emp.Id, emp.Gender })
                .ToDictionaryAsync(x => x.Id, x => x.Gender);
        }
    }
}
