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

    /// <summary>v2.13.61 新增：获取所有启用的员工类型（基础资料真源）</summary>
    Task<List<EmployeeType>> GetEmployeeTypesAsync();

    /// <summary>获取费用标准列表（分页）</summary>
    Task<PagedResult<BillingStandard>> GetStandardsAsync(int page, int pageSize);
    Task<PagedResult<BillingStandard>> GetStandardsAsync(string? keyword, string? isActive, int page, int pageSize);
    /// <summary>v2.13.20 新增适用类型筛选</summary>
    Task<PagedResult<BillingStandard>> GetStandardsAsync(string? keyword, string? applicableType, string? isActive, int page, int pageSize);
    /// <summary>v2.13.20 获取费用标准适用类型去重列表</summary>
    Task<List<string>> GetStandardApplicableTypesAsync();

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
    /// <summary>v2.13.20 查询员工账单列表（新增部门/员工类型/住宿状态筛选）</summary>
    Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int? departmentId, int? employeeTypeId, int? residenceStatusId, int page, int pageSize);
    /// <summary>v2.13.44 查询员工账单列表（新增在职状态筛选）</summary>
    Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int? departmentId, int? employeeTypeId, int? residenceStatusId, int? employmentStatusId, int page, int pageSize);

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
        => await GetStandardsAsync(null, null, null, page, pageSize);

    public async Task<PagedResult<BillingStandard>> GetStandardsAsync(string? keyword, string? isActive, int page, int pageSize)
        => await GetStandardsAsync(keyword, null, isActive, page, pageSize);

    public async Task<PagedResult<BillingStandard>> GetStandardsAsync(string? keyword, string? applicableType, string? isActive, int page, int pageSize)
    {
        var query = _db.BillingStandards.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(s => s.StandardName.Contains(keyword));

        if (!string.IsNullOrWhiteSpace(applicableType))
            query = query.Where(s => s.ApplicableType != null && s.ApplicableType.Contains(applicableType));

        if (isActive == "true")
            query = query.Where(s => s.IsActive);
        else if (isActive == "false")
            query = query.Where(s => !s.IsActive);

        query = query.OrderByDescending(s => s.IsActive).ThenByDescending(s => s.EffectiveFrom);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<BillingStandard> { Items = items, Total = total, PageIndex = page, PageSize = pageSize };
    }

    /// <summary>
    /// v2.13.61 修复：返回所有启用的员工类型字典（供费用标准 Edit/Create 页面下拉用）
    /// 真源：EmployeeType 表（基础资料模块）
    /// </summary>
    public async Task<List<EmployeeType>> GetEmployeeTypesAsync()
    {
        return await _db.EmployeeTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
    }

    public async Task<List<string>> GetStandardApplicableTypesAsync()
    {
        var fromDb = await _db.BillingStandards
            .Where(s => !string.IsNullOrEmpty(s.ApplicableType))
            .Select(s => s.ApplicableType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        // v2.13.61 修复：适用员工类型选项改为从 EmployeeType 真源表读取（FK 关联），不再硬编码
        var fromDict = await _db.EmployeeTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => t.Name)
            .ToListAsync();

        var combined = fromDict.Union(fromDb, StringComparer.OrdinalIgnoreCase).ToList();
        return combined;
    }

    public async Task<(bool ok, string message)> SaveStandardAsync(BillingStandard standard)
    {
        if (string.IsNullOrWhiteSpace(standard.StandardName))
            return (false, "标准名称必填");
        if (standard.EffectiveFrom == default)
            return (false, "生效日期必填");

        // v2.13.42 BUG 修复：日期校验逻辑反转 — 应该是 EffectiveTo < EffectiveFrom 时拒绝
        if (standard.EffectiveTo.HasValue && standard.EffectiveTo.Value < standard.EffectiveFrom)
            return (false, "结束日期不能早于开始日期");

        // v2.13.61 修复：适用员工类型 FK 校验 — 通过 ApplicableTypeId 查 EmployeeType 真源表
        if (standard.ApplicableTypeId <= 0)
            return (false, "适用员工类型必填");
        var employeeType = await _db.EmployeeTypes.FindAsync(standard.ApplicableTypeId);
        if (employeeType == null || !employeeType.IsActive)
            return (false, "所选员工类型不存在或已停用");

        if (standard.IsActive)
        {
            var newStart = standard.EffectiveFrom;
            // v2.13.63 修复：仅对【同员工类型 + 已生效】的记录做时段重叠检查；
            // 之前实现不分类型，导致不同员工类型的标准被错误拒绝（用户报告"启用此标准勾选没有生效"）。
            // 重叠定义：newStart <= existingEnd AND existingStart <= newEnd；
            // 任一端为 null（永久有效）视为无穷大，比较时跳过对应约束。
            var hasOverlap = await _db.BillingStandards
                .AnyAsync(s => s.Id != standard.Id
                    && s.IsActive
                    && s.ApplicableTypeId == standard.ApplicableTypeId
                    && (standard.EffectiveTo == null || s.EffectiveFrom <= standard.EffectiveTo)
                    && (s.EffectiveTo == null || newStart <= s.EffectiveTo));
            if (hasOverlap)
                return (false, "该员工类型在此时段已存在启用中的费用标准");
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
            // v2.13.93 新增：补贴标准持久化
            existing.SubsidyAmount = standard.SubsidyAmount;
            // v2.13.61 修复：适用员工类型改为 FK 关联 + 自动写入冗余 Name 字段
            existing.ApplicableTypeId = standard.ApplicableTypeId;
            existing.ApplicableType = employeeType.Name;
            existing.IsActive = standard.IsActive;
            existing.UpdatedAt = DateTime.Now;  // v2.13.61 强制刷新更新时间
            // v2.13.42 BUG 修复：更新分支必须调用 SaveChangesAsync 才会真正持久化
            await _db.SaveChangesAsync();
            return (true, "更新成功");
        }
        else
        {
            // v2.13.61 修复：新增时同样写入 FK + 冗余 Name
            standard.ApplicableType = employeeType.Name;
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
            result.Errors.Add($"该月份({billingMonth})没有正常的智能抄表记录");
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

        // v2.13.93: 解析当月第一天 + 当月天数
        var monthFirstDay = DateOnly.ParseExact(billingMonth + "-01", "yyyy-MM-dd");
        var monthDays = DateTime.DaysInMonth(monthFirstDay.Year, monthFirstDay.Month);

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

                    // v2.13.93: 计算该员工在当月该房间的实际入住天数（与本月天数取 min）
                    var stayDays = await ComputeEmployeeStayDaysAsync(empId, bill.DormCode, monthFirstDay, monthDays);

                    // v2.13.93: 取该员工类型生效中的费用标准（按 ApplicableTypeId + EffectiveFrom/To 匹配 monthFirstDay）
                    var standard = await _db.BillingStandards.FirstOrDefaultAsync(s =>
                        s.IsActive
                        && s.EffectiveFrom <= monthFirstDay
                        && (s.EffectiveTo == null || s.EffectiveTo >= monthFirstDay)
                        && s.ApplicableTypeId == emp.EmployeeTypeId);

                    // v2.13.93: 补贴 = 标准月补贴 × 入住天数 / 当月天数（四舍五入到分）
                    decimal subsidyAmount = 0m;
                    if (standard != null && standard.SubsidyAmount > 0 && stayDays > 0)
                    {
                        subsidyAmount = Math.Round(standard.SubsidyAmount * stayDays / monthDays, 2);
                    }

                    if (existing != null)
                    {
                        existing.ShareRatio = shareRatio;
                        existing.ResidentCount = stayingEmployees.Count;
                        existing.ColdShareAmount = coldShare;
                        existing.HotShareAmount = hotShare;
                        existing.ElectricityShareAmount = elecShare;
                        existing.TotalShareAmount = totalShare;
                        // v2.13.93: 覆盖补贴金额、住宿天数、部门冗余（财务手工调整过的账单项不会被覆盖：检查 IsPublished）
                        if (!existing.IsPublished)
                        {
                            existing.SubsidyAmount = subsidyAmount;
                            existing.Days = stayDays;
                            existing.Department = emp.Department;
                        }
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
                            // v2.13.93 新增字段
                            SubsidyAmount = subsidyAmount,
                            Days = stayDays,
                            Department = emp.Department,
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

    /// <summary>
    /// v2.13.93 新增：计算员工在某房间当月实际入住天数
    /// 算法：累计 [ActualCheckInDate, ActualCheckOutDate or month-end] 与 [month-start, month-end] 的交集天数（上限 = monthDays）
    /// DormBooking 含 ActualCheckInDate / ActualCheckOutDate 字段（v2.13.24 P75 业务深度字段）
    /// </summary>
    private async Task<int> ComputeEmployeeStayDaysAsync(int employeeId, string dormCode, DateOnly monthFirstDay, int monthDays)
    {
        var monthStart = monthFirstDay.ToDateTime(TimeOnly.MinValue);
        var monthEnd = monthFirstDay.AddDays(monthDays - 1).ToDateTime(TimeOnly.MaxValue);

        var bookings = await _db.DormBookings
            .Where(b => b.EmployeeId == employeeId
                && b.DormCode == dormCode
                && b.BookingDate <= monthFirstDay.AddDays(monthDays - 1)
                && (b.ActualCheckOutDate == null || b.ActualCheckOutDate >= monthFirstDay)
                && (b.Status == (int)BookingStatus.Staying || b.Type == (int)BookingType.CheckIn))
            .Select(b => new { b.ActualCheckInDate, b.ActualCheckOutDate, b.BookingDate })
            .ToListAsync();

        if (!bookings.Any()) return 0;

        int total = 0;
        foreach (var b in bookings)
        {
            // 优先用 ActualCheckInDate；如未填则回退到 BookingDate
            var startDate = b.ActualCheckInDate ?? b.BookingDate;
            var start = startDate.ToDateTime(TimeOnly.MinValue);
            var end = b.ActualCheckOutDate?.ToDateTime(TimeOnly.MaxValue) ?? monthEnd;
            if (start < monthStart) start = monthStart;
            if (end > monthEnd) end = monthEnd;
            if (end >= start)
            {
                var days = (int)Math.Ceiling((end - start).TotalDays);
                if (days > 0) total += days;
            }
        }
        return Math.Min(total, monthDays);
    }

    public async Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int page, int pageSize)
        => await GetEmployeeBillsAsync(billingMonth, dormCode, empKeyword, null, null, null, null, page, pageSize);

    public async Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int? departmentId, int? employeeTypeId, int? residenceStatusId, int page, int pageSize)
        => await GetEmployeeBillsAsync(billingMonth, dormCode, empKeyword, departmentId, employeeTypeId, residenceStatusId, null, page, pageSize);

    public async Task<PagedResult<EmployeeBilling>> GetEmployeeBillsAsync(string? billingMonth, string? dormCode, string? empKeyword, int? departmentId, int? employeeTypeId, int? residenceStatusId, int? employmentStatusId, int page, int pageSize)
    {
        var query = _db.EmployeeBillings.AsQueryable();
        if (!string.IsNullOrWhiteSpace(billingMonth))
            query = query.Where(e => e.BillingMonth == billingMonth);
        if (!string.IsNullOrWhiteSpace(dormCode))
            query = query.Where(e => e.DormCode != null && e.DormCode.Contains(dormCode));

        if (departmentId.HasValue || employeeTypeId.HasValue || residenceStatusId.HasValue || employmentStatusId.HasValue || !string.IsNullOrWhiteSpace(empKeyword))
        {
            var employeeIds = await _db.Employees
                .Where(e =>
                    (!departmentId.HasValue || e.DepartmentId == departmentId.Value) &&
                    (!employeeTypeId.HasValue || e.EmployeeTypeId == employeeTypeId.Value) &&
                    (!residenceStatusId.HasValue || e.ResidenceStatusId == residenceStatusId.Value) &&
                    // v2.13.44: 在职状态筛选（EmploymentStatusId）
                    (!employmentStatusId.HasValue || e.EmploymentStatusId == employmentStatusId.Value) &&
                    (string.IsNullOrWhiteSpace(empKeyword) ||
                     e.EmployeeCode.Contains(empKeyword) ||
                     e.RealName.Contains(empKeyword) ||
                     (e.Phone != null && e.Phone.Contains(empKeyword))))
                .Select(e => e.Id)
                .ToListAsync();

            query = query.Where(e => employeeIds.Contains(e.EmployeeId));
        }

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
