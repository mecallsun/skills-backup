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

        CurrentResidents = currentBookings.Select(b => new BookingRecordDto
        {
            Id = b.Id,
            EmployeeCode = b.EmployeeCode,
            EmployeeName = b.EmployeeName,
            Phone = b.Phone,
            Department = b.Department,
            BookingDate = b.BookingDate,
            CheckInDate = b.BookingDate
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
}
