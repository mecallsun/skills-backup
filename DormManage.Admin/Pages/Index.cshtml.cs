using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Models;
using DormManage.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages;

/// <summary>
/// 首页仪表盘页面模型
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(DormDbContext db, ILogger<IndexModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 宿舍总数
    /// </summary>
    public int TotalDorms { get; set; }

    /// <summary>
    /// 可用床位总数
    /// </summary>
    public int TotalBeds { get; set; }

    /// <summary>
    /// 当前入住人数（在宿）
    /// </summary>
    public int CurrentOccupancy { get; set; }

    /// <summary>
    /// 入住率百分比
    /// </summary>
    public decimal OccupancyRate { get; set; }

    /// <summary>
    /// 员工总数
    /// </summary>
    public int TotalEmployees { get; set; }

    /// <summary>
    /// 在职人数
    /// </summary>
    public int ActiveEmployees { get; set; }

    /// <summary>
    /// 待入职人数
    /// </summary>
    public int OnboardingEmployees { get; set; }

    /// <summary>
    /// 已离职人数
    /// </summary>
    public int LeftEmployees { get; set; }

    /// <summary>
    /// 今日入住记录
    /// </summary>
    public int TodayCheckIns { get; set; }

    /// <summary>
    /// 今日退房记录
    /// </summary>
    public int TodayCheckOuts { get; set; }

    /// <summary>
    /// 预约中数量
    /// </summary>
    public int ReservedCount { get; set; }

    /// <summary>
    /// 最近办理记录
    /// </summary>
    public List<RecentBookingDto> RecentBookings { get; set; } = new();

    public async Task OnGetAsync()
    {
        // ---- 宿舍统计 ----
        var dorms = await _db.Dorms.ToListAsync();
        TotalDorms = dorms.Count;
        TotalBeds = dorms.Sum(d => d.Capacity);

        // 当前入住人数：在宿状态的记录数
        CurrentOccupancy = await _db.DormBookings.CountAsync(b => b.Status == BookingStatus.Staying);

        // 入住率
        OccupancyRate = TotalBeds > 0 ? Math.Round(CurrentOccupancy * 100m / TotalBeds, 1) : 0;

        // ---- 人员统计 ----
        TotalEmployees = await _db.Employees.CountAsync();
        ActiveEmployees = await _db.Employees.CountAsync(e => e.EmploymentStatusId == EmployeeStatus.Active);
        OnboardingEmployees = await _db.Employees.CountAsync(e => e.EmploymentStatusId == EmployeeStatus.Onboarding);
        LeftEmployees = await _db.Employees.CountAsync(e => e.EmploymentStatusId == EmployeeStatus.Left);

        // ---- 今日办理 ----
        var today = DateOnly.FromDateTime(DateTime.Now);
        TodayCheckIns = await _db.DormBookings.CountAsync(b => b.BookingDate == today && b.Type == BookingType.CheckIn);
        TodayCheckOuts = await _db.DormBookings.CountAsync(b => b.BookingDate == today && b.Type == BookingType.CheckOut);
        ReservedCount = await _db.DormBookings.CountAsync(b => b.Status == BookingStatus.Reserved);

        // ---- 最近办理记录（最近10条） ----
        var recent = await _db.DormBookings
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .Select(b => new RecentBookingDto
            {
                EmployeeName = b.EmployeeName,
                EmployeeCode = b.EmployeeCode,
                DormCode = b.DormCode,
                Type = b.Type,
                Status = b.Status,
                BookingDate = b.BookingDate,
                RegistrationDate = b.RegistrationDate,
                Registrar = b.Registrar
            })
            .ToListAsync();

        RecentBookings = recent;
    }
}

/// <summary>
/// 最近办理记录 DTO
/// </summary>
public class RecentBookingDto
{
    public string EmployeeName { get; set; } = "";
    public string EmployeeCode { get; set; } = "";
    public string DormCode { get; set; } = "";
    public int Type { get; set; }
    public int Status { get; set; }
    public DateOnly BookingDate { get; set; }
    public DateTime RegistrationDate { get; set; }
    public string Registrar { get; set; } = "";
}
