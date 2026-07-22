using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 宿舍详情页面模型（v2.13.39 100% 原型对齐）
///
/// 改造点（vs 原型 dorms/details.html）：
/// 1. 基本信息加「房间数」字段
/// 2. 当前入住人员表加 3 列：员工类型、已入住、操作
/// 3. 员工类型按 FK 关联引用 SysEmployee.EmployeeTypeId → EmployeeType.Name
/// 4. 调宿按钮跳 Booking/Edit、退宿按钮调 POST /api/v1/bookings/{id}/check-out
/// </summary>
public class DetailsModel : PageModel
{
    private readonly DormDbContext _db;

    public DetailsModel(DormDbContext db)
    {
        _db = db;
    }

    public DormDetailDto? Dorm { get; set; }

    /// <summary>
    /// 当前入住人员列表
    /// </summary>
    public List<BookingRecordDto> CurrentResidents { get; set; } = new();

    /// <summary>
    /// 员工考勤班次映射（v2.12.40 新增）：EmployeeId → AttendanceBadgeDto
    /// </summary>
    public Dictionary<int, AttendanceBadgeDto> EmployeeAttendanceMap { get; set; } = new();

    /// <summary>
    /// 员工类型映射（v2.13.39 新增）：EmployeeTypeId → EmployeeTypeBadgeDto
    /// FK 关联引用 SysEmployee.EmployeeTypeId → EmployeeType
    /// </summary>
    public Dictionary<int, EmployeeTypeBadgeDto> EmployeeTypeMap { get; set; } = new();

    /// <summary>
    /// 班组映射（v2.13.91 新增）：EmployeeId → TeamBadgeDto
    /// FK 关联引用 SysEmployee.TeamId → Team
    /// </summary>
    public Dictionary<int, TeamBadgeDto> EmployeeTeamMap { get; set; } = new();

    /// <summary>
    /// 历史入住记录
    /// </summary>
    public List<BookingRecordDto> HistoryRecords { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
        {
            return NotFound();
        }

        Dorm = new DormDetailDto
        {
            Id = dorm.Id,
            DormCode = dorm.DormCode,
            BuildingId = dorm.BuildingId,
            BuildingName = dorm.BuildingName ?? "",
            FloorId = dorm.FloorId,
            AddressId = dorm.AddressId,
            AddressText = dorm.AddressText ?? "",
            // v2.13.39 新增：房间数字段（与原型 dorms/details.html basicInfo 房间数 对齐）
            RoomCount = dorm.RoomCount,
            Capacity = dorm.Capacity,
            Gender = dorm.Gender,
            Remark = dorm.Remark,
            IsActive = dorm.IsActive,
            CurrentCount = await _db.DormBookings.CountAsync(b => b.DormCode == dorm.DormCode && b.Status == 2)
        };

        // v2.13.85 派生性别：JOIN DormBookings(Status=2) → SysEmployee 拿男女人数
        var genderStats2 = await _db.DormBookings
            .Where(b => b.DormCode == dorm.DormCode && b.Status == 2)
            .Join(_db.Employees.AsNoTracking(),
                  b => b.EmployeeId, e => e.Id,
                  (b, e) => e.Gender)
            .GroupBy(g => 1)
            .Select(g => new { MaleCount = g.Count(x => x == 1), FemaleCount = g.Count(x => x == 2) })
            .FirstOrDefaultAsync();
        int male = genderStats2?.MaleCount ?? 0;
        int female = genderStats2?.FemaleCount ?? 0;
        Dorm.EffectiveGender = male > 0 ? 1 : (female > 0 ? 2 : 0);
        Dorm.MaleCount = male;
        Dorm.FemaleCount = female;

        // 当前入住人员
        var currentBookings = await _db.DormBookings
            .Where(b => b.DormCode == dorm.DormCode && b.Status == 2)
            .ToListAsync();

        // v2.12.40 关联查询员工床号（FK 关联引用 SysEmployee.BedNo，与人员清单床号列保持同步）
        var employeeIds = currentBookings.Select(b => b.EmployeeId).Distinct().ToList();
        var empDict = await _db.Employees
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e);

        var empBedMap = empDict.ToDictionary(kv => kv.Key, kv => kv.Value.BedNo);

        // v2.12.40 考勤班次字典（FK 关联引用 AttendanceType 表）
        var attendanceTypes = await _db.AttendanceTypes.ToListAsync();
        var attMap = attendanceTypes.ToDictionary(a => a.Id, a => new AttendanceBadgeDto
        {
            Name = a.Name ?? a.Code ?? "-",
            BadgeClass = AttendanceBadgeHelper.GetAttendanceBadgeClass(a.Code)
        });
        foreach (var emp in empDict.Values)
        {
            if (emp.AttendanceTypeId.HasValue && attMap.ContainsKey(emp.AttendanceTypeId.Value))
            {
                EmployeeAttendanceMap[emp.Id] = attMap[emp.AttendanceTypeId.Value];
            }
        }

        // v2.13.39 新增：员工类型字典（FK 关联引用 EmployeeType 表）
        var employeeTypes = await _db.EmployeeTypes.ToListAsync();
        foreach (var et in employeeTypes)
        {
            EmployeeTypeMap[et.Id] = new EmployeeTypeBadgeDto
            {
                Name = et.Name ?? et.Code ?? "-",
                BadgeClass = EmployeeTypeBadgeHelper.GetEmployeeTypeBadgeClass(et.Code)
            };
        }

        // v2.13.91 新增：班组字典（FK 关联引用 Team 表）
        var teams = await _db.Teams.ToListAsync();
        var teamMap = teams.ToDictionary(t => t.Id, t => new TeamBadgeDto
        {
            Name = t.Name ?? t.Code ?? "-",
            BadgeClass = TeamBadgeHelper.GetTeamBadgeClass(t.Code)
        });
        // 维护 EmployeeId → TeamBadgeDto 索引（前端按 EmployeeId 查询，避免模板内再嵌套判断）
        foreach (var emp in empDict.Values)
        {
            if (emp.TeamId > 0 && teamMap.ContainsKey(emp.TeamId))
            {
                EmployeeTeamMap[emp.Id] = teamMap[emp.TeamId];
            }
        }

        // v2.13.39 新增：计算已入住天数（在 Razor 渲染时直接计算，无需 DTO 字段）
        var today = DateOnly.FromDateTime(DateTime.Now);

        CurrentResidents = currentBookings.Select(b => new BookingRecordDto
        {
            Id = b.Id,
            EmployeeId = b.EmployeeId,
            EmployeeCode = b.EmployeeCode,
            EmployeeName = b.EmployeeName,
            Phone = b.Phone,
            Department = b.Department,
            BookingDate = b.BookingDate,
            CheckInDate = b.BookingDate,
            // v2.12.40 床号从 SysEmployee.BedNo 读取
            BedNo = empBedMap.ContainsKey(b.EmployeeId) ? empBedMap[b.EmployeeId] : null,
            // v2.13.39 新增：员工类型名称（前端渲染 Badge 时按 emp.EmployeeTypeId 查 EmployeeTypeMap）
            EmployeeTypeId = empDict.ContainsKey(b.EmployeeId) && empDict[b.EmployeeId].EmployeeTypeId > 0
                ? empDict[b.EmployeeId].EmployeeTypeId : null,
            // v2.13.91 新增：班组 ID（前端渲染 Badge 时按 EmployeeId 查 EmployeeTeamMap）
            TeamId = empDict.ContainsKey(b.EmployeeId) && empDict[b.EmployeeId].TeamId > 0
                ? empDict[b.EmployeeId].TeamId : null
        }).ToList();

        // 历史记录（入住+退房）
        var historyBookings = await _db.DormBookings
            .Where(b => b.DormCode == dorm.DormCode)
            .OrderByDescending(b => b.BookingDate)
            .Take(20)
            .ToListAsync();

        HistoryRecords = historyBookings.Select(b => new BookingRecordDto
        {
            Id = b.Id,
            EmployeeCode = b.EmployeeCode,
            EmployeeName = b.EmployeeName,
            Phone = b.Phone,
            Department = b.Department,
            Type = b.Type,
            Status = b.Status,
            BookingDate = b.BookingDate,
            CheckInDate = b.BookingDate,
            CheckOutDate = b.BookingDate,
            Registrar = b.Registrar,
            RegistrationDate = b.RegistrationDate
        }).ToList();

        return Page();
    }
}

/// <summary>
/// 宿舍详情数据传输对象（v2.13.39 新增 RoomCount）
/// </summary>
public class DormDetailDto
{
    public int Id { get; set; }
    public string DormCode { get; set; } = "";
    public int BuildingId { get; set; }
    public string BuildingName { get; set; } = "";
    public int FloorId { get; set; }
    public int AddressId { get; set; }
    public string AddressText { get; set; } = "";
    /// <summary>房间数（v2.13.39 新增，与原型 basicInfo 房间数 对齐）</summary>
    public int RoomCount { get; set; } = 1;
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; }

    // ========== v2.13.85 派生性别字段 ==========
    public int MaleCount { get; set; }
    public int FemaleCount { get; set; }
    public int EffectiveGender { get; set; }
    public string EffectiveGenderName => EffectiveGender == 1 ? "男" : EffectiveGender == 2 ? "女" : "无";
    public int CurrentCount { get; set; }
}

/// <summary>
/// 入住记录数据传输对象（v2.13.39 新增 EmployeeTypeId）
/// </summary>
public class BookingRecordDto
{
    public long Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public int Type { get; set; }
    public int Status { get; set; }
    public DateOnly BookingDate { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
    public string? Registrar { get; set; }
    public DateTime RegistrationDate { get; set; }
    /// <summary>
    /// 当前床位号（v2.12.40 新增）：从 SysEmployee.BedNo 读取，与人员清单床号列保持同步
    /// </summary>
    public int? BedNo { get; set; }
    /// <summary>
    /// 员工类型 ID（v2.13.39 新增）：FK 关联 SysEmployee.EmployeeTypeId → EmployeeType，
    /// 前端通过 EmployeeTypeMap 渲染 Badge。
    /// </summary>
    public int? EmployeeTypeId { get; set; }
    /// <summary>
    /// 班组 ID（v2.13.91 新增）：FK 关联 SysEmployee.TeamId → Team，
    /// 前端通过 EmployeeTeamMap 渲染 Badge。
    /// </summary>
    public int? TeamId { get; set; }
}

/// <summary>
/// 考勤班次 Badge DTO（v2.12.40）：用于在宿舍详情"当前入住人员"列表中展示考勤班次 Badge
/// </summary>
public class AttendanceBadgeDto
{
    public string Name { get; set; } = "";
    public string BadgeClass { get; set; } = "bg-secondary";
}

/// <summary>
/// 员工类型 Badge DTO（v2.13.39 新增）：用于在宿舍详情"当前入住人员"列表中展示员工类型 Badge
/// </summary>
public class EmployeeTypeBadgeDto
{
    public string Name { get; set; } = "";
    public string BadgeClass { get; set; } = "bg-secondary";
}

/// <summary>
/// 班组 Badge DTO（v2.13.91 新增）：用于在宿舍详情"当前入住人员"列表中展示班组 Badge
/// </summary>
public class TeamBadgeDto
{
    public string Name { get; set; } = "";
    public string BadgeClass { get; set; } = "bg-secondary";
}

/// <summary>
/// 考勤班次 Badge 颜色映射辅助方法（v2.12.40）
/// </summary>
public static partial class AttendanceBadgeHelper
{
    public static string GetAttendanceBadgeClass(string? code)
    {
        return code switch
        {
            "DEFAULT" => "bg-secondary",
            "MORNING" => "bg-warning text-dark",
            "MIDDLE" => "bg-info text-dark",
            "EVENING" => "bg-primary",
            "NIGHT" => "bg-dark",
            "OTHER" => "bg-success",
            _ => "bg-secondary"
        };
    }
}

/// <summary>
/// 员工类型 Badge 颜色映射辅助方法（v2.13.39 新增）
/// </summary>
public static partial class EmployeeTypeBadgeHelper
{
    public static string GetEmployeeTypeBadgeClass(string? code)
    {
        return code switch
        {
            "CONTRACT" => "bg-secondary",
            "TEMPORARY" => "bg-warning text-dark",
            "OUTSOURCE" => "bg-info text-dark",
            "INTERN" => "bg-success",
            "ONSITE" => "bg-dark",
            _ => "bg-secondary"
        };
    }
}

/// <summary>
/// 班组 Badge 颜色映射辅助方法（v2.13.91 新增）
/// 班组无强 enum，按 Code 首字母 A-F 派生命名色，确保视觉区分
/// </summary>
public static partial class TeamBadgeHelper
{
    public static string GetTeamBadgeClass(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "bg-secondary";
        var c = code.ToUpper();
        if (c.StartsWith("A")) return "bg-primary";
        if (c.StartsWith("B")) return "bg-success";
        if (c.StartsWith("C")) return "bg-info text-dark";
        if (c.StartsWith("D")) return "bg-warning text-dark";
        if (c.StartsWith("E")) return "bg-danger";
        if (c.StartsWith("F")) return "bg-dark";
        return "bg-secondary";
    }
}