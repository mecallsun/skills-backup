using ClosedXML.Excel;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DormManage.Shared.Services;

/// <summary>
/// 行政宿舍 Excel 数据导入服务（v2.13.19）
/// 仅导入可靠主数据：部门/楼栋/楼层/地址/班组/考勤班次/员工/宿舍
/// 不导入有质量问题的入住明细关系
/// </summary>
public class DataImportService
{
    private readonly DormDbContext _db;
    private readonly ILogger<DataImportService> _logger;

    public DataImportService(DormDbContext db, ILogger<DataImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 从 Excel 导入正式数据
    /// </summary>
    public async Task<ImportResult> ImportAsync(string excelPath, bool skipExisting = true)
    {
        var result = new ImportResult();
        if (!File.Exists(excelPath))
        {
            result.Errors.Add($"文件不存在: {excelPath}");
            return result;
        }

        using var workbook = new XLWorkbook(excelPath);

        // 1. 导入并保存字典表（必须先 SaveChanges，后续 Dorm/Employee 才能查到 FK）
        await ImportDepartmentsAsync(workbook.Worksheet("部门"), result);
        await ImportBuildingsAsync(workbook.Worksheet("宿舍档案"), result);
        await ImportFloorsAsync(workbook.Worksheet("宿舍档案"), result);
        await ImportAddressesAsync(result);
        await ImportTeamsAsync(workbook.Worksheet("员工班组"), result);
        await ImportAttendanceTypesAsync(result);
        await _db.SaveChangesAsync();

        // 2. 导入宿舍
        await ImportDormsAsync(workbook.Worksheet("宿舍档案"), result);
        await _db.SaveChangesAsync();

        // 3. 导入员工
        await ImportEmployeesAsync(workbook.Worksheet("花名册"), result);
        await _db.SaveChangesAsync();

        return result;
    }

    private async Task ImportDepartmentsAsync(IXLWorksheet sheet, ImportResult result)
    {
        var rows = sheet.RowsUsed().Skip(1); // 跳过表头
        var names = new HashSet<string>();
        foreach (var row in rows)
        {
            var name = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name) || name == "NaN") continue;
            names.Add(name);
        }

        int sort = 1;
        foreach (var name in names)
        {
            var exists = await _db.Departments.AnyAsync(d => d.Name == name);
            if (exists) continue;

            _db.Departments.Add(new Department
            {
                Code = $"DEPT_{sort:D3}",
                Name = name,
                SortOrder = sort++,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.Departments++;
        }
    }

    private async Task ImportBuildingsAsync(IXLWorksheet dormSheet, ImportResult result)
    {
        var codes = new HashSet<string>();
        foreach (var row in dormSheet.RowsUsed().Skip(1))
        {
            var code = ExtractBuildingCode(row.Cell(1).GetString());
            if (!string.IsNullOrEmpty(code)) codes.Add(code);
        }

        int sort = 1;
        foreach (var code in codes.OrderBy(c => c))
        {
            var name = $"{code}号楼";
            var exists = await _db.Buildings.AnyAsync(b => b.Name == name);
            if (exists) continue;

            _db.Buildings.Add(new Building
            {
                Name = name,
                SortOrder = sort++,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.Buildings++;
        }
    }

    private async Task ImportFloorsAsync(IXLWorksheet dormSheet, ImportResult result)
    {
        var floorNos = new HashSet<int>();
        foreach (var row in dormSheet.RowsUsed().Skip(1))
        {
            var no = ExtractFloorNo(row.Cell(1).GetString());
            if (no.HasValue) floorNos.Add(no.Value);
        }

        foreach (var no in floorNos.OrderBy(n => n))
        {
            var exists = await _db.Floors.AnyAsync(f => f.FloorNo == no);
            if (exists) continue;

            _db.Floors.Add(new Floor
            {
                FloorNo = no,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.Floors++;
        }
    }

    private async Task ImportAddressesAsync(ImportResult result)
    {
        var defaultAddresses = new[] { "园区A栋", "园区B栋" };
        foreach (var text in defaultAddresses)
        {
            var exists = await _db.Addresses.AnyAsync(a => a.AddressText == text);
            if (exists) continue;
            _db.Addresses.Add(new Address
            {
                AddressText = text,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.Addresses++;
        }
    }

    private async Task ImportTeamsAsync(IXLWorksheet sheet, ImportResult result)
    {
        var rows = sheet.RowsUsed().Skip(1);
        var names = new List<string> { "默认" };
        foreach (var row in rows)
        {
            var name = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name) || name == "NaN" || name == "默认") continue;
            if (!names.Contains(name)) names.Add(name);
        }

        int sort = 0;
        foreach (var name in names)
        {
            var exists = await _db.Teams.AnyAsync(t => t.Name == name);
            if (exists) continue;

            _db.Teams.Add(new Team
            {
                Code = $"TEAM_{name.TrimEnd('班')}",
                Name = name,
                SortOrder = sort++,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.Teams++;
        }
    }

    private async Task ImportAttendanceTypesAsync(ImportResult result)
    {
        var defaults = new[]
        {
            ("DEFAULT", "默认", "09:00-18:00"),
            ("MORNING", "早班", "06:00-14:00"),
            ("MIDDLE", "中班", "14:00-22:00"),
            ("EVENING", "晚班", "18:00-02:00"),
            ("NIGHT", "夜班", "22:00-06:00"),
            ("OTHER", "其他", "不定期")
        };

        foreach (var (code, name, hours) in defaults)
        {
            var exists = await _db.AttendanceTypes.AnyAsync(a => a.Code == code);
            if (exists) continue;
            _db.AttendanceTypes.Add(new AttendanceType
            {
                Code = code,
                Name = name,
                WorkHours = hours,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.AttendanceTypes++;
        }
    }

    private async Task ImportDormsAsync(IXLWorksheet sheet, ImportResult result)
    {
        var buildings = await _db.Buildings.ToListAsync();
        var floors = await _db.Floors.ToListAsync();
        var addresses = await _db.Addresses.ToListAsync();

        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var dormCode = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(dormCode)) continue;

            var capacityStr = row.Cell(2).GetString().Trim();
            if (!int.TryParse(capacityStr, out var capacity)) capacity = 2;
            if (capacity <= 0) capacity = 1; // 修正异常容量

            var exists = await _db.Dorms.AnyAsync(d => d.DormCode == dormCode);
            if (exists) continue;

            var buildingCode = ExtractBuildingCode(dormCode);
            var floorNo = ExtractFloorNo(dormCode);

            var building = buildings.FirstOrDefault(b => b.Name == $"{buildingCode}号楼");
            var floor = floors.FirstOrDefault(f => f.FloorNo == floorNo);
            var address = addresses.FirstOrDefault(a => a.AddressText == $"园区{buildingCode}栋")
                          ?? addresses.FirstOrDefault();

            var bedNumbers = string.Join(",", Enumerable.Range(1, capacity));

            _db.Dorms.Add(new Dorm
            {
                DormCode = dormCode,
                BuildingId = building?.Id ?? 1,
                BuildingName = building?.Name,
                FloorId = floor?.Id ?? 1,
                AddressId = address?.Id ?? 1,
                AddressText = address?.AddressText,
                Capacity = capacity,
                Gender = 1, // 默认男寝；后续根据实际入住可调整
                RoomCount = 1,
                BedNumbers = bedNumbers,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.Dorms++;
        }
    }

    private async Task ImportEmployeesAsync(IXLWorksheet sheet, ImportResult result)
    {
        var depts = await _db.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);
        // v2.13.21 修复：Team.Name 可能重复，去重后取第一条，避免启动导入崩溃
        var teams = (await _db.Teams.ToListAsync())
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var attendanceDefault = await _db.AttendanceTypes.FirstOrDefaultAsync(a => a.Code == "DEFAULT");

        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var empCode = row.Cell(2).GetString().Trim(); // 员工编号
            var realName = row.Cell(1).GetString().Trim(); // 员工姓名
            if (string.IsNullOrWhiteSpace(empCode) || string.IsNullOrWhiteSpace(realName)) continue;

            var exists = await _db.Employees.AnyAsync(e => e.EmployeeCode == empCode);
            if (exists) continue;

            var deptName = row.Cell(4).GetString().Trim();
            var genderText = row.Cell(5).GetString().Trim();
            var teamName = row.Cell(8).GetString().Trim();
            var position = row.Cell(7).GetString().Trim();
            var phone = row.Cell(11).GetValue<string?>()?.Trim();
            var hireDateText = row.Cell(9).GetString().Trim();

            if (!depts.TryGetValue(deptName, out var deptId)) deptId = depts.Values.FirstOrDefault();
            if (!teams.TryGetValue(teamName, out var teamId))
            {
                // Excel 中是"默认"，种子数据中是"默认班组"
                teamId = teamName == "默认"
                    ? teams.GetValueOrDefault("默认班组")
                    : 0;
            }

            int gender = genderText switch
            {
                "男" => 1,
                "女" => 2,
                _ => 1
            };

            DateOnly? hireDate = null;
            if (DateTime.TryParse(hireDateText, out var hd))
            {
                hireDate = DateOnly.FromDateTime(hd);
            }

            _db.Employees.Add(new SysEmployee
            {
                EmployeeCode = empCode,
                RealName = realName,
                DepartmentId = deptId,
                Department = deptName,
                EmployeeTypeId = 1, // 默认合同工
                EmployeeTypeText = "合同工",
                TeamId = teamId,
                // v2.13.78：移除冗余 Team 字符串字段赋值（DB 中无此列，依赖 FK 关联显示名称）
                Gender = gender,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                EmploymentStatusId = EmployeeStatus.Active,
                Status = EmployeeStatus.Active,
                HireDate = hireDate,
                ResidenceStatusId = 2, // 默认未住宿
                AttendanceTypeId = attendanceDefault?.Id ?? 1,
                Remark = position,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            result.Employees++;
        }
    }

    private static string? ExtractBuildingCode(string dormCode)
    {
        if (string.IsNullOrWhiteSpace(dormCode)) return null;
        var first = dormCode[0];
        return char.IsLetter(first) ? first.ToString().ToUpperInvariant() : null;
    }

    private static int? ExtractFloorNo(string dormCode)
    {
        if (string.IsNullOrWhiteSpace(dormCode) || dormCode.Length < 3) return null;
        // 房号格式：A101 = A栋 1楼 01房；A215 = A栋 2楼 15房
        var afterBuilding = dormCode.Substring(1);
        if (afterBuilding.Length > 0 && char.IsDigit(afterBuilding[0]))
        {
            return int.Parse(afterBuilding[0].ToString());
        }
        return null;
    }
}

/// <summary>
/// 导入结果统计
/// </summary>
public class ImportResult
{
    public int Departments { get; set; }
    public int Buildings { get; set; }
    public int Floors { get; set; }
    public int Addresses { get; set; }
    public int Teams { get; set; }
    public int AttendanceTypes { get; set; }
    public int Dorms { get; set; }
    public int Employees { get; set; }
    public List<string> Errors { get; set; } = new();

    public override string ToString() =>
        $"导入完成：部门 {Departments}，楼栋 {Buildings}，楼层 {Floors}，地址 {Addresses}，班组 {Teams}，考勤班次 {AttendanceTypes}，宿舍 {Dorms}，员工 {Employees}。错误 {Errors.Count} 条。";
}
