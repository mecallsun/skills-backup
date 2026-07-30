using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.Shared.Data;
using Microsoft.EntityFrameworkCore;
using DormManage.Admin.Pages.Shared;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 住宿档案页面模型（v2.13.10 与原型 dorms/list.html 对齐）
/// 筛选条件：楼栋/楼层/状态/关键词（房号/地址）
/// </summary>
public class IndexModel : PaginatedPageModel
{
    private readonly DormDbContext _db;
    private readonly IBasicsService _basicsService;

    public IndexModel(DormDbContext db, IBasicsService basicsService)
    {
        _db = db;
        _basicsService = basicsService;
    }

    /// <summary>住宿列表</summary>
    public PagedResult<DormDto>? Result { get; set; }

    /// <summary>总数（供 PageHeader 组件使用）</summary>
    public int Total => Result?.TotalCount ?? 0;

    // PageIndex / PageSize 继承自 v2.13.104 PaginatedPageModel 基类（含白名单校验）

    /// <summary>楼栋ID</summary>
    [BindProperty(SupportsGet = true)]
    public int? BuildingId { get; set; }

    /// <summary>楼层号（v2.13.10 改为直接 FloorNo int 输入框）</summary>
    [BindProperty(SupportsGet = true)]
    public int? FloorNo { get; set; }

    /// <summary>状态（启用/停用）</summary>
    [BindProperty(SupportsGet = true)]
    public bool? IsActive { get; set; }

    /// <summary>关键词（房号/地址模糊匹配）</summary>
    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    /// <summary>楼栋列表</summary>
    public List<Building> Buildings { get; set; } = new();

    public async Task OnGetAsync()
    {
        EnsureValidPagination();  // v2.13.104
        // 加载基础资料 - 仅楼栋（v2.13.10 简化为 4 项筛选）
        var buildings = await _basicsService.GetBuildingsAsync(null, 1, 100);
        Buildings = buildings.Items.ToList();

        // 查询住宿列表
        var query = _db.Dorms.AsQueryable();

        if (BuildingId.HasValue && BuildingId.Value > 0)
            query = query.Where(d => d.BuildingId == BuildingId.Value);

        // v2.13.10：楼层号筛选（简化：与 Dorm.FloorId 关联，FloorId 直接当楼层号使用）
        if (FloorNo.HasValue && FloorNo.Value > 0)
            query = query.Where(d => d.FloorId == FloorNo.Value);

        if (IsActive.HasValue)
            query = query.Where(d => d.IsActive == IsActive.Value);

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var kw = Keyword.Trim();
            query = query.Where(d => d.DormCode.Contains(kw) || (d.AddressText != null && d.AddressText.Contains(kw)));
        }

        var totalCount = await query.CountAsync();
        var dormList = await query
            .OrderBy(d => d.DormCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // v2.13.85 派生性别（基于当前在宿人员实时计算）
        // JOIN DormBookings(Status=2) → SysEmployee 拿男女人数
        var dormCodes = dormList.Select(d => d.DormCode).ToList();
        var genderStats = await _db.DormBookings
            .Where(b => dormCodes.Contains(b.DormCode) && b.Status == 2)
            .Join(_db.Employees.AsNoTracking(),
                  b => b.EmployeeId, e => e.Id,
                  (b, e) => new { b.DormCode, e.Gender })
            .GroupBy(x => x.DormCode)
            .Select(g => new
            {
                DormCode = g.Key,
                MaleCount = g.Count(x => x.Gender == 1),
                FemaleCount = g.Count(x => x.Gender == 2)
            })
            .ToDictionaryAsync(x => x.DormCode);

        // v2.13.95 派生班次（基于当前在宿人员的 AttendanceTypeId 去重集合）
        // JOIN DormBookings(Status=2) → SysEmployee → AttendanceType，按 DormCode + AttendanceTypeId DISTINCT
        var attendanceStats = await _db.DormBookings
            .Where(b => dormCodes.Contains(b.DormCode) && b.Status == 2)
            .Join(_db.Employees.AsNoTracking(),
                  b => b.EmployeeId, e => e.Id,
                  (b, e) => new { b.DormCode, e.AttendanceTypeId })
            .Where(x => x.AttendanceTypeId.HasValue)
            .Join(_db.AttendanceTypes.AsNoTracking(),
                  x => x.AttendanceTypeId, t => t.Id,
                  (x, t) => new { x.DormCode, TypeId = t.Id, TypeName = t.Name })
            .GroupBy(x => x.DormCode)
            .Select(g => new
            {
                DormCode = g.Key,
                // 按 AttendanceType.Id 升序拼接去重班次名（AttendanceType 无 SortOrder，用 Id 保稳定）
                TypeNames = g.OrderBy(x => x.TypeId).Select(x => x.TypeName).Distinct().ToList()
            })
            .ToDictionaryAsync(x => x.DormCode, x => x.TypeNames);

        // v2.13.111 派生班组（基于当前在宿员工的 TeamId FK → Team.Name，去重 + 按 SortOrder 升序）
        // JOIN DormBookings(Status=2) → SysEmployee(TeamId) → Team(Name + SortOrder)
        var teamStats = await _db.DormBookings
            .Where(b => dormCodes.Contains(b.DormCode) && b.Status == 2)
            .Join(_db.Employees.AsNoTracking(),
                  b => b.EmployeeId, e => e.Id,
                  (b, e) => new { b.DormCode, e.TeamId })
            .Join(_db.Teams.AsNoTracking(),
                  x => x.TeamId, t => t.Id,
                  (x, t) => new { x.DormCode, TeamId = t.Id, TeamName = t.Name, SortOrder = t.SortOrder })
            .GroupBy(x => x.DormCode)
            .Select(g => new
            {
                DormCode = g.Key,
                // 按 Team.SortOrder 升序 + 去重（同 TeamId 仅保留第一个）
                TeamNames = g.OrderBy(x => x.SortOrder).ThenBy(x => x.TeamId)
                             .Select(x => x.TeamName).Distinct().ToList()
            })
            .ToDictionaryAsync(x => x.DormCode, x => x.TeamNames);

        var items = dormList.Select(d => new DormDto
        {
            Id = d.Id,
            DormCode = d.DormCode,
            BuildingName = d.BuildingName ?? "",
            FloorNo = d.FloorId,
            AddressText = d.AddressText ?? "",
            RoomCount = d.RoomCount,
            Capacity = d.Capacity,
            Gender = d.Gender,
            CurrentCount = _db.DormBookings.Count(b => b.DormCode == d.DormCode && b.Status == 2),
            IsActive = d.IsActive,
            HasBookingHistory = _db.DormBookings.Any(b => b.DormCode == d.DormCode),
            // v2.13.85 派生性别字段
            MaleCount = genderStats.GetValueOrDefault(d.DormCode)?.MaleCount ?? 0,
            FemaleCount = genderStats.GetValueOrDefault(d.DormCode)?.FemaleCount ?? 0,
            // v2.13.95 派生班次字段（按 AttendanceType.SortOrder 升序排列）
            AttendanceTypeNames = attendanceStats.GetValueOrDefault(d.DormCode) ?? new List<string>(),
            // v2.13.111 派生班组字段（按 Team.SortOrder 升序 + 去重）
            TeamNames = teamStats.GetValueOrDefault(d.DormCode) ?? new List<string>()
        }).ToList();

        // v2.13.85 计算 EffectiveGender 派生：男>0=1 / 女>0=2 / 空房间=0
        foreach (var item in items)
        {
            if (item.MaleCount > 0 && item.FemaleCount > 0)
            {
                // 极端情况（理论上 v2.13.84 后不会发生）→ 取多数
                item.EffectiveGender = item.MaleCount >= item.FemaleCount ? 1 : 2;
            }
            else if (item.MaleCount > 0) item.EffectiveGender = 1;
            else if (item.FemaleCount > 0) item.EffectiveGender = 2;
            else item.EffectiveGender = 0;

            // v2.12.41 计算 CanDelete：仅当当前无在宿人员 且 无办理登记历史时才允许删除
            item.CanDelete = (item.CurrentCount == 0 && !item.HasBookingHistory);
        }

        Result = new PagedResult<DormDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }

    /// <summary>删除住宿（v2.12.41 删除约束）</summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
        {
            TempData["ErrorMessage"] = "住宿不存在";
            return RedirectToPage("/Dorms/Index");
        }
        // 校验：在宿人数
        var current = await _db.DormBookings.CountAsync(b => b.DormCode == dorm.DormCode && b.Status == 2);
        if (current > 0)
        {
            TempData["ErrorMessage"] = $"该住宿当前有 {current} 人在宿，禁止删除";
            return RedirectToPage("/Dorms/Index");
        }
        // 校验：办理登记历史
        var hasHistory = await _db.DormBookings.AnyAsync(b => b.DormCode == dorm.DormCode);
        if (hasHistory)
        {
            TempData["ErrorMessage"] = $"该住宿 \"{dorm.DormCode}\" 有历史办理登记记录，禁止删除";
            return RedirectToPage("/Dorms/Index");
        }
        _db.Dorms.Remove(dorm);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"住宿 {dorm.DormCode} 已删除";
        return RedirectToPage("/Dorms/Index");
    }
}

/// <summary>住宿数据传输对象（v2.13.10 字段对齐原型：房号/楼栋/楼层/地址/房间数/容量/在住人数/使用率/状态）</summary>
public class DormDto
{
    public int Id { get; set; }
    public string DormCode { get; set; } = "";
    public string BuildingName { get; set; } = "";
    public int FloorNo { get; set; }
    public string AddressText { get; set; } = "";
    public int RoomCount { get; set; }
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public int CurrentCount { get; set; }
    public bool IsActive { get; set; }
    /// <summary>是否存在办理登记历史（v2.12.41）</summary>
    public bool HasBookingHistory { get; set; }
    /// <summary>是否可删除（v2.12.41）</summary>
    public bool CanDelete { get; set; }

    // ========== v2.13.95 派生班次字段 ==========
    /// <summary>当前在宿员工的考勤班次名称集合（按 AttendanceType.SortOrder 升序 + 去重）</summary>
    public List<string> AttendanceTypeNames { get; set; } = new();
    /// <summary>渲染为「早班/中班」格式字符串（前端直接展示）</summary>
    public string AttendanceTypeNamesText => string.Join("/", AttendanceTypeNames);

    // ========== v2.13.111 派生班组字段 ==========
    /// <summary>当前在宿员工的班组名称集合（按 Team.SortOrder 升序 + 去重）。
    /// 数据关系：DormBooking (Status=2 在宿) → SysEmployee (TeamId FK) → Team (Name + SortOrder)。</summary>
    public List<string> TeamNames { get; set; } = new();
    /// <summary>渲染为「一班/三班」格式字符串（前端直接展示）</summary>
    public string TeamNamesText => string.Join("/", TeamNames);

    // ========== v2.13.85 派生性别字段 ==========
    /// <summary>当前在宿男员工数（实时派生）</summary>
    public int MaleCount { get; set; }
    /// <summary>当前在宿女员工数（实时派生）</summary>
    public int FemaleCount { get; set; }
    /// <summary>派生性别（v2.13.85）：男>0=1 / 女>0=2 / 空房间=0（不限）</summary>
    public int EffectiveGender { get; set; }
    /// <summary>派生性别中文名（"男" / "女" / "无"）</summary>
    public string EffectiveGenderName => EffectiveGender == 1 ? "男" : EffectiveGender == 2 ? "女" : "无";
}