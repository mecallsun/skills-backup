using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Api.Controllers.Billing;

/// <summary>
/// 费用管理 API 控制器（费用标准 + 宿舍账单 + 员工账单）
/// v2.13.110 三层防御：API 层校验 billingstandard:add 权限（POST 创建/更新费用标准）
/// </summary>
[ApiController]
[Route("api/v1/billing")]
public class BillingController : ControllerBase
{
    private readonly DormDbContext _db;
    private readonly IBillingService _service;
    private readonly IPermissionService _perm;
    private readonly ILogger<BillingController> _logger;

    /// <summary>v2.13.110 新增费用标准所需权限码（与 PageHeader primaryAction.PermissionCode 一致）</summary>
    public const string RequiredPermissionCode = "billingstandard:add";

    public BillingController(DormDbContext db, IBillingService service, IPermissionService perm, ILogger<BillingController> logger)
    {
        _db = db;
        _service = service;
        _perm = perm;
        _logger = logger;
    }

    #region 费用标准

    /// <summary>获取当前有效的费用标准</summary>
    [HttpGet("standards/active")]
    public async Task<ApiResponse<BillingStandard>> GetActiveStandard()
    {
        var standard = await _service.GetActiveStandardAsync();
        return standard == null
            ? ApiResponse<BillingStandard>.Fail("NOT_FOUND", "没有可用的费用标准")
            : ApiResponse<BillingStandard>.Ok(standard);
    }

    /// <summary>获取费用标准列表（分页）</summary>
    [HttpGet("standards")]
    public async Task<ApiResponse<PagedResult<BillingStandard>>> GetStandards(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetStandardsAsync(page, pageSize);
        return ApiResponse<PagedResult<BillingStandard>>.Ok(result);
    }

    /// <summary>创建/更新费用标准 — v2.13.110 三层防御 API 层</summary>
    [HttpPost("standards")]
    public async Task<ApiResponse> SaveStandard([FromBody] BillingStandard standard)
    {
        // v2.13.110 三层防御（API 层）：无 billingstandard:add 权限 → 403 拒绝
        if (!HasPermission(RequiredPermissionCode))
        {
            _logger.LogWarning("[v2.13.110 RBAC] 用户 {User} 尝试 POST /api/v1/billing/standards 但缺少 {Code} 权限",
                HttpContext.GetCurrentUserName(), RequiredPermissionCode);
            return ApiResponse.Fail("PERMISSION_DENIED", $"无 {RequiredPermissionCode} 权限，无法保存费用标准");
        }

        var (ok, msg) = await _service.SaveStandardAsync(standard);
        return ok ? ApiResponse.Ok(msg) : ApiResponse.Fail("SAVE_FAILED", msg);
    }

    #endregion

    #region 宿舍账单

    /// <summary>查询宿舍账单列表</summary>
    [HttpGet("dorm-bills")]
    public async Task<ApiResponse<PagedResult<DormBilling>>> GetDormBills(
        [FromQuery] string? billingMonth,
        [FromQuery] string? dormCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetDormBillsAsync(billingMonth, dormCode, page, pageSize);
        return ApiResponse<PagedResult<DormBilling>>.Ok(result);
    }

    /// <summary>生成宿舍账单</summary>
    [HttpPost("dorm-bills/generate")]
    public async Task<ApiResponse<BillingGenerateResult>> GenerateDormBills([FromBody] GenerateRequest request)
    {
        var result = await _service.GenerateDormBillsAsync(request.BillingMonth);
        return ApiResponse<BillingGenerateResult>.Ok(result,
            $"生成完成：新增 {result.GeneratedCount} 条，更新 {result.UpdatedCount} 条，跳过 {result.SkippedCount} 条");
    }

    /// <summary>发布宿舍账单</summary>
    [HttpPost("dorm-bills/publish")]
    public async Task<ApiResponse> PublishDormBills([FromBody] GenerateRequest request)
    {
        var (ok, msg) = await _service.PublishDormBillsAsync(request.BillingMonth);
        return ok ? ApiResponse.Ok(msg) : ApiResponse.Fail("PUBLISH_FAILED", msg);
    }

    #endregion

    #region 员工账单

    /// <summary>查询员工账单列表</summary>
    [HttpGet("employee-bills")]
    public async Task<ApiResponse<PagedResult<EmployeeBilling>>> GetEmployeeBills(
        [FromQuery] string? billingMonth,
        [FromQuery] string? dormCode,
        [FromQuery] string? empKeyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetEmployeeBillsAsync(billingMonth, dormCode, empKeyword, page, pageSize);
        return ApiResponse<PagedResult<EmployeeBilling>>.Ok(result);
    }

    /// <summary>生成员工分摊账单</summary>
    [HttpPost("employee-bills/generate")]
    public async Task<ApiResponse<BillingGenerateResult>> GenerateEmployeeBills([FromBody] GenerateRequest request)
    {
        var result = await _service.GenerateEmployeeBillsAsync(request.BillingMonth);
        return ApiResponse<BillingGenerateResult>.Ok(result,
            $"生成完成：新增 {result.GeneratedCount} 条，更新 {result.UpdatedCount} 条，跳过 {result.SkippedCount} 条");
    }

    /// <summary>发布员工账单</summary>
    [HttpPost("employee-bills/publish")]
    public async Task<ApiResponse> PublishEmployeeBills([FromBody] GenerateRequest request)
    {
        var (ok, msg) = await _service.PublishEmployeeBillsAsync(request.BillingMonth);
        return ok ? ApiResponse.Ok(msg) : ApiResponse.Fail("PUBLISH_FAILED", msg);
    }

    #endregion

    /// <summary>
    /// v2.13.110：API 层权限辅助方法（与 PersonnelController 保持一致）。
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

public class GenerateRequest
{
    public string BillingMonth { get; set; } = string.Empty;
}
