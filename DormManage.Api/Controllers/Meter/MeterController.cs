using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
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
        var items = await query
            .OrderByDescending(r => r.ReadMonth)
            .ThenBy(r => r.DormCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(r => new MeterRecordDto
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
            StatusName = r?.Status.ToString() ?? "Unknown",
            Remark = r.Remark,
            ServerCreatedAt = r.ServerCreatedAt
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

        var dto = new MeterRecordDto
        {
            Id = record.Id,
            DormId = record.DormId,
            DormCode = record.DormCode,
            ReadMonth = record.ReadMonth,
            ColdMeter = record.ColdMeter ,
            HotMeter = record.HotMeter ,
            ElectricMeter = record.ElectricMeter ,
            Operator = record.Operator,
            Status = record.Status,
            StatusName = record?.Status.ToString() ?? "Unknown",
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

            // 覆盖更新
            existing.ColdMeter = request.ColdMeter;
            existing.HotMeter = request.HotMeter;
            existing.ElectricMeter = request.ElectricMeter;
            existing.Operator = string.IsNullOrEmpty(request.Operator) ? existing.Operator : request.Operator;
            existing.Status = (byte)((request.ColdMeter != 0 || request.HotMeter != 0 || request.ElectricMeter != 0) ? 1 : 0);
            existing.Remark = newRemark;
            existing.UpdatedAt = DateTime.Now;
            existing.ServerCreatedAt = DateTime.Now;

            record = existing;
            message = "更新成功";
        }
        else
        {
            // 新建记录
            record = new MeterRecord
            {
                DormId = dorm.Id,
                DormCode = request.DormCode,
                ReadMonth = request.ReadMonth,
                ColdMeter = request.ColdMeter,
                HotMeter = request.HotMeter,
                ElectricMeter = request.ElectricMeter,
                Operator = request.Operator ?? "管理员",
                Status = (byte)((request.ColdMeter != 0 || request.HotMeter != 0 || request.ElectricMeter != 0) ? 1 : 0),
                Remark = request.Remark,
                ServerCreatedAt = DateTime.Now
            };

            _db.MeterRecords.Add(record);
            message = "创建成功";
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

        // 生成 CSV (Excel 可打开)
        var sb = new global::System.Text.StringBuilder();
        sb.AppendLine("序号,宿舍号,抄表月份,冷水(m³),热水(m³),电(度),操作员,状态,备注");
        var index = 1;
        foreach (var r in records)
        {
            sb.AppendLine($"{index},{r.DormCode},{r.ReadMonth},{r.ColdMeter},{r.HotMeter},{r.ElectricMeter},{r.Operator},{r?.Status.ToString() ?? "Unknown"},{r.Remark ?? ""}");
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
/// 抄表记录数据传输对象
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
    public string Operator { get; set; } = "";
    public byte Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? Remark { get; set; }
    public DateTime ServerCreatedAt { get; set; }
}

/// <summary>
/// 抄表记录保存请求
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
}

/// <summary>
/// 抄表记录修正请求
/// </summary>
public class MeterRecordCorrectRequest
{
    public string? Remark { get; set; }
}

/// <summary>
/// 抄表记录作废请求（P1-8）
/// </summary>
public class MeterVoidRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? Operator { get; set; }
}
