using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 宿舍详情页面模型
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
            Capacity = dorm.Capacity,
            Gender = dorm.Gender,
            Remark = dorm.Remark,
            IsActive = dorm.IsActive,
            CurrentCount = await _db.DormBookings.CountAsync(b => b.DormCode == dorm.DormCode && b.Status == 2)
        };

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
            BedNo = empBedMap.ContainsKey(b.EmployeeId) ? empBedMap[b.EmployeeId] : null
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
/// 宿舍详情数据传输对象
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
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; }
    public int CurrentCount { get; set; }
}

/// <summary>
/// 入住记录数据传输对象
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
}

/// <summary>
/// 考勤班次 Badge DTO（v2.12.40 新增）：用于在宿舍详情"当前入住人员"列表中展示考勤班次 Badge
/// </summary>
public class AttendanceBadgeDto
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
