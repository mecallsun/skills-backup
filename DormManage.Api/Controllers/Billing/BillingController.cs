using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Api.Controllers.Billing;

/// <summary>
/// 费用管理 API 控制器（费用标准 + 宿舍账单 + 员工账单）
/// </summary>
[ApiController]
[Route("api/v1/billing")]
public class BillingController : ControllerBase
{
    private readonly DormDbContext _db;
    private readonly IBillingService _service;

    public BillingController(DormDbContext db, IBillingService service)
    {
        _db = db;
        _service = service;
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
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetStandardsAsync(page, pageSize);
        return ApiResponse<PagedResult<BillingStandard>>.Ok(result);
    }

    /// <summary>创建/更新费用标准</summary>
    [HttpPost("standards")]
    public async Task<ApiResponse> SaveStandard([FromBody] BillingStandard standard)
    {
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
        [FromQuery] int pageSize = 20)
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
        [FromQuery] int pageSize = 20)
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
}

public class GenerateRequest
{
    public string BillingMonth { get; set; } = string.Empty;
}
