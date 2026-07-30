using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 住宿住宿历史页面模型
/// </summary>
public class HistoryModel : PageModel
{
    private readonly DormDbContext _db;

    public HistoryModel(DormDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>
    /// 住宿信息
    /// </summary>
    public DormInfoDto? DormInfo { get; set; }

    /// <summary>
    /// 历史记录列表
    /// </summary>
    public List<HistoryRecordDto> HistoryRecords { get; set; } = new();

    /// <summary>
    /// 累计住宿人次
    /// </summary>
    public int TotalHistoryCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // 加载住宿信息
        var dorm = await _db.Dorms
            .FirstOrDefaultAsync(d => d.Id == Id);

        if (dorm == null)
        {
            TempData["ErrorMessage"] = "住宿不存在";
            return RedirectToPage("/Dorms/Index");
        }

        DormInfo = new DormInfoDto
        {
            DormCode = dorm.DormCode,
            BuildingName = dorm.BuildingName ?? "-",
            FloorName = dorm.FloorId.ToString(),
            AddressText = dorm.AddressText ?? "-",
            Capacity = dorm.Capacity,
            Gender = dorm.Gender,
            IsActive = dorm.IsActive
        };

        // 从 DormBooking 表查询该住宿的所有办理记录
        var bookings = await _db.DormBookings
            .Where(b => b.DormCode == dorm.DormCode)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();

        TotalHistoryCount = bookings.Count;

        // v2.13.86 批量 JOIN SysEmployee 拿 Gender（避免 N+1）
        var empIds = bookings.Select(b => b.EmployeeId).Distinct().Where(id => id > 0).ToList();
        var genderMap = await _db.Employees
            .Where(emp => empIds.Contains(emp.Id))
            .Select(emp => new { emp.Id, emp.Gender })
            .ToDictionaryAsync(x => x.Id, x => x.Gender);

        var now = DateOnly.FromDateTime(DateTime.Now);
        HistoryRecords = bookings.Select(b =>
        {
            var isCheckedOut = b.Type == 2 && b.Status == 3;
            int stayDays = 0;
            if (isCheckedOut)
            {
                // 已退房：使用同一天作为基准（实际可关联另一条入住记录的 BookingDate）
                stayDays = 0;
            }
            else if (b.Status == 2)
            {
                stayDays = now.DayNumber - b.BookingDate.DayNumber;
            }

            return new HistoryRecordDto
            {
                EmployeeId = b.EmployeeId,
                EmployeeCode = b.EmployeeCode,
                EmployeeName = b.EmployeeName,
                Gender = genderMap.GetValueOrDefault(b.EmployeeId, 0),  // v2.13.86
                Department = b.Department ?? "-",
                CheckInDate = b.BookingDate,
                LeaveDate = b.Type == 2 ? b.BookingDate : (DateOnly?)null,
                Reason = b.Reason ?? "-",
                Status = b.Status,
                IsCheckedOut = isCheckedOut,
                StayDays = stayDays
            };
        }).ToList();

        return Page();
    }
}

/// <summary>
/// 住宿信息数据传输对象
/// </summary>
public class DormInfoDto
{
    public string DormCode { get; set; } = "";
    public string BuildingName { get; set; } = "";
    public string FloorName { get; set; } = "";
    public string AddressText { get; set; } = "";
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// 住宿历史记录数据传输对象
/// </summary>
public class HistoryRecordDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    /// <summary>v2.13.86 性别（JOIN SysEmployee 取实时）</summary>
    public int Gender { get; set; }
    public string Department { get; set; } = "";
    public DateOnly CheckInDate { get; set; }
    public DateOnly? LeaveDate { get; set; }
    public string Reason { get; set; } = "";
    public int Status { get; set; }
    public bool IsCheckedOut { get; set; }
    public int StayDays { get; set; }
}
