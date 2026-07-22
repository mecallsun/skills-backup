using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Api.Controllers.Meter;

/// <summary>
/// 抄表记录 API 控制器
/// </summary>
[ApiController]
[Route("api/meter")]
public class MeterController : ControllerBase
{
    private readonly DormDbContext _db;

    public MeterController(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取抄表记录列表
    /// </summary>
    [HttpGet("records")]
    public async Task<ApiResponse<PagedResult<MeterRecordDto>>> GetRecords(
        string? dormCode,
        string? readMonth,
        byte? status,
        int page = 1,
        int pageSize = 20)
    {
        var query = _db.MeterRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(dormCode))
            query = query.Where(r => r.DormCode.Contains(dormCode));

        if (!string.IsNullOrWhiteSpace(readMonth))
            query = query.Where(r => r.ReadMonth == readMonth);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync();
        // v2.13.89：JOIN SysUser 拿抄表员 DisplayName（页面渲染）
        var itemsWithUser = await query
            .OrderByDescending(r => r.ReadMonth)
            .ThenBy(r => r.DormCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(_db.SysUsers.AsNoTracking(),
                r => r.OperatorUserId,
                u => u.Id,
                (r, userGroup) => new { Record = r, User = userGroup.FirstOrDefault() })
            .ToListAsync();

        var dtos = itemsWithUser.Select(x => new MeterRecordDto
        {
            Id = x.Record.Id,
            DormId = x.Record.DormId,
            DormCode = x.Record.DormCode,
            ReadMonth = x.Record.ReadMonth,
            ColdMeter = x.Record.ColdMeter,
            HotMeter = x.Record.HotMeter,
            ElectricMeter = x.Record.ElectricMeter,
            ColdUsage = x.Record.ColdUsage,
            HotUsage = x.Record.HotUsage,
            ElectricUsage = x.Record.ElectricUsage,
            PreviousColdReading = x.Record.PreviousColdReading,
            PreviousHotReading = x.Record.PreviousHotReading,
            PreviousElectricReading = x.Record.PreviousElectricReading,
            ReadDate = x.Record.ReadDate,
            ReadMode = x.Record.ReadMode,
            CorrectionReason = x.Record.CorrectionReason,
            CorrectedBy = x.Record.CorrectedBy,
            CorrectedAt = x.Record.CorrectedAt,
            ConfirmedAt = x.Record.ConfirmedAt,
            Operator = x.Record.Operator,
            OperatorUserId = x.Record.OperatorUserId,
            // v2.13.89：JOIN DisplayName 优先显示，回退到冗余 Operator 字符串
            OperatorDisplayName = x.User != null ? x.User.DisplayName : x.Record.Operator,
            Status = x.Record.Status,
            StatusName = x.Record.Status.ToString() ?? "Unknown",
            Remark = x.Record.Remark,
            ServerCreatedAt = x.Record.ServerCreatedAt
        }).ToList();

        return ApiResponse<PagedResult<MeterRecordDto>>.Ok(new PagedResult<MeterRecordDto>
        {
            Items = dtos,
            TotalCount = total,
            PageIndex = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 获取抄表记录详情
    /// </summary>
    [HttpGet("records/{id}")]
    public async Task<ApiResponse<MeterRecordDto>> GetRecord(long id)
    {
        var record = await _db.MeterRecords.FindAsync(id);
        if (record == null)
            return ApiResponse<MeterRecordDto>.Fail("NOT_FOUND", "记录不存在");

        // v2.13.89：JOIN SysUser 拿抄表员 DisplayName
        var operatorUser = record.OperatorUserId.HasValue
            ? await _db.SysUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == record.OperatorUserId.Value)
            : null;

        var dto = new MeterRecordDto
        {
            Id = record.Id,
            DormId = record.DormId,
            DormCode = record.DormCode,
            ReadMonth = record.ReadMonth,
            ColdMeter = record.ColdMeter ,
            HotMeter = record.HotMeter ,
            ElectricMeter = record.ElectricMeter ,
            ColdUsage = record.ColdUsage,
            HotUsage = record.HotUsage,
            ElectricUsage = record.ElectricUsage,
            PreviousColdReading = record.PreviousColdReading,
            PreviousHotReading = record.PreviousHotReading,
            PreviousElectricReading = record.PreviousElectricReading,
            ReadDate = record.ReadDate,
            ReadMode = record.ReadMode,
            CorrectionReason = record.CorrectionReason,
            CorrectedBy = record.CorrectedBy,
            CorrectedAt = record.CorrectedAt,
            ConfirmedAt = record.ConfirmedAt,
            Operator = record.Operator,
            OperatorUserId = record.OperatorUserId,
            OperatorDisplayName = operatorUser != null ? operatorUser.DisplayName : record.Operator,
            Status = record.Status,
            StatusName = record.Status.ToString() ?? "Unknown",
            Remark = record.Remark,
            ServerCreatedAt = record.ServerCreatedAt
        };

        return ApiResponse<MeterRecordDto>.Ok(dto);
    }

    /// <summary>
    /// 新增/更新抄表记录（覆盖模式）
    /// </summary>
    [HttpPost("records")]
    public async Task<ApiResponse<MeterRecordDto>> SaveRecord([FromBody] MeterRecordSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DormCode))
            return ApiResponse<MeterRecordDto>.Fail("CODE_REQUIRED", "宿舍号不能为空");

        if (string.IsNullOrWhiteSpace(request.ReadMonth))
            return ApiResponse<MeterRecordDto>.Fail("MONTH_REQUIRED", "抄表月份不能为空");

        // 查找宿舍
        var dorm = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == request.DormCode && d.IsActive);
        if (dorm == null)
            return ApiResponse<MeterRecordDto>.Fail("DORM_NOT_FOUND", "宿舍不存在或已停用");

        // 查找已有记录
        var existing = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.DormCode == request.DormCode && r.ReadMonth == request.ReadMonth);

        MeterRecord record;
        string message;

        if (existing != null)
        {
            // 检查是否为已修正状态
            if (existing.Status == 2)
            {
                return ApiResponse<MeterRecordDto>.Fail("CORRECTED_RECORD", "该记录已定稿，需走修正流程");
            }

            // 记录历史
            var historyEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm} 覆盖前: 冷水={existing.ColdMeter}, 热水={existing.HotMeter}, 电={existing.ElectricMeter}]";
            var newRemark = string.IsNullOrEmpty(request.Remark) ? historyEntry : request.Remark + " " + historyEntry;

            // v2.13.24 P76：覆盖时计算用量 = 本次读数 - 上月读数
            var newColdUsage = Math.Max(0, request.ColdMeter - existing.PreviousColdReading);
            var newHotUsage = Math.Max(0, request.HotMeter - existing.PreviousHotReading);
            var newElectricUsage = Math.Max(0, request.ElectricMeter - existing.PreviousElectricReading);

            // 覆盖更新
            existing.PreviousColdReading = existing.ColdMeter;  // 更新下次的上月参考
            existing.PreviousHotReading = existing.HotMeter;
            existing.PreviousElectricReading = existing.ElectricMeter;
            existing.ColdMeter = request.ColdMeter;
            existing.HotMeter = request.HotMeter;
            existing.ElectricMeter = request.ElectricMeter;
            existing.ColdUsage = newColdUsage;
            existing.HotUsage = newHotUsage;
            existing.ElectricUsage = newElectricUsage;
            existing.Operator = string.IsNullOrEmpty(request.Operator) ? existing.Operator : request.Operator;
            existing.Status = MeterRecord.DetermineStatus(newColdUsage, newHotUsage, newElectricUsage);
            existing.Remark = newRemark;
            existing.UpdatedAt = DateTime.Now;
            existing.ServerCreatedAt = DateTime.Now;
            if (request.ReadDate.HasValue) existing.ReadDate = request.ReadDate;
            if (request.ReadMode.HasValue) existing.ReadMode = request.ReadMode.Value;

            record = existing;
            message = "更新成功";
        }
        else
        {
            // 新建记录
            // v2.13.24 P76：取上月读数作为 PreviousXxxReading，并计算用量
            var previousRecord = await _db.MeterRecords
                .Where(r => r.DormCode == request.DormCode && r.ReadMonth.CompareTo(request.ReadMonth) < 0)
                .OrderByDescending(r => r.ReadMonth)
                .FirstOrDefaultAsync();

            var prevCold = previousRecord?.ColdMeter ?? 0;
            var prevHot = previousRecord?.HotMeter ?? 0;
            var prevElec = previousRecord?.ElectricMeter ?? 0;
            var coldUsage = Math.Max(0, request.ColdMeter - prevCold);
            var hotUsage = Math.Max(0, request.HotMeter - prevHot);
            var elecUsage = Math.Max(0, request.ElectricMeter - prevElec);

            record = new MeterRecord
            {
                DormId = dorm.Id,
                DormCode = request.DormCode,
                ReadMonth = request.ReadMonth,
                ColdMeter = request.ColdMeter,
                HotMeter = request.HotMeter,
                ElectricMeter = request.ElectricMeter,
                ColdUsage = coldUsage,
                HotUsage = hotUsage,
                ElectricUsage = elecUsage,
                PreviousColdReading = prevCold,
                PreviousHotReading = prevHot,
                PreviousElectricReading = prevElec,
                Operator = request.Operator ?? "管理员",
                // v2.13.89：抄表员 UserId FK（用户需求：表存 FK，页面显示姓名）
                OperatorUserId = HttpContext.GetCurrentUserId() > 0 ? HttpContext.GetCurrentUserId() : null,
                // v2.13.80 修复：SQL Server ClientRecordId + DeviceSn 都是 NOT NULL，但 C# 模型为 string?
                // 手动补录/PDA 上传时必须赋值，否则 INSERT 失败 "不能将值 NULL 插入列 ClientRecordId"
                // 手动补录场景：DeviceSn="" + ClientRecordId="MANUAL-{Guid}"
                DeviceSn = request.DeviceSn ?? "",
                ClientRecordId = !string.IsNullOrWhiteSpace(request.ClientRecordId)
                    ? request.ClientRecordId
                    : $"MANUAL-{Guid.NewGuid():N}".Substring(0, 32),
                ClientCreatedAt = request.ClientCreatedAt ?? DateTime.Now,
                Status = MeterRecord.DetermineStatus(coldUsage, hotUsage, elecUsage),
                Remark = request.Remark,
                ReadDate = request.ReadDate ?? DateOnly.FromDateTime(DateTime.Now),
                ReadMode = request.ReadMode ?? MeterReadMode.Manual,
                ServerCreatedAt = DateTime.Now
            };

            _db.MeterRecords.Add(record);
            message = "创建成功";
        }

        // v2.13.24 P77 联动3：同步 Dorm 表抄表缓存字段（PDA 扫码抄表性能优化）
        var dormCache = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == record.DormCode);
        if (dormCache != null)
        {
            dormCache.LastReadMonth = record.ReadMonth;
            dormCache.LastColdMeter = record.ColdMeter;
            dormCache.LastHotMeter = record.HotMeter;
            dormCache.LastElectricMeter = record.ElectricMeter;
            dormCache.LastReadAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        return ApiResponse<MeterRecordDto>.Ok(new MeterRecordDto
        {
            Id = record.Id,
            DormId = record.DormId,
            DormCode = record.DormCode,
            ReadMonth = record.ReadMonth,
            ColdMeter = record.ColdMeter ,
            HotMeter = record.HotMeter ,
            ElectricMeter = record.ElectricMeter ,
            ColdUsage = record.ColdUsage,
            HotUsage = record.HotUsage,
            ElectricUsage = record.ElectricUsage,
            PreviousColdReading = record.PreviousColdReading,
            PreviousHotReading = record.PreviousHotReading,
            PreviousElectricReading = record.PreviousElectricReading,
            ReadDate = record.ReadDate,
            ReadMode = record.ReadMode,
            CorrectionReason = record.CorrectionReason,
            CorrectedBy = record.CorrectedBy,
            CorrectedAt = record.CorrectedAt,
            ConfirmedAt = record.ConfirmedAt,
            Operator = record.Operator,
            Status = record.Status,
            StatusName = record?.Status.ToString() ?? "Unknown",
            Remark = record.Remark,
            ServerCreatedAt = record.ServerCreatedAt
        }, message);
    }

    /// <summary>
    /// 修正抄表记录
    /// </summary>
    [HttpPut("records/{id}/correct")]
    public async Task<ApiResponse<MeterRecordDto>> CorrectRecord(long id, [FromBody] MeterRecordCorrectRequest request)
    {
        var record = await _db.MeterRecords.FindAsync(id);
        if (record == null)
            return ApiResponse<MeterRecordDto>.Fail("NOT_FOUND", "记录不存在");

        if (record.Status != 1)
            return ApiResponse<MeterRecordDto>.Fail("INVALID_STATUS", "只有正常状态的记录才能修正");

        // 记录历史
        var historyEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm} 修正前: 冷水={record.ColdMeter}, 热水={record.HotMeter}, 电={record.ElectricMeter}]";
        record.Remark = (request.Remark ?? "") + " " + historyEntry;
        record.UpdatedAt = DateTime.Now;
        record.ServerCreatedAt = DateTime.Now;
        record.Status = 2; // 已修正
        // v2.13.24 P76：修正追踪
        record.CorrectionReason = request.CorrectionReason ?? request.Remark ?? "";
        record.CorrectedBy = request.CorrectedBy ?? "admin";
        record.CorrectedAt = DateTime.Now;
        // v2.13.24 P76：修正后重新计算用量
        if (record.PreviousColdReading > 0) record.ColdUsage = Math.Max(0, record.ColdMeter - record.PreviousColdReading);
        if (record.PreviousHotReading > 0) record.HotUsage = Math.Max(0, record.HotMeter - record.PreviousHotReading);
        if (record.PreviousElectricReading > 0) record.ElectricUsage = Math.Max(0, record.ElectricMeter - record.PreviousElectricReading);

        await _db.SaveChangesAsync();

        return ApiResponse<MeterRecordDto>.Ok(new MeterRecordDto
        {
            Id = record.Id,
            DormId = record.DormId,
            DormCode = record.DormCode,
            ReadMonth = record.ReadMonth,
            ColdMeter = record.ColdMeter ,
            HotMeter = record.HotMeter ,
            ElectricMeter = record.ElectricMeter ,
            ColdUsage = record.ColdUsage,
            HotUsage = record.HotUsage,
            ElectricUsage = record.ElectricUsage,
            PreviousColdReading = record.PreviousColdReading,
            PreviousHotReading = record.PreviousHotReading,
            PreviousElectricReading = record.PreviousElectricReading,
            ReadDate = record.ReadDate,
            ReadMode = record.ReadMode,
            CorrectionReason = record.CorrectionReason,
            CorrectedBy = record.CorrectedBy,
            CorrectedAt = record.CorrectedAt,
            ConfirmedAt = record.ConfirmedAt,
            Operator = record.Operator,
            Status = record.Status,
            StatusName = record?.Status.ToString() ?? "Unknown",
            Remark = record.Remark,
            ServerCreatedAt = record.ServerCreatedAt
        }, "修正成功");
    }

    /// <summary>
    /// 删除抄表记录（仅未完成状态）
    /// </summary>
    [HttpDelete("records/{id}")]
    public async Task<ApiResponse> DeleteRecord(long id)
    {
        var record = await _db.MeterRecords.FindAsync(id);
        if (record == null)
            return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        if (record.Status != 0)
            return ApiResponse.Fail("INVALID_STATUS", "仅未完成状态的记录可删除");

        var dormCode = record.DormCode;
        var readMonth = record.ReadMonth;

        _db.MeterRecords.Remove(record);
        await _db.SaveChangesAsync();

        // 删除后重建占位记录
        var hasRecord = await _db.MeterRecords.AnyAsync(r => r.DormCode == dormCode && r.ReadMonth == readMonth);
        if (!hasRecord)
        {
            var dorm = await _db.Dorms.FirstOrDefaultAsync(d => d.DormCode == dormCode && d.IsActive);
            if (dorm != null)
            {
                _db.MeterRecords.Add(new MeterRecord
                {
                    DormId = dorm.Id,
                    DormCode = dormCode,
                    ReadMonth = readMonth,
                    ColdMeter = 0,
                    HotMeter = 0,
                    ElectricMeter = 0,
                    Operator = "系统自动生成",
                    Status = 0,
                    Remark = $"[{DateTime.Now:yyyy-MM-dd HH:mm} 删除后重建占位记录]",
                    ServerCreatedAt = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }
        }

        return ApiResponse.Ok("删除成功");
    }

    /// <summary>
    /// 作废抄表记录（P1-8）：保留数据但标记为已作废，用于误录或错误纠正
    /// </summary>
    [HttpPost("records/{id}/void")]
    public async Task<ApiResponse<MeterRecordDto>> VoidRecord(long id, [FromBody] MeterVoidRequest request)
    {
        var record = await _db.MeterRecords.FindAsync(id);
        if (record == null)
            return ApiResponse<MeterRecordDto>.Fail("NOT_FOUND", "记录不存在");

        if (record.Status == (byte)MeterRecordStatus.Voided)
            return ApiResponse<MeterRecordDto>.Fail("ALREADY_VOIDED", "记录已是作废状态");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<MeterRecordDto>.Fail("REASON_REQUIRED", "作废原因必填");

        var oldStatus = record.Status;
        record.Status = (byte)MeterRecordStatus.Voided;
        var voidNote = $"[作废 {DateTime.Now:yyyy-MM-dd HH:mm} 原状态={oldStatus} 原因={request.Reason} 操作人={request.Operator}]";
        record.Remark = string.IsNullOrEmpty(record.Remark) ? voidNote : $"{record.Remark}\n{voidNote}";

        await _db.SaveChangesAsync();

        return ApiResponse<MeterRecordDto>.Ok(BuildDto(record), $"记录已作废：{request.Reason}");
    }

    /// <summary>
    /// 撤销作废（仅 admin 可操作）
    /// </summary>
    [HttpPost("records/{id}/unvoid")]
    public async Task<ApiResponse<MeterRecordDto>> UnvoidRecord(long id)
    {
        var record = await _db.MeterRecords.FindAsync(id);
        if (record == null)
            return ApiResponse<MeterRecordDto>.Fail("NOT_FOUND", "记录不存在");

        if (record.Status != (byte)MeterRecordStatus.Voided)
            return ApiResponse<MeterRecordDto>.Fail("NOT_VOIDED", "记录非作废状态");

        record.Status = (byte)MeterRecordStatus.Normal;
        var unvoidNote = $"[撤销作废 {DateTime.Now:yyyy-MM-dd HH:mm}]";
        record.Remark = string.IsNullOrEmpty(record.Remark) ? unvoidNote : $"{record.Remark}\n{unvoidNote}";

        await _db.SaveChangesAsync();

        return ApiResponse<MeterRecordDto>.Ok(BuildDto(record), "已撤销作废");
    }

    private static MeterRecordDto BuildDto(MeterRecord r) => new()
    {
        Id = r.Id,
        DormId = r.DormId,
        DormCode = r.DormCode,
        ReadMonth = r.ReadMonth,
        ColdMeter = r.ColdMeter,
        HotMeter = r.HotMeter,
        ElectricMeter = r.ElectricMeter,
        Operator = r.Operator,
        Status = r.Status,
        StatusName = ((MeterRecordStatus)r.Status).GetDisplayName(),
        Remark = r.Remark,
        ServerCreatedAt = r.ServerCreatedAt
    };

    /// <summary>
    /// 获取可选的抄表月份列表
    /// </summary>
    [HttpGet("months")]
    public async Task<ApiResponse<List<string>>> GetMonths()
    {
        var months = await _db.MeterRecords
            .Select(r => r.ReadMonth)
            .Distinct()
            .OrderByDescending(m => m)
            .Take(12)
            .ToListAsync();

        // 添加当前月份
        var currentMonth = DateTime.Now.ToString("yyyy-MM");
        if (!months.Contains(currentMonth))
        {
            months.Insert(0, currentMonth);
        }

        return ApiResponse<List<string>>.Ok(months);
    }

    /// <summary>
    /// 手动补录单条抄表记录
    /// </summary>
    [HttpPost("manual-entry")]
    public async Task<ApiResponse<MeterRecordDto>> ManualEntry([FromBody] MeterRecordSaveRequest request)
    {
        return await SaveRecord(request);
    }

    /// <summary>
    /// 批量导入抄表记录
    /// </summary>
    [HttpPost("batch-import")]
    public async Task<ApiResponse<BatchImportResult>> BatchImport([FromBody] List<MeterRecordSaveRequest> records)
    {
        if (records == null || records.Count == 0)
            return ApiResponse<BatchImportResult>.Fail("EMPTY_DATA", "导入数据不能为空");

        var result = new BatchImportResult
        {
            TotalCount = records.Count,
            SuccessCount = 0,
            FailCount = 0,
            FailedItems = new List<string>()
        };

        foreach (var record in records)
        {
            try
            {
                var resp = await SaveRecord(record);
                if (resp.Success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailCount++;
                    result.FailedItems.Add($"{record.DormCode}/{record.ReadMonth}: {resp.Message}");
                }
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.FailedItems.Add($"{record.DormCode}/{record.ReadMonth}: {ex.Message}");
            }
        }

        return ApiResponse<BatchImportResult>.Ok(result,
            $"导入完成：成功 {result.SuccessCount} 条，失败 {result.FailCount} 条");
    }

    /// <summary>
    /// 导出抄表记录为 Excel (CSV 格式)
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        string? dormCode,
        string? readMonth,
        byte? status)
    {
        var query = _db.MeterRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(dormCode))
            query = query.Where(r => r.DormCode.Contains(dormCode));

        if (!string.IsNullOrWhiteSpace(readMonth))
            query = query.Where(r => r.ReadMonth == readMonth);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var records = await query
            .OrderByDescending(r => r.ReadMonth)
            .ThenBy(r => r.DormCode)
            .ToListAsync();

        // 生成 CSV (Excel 可打开) — v2.13.24 P76：扩展列含用量
        var sb = new global::System.Text.StringBuilder();
        sb.AppendLine("序号,宿舍号,抄表月份,冷水读数(m³),热水读数(m³),电读数(度),冷水用量(m³),热水用量(m³),电用量(度),操作员,状态,抄表日期,抄表方式,备注");
        var index = 1;
        foreach (var r in records)
        {
            sb.AppendLine($"{index},{r.DormCode},{r.ReadMonth},{r.ColdMeter},{r.HotMeter},{r.ElectricMeter},{r.ColdUsage},{r.HotUsage},{r.ElectricUsage},{r.Operator},{r?.Status.ToString() ?? "Unknown"},{r.ReadDate},{r.ReadMode},{r.Remark ?? ""}");
            index++;
        }

        var bytes = global::System.Text.Encoding.UTF8.GetPreamble()
            .Concat(global::System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        var fileName = $"抄表记录_{DateTime.Now:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}

/// <summary>
/// 批量导入结果
/// </summary>
public class BatchImportResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<string> FailedItems { get; set; } = new();
}

/// <summary>
/// 抄表记录数据传输对象（v2.13.24 P76：增加用量字段 + 上月读数参考 + 业务深度字段）
/// </summary>
public class MeterRecordDto
{
    public long Id { get; set; }
    public int DormId { get; set; }
    public string DormCode { get; set; } = "";
    public string ReadMonth { get; set; } = "";
    public decimal ColdMeter { get; set; }
    public decimal HotMeter { get; set; }
    public decimal ElectricMeter { get; set; }
    // v2.13.24 P76：用量字段
    public decimal ColdUsage { get; set; }
    public decimal HotUsage { get; set; }
    public decimal ElectricUsage { get; set; }
    // v2.13.24 P76：上月读数参考
    public decimal PreviousColdReading { get; set; }
    public decimal PreviousHotReading { get; set; }
    public decimal PreviousElectricReading { get; set; }
    // v2.13.24 P76：业务深度字段
    public DateOnly? ReadDate { get; set; }
    public byte ReadMode { get; set; }
    public string? CorrectionReason { get; set; }
    public string? CorrectedBy { get; set; }
    public DateTime? CorrectedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    // v2.13.89：抄表员 UserId FK + DisplayName（JOIN 派生）
    public int? OperatorUserId { get; set; }
    public string? OperatorDisplayName { get; set; }

    public string Operator { get; set; } = "";
    public byte Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Remark { get; set; }
    public DateTime ServerCreatedAt { get; set; }
}

/// <summary>
/// 抄表记录保存请求（v2.13.24 P76：增加可选的 ReadDate + ReadMode）
/// </summary>
public class MeterRecordSaveRequest
{
    public string DormCode { get; set; } = "";
    public string ReadMonth { get; set; } = "";
    public decimal ColdMeter { get; set; }
    public decimal HotMeter { get; set; }
    public decimal ElectricMeter { get; set; }
    public string? Operator { get; set; }
    public string? Remark { get; set; }
    // v2.13.24 P76：可选业务字段（手动补录时可填，批量导入/PDA 上传时由系统自动填）
    public DateOnly? ReadDate { get; set; }
    public byte? ReadMode { get; set; }
    // v2.13.80 新增：PdaDeviceSn + ClientRecordId（手动补录时不填，由系统生成）
    public string? DeviceSn { get; set; }
    public string? ClientRecordId { get; set; }
    public DateTime? ClientCreatedAt { get; set; }
}

/// <summary>
/// 抄表记录修正请求
/// </summary>
public class MeterRecordCorrectRequest
{
    public string? Remark { get; set; }
    // v2.13.24 P76：修正原因 + 修正人（可选，未填时默认使用 Remark + admin）
    public string? CorrectionReason { get; set; }
    public string? CorrectedBy { get; set; }
}

/// <summary>
/// 抄表记录作废请求（P1-8）
/// </summary>
public class MeterVoidRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? Operator { get; set; }
}
