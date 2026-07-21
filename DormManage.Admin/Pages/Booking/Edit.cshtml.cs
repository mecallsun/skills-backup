using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.Booking;

public class EditModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBookingService _service;

    public EditModel(DormDbContext db, IBookingService service)
    {
        _db = db;
        _service = service;
    }

    public DormBooking? Booking { get; set; }

    /// <summary>员工工号</summary>
    public string? EmployeeCode { get; set; }
    /// <summary>员工姓名</summary>
    public string? EmployeeName { get; set; }
    /// <summary>部门名称</summary>
    public string? Department { get; set; }
    /// <summary>员工类型名称</summary>
    public string? EmployeeTypeName { get; set; }
    /// <summary>考勤班次名称</summary>
    public string? AttendanceTypeName { get; set; }
    /// <summary>手机号</summary>
    public string? Phone { get; set; }
    /// <summary>入职日期</summary>
    public DateOnly? HireDate { get; set; }
    /// <summary>在职状态名称</summary>
    public string? EmploymentStatusName { get; set; }
    /// <summary>班组（P2-3 / P2-8）</summary>
    public string? Team { get; set; }
    /// <summary>离职日期</summary>
    public DateOnly? LeaveDate { get; set; }
    /// <summary>住宿状态名称</summary>
    public string? ResidenceStatusName { get; set; }

    public async Task OnGetAsync(int id)
    {
        Booking = await _service.GetByIdAsync(id);
        if (Booking == null) return;

        // 加载员工详细信息
        var employee = await _db.Employees
            .Include(e => e.EmployeeType)
            .Include(e => e.AttendanceType)
            .Include(e => e.ResidenceStatus)
            .Include(e => e.Team)  // v2.13.78 BUG 修复：班组 FK 关联
            .FirstOrDefaultAsync(e => e.Id == Booking.EmployeeId);

        if (employee != null)
        {
            EmployeeCode = employee.EmployeeCode;
            EmployeeName = employee.RealName;
            Department = employee.Department;
            EmployeeTypeName = employee.EmployeeType?.Name;
            AttendanceTypeName = employee.AttendanceType?.Name;
            Phone = employee.Phone;
            HireDate = employee.HireDate;
            LeaveDate = employee.LeaveDate;
            // v2.13.78 BUG 修复：班组名称走 FK 关联（之前 employee.Team 字符串字段被 DbContext.Ignore，永远为 null）
            Team = employee.Team?.Name;
            ResidenceStatusName = employee.ResidenceStatus?.Name;

            // v2.13.11 在职状态名称映射（改用 EmploymentStatusId 关联引用基础资料-在职状态表，避免废弃 Status 属性）
#pragma warning disable CS0618 // 兼容旧数据回退路径
            var statusValue = employee.EmploymentStatusId > 0 ? employee.EmploymentStatusId : (int)employee.Status;
#pragma warning restore CS0618
            EmploymentStatusName = statusValue switch
            {
                1 => "在职",
                2 => "待入职",
                3 => "已离职",
                _ => "未知"
            };
        }
    }
}
