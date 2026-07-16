using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Shared.Services;

/// <summary>
/// 费用管理服务：费用标准 CRUD + 宿舍账单生成 + 员工分摊计算
/// </summary>
public interface IBillingService
{
    /// <summary>获取当前有效费用标准</summary>
    Task<BillingStandard?> GetActiveStandardAsync();

    /// <summary>获取费用标准列表（分页）</summary>
    Task<PagedResult<BillingStandard>> GetStandardsAsync(int page, int pageSize);

    /// <summary>创建/更新费用标准</summary>
    Task<(bool ok, string message)> SaveStandardAsync(BillingStandard standard);

    /// <summary>生成指定月份的宿舍账单</summary>
    Task<BillingGenerateResult> GenerateDormBillsAsync(string billingMonth);

    /// <summary>查询宿舍账单列表</summary>
    Task<PagedResult<DormBilling>> GetDormBillsAsync(string? billingMonth, string? dormCode, int page, int pageSize);

    /// <summary>生成指定月份的员工分摊账单</summary>
    Task<BillingGenerateResult> GenerateEmployeeBillsAsync(string billingMonth);

    /// <summary>查询员工账单列表</summary>
    Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int page, int pageSize);

    /// <summary>发布宿舍账单</summary>
    Task<(bool ok, string message)> PublishDormBillsAsync(string billingMonth);

    /// <summary>发布员工账单</summary>
    Task<(bool ok, string message)> PublishEmployeeBillsAsync(string billingMonth);
}

public class BillingGenerateResult
{
    public int GeneratedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class BillingService : IBillingService
{
    private readonly DormDbContext _db;

    public BillingService(DormDbContext db) => _db = db;

    public async Task<BillingStandard?> GetActiveStandardAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await _db.BillingStandards
            .FirstOrDefaultAsync(s => s.IsActive
                && s.EffectiveFrom <= today
                && (s.EffectiveTo == null || s.EffectiveTo >= today));
    }

    public async Task<PagedResult<BillingStandard>> GetStandardsAsync(int page, int pageSize)
    {
        var query = _db.BillingStandards.OrderByDescending(s => s.IsActive).ThenByDescending(s => s.EffectiveFrom);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<BillingStandard> { Items = items, Total = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<(bool ok, string message)> SaveStandardAsync(BillingStandard standard)
    {
        if (string.IsNullOrWhiteSpace(standard.StandardName))
            return (false, "标准名称必填");
        if (standard.EffectiveFrom == null)
            return (false, "生效日期必填");

        if (standard.IsActive)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var existingActive = await _db.BillingStandards
                .AnyAsync(s => s.Id != standard.Id && s.IsActive
                    && s.EffectiveFrom <= today
                    && (s.EffectiveTo == null || s.EffectiveTo >= today));
            if (existingActive)
                return (false, "该时段已存在启用中的费用标准");
        }

        if (standard.Id > 0)
        {
            var existing = await _db.BillingStandards.FindAsync(standard.Id);
            if (existing == null) return (false, "记录不存在");
            existing.StandardName = standard.StandardName;
            existing.EffectiveFrom = standard.EffectiveFrom;
            existing.EffectiveTo = standard.EffectiveTo;
            existing.HotWaterUnitPrice = standard.HotWaterUnitPrice;
            existing.ColdWaterUnitPrice = standard.ColdWaterUnitPrice;
            existing.ElectricUnitPrice = standard.ElectricUnitPrice;
            existing.ApplicableType = standard.ApplicableType;
            existing.IsActive = standard.IsActive;
            return (true, "更新成功");
        }
        else
        {
            standard.CreatedAt = DateTime.Now;
            _db.BillingStandards.Add(standard);
            await _db.SaveChangesAsync();
            return (true, "新增成功");
        }
    }

    public async Task<BillingGenerateResult> GenerateDormBillsAsync(string billingMonth)
    {
        var result = new BillingGenerateResult();
        var standard = await GetActiveStandardAsync();
        if (standard == null)
        {
            result.Errors.Add("没有可用的费用标准，请先创建费用标准");
            return result;
        }

        var records = await _db.MeterRecords
            .Where(r => r.ReadMonth == billingMonth && r.Status == 1)
            .OrderBy(r => r.DormCode)
            .ToListAsync();

        if (!records.Any())
        {
            result.Errors.Add($"该月份({billingMonth})没有正常的抄表记录");
            return result;
        }

        foreach (var rec in records)
        {
            try
            {
                var allRecords = await _db.MeterRecords
                    .Where(r => r.DormCode == rec.DormCode && r.Status == 1)
                    .OrderByDescending(r => r.ReadMonth)
                    .ToListAsync();
                var prevRecord = allRecords.Skip(1).FirstOrDefault();

                decimal coldUsage = Math.Max(0, rec.ColdMeter - (prevRecord?.ColdMeter ?? 0));
                decimal hotUsage = Math.Max(0, rec.HotMeter - (prevRecord?.HotMeter ?? 0));
                decimal electricUsage = Math.Max(0, rec.ElectricMeter - (prevRecord?.ElectricMeter ?? 0));

                decimal coldAmount = Math.Round(coldUsage * standard.ColdWaterUnitPrice, 2);
                decimal hotAmount = Math.Round(hotUsage * standard.HotWaterUnitPrice, 2);
                decimal electricAmount = Math.Round(electricUsage * standard.ElectricUnitPrice, 2);
                decimal totalAmount = coldAmount + hotAmount + electricAmount;

                var residentCount = await _db.DormBookings
                    .CountAsync(b => b.DormCode == rec.DormCode
                        && b.BookingDate <= DateOnly.ParseExact(billingMonth + "-01", "yyyy-MM-dd")
                        && (b.Status == (int)BookingStatus.Staying
                            || (b.Type == (int)BookingType.CheckIn && b.Status != (int)BookingStatus.CheckedOut)));

                var existing = await _db.DormBillings
                    .FirstOrDefaultAsync(d => d.DormCode == rec.DormCode && d.BillingMonth == billingMonth);

                if (existing != null)
                {
                    existing.ColdUsage = coldUsage;
                    existing.HotUsage = hotUsage;
                    existing.ElectricityUsage = electricUsage;
                    existing.ColdAmount = coldAmount;
                    existing.HotAmount = hotAmount;
                    existing.ElectricityAmount = electricAmount;
                    existing.TotalAmount = totalAmount;
                    existing.ResidentCount = residentCount;
                    existing.BillingStandardId = standard.Id;
                    result.UpdatedCount++;
                }
                else
                {
                    _db.DormBillings.Add(new DormBilling
                    {
                        DormId = rec.DormId,
                        DormCode = rec.DormCode,
                        BillingMonth = billingMonth,
                        ColdUsage = coldUsage,
                        HotUsage = hotUsage,
                        ElectricityUsage = electricUsage,
                        ColdAmount = coldAmount,
                        HotAmount = hotAmount,
                        ElectricityAmount = electricAmount,
                        TotalAmount = totalAmount,
                        ResidentCount = residentCount,
                        BillingStandardId = standard.Id,
                        GeneratedAt = DateTime.Now,
                        GeneratedBy = "系统",
                        IsPublished = false
                    });
                    result.GeneratedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{rec.DormCode}: {ex.Message}");
                result.SkippedCount++;
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<PagedResult<DormBilling>> GetDormBillsAsync(string? billingMonth, string? dormCode, int page, int pageSize)
    {
        var query = _db.DormBillings.AsQueryable();
        if (!string.IsNullOrWhiteSpace(billingMonth))
            query = query.Where(d => d.BillingMonth == billingMonth);
        if (!string.IsNullOrWhiteSpace(dormCode))
            query = query.Where(d => d.DormCode.Contains(dormCode));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.BillingMonth)
            .ThenBy(d => d.DormCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<DormBilling>
        {
            Items = items,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<BillingGenerateResult> GenerateEmployeeBillsAsync(string billingMonth)
    {
        var result = new BillingGenerateResult();

        var dormBills = await _db.DormBillings
            .Where(d => d.BillingMonth == billingMonth && d.IsPublished)
            .ToListAsync();

        if (!dormBills.Any())
        {
            result.Errors.Add("没有已发布的宿舍账单，请先生成宿舍账单并发布");
            return result;
        }

        foreach (var bill in dormBills)
        {
            try
            {
                var stayingEmployees = await _db.DormBookings
                    .Where(b => b.DormCode == bill.DormCode && b.Status == (int)BookingStatus.Staying)
                    .Select(b => b.EmployeeId)
                    .Distinct()
                    .ToListAsync();

                if (!stayingEmployees.Any()) continue;

                var shareRatio = 1m / stayingEmployees.Count;

                foreach (var empId in stayingEmployees)
                {
                    var emp = await _db.Employees.FindAsync(empId);
                    if (emp == null) continue;

                    var existing = await _db.EmployeeBillings
                        .FirstOrDefaultAsync(e => e.EmployeeId == empId && e.BillingMonth == billingMonth && e.DormBillId == bill.Id);

                    var coldShare = Math.Round(bill.ColdAmount * shareRatio, 2);
                    var hotShare = Math.Round(bill.HotAmount * shareRatio, 2);
                    var elecShare = Math.Round(bill.ElectricityAmount * shareRatio, 2);
                    var totalShare = Math.Round(coldShare + hotShare + elecShare, 2);

                    if (existing != null)
                    {
                        existing.ShareRatio = shareRatio;
                        existing.ResidentCount = stayingEmployees.Count;
                        existing.ColdShareAmount = coldShare;
                        existing.HotShareAmount = hotShare;
                        existing.ElectricityShareAmount = elecShare;
                        existing.TotalShareAmount = totalShare;
                        result.UpdatedCount++;
                    }
                    else
                    {
                        _db.EmployeeBillings.Add(new EmployeeBilling
                        {
                            EmployeeId = empId,
                            EmployeeCode = emp.EmployeeCode,
                            EmployeeName = emp.RealName,
                            DormCode = emp.DormCode,
                            BillingMonth = billingMonth,
                            ShareRatio = shareRatio,
                            ResidentCount = stayingEmployees.Count,
                            ColdShareAmount = coldShare,
                            HotShareAmount = hotShare,
                            ElectricityShareAmount = elecShare,
                            TotalShareAmount = totalShare,
                            DormBillId = bill.Id,
                            GeneratedAt = DateTime.Now,
                            IsPublished = false
                        });
                        result.GeneratedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{bill.DormCode}: {ex.Message}");
                result.SkippedCount++;
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int page, int pageSize)
    {
        var query = _db.EmployeeBillings.AsQueryable();
        if (!string.IsNullOrWhiteSpace(billingMonth))
            query = query.Where(e => e.BillingMonth == billingMonth);
        if (!string.IsNullOrWhiteSpace(dormCode))
            query = query.Where(e => e.DormCode != null && e.DormCode.Contains(dormCode));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.BillingMonth)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<EmployeeBilling>
        {
            Items = items,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool ok, string message)> PublishDormBillsAsync(string billingMonth)
    {
        var count = await _db.DormBillings
            .Where(d => d.BillingMonth == billingMonth && !d.IsPublished)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.IsPublished, true));

        if (count == 0)
            return (false, $"该月份({billingMonth})没有未发布的账单");
        return (true, $"已发布 {count} 条宿舍账单");
    }

    public async Task<(bool ok, string message)> PublishEmployeeBillsAsync(string billingMonth)
    {
        var count = await _db.EmployeeBillings
            .Where(e => e.BillingMonth == billingMonth && !e.IsPublished)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsPublished, true));

        if (count == 0)
            return (false, $"该月份({billingMonth})没有未发布的账单");
        return (true, $"已发布 {count} 条员工账单");
    }
}
