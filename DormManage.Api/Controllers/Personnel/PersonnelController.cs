using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Services;
using DormManage.Shared.Models;
using DormManage.Shared.Extensions;
using DormManage.Shared.Security;
using SysEmployeeModel = DormManage.Shared.Models.SysEmployee;

namespace DormManage.Api.Controllers.Personnel;

/// <summary>
/// 人员清单 API 控制器（P1-14）
/// v2.13.106 三层防御：API 层校验 personnel:add 权限（POST 创建/批量导入）
/// </summary>
[ApiController]
[Route("api/v1/personnel")]
public class PersonnelController : ControllerBase
{
    private readonly IPersonnelService _service;
    private readonly IPermissionService _perm;
    private readonly ILogger<PersonnelController> _logger;
    private readonly DormDbContext _db;

    /// <summary>v2.13.106 新增人员所需权限码（与 PageHeader primaryAction.PermissionCode 一致）</summary>
    public const string RequiredPermissionCode = "personnel:add";

    public PersonnelController(IPersonnelService service, IPermissionService perm, ILogger<PersonnelController> logger, DormDbContext db)
    {
        _service = service;
        _perm = perm;
        _logger = logger;
        _db = db;
    }

    [HttpGet]
    public async Task<ApiResponse<PagedResult<SysEmployeeModel>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? department,
        [FromQuery] int? employeeTypeId,
        [FromQuery] int? employmentStatusId,
        [FromQuery] string? team,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
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
        // v2.13.106：导入批量新增同样需要 personnel:add 权限（防御：API 直接调用绕过 UI）
        if (!HasPermission(RequiredPermissionCode))
        {
            _logger.LogWarning("[v2.13.106 RBAC] 用户 {User} 尝试 POST /api/v1/personnel/import 但缺少 {Code} 权限",
                HttpContext.GetCurrentUserName(), RequiredPermissionCode);
            return ApiResponse<PersonnelImportResult>.Fail("PERMISSION_DENIED", $"无 {RequiredPermissionCode} 权限，无法导入人员");
        }

        if (file == null || file.Length == 0)
            return ApiResponse<PersonnelImportResult>.Fail("FILE_REQUIRED", "请选择 CSV 文件");

        using var stream = file.OpenReadStream();
        var result = await _service.ImportCsvAsync(stream);
        return ApiResponse<PersonnelImportResult>.Ok(result, $"导入完成：新增 {result.SuccessCount} 条，更新 {result.UpdateCount} 条，失败 {result.FailCount} 条");
    }

    /// <summary>新增员工（项1）— v2.13.106 三层防御 API 层</summary>
    [HttpPost]
    public async Task<ApiResponse<int>> Create([FromBody] PersonnelEditDto dto)
    {
        // v2.13.106 三层防御（API 层）：无 personnel:add 权限 → 403 拒绝
        if (!HasPermission(RequiredPermissionCode))
        {
            _logger.LogWarning("[v2.13.106 RBAC] 用户 {User} 尝试 POST /api/v1/personnel 但缺少 {Code} 权限",
                HttpContext.GetCurrentUserName(), RequiredPermissionCode);
            return ApiResponse<int>.Fail("PERMISSION_DENIED", $"无 {RequiredPermissionCode} 权限，无法新增人员");
        }

        // v2.13.149 试用模式限制：未注册时 人员清单最多 5 条记录
        var trialCheck = LicenseGuard.CheckTrialRecordLimit(
            "人员清单",
            await _db.Employees.CountAsync());
        if (!trialCheck.IsAllowed)
        {
            return ApiResponse<int>.Fail(LicenseGuard.TrialLimitErrorCode, trialCheck.Message);
        }

        var (ok, msg, id) = await _service.CreateAsync(dto);
        return ok ? ApiResponse<int>.Ok(id, msg) : ApiResponse<int>.Fail("CREATE_FAILED", msg);
    }

    /// <summary>编辑员工（项1）</summary>
    [HttpPut("{id:int}")]
    public async Task<ApiResponse> Update(int id, [FromBody] PersonnelEditDto dto)
    {
        var (ok, msg) = await _service.UpdateAsync(id, dto);
        return ok ? ApiResponse.Ok(msg) : ApiResponse.Fail("UPDATE_FAILED", msg);
    }

    /// <summary>标记离职（项1）</summary>
    [HttpPost("{id:int}/leave")]
    public async Task<ApiResponse> Leave(int id, [FromQuery] DateOnly? leaveDate)
    {
        var (ok, msg) = await _service.MarkLeftAsync(id, leaveDate ?? DateOnly.FromDateTime(DateTime.Today));
        return ok ? ApiResponse.Ok(msg) : ApiResponse.Fail("LEAVE_FAILED", msg);
    }

    /// <summary>
    /// v2.13.106：API 层权限辅助方法
    /// 优先从 HttpContext.User（Cookie 认证）取 userId，否则从 X-User-Id header 取。
    /// 然后用 IPermissionService 同步检查权限码（每请求 Items 缓存避免重复查询）。
    /// </summary>
    private bool HasPermission(string code)
    {
        if (string.IsNullOrEmpty(code)) return true; // 兜底：未配置权限码时放行
        var userId = HttpContext.GetCurrentUserId();
        if (userId <= 0) return false; // 未识别用户 → 拒绝
        var accessor = HttpContext.RequestServices.GetService<IHttpContextAccessor>();
        if (accessor == null)
        {
            // 兜底：构造一个临时 accessor（生产环境 IHttpContextAccessor 一定存在）
            accessor = new HttpContextAccessor { HttpContext = HttpContext };
        }
        return _perm.CurrentUserHasCode(accessor, code);
    }
}