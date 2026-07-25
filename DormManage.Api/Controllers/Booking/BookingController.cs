using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Extensions;
using DormManage.Shared.Models;
using DormManage.Shared.Security;
using DormManage.Shared.Services;

namespace DormManage.Api.Controllers.Booking;

/// <summary>
/// 办理记录 API 控制器
/// </summary>
[ApiController]
[Route("api/v1/bookings")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _service;
    private readonly DormDbContext _db;

    public BookingController(IBasicsService basicsService, IBookingService bookingService, DormDbContext db)
    {
        _service = bookingService;
        _db = db;
    }

    /// <summary>
    /// 获取办理记录列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<DormBooking>>> GetList(
        [FromQuery] string? keyword = null,
        [FromQuery] string? department = null,
        [FromQuery] string? dormCode = null,
        [FromQuery] int? type = null,
        [FromQuery] int? status = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        DateOnly? df = string.IsNullOrEmpty(dateFrom) ? null : DateOnly.Parse(dateFrom);
        DateOnly? dt = string.IsNullOrEmpty(dateTo) ? null : DateOnly.Parse(dateTo);

        var result = await _service.GetListAsync(keyword, department, dormCode, type, status, df, dt, page, pageSize);
        return ApiResponse<PagedResult<DormBooking>>.Ok(result);
    }

    /// <summary>
    /// 获取办理记录详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<DormBooking>> GetById(int id)
    {
        var entity = await _service.GetByIdAsync(id);
        if (entity == null) return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<DormBooking>.Ok(entity);
    }

    /// <summary>
    /// 办理入住
    /// </summary>
    [HttpPost("check-in")]
    public async Task<ApiResponse<DormBooking>> CheckIn([FromBody] CheckInRequest request)
    {
        // v2.13.149 试用模式限制：未注册时 住宿登记/宿舍档案/人员清单 最多 5 条记录
        var trialCheck = LicenseGuard.CheckTrialRecordLimit(
            "住宿登记",
            await _db.DormBookings.CountAsync());
        if (!trialCheck.IsAllowed)
        {
            return ApiResponse<DormBooking>.Fail(LicenseGuard.TrialLimitErrorCode, trialCheck.Message);
        }

        // v2.13.29: 从登录会话/请求头获取真实用户名（替换原 TODO 兜底 admin）
        var registrar = HttpContext.GetCurrentUserName();
        // v2.13.89: 同时获取 UserId 用于 FK 关联（用户需求：表存 FK，页面显示姓名）
        var registrarUserId = HttpContext.GetCurrentUserId();
        return await _service.CheckInAsync(new BookingCheckInRequest
        {
            EmployeeId = request.EmployeeId,
            DormCode = request.DormCode,
            BookingDate = DateOnly.Parse(request.BookingDate),
            Reason = request.Reason,
            Remark = request.Remark
        }, registrar, registrarUserId);
    }

    /// <summary>
    /// 办理退房
    /// </summary>
    [HttpPost("{id}/check-out")]
    public async Task<ApiResponse<DormBooking>> CheckOut(int id, [FromBody] CheckOutRequest request)
    {
        var registrar = HttpContext.GetCurrentUserName();
        var registrarUserId = HttpContext.GetCurrentUserId();
        return await _service.CheckOutAsync(id, DateOnly.Parse(request.CheckOutDate), request.Reason, request.Remark, registrar, registrarUserId);
    }

    /// <summary>
    /// 更新记录
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse<DormBooking>> Update(int id, [FromBody] UpdateRequest request)
    {
        return await _service.UpdateAsync(id, new BookingUpdateRequest
        {
            BookingDate = DateOnly.Parse(request.BookingDate),
            Reason = request.Reason,
            Remark = request.Remark,
            Status = request.Status  // v2.13.38: 透传 Status 字段
        });
    }

    /// <summary>
    /// 快速确认入住（v2.11.8）：Type=1 入住 && Status=1 预约 → Status=2 在宿
    /// </summary>
    [HttpPost("{id}/confirm-checkin")]
    public async Task<ApiResponse<DormBooking>> ConfirmCheckIn(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        var registrarUserId = HttpContext.GetCurrentUserId();
        return await _service.ConfirmCheckInAsync(id, registrar, registrarUserId);
    }

    /// <summary>
    /// 撤销退房（v2.11.10）：Type=2 退房 && Status=3 已退房 && BookingDate=今天 → Status=2 在宿
    /// </summary>
    [HttpPost("{id}/undo-checkout")]
    public async Task<ApiResponse<DormBooking>> UndoCheckOut(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        var registrarUserId = HttpContext.GetCurrentUserId();
        return await _service.UndoCheckOutAsync(id, registrar, registrarUserId);
    }

    /// <summary>
    /// 撤销预约（v2.11.11）：Status=1 预约 → Status=4 已取消
    /// </summary>
    [HttpPost("{id}/cancel-reservation")]
    public async Task<ApiResponse<DormBooking>> CancelReservation(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        var registrarUserId = HttpContext.GetCurrentUserId();
        return await _service.CancelReservationAsync(id, registrar, registrarUserId);
    }

    /// <summary>
    /// 确认预约退房（v2.11.12）：Type=2 退房 && Status=1 预约 → Status=3 已退房
    /// </summary>
    [HttpPost("{id}/confirm-checkout")]
    public async Task<ApiResponse<DormBooking>> ConfirmCheckout(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        var registrarUserId = HttpContext.GetCurrentUserId();
        return await _service.ConfirmReservedCheckOutAsync(id, registrar, registrarUserId);
    }

    /// <summary>
    /// 撤销在宿（v2.11.17）：Type=1 入住 && Status=2 在宿 && BookingDate=今天 → Status=4 已取消
    /// </summary>
    [HttpPost("{id}/cancel-today")]
    public async Task<ApiResponse<DormBooking>> CancelToday(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        var registrarUserId = HttpContext.GetCurrentUserId();
        return await _service.CancelTodayAsync(id, registrar, registrarUserId);
    }

    /// <summary>
    /// 删除记录
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse> Delete(int id)
    {
        return await _service.DeleteAsync(id);
    }

    /// <summary>
    /// 搜索员工（联动下拉）
    /// </summary>
    [HttpGet("employee-search")]
    public async Task<ApiResponse<List<EmployeeSearchResult>>> SearchEmployee([FromQuery] string keyword)
    {
        var result = await _service.SearchEmployeeAsync(keyword);
        return ApiResponse<List<EmployeeSearchResult>>.Ok(result);
    }

    /// <summary>
    /// 获取可选房间列表
    /// </summary>
    [HttpGet("available-dorms")]
    public async Task<ApiResponse<List<DormOption>>> GetAvailableDorms([FromQuery] int employeeId, [FromQuery] string bookingDate)
    {
        var result = await _service.GetAvailableDormsAsync(employeeId, DateOnly.Parse(bookingDate));
        return ApiResponse<List<DormOption>>.Ok(result);
    }

    /// <summary>
    /// 获取员工的在宿记录（用于退房）
    /// </summary>
    [HttpGet("staying-records/{employeeId}")]
    public async Task<ApiResponse<List<DormBooking>>> GetStayingRecords(int employeeId)
    {
        var result = await _service.GetStayingRecordsAsync(employeeId);
        return ApiResponse<List<DormBooking>>.Ok(result);
    }

    /// <summary>
    /// v2.13.32-hotfix BUG #2：获取员工的办理登记历史（按 EmployeeId，含入住/退房记录）
    /// 供 CheckIn 弹窗"员工历史办理"区块使用
    /// </summary>
    [HttpGet("employee-history/{employeeId}")]
    public async Task<ApiResponse<List<DormBooking>>> GetEmployeeHistory(int employeeId)
    {
        var result = await _db.DormBookings
            .Where(b => b.EmployeeId == employeeId)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.Id)
            .Take(50)
            .ToListAsync();
        return ApiResponse<List<DormBooking>>.Ok(result);
    }

    /// <summary>
    /// v2.13.32-hotfix BUG #2：一次性数据修复 — 把 DormBooking.EmployeeName 与 SysEmployee.RealName 不一致的记录修正
    /// 按 EmployeeId 优先 / EmployeeCode 次之匹配档案姓名
    /// </summary>
    [HttpPost("repair-employee-names")]
    public async Task<ApiResponse<(int Updated, int Skipped, int NotFound)>> RepairEmployeeNames()
    {
        return await _service.RepairBookingEmployeeNamesAsync();
    }

    /// <summary>
    /// v2.13.38：导出办理登记列表（按当前筛选条件生成 .xlsx 文件）
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? keyword = null,
        [FromQuery] string? department = null,
        [FromQuery] string? dormCode = null,
        [FromQuery] int? type = null,
        [FromQuery] int? status = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null)
    {
        DateOnly? df = string.IsNullOrEmpty(dateFrom) ? null : DateOnly.Parse(dateFrom);
        DateOnly? dt = string.IsNullOrEmpty(dateTo) ? null : DateOnly.Parse(dateTo);

        var pagedResult = await _service.GetListAsync(keyword, department, dormCode, type, status, df, dt, 1, 10000);
        var items = pagedResult.Items;

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("办理登记");
        // 表头（v2.13.38 与列表 12 列对齐）
        sheet.Cell(1, 1).Value = "序号";
        sheet.Cell(1, 2).Value = "工号";
        sheet.Cell(1, 3).Value = "姓名";
        sheet.Cell(1, 4).Value = "部门";
        sheet.Cell(1, 5).Value = "班次";
        sheet.Cell(1, 6).Value = "宿舍";
        sheet.Cell(1, 7).Value = "类型";
        sheet.Cell(1, 8).Value = "入退日期";
        sheet.Cell(1, 9).Value = "状态";
        sheet.Cell(1, 10).Value = "原因";
        sheet.Cell(1, 11).Value = "登记人";
        sheet.Cell(1, 12).Value = "登记时间";

        for (var i = 0; i < items.Count; i++)
        {
            var b = items[i];
            sheet.Cell(i + 2, 1).Value = i + 1;
            sheet.Cell(i + 2, 2).Value = b.EmployeeCode ?? "";
            sheet.Cell(i + 2, 3).Value = b.EmployeeName ?? "";
            sheet.Cell(i + 2, 4).Value = b.Department ?? "";
            sheet.Cell(i + 2, 5).Value = ""; // v2.13.38: DormBooking 暂无 AttendanceTypeName 字段，留空
            sheet.Cell(i + 2, 6).Value = b.DormCode ?? "";
            sheet.Cell(i + 2, 7).Value = b.Type == 1 ? "入住" : "退房";
            sheet.Cell(i + 2, 8).Value = b.BookingDate.ToString("yyyy-MM-dd");
            sheet.Cell(i + 2, 9).Value = b.Status switch { 1 => "预约", 2 => "在宿", 3 => "已退房", 4 => "已取消", _ => "-" };
            sheet.Cell(i + 2, 10).Value = b.Reason ?? "";
            sheet.Cell(i + 2, 11).Value = b.Registrar ?? "";
            sheet.Cell(i + 2, 12).Value = b.RegistrationDate.ToString("yyyy-MM-dd HH:mm");
        }

        sheet.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        var fileName = $"办理登记_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}

/// <summary>
/// 入住请求
/// </summary>
public class CheckInRequest
{
    public int EmployeeId { get; set; }
    public string DormCode { get; set; } = string.Empty;
    public string BookingDate { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 退房请求
/// </summary>
public class CheckOutRequest
{
    public string CheckOutDate { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新请求
/// </summary>
public class UpdateRequest
{
    public string BookingDate { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Remark { get; set; }
    // v2.13.38: 新增 Status 字段（前端可下拉修改预约记录的状态：1预约/2在宿/3已退房/4已取消）
    public int? Status { get; set; }
}
