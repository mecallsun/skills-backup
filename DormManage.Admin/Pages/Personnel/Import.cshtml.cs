using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Personnel;

/// <summary>
/// 人员导入页面模型（v2.13.40 100% 原型对齐）
///
/// 改造点（vs 原型 personnel/import.html）：
/// 1. 模板改 11 列（工号/姓名/部门/员工类型/考勤班次/班组/手机号/入职日期/离职日期/房号/备注），与原型和 Razor 文案完全一致
/// 2. UploadAsync 真正持久化 Employee（之前 v2.13.29 只增计数器，无实体变更）
/// 3. 部门/员工类型/考勤班次/班组按 Name → Id 映射；房号按 DormCode 关联 Dorm 表
/// </summary>
public class ImportModel : PageModel
{
    private readonly DormDbContext _db;

    public ImportModel(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 当前步骤（1=下载模板, 2=上传文件, 3=查看结果）
    /// </summary>
    public int Step => ImportResult != null ? 3 : 2;

    /// <summary>
    /// 导入结果
    /// </summary>
    public ImportResultDto? ImportResult { get; set; }

    /// <summary>
    /// 下载导入模板（v2.13.40：11 列与原型/Razor 文案完全一致）
    /// </summary>
    public IActionResult OnGetDownloadTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("人员清单");
        // v2.13.83：12 列（含「性别」作为第 3 列），与 personnel/import.html 原型 + Razor 文案一致
        string[] headers = { "工号", "姓名", "性别", "部门", "员工类型", "班次", "班组", "手机号", "入职日期", "离职日期", "房号", "备注" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;

        // 示例行（参考）
        ws.Cell(2, 1).Value = "EMP-2026-001";
        ws.Cell(2, 2).Value = "张三";
        ws.Cell(2, 3).Value = "男";  // v2.13.83 性别示例
        ws.Cell(2, 4).Value = "生产部";
        ws.Cell(2, 5).Value = "合同工";
        ws.Cell(2, 6).Value = "早班";
        ws.Cell(2, 7).Value = "A班";
        ws.Cell(2, 8).Value = "13800138000";
        ws.Cell(2, 9).Value = "2026-01-15";
        ws.Cell(2, 10).Value = "";
        ws.Cell(2, 11).Value = "D-301";
        ws.Cell(2, 12).Value = "示例备注";
        ws.Range(2, 1, 2, headers.Length).Style.Fill.BackgroundColor = XLColor.LightYellow;

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"人员清单导入模板_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// 上传并导入 Excel 文件（v2.13.40：11 列字段映射 + 真正持久化 Employee 实体）
    /// </summary>
    public async Task<IActionResult> OnPostUploadAsync(IFormFile file, bool overwriteExisting = false)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "请选择要上传的文件");
            return Page();
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("file", "仅支持 .xlsx 格式的 Excel 文件");
            return Page();
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            ModelState.AddModelError("file", "文件大小不能超过 10MB");
            return Page();
        }

        var result = new ImportResultDto();
        var errors = new List<ImportErrorDto>();

        try
        {
            // v2.13.40 预加载基础资料字典（部门/员工类型/考勤班次/班组），用于按 Name 解析 Id
            var deptMap = await _db.Departments
                .Where(d => d.IsActive)
                .ToDictionaryAsync(d => d.Name, d => d.Id);
            var typeMap = await _db.EmployeeTypes
                .Where(t => t.IsActive)
                .ToDictionaryAsync(t => t.Name, t => t.Id);
            var attMap = await _db.AttendanceTypes
                .Where(a => a.IsActive)
                .ToDictionaryAsync(a => a.Name, a => a.Id);
            var teamMap = await _db.Teams
                .Where(t => t.IsActive)
                .ToDictionaryAsync(t => t.Name, t => t.Id);

            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int row = 2; row <= lastRow; row++)
            {
                result.TotalRows++;
                try
                {
                    // v2.13.83：12 列字段映射（含「性别」作为第 3 列）
                    var empNo = ws.Cell(row, 1).GetString().Trim();
                    var name = ws.Cell(row, 2).GetString().Trim();
                    var genderStr = ws.Cell(row, 3).GetString().Trim();
                    var deptName = ws.Cell(row, 4).GetString().Trim();
                    var typeName = ws.Cell(row, 5).GetString().Trim();
                    var attName = ws.Cell(row, 6).GetString().Trim();
                    var teamName = ws.Cell(row, 7).GetString().Trim();
                    var phone = ws.Cell(row, 8).GetString().Trim();
                    var hireDateStr = ws.Cell(row, 9).GetString().Trim();
                    var leaveDateStr = ws.Cell(row, 10).GetString().Trim();
                    var dormCode = ws.Cell(row, 11).GetString().Trim();
                    var remark = ws.Cell(row, 12).GetString().Trim();

                    // v2.13.83 性别解析（中文「男/女」或数字「1/2」）
                    int gender = 1;
                    if (!string.IsNullOrEmpty(genderStr))
                    {
                        if (genderStr == "男" || genderStr == "1") gender = 1;
                        else if (genderStr == "女" || genderStr == "2") gender = 2;
                    }

                    // 必填校验
                    if (string.IsNullOrEmpty(empNo))
                    {
                        errors.Add(new ImportErrorDto { RowNumber = row, FieldName = "工号", ErrorMessage = "工号为空" });
                        result.FailedRows++;
                        continue;
                    }
                    if (string.IsNullOrEmpty(name))
                    {
                        errors.Add(new ImportErrorDto { RowNumber = row, FieldName = "姓名", ErrorMessage = "姓名为空" });
                        result.FailedRows++;
                        continue;
                    }

                    // 解析 FK（按 Name）
                    int departmentId = 0;
                    if (!string.IsNullOrEmpty(deptName))
                    {
                        if (!deptMap.TryGetValue(deptName, out departmentId))
                        {
                            errors.Add(new ImportErrorDto { RowNumber = row, FieldName = "部门", ErrorMessage = $"部门不存在：{deptName}" });
                            result.FailedRows++;
                            continue;
                        }
                    }

                    int employeeTypeId = 0;
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        if (!typeMap.TryGetValue(typeName, out employeeTypeId))
                        {
                            errors.Add(new ImportErrorDto { RowNumber = row, FieldName = "员工类型", ErrorMessage = $"员工类型不存在：{typeName}" });
                            result.FailedRows++;
                            continue;
                        }
                    }

                    int? attendanceTypeId = null;
                    if (!string.IsNullOrEmpty(attName) && attName != "默认")
                    {
                        if (attMap.TryGetValue(attName, out var aid))
                            attendanceTypeId = aid;
                        // 留空默认"默认" → 不设置
                    }

                    int teamId = 0;
                    if (!string.IsNullOrEmpty(teamName) && teamName != "默认")
                    {
                        if (!teamMap.TryGetValue(teamName, out teamId))
                        {
                            // 班组不存在时不报错，保留为空（班组为非必填）
                            teamId = 0;
                        }
                    }

                    // 日期解析
                    DateOnly? hireDate = null;
                    if (!string.IsNullOrEmpty(hireDateStr))
                    {
                        if (!DateOnly.TryParse(hireDateStr, out var hd))
                        {
                            errors.Add(new ImportErrorDto { RowNumber = row, FieldName = "入职日期", ErrorMessage = "日期格式错误：" + hireDateStr });
                            result.FailedRows++;
                            continue;
                        }
                        hireDate = hd;
                    }

                    DateOnly? leaveDate = null;
                    if (!string.IsNullOrEmpty(leaveDateStr))
                    {
                        if (!DateOnly.TryParse(leaveDateStr, out var ld))
                        {
                            errors.Add(new ImportErrorDto { RowNumber = row, FieldName = "离职日期", ErrorMessage = "日期格式错误：" + leaveDateStr });
                            result.FailedRows++;
                            continue;
                        }
                        leaveDate = ld;
                    }

                    // 房号关联（仅记录 DormCode 字符串，不实际分配床位）
                    var dormCodeToSave = string.IsNullOrEmpty(dormCode) ? null : dormCode;

                    // 检查是否已存在
                    var existing = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == empNo);
                    if (existing != null && !overwriteExisting)
                    {
                        result.SkippedRows++;
                        continue;
                    }

                    if (existing == null)
                    {
                        // v2.13.40 真正新增 Employee 实体
                        var emp = new SysEmployee
                        {
                            EmployeeCode = empNo,
                            RealName = name,
                            DepartmentId = departmentId,
                            Department = deptName,
                            EmployeeTypeId = employeeTypeId,
                            EmployeeTypeText = typeName,
                            TeamId = teamId,
                            // v2.13.78：移除冗余 Team 字符串字段赋值（DB 中无此列，依赖 FK 关联显示名称）
                            AttendanceTypeId = attendanceTypeId,
                            Phone = phone,
                            HireDate = hireDate,
                            LeaveDate = leaveDate,
                            DormCode = dormCodeToSave,
                            Remark = remark,
                            // v2.13.40: 移除过时的 Status 字段（应使用 EmploymentStatusId + 导航属性）
                            Gender = gender,  // v2.13.83 从第 3 列解析（不再硬编码 1）
                            ResidenceStatusId = 2,  // 默认未住宿
                            EmploymentStatusId = leaveDate.HasValue ? 3 : 1,  // 有离职日期→已离职，否则在职
                            CreatedAt = DateTime.Now
                        };
                        _db.Employees.Add(emp);
                        result.SuccessRows++;
                    }
                    else
                    {
                        // v2.13.40 真正覆盖模式：更新所有字段
                        existing.RealName = name;
                        existing.DepartmentId = departmentId;
                        existing.Department = deptName;
                        existing.EmployeeTypeId = employeeTypeId;
                        existing.EmployeeTypeText = typeName;
                        existing.TeamId = teamId;
                        // v2.13.78：移除冗余 Team 字符串字段赋值
                        existing.AttendanceTypeId = attendanceTypeId;
                        existing.Phone = phone;
                        existing.HireDate = hireDate;
                        existing.LeaveDate = leaveDate;
                        existing.DormCode = dormCodeToSave;
                        existing.Remark = remark;
                        existing.Gender = gender;  // v2.13.83 覆盖模式下也更新性别
                        if (leaveDate.HasValue) existing.EmploymentStatusId = 3;
                        existing.UpdatedAt = DateTime.Now;
                        result.SuccessRows++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new ImportErrorDto { RowNumber = row, FieldName = "-", ErrorMessage = ex.Message });
                    result.FailedRows++;
                }
            }

            // v2.13.40 真持久化：所有变更一次性提交
            if (result.SuccessRows > 0)
                await _db.SaveChangesAsync();

            result.Errors = errors.Count > 0 ? errors : null;
            ImportResult = result;
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("upload", $"导入失败：{ex.Message}");
            return Page();
        }

        return Page();
    }

    /// <summary>
    /// 下载模板（POST 处理，保留向后兼容）
    /// </summary>
    public IActionResult OnPostDownloadTemplate()
    {
        return OnGetDownloadTemplate();
    }
}

/// <summary>
/// 导入结果数据传输对象
/// </summary>
public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public int SkippedRows { get; set; }
    public List<ImportErrorDto>? Errors { get; set; }
}

/// <summary>
/// 导入错误信息
/// </summary>
public class ImportErrorDto
{
    public int RowNumber { get; set; }
    public string FieldName { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}