using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Api.Controllers.System;

/// <summary>
/// 字段权限 API（v2.13.92 新增）：SysFieldPermission CRUD
///
/// 端点：
/// - GET  /api/v1/system/field-permissions       全表列表（按 SortOrder 排序）
/// - GET  /api/v1/system/field-permissions/meta  元数据（v2.13.196 新增：模块+字段下拉联动数据源）
/// - POST /api/v1/system/field-permissions       新增字段（v2.13.195 新增）
/// - PUT  /api/v1/system/field-permissions       批量更新（设置 IsActive + SortOrder）
///
/// 说明：
///   - PermissionType=3（privacy:field:enable）的检查通过现有 IPermissionService 标准权限流自动加载
///   - v2.13.196 升级：模块和字段都通过下拉选择，避免手动输入错误
/// </summary>
[ApiController]
[Route("api/v1/system/field-permissions")]
public class FieldPermissionController : ControllerBase
{
    private readonly ISysFieldPermissionService _svc;
    private readonly IOperationLogService _opLog;

    public FieldPermissionController(ISysFieldPermissionService svc, IOperationLogService opLog)
    {
        _svc = svc;
        _opLog = opLog;
    }

    /// <summary>列出全部字段权限记录（含 IsActive=false）</summary>
    [HttpGet]
    public async Task<ApiResponse<List<SysFieldPermission>>> List()
    {
        var list = await _svc.GetAllAsync();
        return ApiResponse<List<SysFieldPermission>>.Ok(list);
    }

    /// <summary>
    /// 获取字段权限新增表单的元数据（v2.13.196 新增）
    /// 返回主菜单中有列表页面的模块，以及每个模块对应数据库表的可选字段
    /// 用于前端两级下拉联动（模块→字段）
    /// </summary>
    [HttpGet("meta")]
    public async Task<ApiResponse<FieldPermissionMetaViewModel>> GetMeta()
    {
        // 从已存在的 FieldPermissions 中提取已使用的 FieldKey，
        // 用于前端标记这些字段已经存在（灰色不可选）
        var existing = await _svc.GetAllAsync();
        var existingKeys = existing.Select(e => e.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 主菜单中包含列表页面的模块（v2.13.196 设计：基于 FIXED_TABS 的 tab-bar.js 对齐）
        var modules = FieldPermissionMetaViewModel.GetDefaultModules();

        // 为每个模块的字段添加 isExisting 标记
        foreach (var module in modules)
        {
            foreach (var field in module.Fields)
            {
                field.IsExisting = existingKeys.Contains(field.Key);
            }
        }

        return ApiResponse<FieldPermissionMetaViewModel>.Ok(new FieldPermissionMetaViewModel
        {
            Modules = modules,
            DefaultSensitivityLevel = 2  // 中等
        });
    }

    /// <summary>新增字段权限（v2.13.195 新增）</summary>
    [HttpPost]
    public async Task<ApiResponse> Create([FromBody] DormManage.Shared.Services.SysFieldPermissionCreateDto dto)
    {
        if (dto == null)
            return ApiResponse.Fail("VALIDATION_ERROR", "请求体不能为空");

        // 显式校验必填字段（防御性）
        if (string.IsNullOrWhiteSpace(dto.FieldKey))
            return ApiResponse.Fail("VALIDATION_ERROR", "字段键不能为空");
        if (string.IsNullOrWhiteSpace(dto.Module))
            return ApiResponse.Fail("VALIDATION_ERROR", "模块不能为空");
        if (string.IsNullOrWhiteSpace(dto.FieldName))
            return ApiResponse.Fail("VALIDATION_ERROR", "字段显示名不能为空");
        if (string.IsNullOrWhiteSpace(dto.Description))
            return ApiResponse.Fail("VALIDATION_ERROR", "描述不能为空");

        try
        {
            var created = await _svc.CreateAsync(dto);
            await _opLog.LogAsync("FieldPermission", "新增字段", $"字段键：{created.FieldKey}，模块：{created.Module}, 字段名：{created.FieldName}");
            return ApiResponse.Ok("字段添加成功，ID: " + created.Id);
        }
        catch (ArgumentException ex)
        {
            return ApiResponse.Fail("VALIDATION_ERROR", ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            // v2.13.196 增强：显示内部异常（数据库约束违反）
            var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
            return ApiResponse.Fail("CREATE_FAILED", $"数据库保存失败：{innerMsg}");
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            return ApiResponse.Fail("CREATE_FAILED", $"创建字段权限失败：{innerMsg}");
        }
    }

    /// <summary>批量更新字段权限（IsActive + SortOrder）</summary>
    [HttpPut]
    public async Task<ApiResponse> Update([FromBody] SysFieldPermissionUpdateRequest body)
    {
        if (body?.Updates == null || body.Updates.Count == 0)
            return ApiResponse.Ok("无变更");

        var updatedBy = User.Identity?.Name ?? "system";
        var result = await _svc.UpdateAsync(body.Updates, updatedBy);

        if (result.Success)
        {
            await _opLog.LogAsync("FieldPermission", "批量更新", $"更新字段数：{body.Updates.Count}（启用：{body.Updates.Count(u => u.IsActive)}）");
        }

        return result;
    }
}

/// <summary>批量更新请求体</summary>
public class SysFieldPermissionUpdateRequest
{
    public List<DormManage.Shared.Services.SysFieldPermissionUpdateDto> Updates { get; set; } = new();
}

/// <summary>
/// 字段权限新增表单元数据 ViewModel（v2.13.196 新增）
/// 用于前端两级下拉联动：选择模块后展示该模块下的可选字段
/// </summary>
public class FieldPermissionMetaViewModel
{
    /// <summary>可选模块列表（基于主菜单中有列表页面的模块）</summary>
    public List<ModuleOptionViewModel> Modules { get; set; } = new();

    /// <summary>默认敏感等级（前端表单默认值）</summary>
    public byte DefaultSensitivityLevel { get; set; } = 2;

    /// <summary>
    /// 获取默认模块列表（基于 tab-bar.js 的 FIXED_TABS，v2.13.196 同步）
    /// 每个模块对应一个数据库主表
    /// </summary>
    public static List<ModuleOptionViewModel> GetDefaultModules()
    {
        return new List<ModuleOptionViewModel>
        {
            new() { Code = "Personnel", DisplayName = "人员清单", EntityName = "SysEmployee", Icon = "bi-people-fill", Fields = GetPersonnelFields() },
            new() { Code = "Booking", DisplayName = "办理登记", EntityName = "DormBooking", Icon = "bi-clipboard-check", Fields = GetBookingFields() },
            new() { Code = "Dorm", DisplayName = "住宿档案", EntityName = "Dorm", Icon = "bi-building", Fields = GetDormFields() },
            new() { Code = "Meter", DisplayName = "智能抄表", EntityName = "MeterRecord", Icon = "bi-clipboard-data", Fields = GetMeterFields() },
            new() { Code = "BillingStandard", DisplayName = "费用标准", EntityName = "BillingStandard", Icon = "bi-cash-stack", Fields = GetBillingStandardFields() },
            new() { Code = "DormBilling", DisplayName = "住宿账单", EntityName = "DormBilling", Icon = "bi-receipt", Fields = GetDormBillingFields() },
            new() { Code = "EmployeeBilling", DisplayName = "员工账单", EntityName = "EmployeeBilling", Icon = "bi-wallet2", Fields = GetEmployeeBillingFields() }
        };
    }

    private static List<FieldOptionViewModel> GetPersonnelFields() => new()
    {
        new() { Key = "employee.realname", DisplayName = "姓名", FieldType = "string", SensitivityHint = 1, DescriptionHint = "员工真实姓名（高 PII）" },
        new() { Key = "employee.employeecode", DisplayName = "工号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "公司内唯一标识" },
        new() { Key = "employee.phone", DisplayName = "手机号", FieldType = "string", SensitivityHint = 1, DescriptionHint = "联系电话（高 PII）" },
        new() { Key = "employee.idnumber", DisplayName = "身份证号", FieldType = "string", SensitivityHint = 1, DescriptionHint = "18 位中国大陆居民身份证号（极高 PII）" },
        new() { Key = "employee.department", DisplayName = "部门", FieldType = "string", SensitivityHint = 2, DescriptionHint = "所属部门（可推断组织信息）" },
        new() { Key = "employee.team", DisplayName = "班组", FieldType = "string", SensitivityHint = 2, DescriptionHint = "所属班组（员工基础组织信息）" },
        new() { Key = "employee.attendance", DisplayName = "考勤班次", FieldType = "string", SensitivityHint = 2, DescriptionHint = "考勤班次（可推断作息规律）" },
        new() { Key = "employee.hiredate", DisplayName = "入职日期", FieldType = "date", SensitivityHint = 2, DescriptionHint = "员工入职日期（隐私履历）" },
        new() { Key = "employee.leavedate", DisplayName = "离职日期", FieldType = "date", SensitivityHint = 2, DescriptionHint = "员工离职日期（高敏感）" },
        new() { Key = "employee.dormcode", DisplayName = "住宿房号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "当前入住房号（隐私住址）" },
        new() { Key = "employee.bedno", DisplayName = "床号", FieldType = "number", SensitivityHint = 2, DescriptionHint = "当前入住床号" },
        new() { Key = "employee.remark", DisplayName = "备注", FieldType = "string", SensitivityHint = 2, DescriptionHint = "自由文本备注（可能含敏感信息）" }
    };

    private static List<FieldOptionViewModel> GetBookingFields() => new()
    {
        new() { Key = "booking.employeename", DisplayName = "员工姓名", FieldType = "string", SensitivityHint = 1, DescriptionHint = "住宿员工姓名" },
        new() { Key = "booking.employeecode", DisplayName = "员工工号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "住宿员工工号" },
        new() { Key = "booking.dormcode", DisplayName = "入住房号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "住宿登记房号" },
        new() { Key = "booking.bedno", DisplayName = "入住床号", FieldType = "number", SensitivityHint = 2, DescriptionHint = "住宿登记床号" },
        new() { Key = "booking.registrar", DisplayName = "登记人", FieldType = "string", SensitivityHint = 1, DescriptionHint = "办理入住登记的操作人" },
        new() { Key = "booking.checkindate", DisplayName = "入住日期", FieldType = "date", SensitivityHint = 2, DescriptionHint = "实际入住日期" },
        new() { Key = "booking.expectedleavedate", DisplayName = "预计退房日期", FieldType = "date", SensitivityHint = 2, DescriptionHint = "预计退房日期" }
    };

    private static List<FieldOptionViewModel> GetDormFields() => new()
    {
        new() { Key = "dorm.dormcode", DisplayName = "房号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "住宿房号标识" },
        new() { Key = "dorm.capacity", DisplayName = "容量（床位数）", FieldType = "number", SensitivityHint = 3, DescriptionHint = "住宿可容纳人数" },
        new() { Key = "dorm.currentcount", DisplayName = "在住人数", FieldType = "number", SensitivityHint = 3, DescriptionHint = "当前住宿人数" },
        new() { Key = "dorm.location", DisplayName = "位置/区域", FieldType = "string", SensitivityHint = 2, DescriptionHint = "住宿楼栋/楼层/位置信息" },
        new() { Key = "dorm.remark", DisplayName = "备注", FieldType = "string", SensitivityHint = 2, DescriptionHint = "住宿备注信息" }
    };

    private static List<FieldOptionViewModel> GetMeterFields() => new()
    {
        new() { Key = "meter.dormcode", DisplayName = "房号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "抄表对应房号" },
        new() { Key = "meter.coldmeter", DisplayName = "冷水表读数", FieldType = "number", SensitivityHint = 3, DescriptionHint = "冷水表本月读数" },
        new() { Key = "meter.hotmeter", DisplayName = "热水表读数", FieldType = "number", SensitivityHint = 3, DescriptionHint = "热水表本月读数" },
        new() { Key = "meter.electricmeter", DisplayName = "电表读数", FieldType = "number", SensitivityHint = 3, DescriptionHint = "电表本月读数" },
        new() { Key = "meter.clientrecordid", DisplayName = "PDA 客户端记录 ID", FieldType = "string", SensitivityHint = 3, DescriptionHint = "PDA 上传时的客户端标识" }
    };

    private static List<FieldOptionViewModel> GetBillingStandardFields() => new()
    {
        new() { Key = "billingstandard.standardname", DisplayName = "标准名称", FieldType = "string", SensitivityHint = 3, DescriptionHint = "费用标准名称" },
        new() { Key = "billingstandard.amount", DisplayName = "金额", FieldType = "number", SensitivityHint = 3, DescriptionHint = "费用金额" },
        new() { Key = "billingstandard.remark", DisplayName = "备注", FieldType = "string", SensitivityHint = 2, DescriptionHint = "费用标准备注" }
    };

    private static List<FieldOptionViewModel> GetDormBillingFields() => new()
    {
        new() { Key = "dormbilling.dormcode", DisplayName = "房号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "账单对应房号" },
        new() { Key = "dormbilling.amount", DisplayName = "金额", FieldType = "number", SensitivityHint = 3, DescriptionHint = "账单金额" },
        new() { Key = "dormbilling.coldusage", DisplayName = "冷水用量", FieldType = "number", SensitivityHint = 3, DescriptionHint = "冷水表用量" },
        new() { Key = "dormbilling.hotusage", DisplayName = "热水用量", FieldType = "number", SensitivityHint = 3, DescriptionHint = "热水表用量" },
        new() { Key = "dormbilling.electricusage", DisplayName = "电用量", FieldType = "number", SensitivityHint = 3, DescriptionHint = "电表用量" }
    };

    private static List<FieldOptionViewModel> GetEmployeeBillingFields() => new()
    {
        new() { Key = "employeebilling.employeecode", DisplayName = "员工工号", FieldType = "string", SensitivityHint = 2, DescriptionHint = "账单员工工号" },
        new() { Key = "employeebilling.employeename", DisplayName = "员工姓名", FieldType = "string", SensitivityHint = 1, DescriptionHint = "账单员工姓名" },
        new() { Key = "employeebilling.amount", DisplayName = "金额", FieldType = "number", SensitivityHint = 3, DescriptionHint = "账单金额" },
        new() { Key = "employeebilling.coldusage", DisplayName = "冷水用量", FieldType = "number", SensitivityHint = 3, DescriptionHint = "冷水表用量" },
        new() { Key = "employeebilling.hotusage", DisplayName = "热水用量", FieldType = "number", SensitivityHint = 3, DescriptionHint = "热水表用量" },
        new() { Key = "employeebilling.electricusage", DisplayName = "电用量", FieldType = "number", SensitivityHint = 3, DescriptionHint = "电表用量" }
    };
}

/// <summary>模块选项 ViewModel</summary>
public class ModuleOptionViewModel
{
    /// <summary>模块代码（用于 SysFieldPermission.Module 字段）</summary>
    public string Code { get; set; } = "";
    /// <summary>模块显示名称（中文）</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>对应数据库实体名称</summary>
    public string EntityName { get; set; } = "";
    /// <summary>图标 class（Bootstrap Icons）</summary>
    public string Icon { get; set; } = "";
    /// <summary>该模块下所有可选字段</summary>
    public List<FieldOptionViewModel> Fields { get; set; } = new();
}

/// <summary>字段选项 ViewModel</summary>
public class FieldOptionViewModel
{
    /// <summary>字段键（如 employee.phone）</summary>
    public string Key { get; set; } = "";
    /// <summary>字段显示名（中文）</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>字段类型（string/number/date/datetime/boolean）</summary>
    public string FieldType { get; set; } = "string";
    /// <summary>建议的敏感等级（1=高 2=中 3=低）</summary>
    public byte SensitivityHint { get; set; } = 2;
    /// <summary>建议的描述文本</summary>
    public string DescriptionHint { get; set; } = "";
    /// <summary>该字段是否已在 SysFieldPermission 中存在（前端标记为不可选）</summary>
    public bool IsExisting { get; set; }
}