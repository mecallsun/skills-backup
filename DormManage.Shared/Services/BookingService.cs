using Microsoft.EntityFrameworkCore;
using System.Data;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 办理记录服务接口
/// </summary>
public interface IBookingService
{
    /// <summary>
    /// 获取办理记录列表
    /// </summary>
    Task<PagedResult<DormBooking>> GetListAsync(string? keyword, string? department, string? dormCode, int? type, int? status, DateOnly? dateFrom, DateOnly? dateTo, int page, int pageSize);

    /// <summary>
    /// 获取办理记录详情
    /// </summary>
    Task<DormBooking?> GetByIdAsync(int id);

    /// <summary>
    /// 办理入住
    /// </summary>
    Task<ApiResponse<DormBooking>> CheckInAsync(BookingCheckInRequest request, string registrar);

    /// <summary>
    /// 办理退房
    /// </summary>
    Task<ApiResponse<DormBooking>> CheckOutAsync(int id, DateOnly checkOutDate, string? reason, string? remark, string registrar);

    /// <summary>
    /// 快速确认入住（v2.11.8）：Type=1 入住 && Status=1 预约 → 即时变更为 Status=2 在宿
    /// v2.11.18 修订：同步 SysEmployee.DormCode
    /// </summary>
    Task<ApiResponse<DormBooking>> ConfirmCheckInAsync(int id, string registrar);

    /// <summary>
    /// 撤销退房（v2.11.10）：Type=2 退房 && Status=3 已退房 && BookingDate=今天 → 变更为 Status=2 在宿
    /// v2.11.18 修订：同步 SysEmployee.DormCode
    /// </summary>
    Task<ApiResponse<DormBooking>> UndoCheckOutAsync(int id, string registrar);

    /// <summary>
    /// 撤销预约（v2.11.11）：Status=1 预约 → 即时变更为 Status=4 已取消
    /// v2.11.18 修订：未生效预约，不修改 SysEmployee.DormCode
    /// </summary>
    Task<ApiResponse<DormBooking>> CancelReservationAsync(int id, string registrar);

    /// <summary>
    /// 撤销在宿（v2.11.17）：Type=1 入住 && Status=2 在宿 && BookingDate=今天 → 变更为 Status=4 已取消
    /// v2.11.18 修订：同时清空 SysEmployee.DormCode
    /// </summary>
    Task<ApiResponse<DormBooking>> CancelTodayAsync(int id, string registrar);

    /// <summary>
    /// 创建退房记录（v2.11.17）：Type=1 入住 && Status=2 在宿 → 创建 Type=2 退房新记录
    /// v2.11.18 修订：同时清空 SysEmployee.DormCode
    /// </summary>
    Task<ApiResponse<DormBooking>> ConfirmCheckOutCreateAsync(int id, DateOnly checkOutDate, string? reason, string? remark, string registrar);

    /// <summary>
    /// 更新记录
    /// </summary>
    Task<ApiResponse<DormBooking>> UpdateAsync(int id, BookingUpdateRequest request);

    /// <summary>
    /// 删除记录（仅预约/已取消状态可删除）
    /// </summary>
    Task<ApiResponse> DeleteAsync(int id);

    /// <summary>
    /// 搜索员工（用于联动下拉）
    /// </summary>
    Task<List<EmployeeSearchResult>> SearchEmployeeAsync(string keyword);

    /// <summary>
    /// 获取可选房间列表（有余量且日期不冲突）
    /// </summary>
    Task<List<DormOption>> GetAvailableDormsAsync(int employeeId, DateOnly bookingDate);

    /// <summary>
    /// v2.13.20 获取所有启用房号（用于列表页房号 combobox 候选）
    /// </summary>
    Task<List<string>> GetAllDormCodesAsync();

    /// <summary>
    /// 获取员工的在宿记录（用于退房选择）
    /// </summary>
    Task<List<DormBooking>> GetStayingRecordsAsync(int employeeId);
    Task<ApiResponse<DormBooking>> ConfirmReservedCheckOutAsync(int id, string registrar);
}

/// <summary>
/// 办理入住请求
/// </summary>
public class BookingCheckInRequest
{
    public int EmployeeId { get; set; }
    public string DormCode { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; }
    public string? Reason { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新记录请求
/// </summary>
public class BookingUpdateRequest
{
    public DateOnly BookingDate { get; set; }
    public string? Reason { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 员工搜索结果
/// </summary>
public class EmployeeSearchResult
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string RealName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public DateOnly? HireDate { get; set; }
    public string? DormCode { get; set; }
}

/// <summary>
/// 宿舍选项
/// </summary>
public class DormOption
{
    public string DormCode { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int CurrentCount { get; set; }
    public int AvailableCount => Capacity - CurrentCount;
}

/// <summary>
/// 办理记录服务实现
/// </summary>
public class BookingService : IBookingService
{
    private readonly DormDbContext _db;

    public BookingService(DormDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<DormBooking>> GetListAsync(string? keyword, string? department, string? dormCode, int? type, int? status, DateOnly? dateFrom, DateOnly? dateTo, int page, int pageSize)
    {
        // v2.13.24：通过 JOIN SysEmployee 实时取考勤班次（NULL 时 fallback 到 DormBooking.AttendanceTypeId）
        var query =
            from b in _db.DormBookings
            join emp in _db.Employees on b.EmployeeId equals emp.Id into empGroup
            from emp in empGroup.DefaultIfEmpty()
            select new
            {
                Booking = b,
                // 实时取考勤班次（v2.11.7 起 DormBooking.AttendanceTypeId 与 SysEmployee.AttendanceTypeId 同步）
                AttendanceTypeId = (int?)(emp != null ? emp.AttendanceTypeId : b.AttendanceTypeId)
            };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.ToLower();
            query = query.Where(x =>
                x.Booking.EmployeeCode.ToLower().Contains(keyword) ||
                x.Booking.EmployeeName.ToLower().Contains(keyword) ||
                (x.Booking.Phone != null && x.Booking.Phone.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var dept = department.Trim().ToLower();
            query = query.Where(x => x.Booking.Department != null && x.Booking.Department.ToLower().Contains(dept));
        }

        if (!string.IsNullOrWhiteSpace(dormCode))
        {
            var dc = dormCode.Trim().ToLower();
            query = query.Where(x => x.Booking.DormCode.ToLower().Contains(dc));
        }

        if (type.HasValue)
            query = query.Where(x => x.Booking.Type == type.Value);

        if (status.HasValue)
            query = query.Where(x => x.Booking.Status == status.Value);

        if (dateFrom.HasValue)
            query = query.Where(x => x.Booking.BookingDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.Booking.BookingDate <= dateTo.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.Booking.BookingDate)
            .ThenByDescending(x => x.Booking.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 物化：把 AttendanceTypeId 写入 DormBooking.AttendanceTypeId（v2.13.24 字段），便于 Razor 直接渲染
        foreach (var item in items)
        {
            if (item.AttendanceTypeId.HasValue)
            {
                item.Booking.AttendanceTypeId = item.AttendanceTypeId;
            }
        }

        return new PagedResult<DormBooking>
        {
            Items = items.Select(x => x.Booking).ToList(),
            TotalCount = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<DormBooking?> GetByIdAsync(int id)
    {
        return await _db.DormBookings.FindAsync(id);
    }

    /// <summary>
    /// 在「可串行化」事务中执行容量敏感操作，消除并发超容 / 同床双分配竞态。
    /// - 经执行策略包裹以兼容 EnableRetryOnFailure（死锁 1205 自动重试整个委托）；
    /// - 每次尝试前 ChangeTracker.Clear() 保证重试干净（委托内部重新加载实体）；
    /// - 校验失败/提前返回时依赖 await using 自动回滚，仅成功路径显式提交。
    /// </summary>
    private Task<ApiResponse<DormBooking>> InSerializableTxAsync(Func<Task<ApiResponse<DormBooking>>> body)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await body();
            if (result.Success)
                await tx.CommitAsync();
            return result;
        });
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> CheckInAsync(BookingCheckInRequest request, string registrar)
    {
        return await InSerializableTxAsync(async () =>
        {
        // 1. 获取员工信息
        var employee = await _db.Employees.FindAsync(request.EmployeeId);
        if (employee == null)
            return ApiResponse<DormBooking>.Fail("EMPLOYEE_NOT_FOUND", "员工不存在");

        // 2. 获取宿舍信息
        var dorm = await _db.Dorms.FirstOrDefaultAsync(x => x.DormCode == request.DormCode);
        if (dorm == null)
            return ApiResponse<DormBooking>.Fail("DORM_NOT_FOUND", "宿舍不存在");

        if (!dorm.IsActive)
            return ApiResponse<DormBooking>.Fail("DORM_INACTIVE", "宿舍已停用");

        // 3. 校验：入住日期不能早于入职日期
        if (employee.HireDate.HasValue && request.BookingDate < employee.HireDate.Value)
            return ApiResponse<DormBooking>.Fail("DATE_BEFORE_HIRE", "入住日期不能早于入职日期");

        // 4. 校验：获取员工最后一条记录
        var lastRecord = await _db.DormBookings
            .Where(x => x.EmployeeId == request.EmployeeId)
            .OrderByDescending(x => x.BookingDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (lastRecord != null)
        {
            // 若最后一条为「在宿」→ 报错
            if (lastRecord.Status == BookingStatus.Staying)
                return ApiResponse<DormBooking>.Fail("ALREADY_STAYING", $"员工已在宿 {lastRecord.DormCode}，请先办理退房");

            // 若最后一条为「退房」且日期 ≥ 本次入住日期 → 报错
            if (lastRecord.Type == BookingType.CheckOut && lastRecord.BookingDate >= request.BookingDate)
                return ApiResponse<DormBooking>.Fail("DATE_ORDER_ERROR", "请检查日期顺序");

            // 入住日期必须 ≥ 上一次退房日期 + 1天
            if (lastRecord.Type == BookingType.CheckOut && request.BookingDate < lastRecord.BookingDate.AddDays(1))
                return ApiResponse<DormBooking>.Fail("DATE_GAP_REQUIRED", "入住日期必须在上一次退房日期之后");
        }

        // 5. 校验房间余量
        var currentStaying = await _db.DormBookings.CountAsync(x =>
            x.DormCode == request.DormCode && x.Status == BookingStatus.Staying);

        var reserved = await _db.DormBookings.CountAsync(x =>
            x.DormCode == request.DormCode && x.Type == BookingType.CheckIn && x.Status == BookingStatus.Reserved &&
            x.BookingDate <= request.BookingDate);

        var available = dorm.Capacity - currentStaying - reserved;
        if (available <= 0)
            return ApiResponse<DormBooking>.Fail("NO_CAPACITY", "该宿舍已满员");

        // 6. 创建办理记录
        // v2.13.24 P75：同步填充 BedNo = activeCount+1, ActualCheckInDate, CheckInOperator
        var activeCount = currentStaying + reserved + 1;  // 含本次
        var booking = new DormBooking
        {
            EmployeeId = request.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = employee.RealName,
            Phone = employee.Phone,
            Department = employee.Department,
            DormCode = request.DormCode,
            Type = BookingType.CheckIn,
            BookingDate = request.BookingDate,
            Status = BookingStatus.Staying,
            Reason = request.Reason,
            Remark = request.Remark,
            // v2.13.24 P75 新增字段
            BedNo = activeCount,
            ActualCheckInDate = request.BookingDate,
            CheckInOperator = registrar,
            AttendanceTypeId = employee.AttendanceTypeId,
            RegistrationDate = DateTime.Now,
            Registrar = registrar,
            CreatedAt = DateTime.Now
        };

        _db.DormBookings.Add(booking);

        // 7. 更新员工的当前宿舍
        employee.DormCode = request.DormCode;
        _db.Employees.Update(employee);

        await _db.SaveChangesAsync();

        return ApiResponse<DormBooking>.Ok(booking);
        });
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> CheckOutAsync(int id, DateOnly checkOutDate, string? reason, string? remark, string registrar)
    {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        if (booking.Status != BookingStatus.Staying)
            return ApiResponse<DormBooking>.Fail("STATUS_ERROR", "只有「在宿」状态的记录才能办理退房");

        // 退房日期不能晚于今天
        if (checkOutDate > DateOnly.FromDateTime(DateTime.Now))
            return ApiResponse<DormBooking>.Fail("DATE_FUTURE", "退房日期不能晚于今天");

        // 获取下一条记录（如果有）
        var nextRecord = await _db.DormBookings
            .Where(x => x.EmployeeId == booking.EmployeeId && x.BookingDate > booking.BookingDate)
            .OrderBy(x => x.BookingDate)
            .FirstOrDefaultAsync();

        if (nextRecord != null && nextRecord.Type == BookingType.CheckIn && checkOutDate >= nextRecord.BookingDate)
            return ApiResponse<DormBooking>.Fail("DATE_CONFLICT", "退房日期不能晚于下一次入住日期");

        // 更新记录
        booking.Type = BookingType.CheckOut;
        booking.BookingDate = checkOutDate;
        booking.Status = BookingStatus.CheckedOut;
        booking.Reason = reason;
        booking.Remark = remark;
        booking.UpdatedAt = DateTime.Now;
        // v2.13.24 P75：退房时记录实际退房日期和操作人
        booking.ActualCheckOutDate = checkOutDate;
        booking.CheckOutOperator = registrar;

        _db.DormBookings.Update(booking);

        // v2.11.18：清除员工的当前宿舍（同步 PERSONNEL.dormCode）
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "退房");

        await _db.SaveChangesAsync();

        return ApiResponse<DormBooking>.Ok(booking);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> ConfirmCheckInAsync(int id, string registrar)
    {
        return await InSerializableTxAsync(async () =>
        {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        if (booking.Type != BookingType.CheckIn)
            return ApiResponse<DormBooking>.Fail("INVALID_TYPE", "仅 Type=1 入住类型可快速确认");

        if (booking.Status != BookingStatus.Reserved)
            return ApiResponse<DormBooking>.Fail("INVALID_STATUS", "仅 Status=1 预约状态可快速确认");

        // 校验房间床位余量
        var dorm = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == booking.DormCode);
        if (dorm == null)
            return ApiResponse<DormBooking>.Fail("DORM_NOT_FOUND", "宿舍不存在");

        var currentStaying = await _db.DormBookings.CountAsync(b =>
            b.DormCode == booking.DormCode && b.Status == BookingStatus.Staying && b.Id != booking.Id);
        if (currentStaying >= dorm.Capacity)
            return ApiResponse<DormBooking>.Fail("NO_CAPACITY", "床位已满，请更换其他房间");

        booking.Status = BookingStatus.Staying;
        booking.Registrar = registrar;
        booking.RegistrationDate = DateTime.Now;
        booking.UpdatedAt = DateTime.Now;
        // v2.13.24 P75：快速确认入住时记录实际入住日期和操作人
        booking.ActualCheckInDate = booking.BookingDate;
        booking.CheckInOperator = registrar;
        _db.DormBookings.Update(booking);

        // v2.11.18：同步 PERSONNEL.dormCode = 该记录的 dormCode
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, booking.DormCode, registrar, "快速确认入住");

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(booking);
        });
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> UndoCheckOutAsync(int id, string registrar)
    {
        return await InSerializableTxAsync(async () =>
        {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        if (booking.Status != BookingStatus.CheckedOut)
            return ApiResponse<DormBooking>.Fail("INVALID_STATUS", "仅 Status=3 已退房可撤销");

        if (booking.BookingDate != DateOnly.FromDateTime(DateTime.Now))
            return ApiResponse<DormBooking>.Fail("NOT_TODAY", "仅入退日期为今天的记录可撤销");

        // 校验房间床位余量
        var dorm = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == booking.DormCode);
        if (dorm == null)
            return ApiResponse<DormBooking>.Fail("DORM_NOT_FOUND", "宿舍不存在");

        var currentStaying = await _db.DormBookings.CountAsync(b =>
            b.DormCode == booking.DormCode && b.Status == BookingStatus.Staying && b.Id != booking.Id);
        if (currentStaying >= dorm.Capacity)
            return ApiResponse<DormBooking>.Fail("NO_CAPACITY", "床位已满，撤销退房失败！");

        booking.Status = BookingStatus.Staying;
        booking.Registrar = registrar;
        booking.RegistrationDate = DateTime.Now;
        booking.UpdatedAt = DateTime.Now;
        _db.DormBookings.Update(booking);

        // v2.11.18：撤销退房 → 同步恢复 PERSONNEL.dormCode
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, booking.DormCode, registrar, "撤销退房");

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(booking);
        });
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> CancelReservationAsync(int id, string registrar)
    {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        if (booking.Status != BookingStatus.Reserved)
            return ApiResponse<DormBooking>.Fail("INVALID_STATUS", "仅 Status=1 预约可撤销");

        booking.Status = BookingStatus.Cancelled;
        booking.Registrar = registrar;
        booking.RegistrationDate = DateTime.Now;
        booking.UpdatedAt = DateTime.Now;
        // v2.13.24 P75：取消时记录操作人
        booking.CheckInOperator = registrar;  // 视为撤销该次预约
        _db.DormBookings.Update(booking);

        // v2.11.18：撤销预约（未生效），不修改 PERSONNEL.dormCode
        // 因为预约从未将 PERSONNEL.dormCode 设置为该记录 dormCode

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(booking);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> CancelTodayAsync(int id, string registrar)
    {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        if (booking.Type != BookingType.CheckIn || booking.Status != BookingStatus.Staying)
            return ApiResponse<DormBooking>.Fail("INVALID_STATUS", "仅 Type=1 入住 && Status=2 在宿 可撤销");

        if (booking.BookingDate != DateOnly.FromDateTime(DateTime.Now))
            return ApiResponse<DormBooking>.Fail("NOT_TODAY", "仅入退日期为今天的记录可撤销");

        booking.Status = BookingStatus.Cancelled;
        booking.Registrar = registrar;
        booking.RegistrationDate = DateTime.Now;
        booking.UpdatedAt = DateTime.Now;
        // v2.13.24 P75：撤销在宿时记录
        booking.CheckInOperator = registrar;
        _db.DormBookings.Update(booking);

        // v2.11.18：撤销在宿 → 清空 PERSONNEL.dormCode
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "撤销在宿");

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(booking);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> ConfirmCheckOutCreateAsync(int id, DateOnly checkOutDate, string? reason, string? remark, string registrar)
    {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        if (booking.Type != BookingType.CheckIn || booking.Status != BookingStatus.Staying)
            return ApiResponse<DormBooking>.Fail("INVALID_STATUS", "仅 Type=1 入住 && Status=2 在宿 可退房");

        // 创建一条退房记录
        // v2.13.24 P75：填充 BedNo 同步、ActualCheckOutDate、CheckOutOperator
        var checkOutBooking = new DormBooking
        {
            EmployeeId = booking.EmployeeId,
            EmployeeCode = booking.EmployeeCode,
            EmployeeName = booking.EmployeeName,
            Phone = booking.Phone,
            Department = booking.Department,
            DormCode = booking.DormCode,
            BedNo = booking.BedNo,
            Type = BookingType.CheckOut,
            BookingDate = checkOutDate,
            Status = BookingStatus.CheckedOut,
            Reason = reason ?? "退房",
            Remark = remark,
            ActualCheckInDate = booking.ActualCheckInDate ?? booking.BookingDate,
            ActualCheckOutDate = checkOutDate,
            CheckInOperator = booking.CheckInOperator,
            CheckOutOperator = registrar,
            AttendanceTypeId = booking.AttendanceTypeId,
            RegistrationDate = DateTime.Now,
            Registrar = registrar,
            CreatedAt = DateTime.Now
        };
        _db.DormBookings.Add(checkOutBooking);

        // 将原记录状态变更为已退房（保留作为历史）
        booking.Status = BookingStatus.CheckedOut;
        booking.UpdatedAt = DateTime.Now;
        booking.ActualCheckOutDate = checkOutDate;
        booking.CheckOutOperator = registrar;
        _db.DormBookings.Update(booking);

        // v2.11.18：创建退房记录 → 清空 PERSONNEL.dormCode
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "创建退房记录");

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(checkOutBooking);
    }

    /// <summary>
    /// v2.11.18 新增：同步 SysEmployee.DormCode
    /// v2.11.20 修订：同时同步 ResidenceStatusId（关联引用基础资料-住宿状态字典）
    /// </summary>
    /// <remarks>
    /// 规则：
    /// - dormCode != null → 设置 PERSONNEL.DormCode = dormCode，ResidenceStatusId = 1(LODGED)
    /// - dormCode == null → 清空 PERSONNEL.DormCode = NULL，ResidenceStatusId = 2(NOT_LODGED)
    /// - 异常时记录日志，不中断主流程（保证 BOOKINGS 操作不影响）
    /// </remarks>
    private async Task SyncEmployeeDormCodeAsync(int employeeId, string? dormCode, string registrar, string operation)
    {
        try
        {
            var employee = await _db.Employees.FindAsync(employeeId);
            if (employee == null) return;

            if (dormCode != null)
            {
                // 入住/撤销退房 → 同步更新 dormCode 和 住宿状态=LODGED
                employee.DormCode = dormCode;
                employee.ResidenceStatusId = 1; // LODGED 已住宿
                _db.Employees.Update(employee);
                Console.WriteLine($"[v2.11.20 SyncEmployeeDormCode] {operation}: EmployeeId={employeeId}, DormCode={dormCode}, ResidenceStatusId=1(LODGED), Registrar={registrar}");
            }
            else
            {
                // 退房/撤销入住 → 清空 dormCode 和 住宿状态=NOT_LODGED
                if (employee.DormCode != null || employee.ResidenceStatusId != 2)
                {
                    employee.DormCode = null;
                    employee.ResidenceStatusId = 2; // NOT_LODGED 未住宿
                    _db.Employees.Update(employee);
                    Console.WriteLine($"[v2.11.20 SyncEmployeeDormCode] {operation}: EmployeeId={employeeId}, DormCode=NULL, ResidenceStatusId=2(NOT_LODGED), Registrar={registrar}");
                }
            }
        }
        catch (Exception ex)
        {
            // 异常时提示信息，不中断主流程（用户可在前端提示确认）
            Console.WriteLine($"[v2.11.20 SyncEmployeeDormCode ERROR] {operation}: EmployeeId={employeeId}, Error={ex.Message}");
            throw new InvalidOperationException($"同步人员宿舍失败：{ex.Message}。请确认后返回重试。", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> UpdateAsync(int id, BookingUpdateRequest request)
    {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        // v2.13.12: 仅预约状态(Status=1)可修改
        if (booking.Status != BookingStatus.Reserved)
            return ApiResponse<DormBooking>.Fail("STATUS_ERROR", "仅预约状态的记录可以修改");

        booking.BookingDate = request.BookingDate;
        booking.Reason = request.Reason;
        booking.Remark = request.Remark;
        booking.UpdatedAt = DateTime.Now;

        _db.DormBookings.Update(booking);
        await _db.SaveChangesAsync();

        return ApiResponse<DormBooking>.Ok(booking);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse> DeleteAsync(int id)
    {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        if (booking.Status != BookingStatus.Reserved && booking.Status != BookingStatus.Cancelled)
            return ApiResponse.Fail("STATUS_ERROR", "只有「预约」或「已取消」状态的记录可以删除");

        _db.DormBookings.Remove(booking);
        await _db.SaveChangesAsync();

        return ApiResponse.Ok();
    }

    /// <inheritdoc/>
    public async Task<List<EmployeeSearchResult>> SearchEmployeeAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<EmployeeSearchResult>();

        keyword = keyword.ToLower();
        return await _db.Employees
            .Where(x => x.Status == EmployeeStatus.Active &&
                (x.EmployeeCode.ToLower().Contains(keyword) ||
                 x.RealName.ToLower().Contains(keyword) ||
                 (x.Phone != null && x.Phone.Contains(keyword))))
            .Take(10)
            .Select(x => new EmployeeSearchResult
            {
                Id = x.Id,
                EmployeeCode = x.EmployeeCode,
                RealName = x.RealName,
                Phone = x.Phone,
                Department = x.Department,
                HireDate = x.HireDate,
                DormCode = x.DormCode
            })
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<DormOption>> GetAvailableDormsAsync(int employeeId, DateOnly bookingDate)
    {
        // 获取当前在宿人数和预约入住人数
        var stayingCounts = await _db.DormBookings
            .Where(x => x.Status == BookingStatus.Staying)
            .GroupBy(x => x.DormCode)
            .Select(g => new { DormCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DormCode, x => x.Count);

        var reservedCounts = await _db.DormBookings
            .Where(x => x.Type == BookingType.CheckIn && x.Status == BookingStatus.Reserved && x.BookingDate <= bookingDate)
            .GroupBy(x => x.DormCode)
            .Select(g => new { DormCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DormCode, x => x.Count);

        var availableDorms = await _db.Dorms
            .Where(x => x.IsActive)
            .ToListAsync();

        return availableDorms
            .Where(d =>
            {
                var staying = stayingCounts.GetValueOrDefault(d.DormCode, 0);
                var reserved = reservedCounts.GetValueOrDefault(d.DormCode, 0);
                return d.Capacity - staying - reserved > 0;
            })
            .Select(d => new DormOption
            {
                DormCode = d.DormCode,
                BuildingName = d.BuildingName ?? "",
                AddressText = d.AddressText ?? "",
                Capacity = d.Capacity,
                CurrentCount = stayingCounts.GetValueOrDefault(d.DormCode, 0)
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAllDormCodesAsync()
    {
        return await _db.Dorms
            .Where(x => x.IsActive)
            .OrderBy(x => x.DormCode)
            .Select(x => x.DormCode)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<DormBooking>> GetStayingRecordsAsync(int employeeId)
    {
        return await _db.DormBookings
            .Where(x => x.EmployeeId == employeeId && x.Status == BookingStatus.Staying)
            .OrderByDescending(x => x.BookingDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DormBooking>> ConfirmReservedCheckOutAsync(int id, string registrar)
    {
        var booking = await _db.DormBookings.FindAsync(id);
        if (booking == null)
            return ApiResponse<DormBooking>.Fail("NOT_FOUND", "记录不存在");

        if (booking.Type != BookingType.CheckOut)
            return ApiResponse<DormBooking>.Fail("INVALID_TYPE", "仅 Type=2 退房类型可快速确认");

        if (booking.Status != BookingStatus.Reserved)
            return ApiResponse<DormBooking>.Fail("INVALID_STATUS", "仅 Status=1 预约状态可快速确认");

        // 退房日期不能晚于今天
        if (booking.BookingDate > DateOnly.FromDateTime(DateTime.Now))
            return ApiResponse<DormBooking>.Fail("DATE_FUTURE", "退房日期不能晚于今天");

        // 退房日期不能晚于下一次入住日期
        var nextRecord = await _db.DormBookings
            .Where(x => x.EmployeeId == booking.EmployeeId && x.BookingDate > booking.BookingDate)
            .OrderBy(x => x.BookingDate)
            .FirstOrDefaultAsync();
        if (nextRecord != null && nextRecord.Type == BookingType.CheckIn && booking.BookingDate >= nextRecord.BookingDate)
            return ApiResponse<DormBooking>.Fail("DATE_CONFLICT", "退房日期不能晚于下一次入住日期");

        // 更新预约记录为已退房
        booking.Status = BookingStatus.CheckedOut;
        booking.Registrar = registrar;
        booking.RegistrationDate = DateTime.Now;
        booking.UpdatedAt = DateTime.Now;
        _db.DormBookings.Update(booking);

        // 同步 PERSONNEL.dormCode=null
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "快速确认退房");

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(booking);
    }
}


