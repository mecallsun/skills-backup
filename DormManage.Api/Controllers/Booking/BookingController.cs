using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Extensions;
using DormManage.Shared.Models;
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

    public BookingController(IBasicsService basicsService, IBookingService bookingService)
    {
        _service = bookingService;
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
        [FromQuery] int pageSize = 20)
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
        // v2.13.29: 从登录会话/请求头获取真实用户名（替换原 TODO 兜底 admin）
        var registrar = HttpContext.GetCurrentUserName();
        return await _service.CheckInAsync(new BookingCheckInRequest
        {
            EmployeeId = request.EmployeeId,
            DormCode = request.DormCode,
            BookingDate = DateOnly.Parse(request.BookingDate),
            Reason = request.Reason,
            Remark = request.Remark
        }, registrar);
    }

    /// <summary>
    /// 办理退房
    /// </summary>
    [HttpPost("{id}/check-out")]
    public async Task<ApiResponse<DormBooking>> CheckOut(int id, [FromBody] CheckOutRequest request)
    {
        var registrar = HttpContext.GetCurrentUserName();
        return await _service.CheckOutAsync(id, DateOnly.Parse(request.CheckOutDate), request.Reason, request.Remark, registrar);
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
            Remark = request.Remark
        });
    }

    /// <summary>
    /// 快速确认入住（v2.11.8）：Type=1 入住 && Status=1 预约 → Status=2 在宿
    /// </summary>
    [HttpPost("{id}/confirm-checkin")]
    public async Task<ApiResponse<DormBooking>> ConfirmCheckIn(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        return await _service.ConfirmCheckInAsync(id, registrar);
    }

    /// <summary>
    /// 撤销退房（v2.11.10）：Type=2 退房 && Status=3 已退房 && BookingDate=今天 → Status=2 在宿
    /// </summary>
    [HttpPost("{id}/undo-checkout")]
    public async Task<ApiResponse<DormBooking>> UndoCheckOut(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        return await _service.UndoCheckOutAsync(id, registrar);
    }

    /// <summary>
    /// 撤销预约（v2.11.11）：Status=1 预约 → Status=4 已取消
    /// </summary>
    [HttpPost("{id}/cancel-reservation")]
    public async Task<ApiResponse<DormBooking>> CancelReservation(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        return await _service.CancelReservationAsync(id, registrar);
    }

    /// <summary>
    /// 确认预约退房（v2.11.12）：Type=2 退房 && Status=1 预约 → Status=3 已退房
    /// </summary>
    [HttpPost("{id}/confirm-checkout")]
    public async Task<ApiResponse<DormBooking>> ConfirmCheckout(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        return await _service.ConfirmReservedCheckOutAsync(id, registrar);
    }

    /// <summary>
    /// 撤销在宿（v2.11.17）：Type=1 入住 && Status=2 在宿 && BookingDate=今天 → Status=4 已取消
    /// </summary>
    [HttpPost("{id}/cancel-today")]
    public async Task<ApiResponse<DormBooking>> CancelToday(int id)
    {
        var registrar = HttpContext.GetCurrentUserName();
        return await _service.CancelTodayAsync(id, registrar);
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
}
