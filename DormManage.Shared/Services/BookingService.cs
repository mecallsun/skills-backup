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

    /// <summary>
    /// v2.13.32-hotfix BUG：一次性数据修复 — 把 DormBooking.EmployeeName 与 SysEmployee.RealName 不一致的记录
    /// 用 SysEmployee.RealName 修正（按 EmployeeId 优先 / EmployeeCode 次之匹配）
    /// </summary>
    Task<ApiResponse<(int Updated, int Skipped, int NotFound)>> RepairBookingEmployeeNamesAsync();
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
    // v2.13.38: 新增 Status 字段（前端可下拉修改预约记录的状态：1预约/2在宿/3已退房/4已取消）
    public int? Status { get; set; }
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
    // v2.13.38: 补充员工类型 + 考勤班次字段（供 CheckIn 页面 Badge 渲染）
    public string? EmployeeType { get; set; }
    public string? EmployeeTypeName { get; set; }
    public string? AttendanceType { get; set; }
    public string? AttendanceTypeName { get; set; }
    // v2.13.88: 补充考勤班次编码（DEFAULT/MORNING/MIDDLE/EVENING/NIGHT/OTHER）用于 Badge 颜色映射
    public string? AttendanceTypeCode { get; set; }
    // v2.13.88: 性别（1=男 2=女 0=未知），用于 CheckIn 提示信息显示
    public int? Gender { get; set; }
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

    // ========== v2.13.84 性别约束新增字段 ==========
    /// <summary>当前在宿男员工数（Status=2 JOIN SysEmployee.Gender=1）</summary>
    public int MaleCount { get; set; }
    /// <summary>当前在宿女员工数（Status=2 JOIN SysEmployee.Gender=2）</summary>
    public int FemaleCount { get; set; }
    /// <summary>是否住满（CurrentCount >= Capacity）</summary>
    public bool IsFull => CurrentCount >= Capacity;
    /// <summary>不可选原因（"已住满" / "与现有男/女员工冲突" / ""=可分配）</summary>
    public string BlockReason { get; set; } = "";

    // ========== v2.13.88 床位号字段（用户需求：分配房间同时显示分配的床位号） ==========
    /// <summary>该宿舍的全部床位号列表（从 Dorm.BedNumbers CSV 解析）</summary>
    public List<int> AllBedNos { get; set; } = new();
    /// <summary>当前可用的床位号（AllBedNos 排除 Status=2 在宿员工的 BedNo）</summary>
    public List<int> AvailableBedNos { get; set; } = new();
    /// <summary>预览：下一个将分配的床位号（AvailableBedNos 最小值，0=无）</summary>
    public int NextAssignedBedNo { get; set; }
    /// <summary>床位号摘要（如 "3 / 4"，即 4 个床位已用 3 个）</summary>
    public string BedNoSummary { get; set; } = "";
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
        // v2.13.32-hotfix BUG：同时用 SysEmployee.RealName 实时覆盖 Booking.EmployeeName，
        //   解决「工号显示姓名与档案不一致」问题（档案改名后历史登记记录显示老姓名）。
        // v2.13.47 BUG：补充用 SysEmployee.EmployeeCode 实时覆盖 Booking.EmployeeCode，
        //   解决「人员清单改工号后历史登记记录显示旧工号」问题（与姓名同步策略一致）。
        // v2.13.66 BUG：补充用 SysEmployee.Department 实时覆盖 Booking.Department，
        //   解决「住宿登记列表部门未与档案同步」问题（用户在「人员清单」修改部门后，
        //   住宿登记列表仍显示旧部门或 NULL）。同步策略与姓名/工号完全一致。
        // v2.13.59 P0 BUG：使用 .AsNoTracking() + 在投影中用 ?? "" 防御性处理 NULL 字段，
        //   解决生产数据库历史脏数据（[Required] 字段实际为 NULL）导致的 SqlNullValueException。
        //   之前 EF Core 物化 DormBooking 实体时会在 ReadObject 阶段调用 GetString(i) 抛错。
        var query =
            from b in _db.DormBookings.AsNoTracking()
            join emp in _db.Employees.AsNoTracking() on b.EmployeeId equals emp.Id into empGroup
            from emp in empGroup.DefaultIfEmpty()
            select new
            {
                Booking = b,
                // 实时取考勤班次（v2.11.7 起 DormBooking.AttendanceTypeId 与 SysEmployee.AttendanceTypeId 同步）
                AttendanceTypeId = (int?)(emp != null ? emp.AttendanceTypeId : b.AttendanceTypeId),
                // v2.13.32-hotfix BUG：实时取最新姓名（档案优先；档案为空时回退登记时写入的姓名）
                RealName = emp != null && !string.IsNullOrEmpty(emp.RealName) ? emp.RealName : (b.EmployeeName ?? ""),
                // v2.13.47 BUG：实时取最新工号（人员清单为唯一真源；档案缺失时回退登记时写入的工号）
                EmployeeCode = emp != null && !string.IsNullOrEmpty(emp.EmployeeCode) ? emp.EmployeeCode : (b.EmployeeCode ?? ""),
                // v2.13.66 BUG：实时取最新部门（人员清单为唯一真源；档案缺失/为空时回退冗余字段；都不存在则 NULL）
                Department = emp != null && !string.IsNullOrEmpty(emp.Department) ? emp.Department : b.Department,
                // v2.13.86 性别：实时取档案 Gender（人员清单为唯一真源；档案缺失时回退 0=未知）
                Gender = (int?)(emp != null ? emp.Gender : 0) ?? 0
            };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.ToLower();
            query = query.Where(x =>
                (x.EmployeeCode ?? "").ToLower().Contains(keyword) ||
                (x.Booking.EmployeeCode ?? "").ToLower().Contains(keyword) ||
                (x.Booking.EmployeeName ?? "").ToLower().Contains(keyword) ||
                (x.RealName ?? "").ToLower().Contains(keyword) ||
                (x.Booking.Phone != null && x.Booking.Phone.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            // v2.13.66 BUG：部门筛选使用 emp.Department 优先（与覆盖策略一致），回退到 Booking.Department
            var dept = department.Trim().ToLower();
            query = query.Where(x =>
                (x.Department != null && x.Department.ToLower().Contains(dept)) ||
                (x.Booking.Department != null && x.Booking.Department.ToLower().Contains(dept)));
        }

        if (!string.IsNullOrWhiteSpace(dormCode))
        {
            var dc = dormCode.Trim().ToLower();
            query = query.Where(x => (x.Booking.DormCode ?? "").ToLower().Contains(dc));
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

        // v2.13.59 物化修复：使用 BookingDto 替代直接返回 DormBooking 实体，
        // 避免 EF Core 物化 [Required] string 字段为 NULL 时抛 SqlNullValueException。
        var dtos = items.Select(x => new BookingListDto
        {
            Id = x.Booking.Id,
            EmployeeId = x.Booking.EmployeeId,
            EmployeeCode = x.EmployeeCode ?? "",
            EmployeeName = x.RealName ?? "",
            Gender = x.Gender,
            Phone = x.Booking.Phone,
            Department = x.Department ?? x.Booking.Department,
            AttendanceTypeId = x.AttendanceTypeId ?? x.Booking.AttendanceTypeId,
            BedNo = x.Booking.BedNo,
            MoveFromDormCode = x.Booking.MoveFromDormCode,
            ActualCheckInDate = x.Booking.ActualCheckInDate,
            ActualCheckOutDate = x.Booking.ActualCheckOutDate,
            DormCode = x.Booking.DormCode ?? "",
            Type = x.Booking.Type,
            BookingDate = x.Booking.BookingDate,
            Status = x.Booking.Status,
            Reason = x.Booking.Reason,
            CancellationReason = x.Booking.CancellationReason,
            Remark = x.Booking.Remark,
            RegistrationDate = x.Booking.RegistrationDate,
            Registrar = x.Booking.Registrar ?? "",
            CheckInOperator = x.Booking.CheckInOperator,
            CheckOutOperator = x.Booking.CheckOutOperator,
            IsActive = x.Booking.IsActive,
            CreatedAt = x.Booking.CreatedAt,
            UpdatedAt = x.Booking.UpdatedAt
        }).ToList();

        // v2.13.32-hotfix / v2.13.47 物化阶段：覆盖 AttendanceTypeId / RealName / EmployeeCode
        // v2.13.66 BUG 修复扩展：增加覆盖 Department（人员清单为唯一真源）
        // v2.13.59 P0 BUG 修复：仅对非空字符串赋值（避免 NULL 覆盖回 null，符合 .AsNoTracking() 语义）
        foreach (var item in items)
        {
            if (item.AttendanceTypeId.HasValue)
            {
                item.Booking.AttendanceTypeId = item.AttendanceTypeId;
            }
            // v2.13.32-hotfix BUG：覆盖显示用姓名（仅 RAM，DB 不写）
            if (!string.IsNullOrEmpty(item.RealName))
            {
                item.Booking.EmployeeName = item.RealName;
            }
            // v2.13.47 BUG：覆盖显示用工号（仅 RAM，DB 不写；与姓名覆盖策略一致 — 人员清单为唯一真源）
            if (!string.IsNullOrEmpty(item.EmployeeCode))
            {
                item.Booking.EmployeeCode = item.EmployeeCode;
            }
            // v2.13.66 BUG：覆盖显示用部门（仅 RAM，DB 不写；人员清单为唯一真源）
            if (!string.IsNullOrEmpty(item.Department))
            {
                item.Booking.Department = item.Department;
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

    /// <summary>
    /// v2.13.59 P0 BUG 修复：办理记录列表 DTO
    /// 解决 EF Core 物化 DormBooking 实体时遇到 NULL 字符串字段抛 SqlNullValueException 的问题
    /// 所有 string 字段都使用 ?? "" 默认值，DB NULL 安全
    /// </summary>
    public class BookingListDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        // v2.13.86 性别字段（JOIN SysEmployee 取实时 Gender，人员清单为唯一真源）
        public int Gender { get; set; }
        public string GenderName => Gender == 1 ? "男" : Gender == 2 ? "女" : "未知";
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public int? AttendanceTypeId { get; set; }
        public int? BedNo { get; set; }
        public string? MoveFromDormCode { get; set; }
        public DateOnly? ActualCheckInDate { get; set; }
        public DateOnly? ActualCheckOutDate { get; set; }
        public string DormCode { get; set; } = "";
        public int Type { get; set; }
        public DateOnly BookingDate { get; set; }
        public int Status { get; set; }
        public string? Reason { get; set; }
        public string? CancellationReason { get; set; }
        public string? Remark { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string Registrar { get; set; } = "";
        public string? CheckInOperator { get; set; }
        public string? CheckOutOperator { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// v2.13.32-hotfix BUG：一次性数据修复 — 把 DormBooking 表中 EmployeeName 与 SysEmployee.RealName 不一致的记录
    /// 用 SysEmployee.RealName 修正（通过 EmployeeId JOIN，无 EmployeeId 时按 EmployeeCode 反查）。
    /// 用于"以人员清单的档案姓名的工号进行数据补充更正 关联"场景。
    /// v2.13.47 BUG 扩展：同时回填 EmployeeCode（人员清单为唯一真源，工号改了后历史 DormBooking.EmployeeCode 也需同步）。
    /// v2.13.66 BUG 扩展：同时回填 Department（人员清单为唯一真源，部门改了后历史 DormBooking.Department 也需同步）。
    /// </summary>
    public async Task<ApiResponse<(int Updated, int Skipped, int NotFound)>> RepairBookingEmployeeNamesAsync()
    {
        var allBookings = await _db.DormBookings
            .Where(b => !string.IsNullOrEmpty(b.EmployeeCode))
            .Select(b => new { b.Id, b.EmployeeId, b.EmployeeCode, b.EmployeeName, b.Department })
            .ToListAsync();

        if (allBookings.Count == 0)
            return ApiResponse<(int, int, int)>.Ok((0, 0, 0));

        var empIds = allBookings.Where(b => b.EmployeeId > 0).Select(b => b.EmployeeId).Distinct().ToList();
        var empCodes = allBookings.Select(b => b.EmployeeCode).Distinct().ToList();

        var employees = await _db.Employees
            .Where(e => empIds.Contains(e.Id) || empCodes.Contains(e.EmployeeCode))
            .Select(e => new { e.Id, e.EmployeeCode, e.RealName, e.Department })
            .ToListAsync();

        var byId = employees.ToDictionary(e => e.Id);
        var byCode = employees.ToDictionary(e => e.EmployeeCode);

        int updated = 0, skipped = 0, notFound = 0;
        var affected = new List<DormBooking>();
        foreach (var b in allBookings)
        {
            // v2.13.47：先按 EmployeeId 取人员，再按工号反查
            SysEmployeeLite? emp = null;
            if (b.EmployeeId > 0 && byId.TryGetValue(b.EmployeeId, out var byIdEmp))
                emp = new SysEmployeeLite { Id = byIdEmp.Id, EmployeeCode = byIdEmp.EmployeeCode, RealName = byIdEmp.RealName, Department = byIdEmp.Department };
            else if (byCode.TryGetValue(b.EmployeeCode, out var byCodeEmp))
                emp = new SysEmployeeLite { Id = byCodeEmp.Id, EmployeeCode = byCodeEmp.EmployeeCode, RealName = byCodeEmp.RealName, Department = byCodeEmp.Department };

            if (emp is null) { notFound++; continue; }

            // v2.13.47：仅修正 RealName/EmployeeCode 都不一致的记录
            // v2.13.66 BUG：扩展为 RealName/EmployeeCode/Department 任一不一致即更新
            bool nameChanged = !string.IsNullOrEmpty(emp.RealName) && !string.Equals(emp.RealName, b.EmployeeName, StringComparison.Ordinal);
            bool codeChanged = !string.IsNullOrEmpty(emp.EmployeeCode) && !string.Equals(emp.EmployeeCode, b.EmployeeCode, StringComparison.Ordinal);
            bool deptChanged = !string.IsNullOrEmpty(emp.Department) && !string.Equals(emp.Department, b.Department, StringComparison.Ordinal);
            if (!nameChanged && !codeChanged && !deptChanged) { skipped++; continue; }

            // 找出原 booking 实体并更新
            var entity = await _db.DormBookings.FindAsync(b.Id);
            if (entity is null) continue;
            if (nameChanged) entity.EmployeeName = emp.RealName;
            if (codeChanged) entity.EmployeeCode = emp.EmployeeCode;
            if (deptChanged) entity.Department = emp.Department;
            entity.UpdatedAt = DateTime.Now;
            affected.Add(entity);
            updated++;
        }

        if (affected.Count > 0)
            await _db.SaveChangesAsync();

        return ApiResponse<(int, int, int)>.Ok((updated, skipped, notFound));
    }

    /// <summary>
    /// v2.13.47：人员清单轻量 DTO（用于 Repair API 内部传递）
    /// v2.13.66 BUG：扩展包含 Department
    /// </summary>
    private class SysEmployeeLite
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string RealName { get; set; } = "";
        public string? Department { get; set; }
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

        // 5.5 v2.13.84 性别约束（业务硬约束三层防御的第二层：Service 层兜底）
        // 即使前端 disabled/隐藏了不同性别房间，恶意请求绕过时仍由 Service 校验拒绝
        var stayingGenders = await _db.DormBookings
            .Where(x => x.DormCode == request.DormCode && x.Status == BookingStatus.Staying)
            .Join(_db.Employees.AsNoTracking(),
                  b => b.EmployeeId, e => e.Id,
                  (b, e) => e.Gender)
            .ToListAsync();

        if (stayingGenders.Any())
        {
            // 房间已有在宿人员 → 必须同性别
            int empGender = employee.Gender;
            if (empGender == 0)
                return ApiResponse<DormBooking>.Fail("EMP_GENDER_UNKNOWN",
                    "员工档案性别未知，请先在「人员清单」完善性别信息");

            bool hasMale = stayingGenders.Any(g => g == 1);
            bool hasFemale = stayingGenders.Any(g => g == 2);

            if (empGender == 1 && hasFemale)
                return ApiResponse<DormBooking>.Fail("DORM_GENDER_CONFLICT",
                    $"该宿舍当前在宿女员工 {stayingGenders.Count(g => g == 2)} 人，禁止男员工入住");

            if (empGender == 2 && hasMale)
                return ApiResponse<DormBooking>.Fail("DORM_GENDER_CONFLICT",
                    $"该宿舍当前在宿男员工 {stayingGenders.Count(g => g == 1)} 人，禁止女员工入住");
        }
        // 空房间或同性别 → 放行

        // 6. 创建办理记录
        // v2.13.24 P75：同步填充 BedNo, ActualCheckInDate, CheckInOperator
        // v2.13.88 BUG 修复：基于 Dorm.BedNumbers（CSV 床位号字符串）分配最小未占用床位号
        // 旧逻辑：BedNo = activeCount+1 → 仅是占位序号，与真实床位号不符
        var targetDorm = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == request.DormCode);
        int assignedBedNo = 0;
        if (targetDorm != null && !string.IsNullOrWhiteSpace(targetDorm.BedNumbers))
        {
            // 解析 CSV 床位号（如 "1,2,3,4"）
            var allBedNos = targetDorm.BedNumbers
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var n) ? (int?)n : null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            if (allBedNos.Count > 0)
            {
                // 取出当前已占用的床位号（双重 fallback：历史脏数据 DormBooking.BedNo=NULL 时用 SysEmployee.BedNo）
                var occupiedBedNos2 = new List<int>();
                var bookingBedNos2 = await _db.DormBookings
                    .Where(b => b.DormCode == request.DormCode && b.Status == BookingStatus.Staying && b.BedNo.HasValue)
                    .Select(b => b.BedNo!.Value)
                    .ToListAsync();
                occupiedBedNos2.AddRange(bookingBedNos2);
                var empBedNos2 = await _db.Employees
                    .Where(e => e.DormCode == request.DormCode && e.BedNo.HasValue)
                    .Select(e => e.BedNo!.Value)
                    .ToListAsync();
                occupiedBedNos2.AddRange(empBedNos2);
                var occupiedBedNos = occupiedBedNos2.Distinct().ToList();

                // 从 allBedNos 中排除已占用，取最小值
                var availableBedNos = allBedNos.Except(occupiedBedNos).OrderBy(n => n).ToList();
                if (availableBedNos.Count > 0)
                {
                    assignedBedNo = availableBedNos.First();
                }
                else
                {
                    // 床位号全部被占用 → 回退到 activeCount+1 模式（兜底）
                    assignedBedNo = currentStaying + reserved + 1;
                }
            }
            else
            {
                // 床位号未配置 → 回退到 activeCount+1
                assignedBedNo = currentStaying + reserved + 1;
            }
        }
        else
        {
            // Dorm 表无 BedNumbers → 回退到 activeCount+1
            assignedBedNo = currentStaying + reserved + 1;
        }

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
            // v2.13.24 P75 新增字段 + v2.13.88 床位号分配（基于 Dorm.BedNumbers 真实床位）
            BedNo = assignedBedNo,
            ActualCheckInDate = request.BookingDate,
            CheckInOperator = registrar,
            AttendanceTypeId = employee.AttendanceTypeId,
            RegistrationDate = DateTime.Now,
            Registrar = registrar,
            CreatedAt = DateTime.Now
        };

        _db.DormBookings.Add(booking);

        // 7. 更新员工的当前宿舍 + 床位号 + 住宿状态（v2.13.77：统一通过 SyncEmployeeDormCodeAsync 联动）
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, booking.DormCode, registrar, "入住办理", booking.BedNo);

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
        // v2.13.77：扩展 bedNo 参数，退房时清空 BedNo
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "退房", booking.BedNo);

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
        // v2.13.77：扩展 bedNo 参数，写入 BedNo
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, booking.DormCode, registrar, "快速确认入住", booking.BedNo);

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
        // v2.13.77：恢复 BedNo
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, booking.DormCode, registrar, "撤销退房", booking.BedNo);

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
        // v2.13.77：清空 BedNo
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "撤销在宿", booking.BedNo);

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
        // v2.13.77：清空 BedNo
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "创建退房记录", booking.BedNo);

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(checkOutBooking);
    }

    /// <summary>
    /// v2.11.18 新增：同步 SysEmployee.DormCode
    /// v2.11.20 修订：同时同步 ResidenceStatusId（关联引用基础资料-住宿状态字典）
    /// v2.13.77 修订：扩展 bedNo 参数，退房时同时清空 BedNo（人员清单床位列联动）
    /// </summary>
    /// <remarks>
    /// 规则：
    /// - dormCode != null → 设置 PERSONNEL.DormCode = dormCode，BedNo = bedNo（若提供），ResidenceStatusId = 1(LODGED)
    /// - dormCode == null → 清空 PERSONNEL.DormCode = NULL，BedNo = NULL，ResidenceStatusId = 2(NOT_LODGED)
    /// - 异常时记录日志，不中断主流程（保证 BOOKINGS 操作不影响）
    ///
    /// v2.13.77 业务规则：
    /// - 入住路径（dormCode != null）：如果提供 bedNo → 写入；不提供则保留原值（向后兼容）
    /// - 退房路径（dormCode == null）：**始终清空 BedNo = NULL**（核心修复：之前退房不清床位号）
    /// - 宿舍端联动：Dorm.CurrentCount 由派生查询实时计算（不存储在 Dorm 表），
    ///   退房后下一次查询 Dorm 列表/详情自动反映 -1；Dorm.BedNumbers 是静态候选床位列表，不因退房而变
    /// </remarks>
    private async Task SyncEmployeeDormCodeAsync(int employeeId, string? dormCode, string registrar, string operation, int? bedNo = null)
    {
        try
        {
            var employee = await _db.Employees.FindAsync(employeeId);
            if (employee == null) return;

            if (dormCode != null)
            {
                // 入住/撤销退房 → 同步更新 dormCode / BedNo / 住宿状态=LODGED
                var changed = false;
                if (employee.DormCode != dormCode) { employee.DormCode = dormCode; changed = true; }
                if (bedNo.HasValue && employee.BedNo != bedNo.Value) { employee.BedNo = bedNo.Value; changed = true; }
                if (employee.ResidenceStatusId != 1) { employee.ResidenceStatusId = 1; changed = true; } // LODGED
                if (changed)
                {
                    _db.Employees.Update(employee);
                    Console.WriteLine($"[v2.13.77 SyncEmployeeDormCode] {operation}: EmployeeId={employeeId}, DormCode={dormCode}, BedNo={bedNo}, ResidenceStatusId=1(LODGED), Registrar={registrar}");
                }
            }
            else
            {
                // 退房/撤销入住 → 清空 dormCode / BedNo / 住宿状态=NOT_LODGED
                // v2.13.77 P0 修复：必须同时清空 BedNo，否则人员清单床位列残留旧床位号
                if (employee.DormCode != null || employee.BedNo != null || employee.ResidenceStatusId != 2)
                {
                    employee.DormCode = null;
                    employee.BedNo = null;  // v2.13.77 新增：清空床位号
                    employee.ResidenceStatusId = 2; // NOT_LODGED 未住宿
                    _db.Employees.Update(employee);
                    Console.WriteLine($"[v2.13.77 SyncEmployeeDormCode] {operation}: EmployeeId={employeeId}, DormCode=NULL, BedNo=NULL, ResidenceStatusId=2(NOT_LODGED), Registrar={registrar}");
                }
            }
        }
        catch (Exception ex)
        {
            // 异常时提示信息，不中断主流程（用户可在前端提示确认）
            Console.WriteLine($"[v2.13.77 SyncEmployeeDormCode ERROR] {operation}: EmployeeId={employeeId}, Error={ex.Message}");
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
        // v2.13.38: 当 Status 字段被传入时，更新状态（仅 Status=1 预约时前端才会传）
        if (request.Status.HasValue)
        {
            booking.Status = request.Status.Value;
        }
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
                DormCode = x.DormCode,
                // v2.13.38: 映射员工类型 + 考勤班次（FK Name 用于 Badge 渲染）
                EmployeeType = x.EmployeeTypeId > 0 ? x.EmployeeTypeId.ToString() : null,
                EmployeeTypeName = x.EmployeeType != null ? x.EmployeeType.Name : null,
                AttendanceType = x.AttendanceTypeId.HasValue ? x.AttendanceTypeId.Value.ToString() : null,
                AttendanceTypeName = x.AttendanceType != null ? x.AttendanceType.Name : null,
                // v2.13.88: 补充考勤班次编码 + 性别（FK Name/Code 替代 id 字符串）
                AttendanceTypeCode = x.AttendanceType != null ? x.AttendanceType.Code : null,
                Gender = x.Gender
            })
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<DormOption>> GetAvailableDormsAsync(int employeeId, DateOnly bookingDate)
    {
        // v2.13.84 性别约束：先获取员工性别（Gender=0/1/2）
        var employee = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.Gender })
            .FirstOrDefaultAsync();
        if (employee == null) return new List<DormOption>();
        int empGender = employee.Gender;

        // v2.13.84 性别分布：JOIN DormBookings(Status=2) → SysEmployee 拿在宿人员 Gender 分布
        var stayingDetails = await _db.DormBookings
            .Where(x => x.Status == BookingStatus.Staying)
            .Join(_db.Employees.AsNoTracking(),
                  b => b.EmployeeId, e => e.Id,
                  (b, e) => new { b.DormCode, e.Gender })
            .GroupBy(x => x.DormCode)
            .Select(g => new
            {
                DormCode = g.Key,
                TotalCount = g.Count(),
                MaleCount = g.Count(x => x.Gender == 1),
                FemaleCount = g.Count(x => x.Gender == 2)
            })
            .ToListAsync();
        var stayingMap = stayingDetails.ToDictionary(x => x.DormCode);

        // 预约入住人数（保留原逻辑）
        var reservedCounts = await _db.DormBookings
            .Where(x => x.Type == BookingType.CheckIn && x.Status == BookingStatus.Reserved && x.BookingDate <= bookingDate)
            .GroupBy(x => x.DormCode)
            .Select(g => new { DormCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DormCode, x => x.Count);

        var availableDorms = await _db.Dorms
            .Where(x => x.IsActive)
            .ToListAsync();

        // v2.13.84 三层过滤：余量 + 性别 + 完全隐藏住满
        var result = new List<DormOption>();
        foreach (var d in availableDorms)
        {
            var staying = stayingMap.GetValueOrDefault(d.DormCode);
            var totalCount = staying?.TotalCount ?? 0;
            var maleCount = staying?.MaleCount ?? 0;
            var femaleCount = staying?.FemaleCount ?? 0;
            var reserved = reservedCounts.GetValueOrDefault(d.DormCode, 0);
            var available = d.Capacity - totalCount - reserved;

            // 规则 1：余量 > 0（与原逻辑一致，住满则 available <= 0 自动 skip）
            if (available <= 0) continue;

            // 规则 2：v2.13.84 性别约束
            // - 空房间（totalCount=0）：可选（任何 Gender 员工都能进）
            // - 员工 Gender=0（未知）：仅当房间为空时可选（用户决策）
            // - 员工 Gender=1（男）：现有必须是男（femaleCount=0）
            // - 员工 Gender=2（女）：现有必须是女（maleCount=0）
            if (totalCount == 0)
            {
                // 空房间 → 放行
            }
            else if (empGender == 0)
            {
                continue;  // 性别未知员工禁止入住有人的房间
            }
            else if (empGender == 1 && femaleCount > 0)
            {
                continue;  // 男员工禁止进入有女员工的房间
            }
            else if (empGender == 2 && maleCount > 0)
            {
                continue;  // 女员工禁止进入有男员工的房间
            }

            // v2.13.88 计算床位号：解析 Dorm.BedNumbers → 排除 Status=2 已占用 → 取最小可用
            // 已占用床位号 = DormBooking.BedNo（v2.13.24+）+ SysEmployee.BedNo（fallback 历史脏数据 NULL）
            var allBedNos = string.IsNullOrWhiteSpace(d.BedNumbers)
                ? new List<int>()
                : d.BedNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var n) ? (int?)n : null)
                    .Where(n => n.HasValue).Select(n => n!.Value).ToList();

            var occupiedBedNos = new List<int>();
            // 来源 1：DormBooking.BedNo（v2.13.24+ 才有值）
            var bookingBedNos = await _db.DormBookings
                .Where(b => b.DormCode == d.DormCode && b.Status == BookingStatus.Staying && b.BedNo.HasValue)
                .Select(b => b.BedNo!.Value)
                .ToListAsync();
            occupiedBedNos.AddRange(bookingBedNos);
            // 来源 2：SysEmployee.BedNo（fallback 历史脏数据；该字段始终维护中）
            var empBedNos = await _db.Employees
                .Where(e => e.DormCode == d.DormCode && e.BedNo.HasValue)
                .Select(e => e.BedNo!.Value)
                .ToListAsync();
            occupiedBedNos.AddRange(empBedNos);
            occupiedBedNos = occupiedBedNos.Distinct().ToList();

            var availableBedNos = allBedNos.Except(occupiedBedNos).OrderBy(n => n).ToList();
            var nextBedNo = availableBedNos.FirstOrDefault();
            var bedNoSummary = allBedNos.Count > 0
                ? $"床位 {occupiedBedNos.Count}/{allBedNos.Count} · 下一个分配 {nextBedNo}号"
                : "未配置床位";

            result.Add(new DormOption
            {
                DormCode = d.DormCode,
                BuildingName = d.BuildingName ?? "",
                AddressText = d.AddressText ?? "",
                Capacity = d.Capacity,
                CurrentCount = totalCount,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                // v2.13.88 床位号信息（用户需求：分配房间同时显示分配的床位号）
                AllBedNos = allBedNos,
                AvailableBedNos = availableBedNos,
                NextAssignedBedNo = nextBedNo,
                BedNoSummary = bedNoSummary
            });
        }

        return result;
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

        // 同步 PERSONNEL.dormCode=null + BedNo=NULL
        await SyncEmployeeDormCodeAsync(booking.EmployeeId, null, registrar, "快速确认退房", booking.BedNo);

        await _db.SaveChangesAsync();
        return ApiResponse<DormBooking>.Ok(booking);
    }
}


