using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;
using DormManage.Admin.Pages.Shared;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 智能抄表页面模型（v2.13.10 与原型 meter/index.html 对齐）
/// 列表列：序号/房号/月份/冷水读数/热水读数/电表读数/冷水用量/热水用量/电表用量/抄表员/设备/抄表时间/状态/操作
/// </summary>
public class IndexModel : PaginatedPageModel
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

    // PageIndex / PageSize 继承自 v2.13.104 PaginatedPageModel 基类（含白名单校验）

    [BindProperty(SupportsGet = true)]
    public string? ReadMonth { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? BuildingId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Operator { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    public List<string> Months { get; set; } = new();
    public List<Building> Buildings { get; set; } = new();
    public List<string> DormCodes { get; set; } = new();

    /// <summary>
    /// v2.13.41 新增：抄表覆盖率统计（与原型 meter/index.html coverage alert 1:1 对齐）
    /// </summary>
    public CoverageDto Coverage { get; set; } = new();

    public async Task OnGetAsync()
    {
        EnsureValidPagination();  // v2.13.104
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

        DormCodes = await _db.Dorms
            .Where(d => d.IsActive)
            .OrderBy(d => d.DormCode)
            .Select(d => d.DormCode)
            .ToListAsync();

        var query = _db.MeterRecords.AsQueryable();

        // v2.13.214：默认筛选当前主机月份（避免「全部」返回大量历史数据）
        // URL 优先 → ReadMonth 不为空 → 尊重用户选择
        // URL 未传 → ReadMonth 为空 → 默认当前月份
        var effectiveMonth = !string.IsNullOrWhiteSpace(ReadMonth)
            ? ReadMonth
            : DateTime.Now.ToString("yyyy-MM");
        query = query.Where(r => r.ReadMonth == effectiveMonth);

        if (BuildingId.HasValue && BuildingId.Value > 0)
            query = query.Where(r => _db.Dorms.Any(d => d.Id == r.DormId && d.BuildingId == BuildingId.Value));

        if (!string.IsNullOrWhiteSpace(Operator))
            query = query.Where(r => r.Operator.Contains(Operator));

        if (Status.HasValue && Status.Value >= 0)
        {
            // v2.13.164 重定义 + v2.13.166 修正：状态筛选按值驱动 3 段
            // Status 0=未抄表 / 1=抄表中 / 2=已抄表
            // 在 SQL 侧翻译（不会先 Skip/Take 再过滤导致分页错位）
            query = Status.Value switch
            {
                2 => query.Where(r => r.ColdMeter > 0 && r.HotMeter > 0 && r.ElectricMeter > 0),
                1 => query.Where(r => (r.ColdMeter > 0 || r.HotMeter > 0 || r.ElectricMeter > 0)
                                  && !(r.ColdMeter > 0 && r.HotMeter > 0 && r.ElectricMeter > 0)),
                0 => query.Where(r => r.ColdMeter == 0 && r.HotMeter == 0 && r.ElectricMeter == 0),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(DormCode))
        {
            var dc = DormCode.Trim();
            query = query.Where(r => r.DormCode.Contains(dc));
        }

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var kw = Keyword.Trim();
            query = query.Where(r => (r.Remark != null && r.Remark.Contains(kw)) || r.DormCode.Contains(kw));
        }

        var totalCount = await query.CountAsync();
        var records = await query
            .OrderByDescending(r => r.ReadMonth)
            .ThenBy(r => r.DormCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // 计算用量：本月读数 − 上月读数（同房号）
        // v2.13.89：批量取所有抄表员的 SysUser.DisplayName（1 次 SQL JOIN，避免 N+1）
        var operatorUserIds = records.Where(r => r.OperatorUserId.HasValue).Select(r => r.OperatorUserId!.Value).Distinct().ToList();
        var operatorMap = await _db.SysUsers
            .Where(u => operatorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var items = new List<MeterRecordDto>();
        foreach (var r in records)
        {
            // 查询上月同房号智能抄表
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
                OperatorUserId = r.OperatorUserId,
                // v2.13.89：JOIN DisplayName 优先显示，回退到 Operator 字符串
                OperatorDisplayName = (r.OperatorUserId.HasValue && operatorMap.ContainsKey(r.OperatorUserId.Value))
                    ? operatorMap[r.OperatorUserId.Value]
                    : r.Operator,
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

        // v2.13.209 修复 CoverageDto 计算：应用 v2.13.164 值驱动 3 段逻辑 + GroupBy 去重不变量
        // 关键不变量：每月同房号去重仅留最新（Id DESC）
        var targetMonth = !string.IsNullOrWhiteSpace(ReadMonth) ? ReadMonth : DateTime.Now.ToString("yyyy-MM");
        var totalActiveDorms = await _db.Dorms.CountAsync(d => d.IsActive);

        // Step 1: 按 DormCode 分组取最新一条记录（v2.13.164 关键不变量）
        var latestRecords = await _db.MeterRecords
            .Where(r => r.ReadMonth == targetMonth)
            .GroupBy(r => r.DormCode)
            .Select(g => g.OrderByDescending(r => r.Id).First())
            .ToListAsync();

        // Step 2: 3 段值驱动判定（v2.13.164：与 DashboardService.cs 完全一致）
        // 已抄表：三表读数都 > 0
        var readDormCodes = latestRecords
            .Where(r => r.ColdMeter > 0 && r.HotMeter > 0 && r.ElectricMeter > 0)
            .Select(r => r.DormCode).ToHashSet();
        // 抄表中：至少一表 > 0 但不全 > 0
        var unfinishedDormCodes = latestRecords
            .Where(r => (r.ColdMeter > 0 || r.HotMeter > 0 || r.ElectricMeter > 0)
                     && !(r.ColdMeter > 0 && r.HotMeter > 0 && r.ElectricMeter > 0))
            .Select(r => r.DormCode).ToHashSet();
        // 未抄表：全 0 或当月无任何记录
        var allMeterCodes = latestRecords.Select(r => r.DormCode).ToHashSet();
        var allZeroDormCodes = latestRecords
            .Where(r => r.ColdMeter == 0 && r.HotMeter == 0 && r.ElectricMeter == 0)
            .Select(r => r.DormCode).ToHashSet();
        var noRecordDormCodes = await _db.Dorms
            .Where(d => d.IsActive && !allMeterCodes.Contains(d.DormCode))
            .Select(d => d.DormCode).ToListAsync();
        var uncoveredDormCodes = allZeroDormCodes
            .Concat(noRecordDormCodes).Distinct().ToHashSet();

        Coverage = new CoverageDto
        {
            TargetMonth = targetMonth,
            TotalDorms = totalActiveDorms,
            ReadDorms = readDormCodes.Count,
            UnfinishedDorms = unfinishedDormCodes.Count,   // 抄表中
            UncoveredDorms = uncoveredDormCodes.Count       // 未抄表
        };
    }

    /// <summary>
    /// v2.13.81 新增：手动补录 Modal 的「加载上月读数 + 检测已存在记录」AJAX 端点
    /// JS 在宿舍/月份变更时调用，返回 JSON 包含 prevCold/Hot/Electric、hasPrev、currentStatus 等
    /// </summary>
    public JsonResult OnGetLoadReadings([FromQuery] string? dormCode, [FromQuery] string? readMonth)
    {
        if (string.IsNullOrWhiteSpace(dormCode) || string.IsNullOrWhiteSpace(readMonth))
        {
            return new JsonResult(new { success = false, message = "参数不完整" });
        }

        var dormCodeTrim = dormCode.Trim();
        var readMonthTrim = readMonth.Trim();

        // 查询当前月已存在记录
        var current = _db.MeterRecords
            .Where(r => r.DormCode == dormCodeTrim && r.ReadMonth == readMonthTrim)
            .OrderByDescending(r => r.ServerCreatedAt)
            .FirstOrDefault();

        // 查询上月读数（按字符串 yyyy-MM 字典序比较）
        var prev = _db.MeterRecords
            .Where(r => r.DormCode == dormCodeTrim && r.ReadMonth.CompareTo(readMonthTrim) < 0)
            .OrderByDescending(r => r.ReadMonth)
            .FirstOrDefault();

        var hasPrev = prev != null;
        var isEffective = current != null && (current.Status == 1 || current.Status == 2);

        return new JsonResult(new
        {
            success = true,
            prevCold = prev?.ColdMeter ?? 0m,
            prevHot = prev?.HotMeter ?? 0m,
            prevElectric = prev?.ElectricMeter ?? 0m,
            prevReadMonth = prev?.ReadMonth ?? "",
            hasPrev,
            currentStatus = current?.Status ?? -1,
            currentRecordId = current?.Id ?? 0L,
            isEffective
        });
    }
}

/// <summary>
/// v2.13.41 新增，v2.13.209 修复：抄表覆盖率 DTO（与 v2.13.164 值驱动 3 段对齐）
/// 3 段：已抄表（Cold/Hot/Electric 全>0）/ 抄表中（至少一表>0 但不全>0）/ 未抄表（全0 或 无记录）
/// </summary>
public class CoverageDto
{
    public string TargetMonth { get; set; } = "";
    /// <summary>总宿舍数（IsActive=true）</summary>
    public int TotalDorms { get; set; }
    /// <summary>v2.13.209：已抄表数（ColdMeter>0 AND HotMeter>0 AND ElectricMeter>0 的 DormCode 去重数）</summary>
    public int ReadDorms { get; set; }
    /// <summary>v2.13.209：抄表中数（至少一表>0 但不全>0 的 DormCode 去重数）</summary>
    public int UnfinishedDorms { get; set; }
    /// <summary>v2.13.209：未抄表数（全0 或 当月无任何记录的 DormCode 去重数）</summary>
    public int UncoveredDorms { get; set; }
    /// <summary>覆盖率（已抄表 / 总宿舍 × 100，保留 1 位小数）</summary>
    public double Percentage => TotalDorms > 0 ? Math.Round(ReadDorms * 100.0 / TotalDorms, 1) : 0;
}

/// <summary>智能抄表 DTO（v2.13.10 对齐原型 14 列）</summary>
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
    // v2.13.89：抄表员 UserId FK + DisplayName（JOIN 派生）
    public int? OperatorUserId { get; set; }
    public string? OperatorDisplayName { get; set; }
    public string DeviceSn { get; set; } = "";
    public DateTime ServerCreatedAt { get; set; }
    public byte Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Remark { get; set; }
}