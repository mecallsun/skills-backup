using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 抄表记录页面模型
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;

    public IndexModel(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 抄表记录列表
    /// </summary>
    public PagedResult<MeterRecordDto>? Result { get; set; }

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
    /// 宿舍号
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    /// <summary>
    /// 抄表月份
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReadMonth { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? Status { get; set; }

    /// <summary>
    /// 可选的月份列表
    /// </summary>
    public List<string> Months { get; set; } = new();

    public async Task OnGetAsync()
    {
        // 获取可选月份
        var existingMonths = await _db.MeterRecords
            .Select(r => r.ReadMonth)
            .Distinct()
            .OrderByDescending(m => m)
            .Take(12)
            .ToListAsync();

        var currentMonth = DateTime.Now.ToString("yyyy-MM");
        Months = existingMonths;
        if (!Months.Contains(currentMonth))
        {
            Months.Insert(0, currentMonth);
        }

        // 查询记录
        var query = _db.MeterRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(DormCode))
            query = query.Where(r => r.DormCode.Contains(DormCode));

        if (!string.IsNullOrWhiteSpace(ReadMonth))
            query = query.Where(r => r.ReadMonth == ReadMonth);

        if (Status.HasValue && Status.Value >= 0)
            query = query.Where(r => r.Status == Status.Value);

        var totalCount = await query.CountAsync();
        var records = await query
            .OrderByDescending(r => r.ReadMonth)
            .ThenBy(r => r.DormCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var items = records.Select(r => new MeterRecordDto
        {
            Id = r.Id,
            DormId = r.DormId,
            DormCode = r.DormCode,
            ReadMonth = r.ReadMonth,
            ColdMeter = r.ColdMeter,
            HotMeter = r.HotMeter,
            ElectricMeter = r.ElectricMeter,
            Operator = r.Operator,
            Status = r.Status,
            StatusName = r.GetStatusName(),
            Remark = r.Remark,
            ServerCreatedAt = r.ServerCreatedAt
        }).ToList();

        Result = new PagedResult<MeterRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }
}

/// <summary>
/// 抄表记录数据传输对象
/// </summary>
public class MeterRecordDto
{
    public long Id { get; set; }
    public int DormId { get; set; }
    public string DormCode { get; set; } = "";
    public string ReadMonth { get; set; } = "";
    public decimal ColdMeter { get; set; }
    public decimal HotMeter { get; set; }
    public decimal ElectricMeter { get; set; }
    public string Operator { get; set; } = "";
    public byte Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Remark { get; set; }
    public DateTime ServerCreatedAt { get; set; }
}
