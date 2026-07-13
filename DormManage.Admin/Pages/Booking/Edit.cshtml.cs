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

    public async Task OnGetAsync(int id)
    {
        Booking = await _service.GetByIdAsync(id);
        if (Booking == null) return;

        // 加载员工详细信息
        var employee = await _db.Employees
            .Include(e => e.EmployeeType)
            .Include(e => e.AttendanceType)
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

            // 在职状态名称映射（Status 字段直接映射，非导航属性）
            EmploymentStatusName = employee.Status switch
            {
                EmployeeStatus.Active => "在职",
                EmployeeStatus.Onboarding => "待入职",
                EmployeeStatus.Left => "已离职",
                _ => "未知"
            };
        }
    }
}
