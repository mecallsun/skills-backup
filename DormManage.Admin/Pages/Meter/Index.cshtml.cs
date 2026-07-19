using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 抄表记录页面模型（v2.13.10 与原型 meter/index.html 对齐）
/// 列表列：序号/房号/月份/冷水读数/热水读数/电表读数/冷水用量/热水用量/电表用量/抄表员/设备/抄表时间/状态/操作
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBasicsService _basics;

    public IndexModel(DormDbContext db, IBasicsService basics)
    {
        _db = db;
        _basics = basics;
    }

    public PagedResult<MeterRecordDto>? Result { get; set; }

    /// <summary>总数（供 PageHeader 组件使用）</summary>
    public int Total => Result?.TotalCount ?? 0;

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    [BindProperty(SupportsGet = true)]
    public string? ReadMonth { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? BuildingId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Operator { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    public List<string> Months { get; set; } = new();
    public List<Building> Buildings { get; set; } = new();

    public async Task OnGetAsync()
    {
        var existingMonths = await _db.MeterRecords
            .Select(r => r.ReadMonth)
            .Distinct()
            .OrderByDescending(m => m)
            .Take(12)
            .ToListAsync();

        var currentMonth = DateTime.Now.ToString("yyyy-MM");
        Months = existingMonths;
        if (!Months.Contains(currentMonth)) Months.Insert(0, currentMonth);

        var bld = await _basics.GetBuildingsAsync(null, 1, 100);
        Buildings = bld.Items.ToList();

        var query = _db.MeterRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(ReadMonth))
            query = query.Where(r => r.ReadMonth == ReadMonth);

        if (BuildingId.HasValue && BuildingId.Value > 0)
            query = query.Where(r => _db.Dorms.Any(d => d.Id == r.DormId && d.BuildingId == BuildingId.Value));

        if (!string.IsNullOrWhiteSpace(Operator))
            query = query.Where(r => r.Operator.Contains(Operator));

        if (Status.HasValue && Status.Value >= 0)
            query = query.Where(r => r.Status == Status.Value);

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var kw = Keyword.Trim();
            query = query.Where(r => r.DormCode.Contains(kw) || (r.Remark != null && r.Remark.Contains(kw)));
        }

        var totalCount = await query.CountAsync();
        var records = await query
            .OrderByDescending(r => r.ReadMonth)
            .ThenBy(r => r.DormCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // 计算用量：本月读数 − 上月读数（同房号）
        var items = new List<MeterRecordDto>();
        foreach (var r in records)
        {
            // 查询上月同房号抄表记录
            var prev = await _db.MeterRecords
                .Where(p => p.DormCode == r.DormCode && p.ReadMonth.CompareTo(r.ReadMonth) < 0)
                .OrderByDescending(p => p.ReadMonth)
                .FirstOrDefaultAsync();

            items.Add(new MeterRecordDto
            {
                Id = r.Id,
                DormId = r.DormId,
                DormCode = r.DormCode,
                ReadMonth = r.ReadMonth,
                ColdMeter = r.ColdMeter,
                HotMeter = r.HotMeter,
                ElectricMeter = r.ElectricMeter,
                ColdUsage = prev != null ? Math.Max(0, r.ColdMeter - prev.ColdMeter) : 0,
                HotUsage = prev != null ? Math.Max(0, r.HotMeter - prev.HotMeter) : 0,
                ElectricUsage = prev != null ? Math.Max(0, r.ElectricMeter - prev.ElectricMeter) : 0,
                Operator = r.Operator,
                DeviceSn = r.DeviceSn ?? "-",
                ServerCreatedAt = r.ServerCreatedAt,
                Status = r.Status,
                StatusName = r.GetStatusName(),
                Remark = r.Remark
            });
        }

        Result = new PagedResult<MeterRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }
}

/// <summary>抄表记录 DTO（v2.13.10 对齐原型 14 列）</summary>
public class MeterRecordDto
{
    public long Id { get; set; }
    public int DormId { get; set; }
    public string DormCode { get; set; } = "";
    public string ReadMonth { get; set; } = "";
    public decimal ColdMeter { get; set; }
    public decimal HotMeter { get; set; }
    public decimal ElectricMeter { get; set; }
    public decimal ColdUsage { get; set; }
    public decimal HotUsage { get; set; }
    public decimal ElectricUsage { get; set; }
    public string Operator { get; set; } = "";
    public string DeviceSn { get; set; } = "";
    public DateTime ServerCreatedAt { get; set; }
    public byte Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Remark { get; set; }
}