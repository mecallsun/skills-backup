using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Services;
using DormManage.Shared.Models;
using SysEmployeeModel = DormManage.Shared.Models.SysEmployee;

namespace DormManage.Api.Controllers.Personnel;

/// <summary>
/// 人员清单 API 控制器（P1-14）
/// </summary>
[ApiController]
[Route("api/v1/personnel")]
public class PersonnelController : ControllerBase
{
    private readonly IPersonnelService _service;
    public PersonnelController(IPersonnelService service) { _service = service; }

    [HttpGet]
    public async Task<ApiResponse<PagedResult<SysEmployeeModel>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? department,
        [FromQuery] int? employeeTypeId,
        [FromQuery] int? employmentStatusId,
        [FromQuery] string? team,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetListAsync(keyword, department, employeeTypeId, employmentStatusId, team, page, pageSize);
        return ApiResponse<PagedResult<SysEmployeeModel>>.Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? keyword, [FromQuery] string? department, [FromQuery] string? team)
    {
        var bytes = await _service.ExportCsvAsync(keyword, department, team);
        return File(bytes, "text/csv; charset=utf-8", $"人员清单_{DateTime.Now:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("import")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ApiResponse<PersonnelImportResult>> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResponse<PersonnelImportResult>.Fail("FILE_REQUIRED", "请选择 CSV 文件");

        using var stream = file.OpenReadStream();
        var result = await _service.ImportCsvAsync(stream);
        return ApiResponse<PersonnelImportResult>.Ok(result, $"导入完成：新增 {result.SuccessCount} 条，更新 {result.UpdateCount} 条，失败 {result.FailCount} 条");
    }
}