using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;
// v2.13.86 引用 Dorms/Details.cshtml.cs 中的 Badge DTO 与 Helper（共享 Badge 渲染逻辑）
using DormManage.Admin.Pages.Dorms;

namespace DormManage.Admin.Pages.Personnel;

/// <summary>
/// 人员详情页面模型（v2.13.86 新增 — 配合 v2.13.83 性别字段）
///
/// 布局参考：
/// - 基本信息卡（v2.13.86 新增独立页面，与 v2.13.40 100% 原型对齐 personnel/details.html）
/// - 当前住宿记录
/// - 历史住宿记录
/// - 个人信息 + 部门 + 员工类型 + 考勤班次 + 班组 + 性别 Badge
/// </summary>
public class DetailsModel : PageModel
{
    private readonly DormDbContext _db;

    public DetailsModel(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>员工详情 DTO</summary>
    public EmployeeDetailDto? Employee { get; set; }

    /// <summary>当前在宿记录（v2.13.86 完整状态显示）</summary>
    public BookingHistoryDto? CurrentBooking { get; set; }

    /// <summary>历史住宿记录（按日期倒序）</summary>
    public List<BookingHistoryDto> HistoryBookings { get; set; } = new();

    /// <summary>员工考勤班次映射：AttendanceTypeId → 名称 + Badge</summary>
    public AttendanceBadgeDto? AttendanceBadge { get; set; }

    /// <summary>员工类型映射：EmployeeTypeId → 名称 + Badge</summary>
    public EmployeeTypeBadgeDto? EmployeeTypeBadge { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var emp = await _db.Employees
            .Include(e => e.Team)
            .Include(e => e.EmployeeType)
            .Include(e => e.AttendanceType)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (emp == null) return NotFound();

        Employee = new EmployeeDetailDto
        {
            Id = emp.Id,
            EmployeeCode = emp.EmployeeCode,
            RealName = emp.RealName,
            Gender = emp.Gender,
            GenderName = emp.Gender == 1 ? "男" : emp.Gender == 2 ? "女" : "未知",
            Phone = emp.Phone,
            Department = emp.Department ?? "",
            EmployeeTypeId = emp.EmployeeTypeId,
            EmployeeTypeName = emp.EmployeeType?.Name ?? "",
            AttendanceTypeId = emp.AttendanceTypeId,
            AttendanceTypeName = emp.AttendanceType?.Name ?? "",
            TeamId = emp.TeamId,
            TeamName = emp.Team?.Name ?? "",
            HireDate = emp.HireDate,
            LeaveDate = emp.LeaveDate,
            EmploymentStatusId = emp.EmploymentStatusId,
            EmploymentStatusName = emp.EmploymentStatusId == 1 ? "在职"
                : emp.EmploymentStatusId == 2 ? "试用期" : emp.EmploymentStatusId == 3 ? "已离职" : "未知",
            DormCode = emp.DormCode,
            BedNo = emp.BedNo,
            ResidenceStatusId = emp.ResidenceStatusId,
            IsActive = emp.IsActive,
            Remark = emp.Remark,
            CreatedAt = emp.CreatedAt,
            UpdatedAt = emp.UpdatedAt
        };

        // 考勤班次 Badge
        if (emp.AttendanceTypeId.HasValue)
        {
            AttendanceBadge = new AttendanceBadgeDto
            {
                Name = emp.AttendanceType?.Name ?? emp.AttendanceType?.Code ?? "-",
                BadgeClass = AttendanceBadgeHelper.GetAttendanceBadgeClass(emp.AttendanceType?.Code)
            };
        }

        // 员工类型 Badge
        EmployeeTypeBadge = new EmployeeTypeBadgeDto
        {
            Name = emp.EmployeeType?.Name ?? emp.EmployeeType?.Code ?? "-",
            BadgeClass = EmployeeTypeBadgeHelper.GetEmployeeTypeBadgeClass(emp.EmployeeType?.Code)
        };

        // 当前在宿记录（Status=2）
        var currentStaying = await _db.DormBookings
            .Where(b => b.EmployeeId == emp.Id && b.Status == BookingStatus.Staying)
            .OrderByDescending(b => b.BookingDate)
            .FirstOrDefaultAsync();

        if (currentStaying != null)
        {
            CurrentBooking = new BookingHistoryDto
            {
                Id = currentStaying.Id,
                DormCode = currentStaying.DormCode,
                Type = currentStaying.Type,
                TypeName = currentStaying.Type == BookingType.CheckIn ? "入住" : "退房",
                BookingDate = currentStaying.BookingDate,
                Status = currentStaying.Status,
                StatusName = "在宿",
                Reason = currentStaying.Reason,
                Registrar = currentStaying.Registrar,
                RegistrationDate = currentStaying.RegistrationDate,
                ActualCheckInDate = currentStaying.ActualCheckInDate
            };
        }

        // 历史住宿记录（除当前在宿外的所有记录，倒序）
        HistoryBookings = await _db.DormBookings
            .Where(b => b.EmployeeId == emp.Id && b.Status != BookingStatus.Staying)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.Id)
            .Take(20)  // 最近 20 条
            .Select(b => new BookingHistoryDto
            {
                Id = b.Id,
                DormCode = b.DormCode,
                Type = b.Type,
                TypeName = b.Type == BookingType.CheckIn ? "入住" : "退房",
                BookingDate = b.BookingDate,
                Status = b.Status,
                StatusName = b.Status == BookingStatus.Reserved ? "预约"
                    : b.Status == BookingStatus.CheckedOut ? "已退房"
                    : b.Status == BookingStatus.Cancelled ? "已取消" : "未知",
                Reason = b.Reason,
                Registrar = b.Registrar,
                RegistrationDate = b.RegistrationDate,
                ActualCheckInDate = b.ActualCheckInDate,
                ActualCheckOutDate = b.ActualCheckOutDate
            })
            .ToListAsync();

        return Page();
    }
}

/// <summary>员工详情 DTO（v2.13.86 新增 — 含性别字段）</summary>
public class EmployeeDetailDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string RealName { get; set; } = "";
    public int Gender { get; set; }
    public string GenderName { get; set; } = "";
    public string? Phone { get; set; }
    public string Department { get; set; } = "";
    public int EmployeeTypeId { get; set; }
    public string EmployeeTypeName { get; set; } = "";
    public int? AttendanceTypeId { get; set; }
    public string AttendanceTypeName { get; set; } = "";
    public int TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public DateOnly? HireDate { get; set; }
    public DateOnly? LeaveDate { get; set; }
    public int EmploymentStatusId { get; set; }
    public string EmploymentStatusName { get; set; } = "";
    public string DormCode { get; set; } = "";
    public int? BedNo { get; set; }
    public int ResidenceStatusId { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>住宿历史记录 DTO</summary>
public class BookingHistoryDto
{
    public int Id { get; set; }
    public string DormCode { get; set; } = "";
    public int Type { get; set; }
    public string TypeName { get; set; } = "";
    public DateOnly BookingDate { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Reason { get; set; }
    public string? Registrar { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public DateOnly? ActualCheckInDate { get; set; }
    public DateOnly? ActualCheckOutDate { get; set; }
}