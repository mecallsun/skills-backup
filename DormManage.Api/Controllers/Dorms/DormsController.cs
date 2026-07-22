using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Api.Controllers.Dorms;

/// <summary>
/// 宿舍管理 API 控制器
/// </summary>
[ApiController]
[Route("api/dorms")]
public class DormsController : ControllerBase
{
    private readonly DormDbContext _db;
    private readonly IBasicsService _basicsService;

    public DormsController(DormDbContext db, IBasicsService basicsService)
    {
        _db = db;
        _basicsService = basicsService;
    }

    /// <summary>
    /// 获取宿舍列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<DormDto>>> GetDorms(
        string? dormCode,
        int? buildingId,
        int? floorId,
        int? addressId,
        int page = 1,
        int pageSize = 10)
    {
        var query = _db.Dorms.AsQueryable();

        if (!string.IsNullOrWhiteSpace(dormCode))
            query = query.Where(d => d.DormCode.Contains(dormCode));

        if (buildingId.HasValue && buildingId.Value > 0)
            query = query.Where(d => d.BuildingId == buildingId.Value);

        if (floorId.HasValue && floorId.Value > 0)
            query = query.Where(d => d.FloorId == floorId.Value);

        if (addressId.HasValue && addressId.Value > 0)
            query = query.Where(d => d.AddressId == addressId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.DormCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DormDto
            {
                Id = d.Id,
                DormCode = d.DormCode,
                BuildingId = d.BuildingId,
                BuildingName = d.BuildingName ?? "",
                FloorId = d.FloorId,
                AddressId = d.AddressId,
                AddressText = d.AddressText ?? "",
                Capacity = d.Capacity,
                Gender = d.Gender,
                Remark = d.Remark,
                IsActive = d.IsActive,
                CurrentCount = _db.DormBookings.Count(b => b.DormCode == d.DormCode && b.Status == 2)
            })
            .ToListAsync();

        // v2.13.95 派生班次（避免 N+1：批量一次 JOIN 全部页内宿舍）
        var dormCodes = items.Select(d => d.DormCode).ToList();
        var attendanceMap = await _db.DormBookings
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

        foreach (var d in items)
        {
            if (attendanceMap.TryGetValue(d.DormCode, out var names))
                d.AttendanceTypeNames = names;
        }

        // v2.13.111 派生班组（避免 N+1：批量一次 JOIN 全部页内宿舍）
        // 数据链路：DormBooking(Status=2 在宿) → SysEmployee(TeamId FK) → Team(Name + SortOrder)
        // 与 v2.13.91 宿舍详情班组列 / v2.13.97 Booking 列表班组列采用同 EmployeeTeamMap 模式
        var teamMap = await _db.DormBookings
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

        foreach (var d in items)
        {
            if (teamMap.TryGetValue(d.DormCode, out var tnames))
                d.TeamNames = tnames;
        }

        return ApiResponse<PagedResult<DormDto>>.Ok(new PagedResult<DormDto>
        {
            Items = items,
            TotalCount = total,
            PageIndex = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 获取宿舍详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<DormDto>> GetDorm(int id)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
            return ApiResponse<DormDto>.Fail("NOT_FOUND", "宿舍不存在");

        var dto = new DormDto
        {
            Id = dorm.Id,
            DormCode = dorm.DormCode,
            BuildingId = dorm.BuildingId,
            BuildingName = dorm.BuildingName ?? "",
            FloorId = dorm.FloorId,
            AddressId = dorm.AddressId,
            AddressText = dorm.AddressText ?? "",
            Capacity = dorm.Capacity,
            Gender = dorm.Gender,
            Remark = dorm.Remark,
            IsActive = dorm.IsActive,
            CurrentCount = await _db.DormBookings.CountAsync(b => b.DormCode == dorm.DormCode && b.Status == 2)
        };

        return ApiResponse<DormDto>.Ok(dto);
    }

    /// <summary>
    /// 新增宿舍
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<DormDto>> CreateDorm([FromBody] DormCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DormCode))
            return ApiResponse<DormDto>.Fail("CODE_REQUIRED", "宿舍号不能为空");

        if (await _db.Dorms.AnyAsync(d => d.DormCode == request.DormCode))
            return ApiResponse<DormDto>.Fail("CODE_EXISTS", "该宿舍号已存在");

        var building = await _basicsService.GetBuildingByIdAsync(request.BuildingId);
        var address = await _basicsService.GetAddressByIdAsync(request.AddressId);

        var dorm = new Dorm
        {
            DormCode = request.DormCode,
            BuildingId = request.BuildingId,
            BuildingName = building?.Name ?? "",
            FloorId = request.FloorId,
            AddressId = request.AddressId,
            AddressText = address?.AddressText ?? "",
            Capacity = request.Capacity,
            Gender = request.Gender,
            Remark = request.Remark,
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };

        _db.Dorms.Add(dorm);
        await _db.SaveChangesAsync();

        return ApiResponse<DormDto>.Ok(new DormDto
        {
            Id = dorm.Id,
            DormCode = dorm.DormCode,
            BuildingId = dorm.BuildingId,
            BuildingName = dorm.BuildingName ?? "",
            FloorId = dorm.FloorId,
            AddressId = dorm.AddressId,
            AddressText = dorm.AddressText ?? "",
            Capacity = dorm.Capacity,
            Gender = dorm.Gender,
            Remark = dorm.Remark,
            IsActive = dorm.IsActive
        }, "创建成功");
    }

    /// <summary>
    /// 更新宿舍
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse<DormDto>> UpdateDorm(int id, [FromBody] DormUpdateRequest request)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
            return ApiResponse<DormDto>.Fail("NOT_FOUND", "宿舍不存在");

        if (await _db.Dorms.AnyAsync(d => d.DormCode == request.DormCode && d.Id != id))
            return ApiResponse<DormDto>.Fail("CODE_EXISTS", "该宿舍号已存在");

        // v2.13.82 业务约束：在宿人数 > 0 时禁止停用宿舍（优先于容量校验）
        // 锁定条件：当前 dorm.IsActive=true 且 请求 IsActive=false 且 CurrentCount > 0
        var currentStaying = await _db.DormBookings
            .CountAsync(b => b.DormCode == dorm.DormCode && b.Status == 2);
        if (dorm.IsActive && !request.IsActive && currentStaying > 0)
        {
            return ApiResponse<DormDto>.Fail(
                "DORM_HAS_RESIDENTS",
                $"该宿舍当前在宿 {currentStaying} 人，禁止停用。请先办理所有人员退宿手续后再操作。");
        }

        // v2.13.12: 容量变更约束 — 减少容量时不能超过当前入住人数
        if (request.Capacity < currentStaying)
            return ApiResponse<DormDto>.Fail("CAPACITY_EXCEEDED", $"当前入住 {currentStaying} 人，容量不能少于入住人数");

        // v2.13.12: 减少容量时自动清空多余床位号
        if (request.Capacity < dorm.Capacity)
        {
            var bedNos = (dorm.BedNumbers ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (bedNos.Length > request.Capacity)
                dorm.BedNumbers = string.Join(",", bedNos.Take(request.Capacity));
        }

        var building = await _basicsService.GetBuildingByIdAsync(request.BuildingId);
        var address = await _basicsService.GetAddressByIdAsync(request.AddressId);

        dorm.DormCode = request.DormCode;
        dorm.BuildingId = request.BuildingId;
        dorm.BuildingName = building?.Name ?? "";
        dorm.FloorId = request.FloorId;
        dorm.AddressId = request.AddressId;
        dorm.AddressText = address?.AddressText ?? "";
        dorm.Capacity = request.Capacity;
        dorm.Gender = request.Gender;
        dorm.Remark = request.Remark;
        dorm.IsActive = request.IsActive;
        dorm.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        return ApiResponse<DormDto>.Ok(new DormDto
        {
            Id = dorm.Id,
            DormCode = dorm.DormCode,
            BuildingId = dorm.BuildingId,
            BuildingName = dorm.BuildingName ?? "",
            FloorId = dorm.FloorId,
            AddressId = dorm.AddressId,
            AddressText = dorm.AddressText ?? "",
            Capacity = dorm.Capacity,
            Gender = dorm.Gender,
            Remark = dorm.Remark,
            IsActive = dorm.IsActive
        }, "更新成功");
    }

    /// <summary>
    /// 删除宿舍
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse> DeleteDorm(int id)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
            return ApiResponse.Fail("NOT_FOUND", "宿舍不存在");

        // v2.13.12: 检查在宿 + 预约记录（两者均阻止删除）
        var hasActiveBookings = await _db.DormBookings
            .AnyAsync(b => b.DormCode == dorm.DormCode && (b.Status == 2 || b.Status == 1));
        if (hasActiveBookings)
            return ApiResponse.Fail("HAS_BOOKINGS", "该宿舍有在宿或预约人员，无法删除");

        _db.Dorms.Remove(dorm);
        await _db.SaveChangesAsync();

        return ApiResponse.Ok("删除成功");
    }
}

/// <summary>
/// 宿舍数据传输对象
/// </summary>
public class DormDto
{
    public int Id { get; set; }
    public string DormCode { get; set; } = "";
    public int BuildingId { get; set; }
    public string BuildingName { get; set; } = "";
    public int FloorId { get; set; }
    public int AddressId { get; set; }
    public string AddressText { get; set; } = "";
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; }
    public int CurrentCount { get; set; }
    /// <summary>v2.13.95 派生班次名称集合（按 AttendanceType.SortOrder 升序 + 去重）</summary>
    public List<string> AttendanceTypeNames { get; set; } = new();
    /// <summary>v2.13.111 派生班组名称集合（按 Team.SortOrder 升序 + 去重）。
    /// 数据关系：DormBooking (Status=2 在宿) → SysEmployee (TeamId FK) → Team (Name)。</summary>
    public List<string> TeamNames { get; set; } = new();
}

/// <summary>
/// 宿舍创建请求
/// </summary>
public class DormCreateRequest
{
    public string DormCode { get; set; } = "";
    public int BuildingId { get; set; }
    public int FloorId { get; set; }
    public int AddressId { get; set; }
    public int Capacity { get; set; } = 4;
    public int Gender { get; set; } = 1;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 宿舍更新请求
/// </summary>
public class DormUpdateRequest
{
    public string DormCode { get; set; } = "";
    public int BuildingId { get; set; }
    public int FloorId { get; set; }
    public int AddressId { get; set; }
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; }
}
