using System.Globalization;
using System.Text;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Shared.Services;

/// <summary>
/// 人员清单服务接口
/// </summary>
public interface IPersonnelService
{
    Task<PagedResult<SysEmployee>> GetListAsync(
        string? keyword, string? department, int? employeeTypeId, int? employmentStatusId, string? team, int page, int pageSize);

    /// <summary>导出 CSV 字节流（P1-14）</summary>
    Task<byte[]> ExportCsvAsync(string? keyword = null, string? department = null, string? team = null);

    /// <summary>导入 CSV（P1-14）：支持新增与按 EmployeeCode 覆盖更新</summary>
    Task<PersonnelImportResult> ImportCsvAsync(Stream csvStream);

    /// <summary>按 ID 获取员工（编辑页加载）</summary>
    Task<SysEmployee?> GetByIdAsync(int id);

    /// <summary>新增员工（项1）：校验工号唯一 + 填充真实表 NOT NULL 冗余列</summary>
    Task<(bool ok, string message, int id)> CreateAsync(PersonnelEditDto dto);

    /// <summary>编辑员工（项1）</summary>
    Task<(bool ok, string message)> UpdateAsync(int id, PersonnelEditDto dto);

    /// <summary>标记离职（项1）：EmploymentStatusId=3 + LeaveDate；同步清空 DormCode</summary>
    Task<(bool ok, string message)> MarkLeftAsync(int id, DateOnly leaveDate);
}

/// <summary>
/// 人员清单导入结果
/// </summary>
public class PersonnelImportResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int UpdateCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 员工新增/编辑 DTO（项1）
/// </summary>
public class PersonnelEditDto
{
    public string EmployeeCode { get; set; } = "";
    public string RealName { get; set; } = "";
    public int DepartmentId { get; set; }
    public int EmployeeTypeId { get; set; }
    public int TeamId { get; set; }
    public int Gender { get; set; } = 1;
    public string? Phone { get; set; }
    public DateOnly? HireDate { get; set; }
    public int AttendanceTypeId { get; set; }
    public int EmploymentStatusId { get; set; } = 1;
    public string? DormCode { get; set; }
    public int? BedNo { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 人员清单服务实现（P1-14 完整实现）
/// </summary>
public class PersonnelService : IPersonnelService
{
    private readonly DormDbContext _db;

    public PersonnelService(DormDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SysEmployee>> GetListAsync(
        string? keyword, string? department, int? employeeTypeId, int? employmentStatusId, string? team, int page, int pageSize)
    {
        var query = _db.Employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(e => e.EmployeeCode.Contains(keyword) || e.RealName.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(e => e.Department == department);
        if (employeeTypeId.HasValue)
            query = query.Where(e => e.EmployeeTypeId == employeeTypeId.Value);
        if (employmentStatusId.HasValue)
            query = query.Where(e => e.EmploymentStatusId == employmentStatusId.Value);
        if (!string.IsNullOrWhiteSpace(team))
            query = query.Where(e => e.Team == team);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(e => e.EmployeeCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<SysEmployee>
        {
            Items = items,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<byte[]> ExportCsvAsync(string? keyword = null, string? department = null, string? team = null)
    {
        var query = _db.Employees.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(e => e.EmployeeCode.Contains(keyword) || e.RealName.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(e => e.Department == department);
        if (!string.IsNullOrWhiteSpace(team))
            query = query.Where(e => e.Team == team);

        var employees = await query.OrderBy(e => e.EmployeeCode).ToListAsync();

        var sb = new StringBuilder();
        // 标题行
        sb.AppendLine("工号,姓名,部门,员工类型,入职日期,离职日期,考勤班次,联系电话,备注");

        foreach (var e in employees)
        {
            sb.Append(EscapeCsv(e.EmployeeCode)).Append(',');
            sb.Append(EscapeCsv(e.RealName)).Append(',');
            sb.Append(EscapeCsv(e.Department ?? "")).Append(',');
            sb.Append(e.EmployeeTypeId.ToString()).Append(',');
            sb.Append(e.HireDate?.ToString("yyyy-MM-dd") ?? "").Append(',');
            sb.Append(e.LeaveDate?.ToString("yyyy-MM-dd") ?? "").Append(',');
            sb.Append(e.AttendanceTypeId?.ToString() ?? "").Append(',');
            sb.Append(EscapeCsv(e.Phone ?? "")).Append(',');
            sb.AppendLine(EscapeCsv(e.Remark ?? ""));
        }

        // UTF-8 BOM 让 Excel 正确识别中文
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public async Task<PersonnelImportResult> ImportCsvAsync(Stream csvStream)
    {
        var result = new PersonnelImportResult();

        using var reader = new StreamReader(csvStream, Encoding.UTF8);
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            result.Errors.Add("文件为空或缺少标题行");
            return result;
        }

        // 解析标题，定位列索引
        var headers = ParseCsvLine(headerLine);
        int Col(string name) => Array.FindIndex(headers, h => h.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
        var idxEmpCode = Col("工号");
        var idxName = Col("姓名");
        var idxDept = Col("部门");
        var idxTypeId = Col("员工类型");
        var idxHire = Col("入职日期");
        var idxLeave = Col("离职日期");
        var idxAttendance = Col("考勤班次");
        var idxPhone = Col("联系电话");
        var idxRemark = Col("备注");

        if (idxEmpCode < 0 || idxName < 0)
        {
            result.Errors.Add("CSV 必须包含「工号」和「姓名」列");
            return result;
        }

        var existing = await _db.Employees.ToDictionaryAsync(e => e.EmployeeCode);
        var lineNo = 1;
        while (!reader.EndOfStream)
        {
            lineNo++;
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            result.TotalRows++;

            try
            {
                var cols = ParseCsvLine(line);
                if (cols.Length <= idxEmpCode || cols.Length <= idxName) continue;
                var empCode = cols[idxEmpCode].Trim();
                var name = cols[idxName].Trim();
                if (string.IsNullOrEmpty(empCode) || string.IsNullOrEmpty(name)) continue;

                DateOnly? hire = TryParseDate(cols, idxHire);
                DateOnly? leave = TryParseDate(cols, idxLeave);
                int? typeId = TryParseInt(cols, idxTypeId);
                int? attId = TryParseInt(cols, idxAttendance);

                if (existing.TryGetValue(empCode, out var emp))
                {
                    // 更新
                    emp.RealName = name;
                    if (idxDept >= 0) emp.Department = Get(cols, idxDept);
                    if (typeId.HasValue) emp.EmployeeTypeId = typeId.Value;
                    if (hire.HasValue) emp.HireDate = hire;
                    if (leave.HasValue) emp.LeaveDate = leave;
                    if (attId.HasValue) emp.AttendanceTypeId = attId;
                    if (idxPhone >= 0) emp.Phone = Get(cols, idxPhone);
                    if (idxRemark >= 0) emp.Remark = Get(cols, idxRemark);
                    emp.UpdatedAt = DateTime.Now;
                    result.UpdateCount++;
                }
                else
                {
                    // 新增
                    var newEmp = new SysEmployee
                    {
                        EmployeeCode = empCode,
                        RealName = name,
                        Department = idxDept >= 0 ? Get(cols, idxDept) : null,
                        EmployeeTypeId = typeId ?? 1,
                        HireDate = hire,
                        LeaveDate = leave,
                        AttendanceTypeId = attId,
                        Phone = idxPhone >= 0 ? Get(cols, idxPhone) : null,
                        Remark = idxRemark >= 0 ? Get(cols, idxRemark) : null,
                        EmploymentStatusId = EmployeeStatus.Active,
                        ResidenceStatusId = 2,
                        Status = 1,
                        CreatedAt = DateTime.Now
                    };
                    _db.Employees.Add(newEmp);
                    result.SuccessCount++;
                }
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.Errors.Add($"第 {lineNo} 行：{ex.Message}");
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"数据库保存失败：{ex.Message}");
            result.FailCount += result.SuccessCount + result.UpdateCount;
            result.SuccessCount = 0;
            result.UpdateCount = 0;
        }

        return result;
    }

    private static string Get(string[] cols, int idx) => idx >= 0 && idx < cols.Length ? cols[idx].Trim() : "";

    private static int? TryParseInt(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        var s = cols[idx].Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return int.TryParse(s, out var n) ? n : null;
    }

    private static DateOnly? TryParseDate(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        var s = cols[idx].Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            return d;
        return null;
    }

    private static string EscapeCsv(string? s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuote = false;
        var sb = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuote = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else if (c == '"') inQuote = true;
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }

    // ==================== 项1：员工增删改 ====================

    public async Task<SysEmployee?> GetByIdAsync(int id)
    {
        return await _db.Employees
            .Include(e => e.EmployeeType)
            .Include(e => e.AttendanceType)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<(bool ok, string message, int id)> CreateAsync(PersonnelEditDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode) || string.IsNullOrWhiteSpace(dto.RealName))
            return (false, "工号与姓名必填", 0);
        if (dto.HireDate == null)
            return (false, "入职日期必填", 0);
        if (await _db.Employees.AnyAsync(e => e.EmployeeCode == dto.EmployeeCode))
            return (false, $"工号 {dto.EmployeeCode} 已存在", 0);

        var dept = await _db.Departments.FindAsync(dto.DepartmentId);
        var etype = await _db.EmployeeTypes.FindAsync(dto.EmployeeTypeId);

        var emp = new SysEmployee
        {
            EmployeeCode = dto.EmployeeCode.Trim(),
            RealName = dto.RealName.Trim(),
            DepartmentId = dto.DepartmentId,
            Department = dept?.Name ?? "",
            EmployeeTypeId = dto.EmployeeTypeId,
            EmployeeTypeText = etype?.Name ?? "",       // 真实表 EmployeeType nvarchar NOT NULL
            TeamId = dto.TeamId,                          // 真实表 TeamId NOT NULL
            Gender = dto.Gender,
            Phone = dto.Phone,
            HireDate = dto.HireDate,
            AttendanceTypeId = dto.AttendanceTypeId,
            EmploymentStatusId = dto.EmploymentStatusId,
            Status = dto.EmploymentStatusId,
            ResidenceStatusId = 2,                        // 默认未住宿
            DormCode = dto.DormCode ?? "",                // 真实表 NOT NULL
            BedNo = dto.BedNo ?? 0,                        // 真实表 NOT NULL
            Remark = dto.Remark,
            IsActive = true
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();
        return (true, "新增成功", emp.Id);
    }

    public async Task<(bool ok, string message)> UpdateAsync(int id, PersonnelEditDto dto)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (emp == null) return (false, "员工不存在");
        if (string.IsNullOrWhiteSpace(dto.RealName)) return (false, "姓名必填");

        var dept = await _db.Departments.FindAsync(dto.DepartmentId);
        var etype = await _db.EmployeeTypes.FindAsync(dto.EmployeeTypeId);

        emp.RealName = dto.RealName.Trim();
        emp.DepartmentId = dto.DepartmentId;
        emp.Department = dept?.Name ?? emp.Department;
        emp.EmployeeTypeId = dto.EmployeeTypeId;
        emp.EmployeeTypeText = etype?.Name ?? emp.EmployeeTypeText;
        emp.TeamId = dto.TeamId;
        emp.Gender = dto.Gender;
        emp.Phone = dto.Phone;
        if (dto.HireDate != null) emp.HireDate = dto.HireDate;

        // v2.13.24 联动1：考勤班次变更 → 同步 DormBooking 冗余字段
        var oldAttendanceTypeId = emp.AttendanceTypeId;
        emp.AttendanceTypeId = dto.AttendanceTypeId;
        emp.EmploymentStatusId = dto.EmploymentStatusId;
        emp.Status = dto.EmploymentStatusId;
        emp.DormCode = dto.DormCode ?? emp.DormCode;
        emp.BedNo = dto.BedNo ?? emp.BedNo;
        emp.Remark = dto.Remark;

        // v2.13.24 联动1：若考勤班次或床位号变更，同步该员工所有 DormBooking 冗余字段
        var bookingChanges = new List<string>();
        if (oldAttendanceTypeId != dto.AttendanceTypeId)
        {
            var affected = await _db.DormBookings
                .Where(b => b.EmployeeId == emp.Id && b.Status == BookingStatus.Staying)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.AttendanceTypeId, dto.AttendanceTypeId));
            bookingChanges.Add($"AttendanceTypeId({oldAttendanceTypeId}→{dto.AttendanceTypeId}, 影响{affected}条在宿记录)");
        }
        if (dto.BedNo.HasValue && emp.BedNo != dto.BedNo)
        {
            var newBedNo = dto.BedNo.Value;
            var affected = await _db.DormBookings
                .Where(b => b.EmployeeId == emp.Id && b.Status == BookingStatus.Staying && b.DormCode == emp.DormCode)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.BedNo, (int?)newBedNo));
            bookingChanges.Add($"BedNo({emp.BedNo}→{newBedNo}, 影响{affected}条在宿记录)");
        }

        await _db.SaveChangesAsync();
        if (bookingChanges.Any())
        {
            Console.WriteLine($"[v2.13.24 PersonnelSync] EmployeeId={emp.Id}: {string.Join("; ", bookingChanges)}");
        }
        return (true, "保存成功");
    }

    public async Task<(bool ok, string message)> MarkLeftAsync(int id, DateOnly leaveDate)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (emp == null) return (false, "员工不存在");
        emp.EmploymentStatusId = EmployeeStatus.Left; // 3=已离职
        emp.Status = EmployeeStatus.Left;
        emp.LeaveDate = leaveDate;
        emp.DormCode = "";        // 离职清空当前宿舍（真实表 NOT NULL）
        await _db.SaveChangesAsync();
        return (true, "已标记离职");
    }
}