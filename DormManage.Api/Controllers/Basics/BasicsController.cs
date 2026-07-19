using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Api.Controllers.Basics;

/// <summary>
/// 基础资料 API 控制器
/// </summary>
[ApiController]
[Route("api/basics")]
public class BasicsController : ControllerBase
{
    private readonly IBasicsService _service;

    public BasicsController(IBasicsService service)
    {
        _service = service;
    }

    #region 部门

    /// <summary>
    /// 获取部门列表
    /// </summary>
    [HttpGet("departments")]
    public async Task<ApiResponse<PagedResult<Department>>> GetDepartments(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetDepartmentsAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<Department>>.Ok(result);
    }

    /// <summary>
    /// 获取部门详情
    /// </summary>
    [HttpGet("departments/{id}")]
    public async Task<ApiResponse<Department>> GetDepartment(int id)
    {
        var entity = await _service.GetDepartmentByIdAsync(id);
        if (entity == null) return ApiResponse<Department>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<Department>.Ok(entity);
    }

    /// <summary>
    /// 新增部门
    /// </summary>
    [HttpPost("departments")]
    public async Task<ApiResponse<Department>> CreateDepartment([FromBody] Department model)
    {
        return await _service.CreateDepartmentAsync(model);
    }

    /// <summary>
    /// 更新部门
    /// </summary>
    [HttpPut("departments/{id}")]
    public async Task<ApiResponse<Department>> UpdateDepartment(int id, [FromBody] Department model)
    {
        return await _service.UpdateDepartmentAsync(id, model);
    }

    /// <summary>
    /// 删除部门
    /// </summary>
    [HttpDelete("departments/{id}")]
    public async Task<ApiResponse> DeleteDepartment(int id)
    {
        return await _service.DeleteDepartmentAsync(id);
    }

    #endregion

    #region 楼栋

    /// <summary>
    /// 获取楼栋列表
    /// </summary>
    [HttpGet("buildings")]
    public async Task<ApiResponse<PagedResult<Building>>> GetBuildings(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetBuildingsAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<Building>>.Ok(result);
    }

    /// <summary>
    /// 获取楼栋详情
    /// </summary>
    [HttpGet("buildings/{id}")]
    public async Task<ApiResponse<Building>> GetBuilding(int id)
    {
        var entity = await _service.GetBuildingByIdAsync(id);
        if (entity == null) return ApiResponse<Building>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<Building>.Ok(entity);
    }

    /// <summary>
    /// 新增楼栋
    /// </summary>
    [HttpPost("buildings")]
    public async Task<ApiResponse<Building>> CreateBuilding([FromBody] Building model)
    {
        return await _service.CreateBuildingAsync(model);
    }

    /// <summary>
    /// 更新楼栋
    /// </summary>
    [HttpPut("buildings/{id}")]
    public async Task<ApiResponse<Building>> UpdateBuilding(int id, [FromBody] Building model)
    {
        return await _service.UpdateBuildingAsync(id, model);
    }

    /// <summary>
    /// 删除楼栋
    /// </summary>
    [HttpDelete("buildings/{id}")]
    public async Task<ApiResponse> DeleteBuilding(int id)
    {
        return await _service.DeleteBuildingAsync(id);
    }

    #endregion

    #region 楼层

    /// <summary>
    /// 获取楼层列表
    /// </summary>
    [HttpGet("floors")]
    public async Task<ApiResponse<PagedResult<Floor>>> GetFloors(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetFloorsAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<Floor>>.Ok(result);
    }

    /// <summary>
    /// 获取楼层详情
    /// </summary>
    [HttpGet("floors/{id}")]
    public async Task<ApiResponse<Floor>> GetFloor(int id)
    {
        var entity = await _service.GetFloorByIdAsync(id);
        if (entity == null) return ApiResponse<Floor>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<Floor>.Ok(entity);
    }

    /// <summary>
    /// 新增楼层
    /// </summary>
    [HttpPost("floors")]
    public async Task<ApiResponse<Floor>> CreateFloor([FromBody] Floor model)
    {
        return await _service.CreateFloorAsync(model);
    }

    /// <summary>
    /// 更新楼层
    /// </summary>
    [HttpPut("floors/{id}")]
    public async Task<ApiResponse<Floor>> UpdateFloor(int id, [FromBody] Floor model)
    {
        return await _service.UpdateFloorAsync(id, model);
    }

    /// <summary>
    /// 删除楼层
    /// </summary>
    [HttpDelete("floors/{id}")]
    public async Task<ApiResponse> DeleteFloor(int id)
    {
        return await _service.DeleteFloorAsync(id);
    }

    #endregion

    #region 地址

    /// <summary>
    /// 获取地址列表
    /// </summary>
    [HttpGet("addresses")]
    public async Task<ApiResponse<PagedResult<Address>>> GetAddresses(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetAddressesAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<Address>>.Ok(result);
    }

    /// <summary>
    /// 获取地址详情
    /// </summary>
    [HttpGet("addresses/{id}")]
    public async Task<ApiResponse<Address>> GetAddress(int id)
    {
        var entity = await _service.GetAddressByIdAsync(id);
        if (entity == null) return ApiResponse<Address>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<Address>.Ok(entity);
    }

    /// <summary>
    /// 新增地址
    /// </summary>
    [HttpPost("addresses")]
    public async Task<ApiResponse<Address>> CreateAddress([FromBody] Address model)
    {
        return await _service.CreateAddressAsync(model);
    }

    /// <summary>
    /// 更新地址
    /// </summary>
    [HttpPut("addresses/{id}")]
    public async Task<ApiResponse<Address>> UpdateAddress(int id, [FromBody] Address model)
    {
        return await _service.UpdateAddressAsync(id, model);
    }

    /// <summary>
    /// 删除地址
    /// </summary>
    [HttpDelete("addresses/{id}")]
    public async Task<ApiResponse> DeleteAddress(int id)
    {
        return await _service.DeleteAddressAsync(id);
    }

    #endregion

    #region 员工类型

    /// <summary>
    /// 获取员工类型列表
    /// </summary>
    [HttpGet("employee-types")]
    public async Task<ApiResponse<PagedResult<EmployeeType>>> GetEmployeeTypes(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetEmployeeTypesAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<EmployeeType>>.Ok(result);
    }

    /// <summary>
    /// 获取员工类型详情
    /// </summary>
    [HttpGet("employee-types/{id}")]
    public async Task<ApiResponse<EmployeeType>> GetEmployeeType(int id)
    {
        var entity = await _service.GetEmployeeTypeByIdAsync(id);
        if (entity == null) return ApiResponse<EmployeeType>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<EmployeeType>.Ok(entity);
    }

    /// <summary>
    /// 新增员工类型
    /// </summary>
    [HttpPost("employee-types")]
    public async Task<ApiResponse<EmployeeType>> CreateEmployeeType([FromBody] EmployeeType model)
    {
        return await _service.CreateEmployeeTypeAsync(model);
    }

    /// <summary>
    /// 更新员工类型
    /// </summary>
    [HttpPut("employee-types/{id}")]
    public async Task<ApiResponse<EmployeeType>> UpdateEmployeeType(int id, [FromBody] EmployeeType model)
    {
        return await _service.UpdateEmployeeTypeAsync(id, model);
    }

    /// <summary>
    /// 删除员工类型
    /// </summary>
    [HttpDelete("employee-types/{id}")]
    public async Task<ApiResponse> DeleteEmployeeType(int id)
    {
        return await _service.DeleteEmployeeTypeAsync(id);
    }

    #endregion

    #region 考勤班次

    /// <summary>
    /// 获取考勤班次列表
    /// </summary>
    [HttpGet("attendance-types")]
    public async Task<ApiResponse<PagedResult<AttendanceType>>> GetAttendanceTypes(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetAttendanceTypesAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<AttendanceType>>.Ok(result);
    }

    /// <summary>
    /// 获取考勤班次详情
    /// </summary>
    [HttpGet("attendance-types/{id}")]
    public async Task<ApiResponse<AttendanceType>> GetAttendanceType(int id)
    {
        var entity = await _service.GetAttendanceTypeByIdAsync(id);
        if (entity == null) return ApiResponse<AttendanceType>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<AttendanceType>.Ok(entity);
    }

    /// <summary>
    /// 新增考勤班次
    /// </summary>
    [HttpPost("attendance-types")]
    public async Task<ApiResponse<AttendanceType>> CreateAttendanceType([FromBody] AttendanceType model)
    {
        return await _service.CreateAttendanceTypeAsync(model);
    }

    /// <summary>
    /// 更新考勤班次
    /// </summary>
    [HttpPut("attendance-types/{id}")]
    public async Task<ApiResponse<AttendanceType>> UpdateAttendanceType(int id, [FromBody] AttendanceType model)
    {
        return await _service.UpdateAttendanceTypeAsync(id, model);
    }

    /// <summary>
    /// 删除考勤班次
    /// </summary>
    [HttpDelete("attendance-types/{id}")]
    public async Task<ApiResponse> DeleteAttendanceType(int id)
    {
        return await _service.DeleteAttendanceTypeAsync(id);
    }

    #endregion

    #region 计量单位

    /// <summary>
    /// 获取计量单位列表
    /// </summary>
    [HttpGet("meter-units")]
    public async Task<ApiResponse<PagedResult<MeterUnit>>> GetMeterUnits(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetMeterUnitsAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<MeterUnit>>.Ok(result);
    }

    /// <summary>
    /// 获取计量单位详情
    /// </summary>
    [HttpGet("meter-units/{id}")]
    public async Task<ApiResponse<MeterUnit>> GetMeterUnit(int id)
    {
        var entity = await _service.GetMeterUnitByIdAsync(id);
        if (entity == null) return ApiResponse<MeterUnit>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<MeterUnit>.Ok(entity);
    }

    /// <summary>
    /// 新增计量单位
    /// </summary>
    [HttpPost("meter-units")]
    public async Task<ApiResponse<MeterUnit>> CreateMeterUnit([FromBody] MeterUnit model)
    {
        return await _service.CreateMeterUnitAsync(model);
    }

    /// <summary>
    /// 更新计量单位
    /// </summary>
    [HttpPut("meter-units/{id}")]
    public async Task<ApiResponse<MeterUnit>> UpdateMeterUnit(int id, [FromBody] MeterUnit model)
    {
        return await _service.UpdateMeterUnitAsync(id, model);
    }

    /// <summary>
    /// 删除计量单位
    /// </summary>
    [HttpDelete("meter-units/{id}")]
    public async Task<ApiResponse> DeleteMeterUnit(int id)
    {
        return await _service.DeleteMeterUnitAsync(id);
    }

    #endregion

    #region 住宿状态

    /// <summary>
    /// 获取住宿状态列表
    /// </summary>
    [HttpGet("residence-statuses")]
    public async Task<ApiResponse<PagedResult<ResidenceStatus>>> GetResidenceStatuses(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetResidenceStatusesAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<ResidenceStatus>>.Ok(result);
    }

    /// <summary>
    /// 获取住宿状态详情
    /// </summary>
    [HttpGet("residence-statuses/{id}")]
    public async Task<ApiResponse<ResidenceStatus>> GetResidenceStatus(int id)
    {
        var entity = await _service.GetResidenceStatusByIdAsync(id);
        if (entity == null) return ApiResponse<ResidenceStatus>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<ResidenceStatus>.Ok(entity);
    }

    /// <summary>
    /// 新增住宿状态
    /// </summary>
    [HttpPost("residence-statuses")]
    public async Task<ApiResponse<ResidenceStatus>> CreateResidenceStatus([FromBody] ResidenceStatus model)
    {
        return await _service.CreateResidenceStatusAsync(model);
    }

    /// <summary>
    /// 更新住宿状态
    /// </summary>
    [HttpPut("residence-statuses/{id}")]
    public async Task<ApiResponse<ResidenceStatus>> UpdateResidenceStatus(int id, [FromBody] ResidenceStatus model)
    {
        return await _service.UpdateResidenceStatusAsync(id, model);
    }

    /// <summary>
    /// 删除住宿状态
    /// </summary>
    [HttpDelete("residence-statuses/{id}")]
    public async Task<ApiResponse> DeleteResidenceStatus(int id)
    {
        return await _service.DeleteResidenceStatusAsync(id);
    }

    #endregion

    #region 在职状态

    /// <summary>
    /// 获取在职状态列表
    /// </summary>
    [HttpGet("employment-statuses")]
    public async Task<ApiResponse<PagedResult<EmploymentStatus>>> GetEmploymentStatuses(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetEmploymentStatusesAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<EmploymentStatus>>.Ok(result);
    }

    /// <summary>
    /// 获取在职状态详情
    /// </summary>
    [HttpGet("employment-statuses/{id}")]
    public async Task<ApiResponse<EmploymentStatus>> GetEmploymentStatus(int id)
    {
        var entity = await _service.GetEmploymentStatusByIdAsync(id);
        if (entity == null) return ApiResponse<EmploymentStatus>.Fail("NOT_FOUND", "记录不存在");
        return ApiResponse<EmploymentStatus>.Ok(entity);
    }

    /// <summary>
    /// 新增在职状态
    /// </summary>
    [HttpPost("employment-statuses")]
    public async Task<ApiResponse<EmploymentStatus>> CreateEmploymentStatus([FromBody] EmploymentStatus model)
    {
        return await _service.CreateEmploymentStatusAsync(model);
    }

    /// <summary>
    /// 更新在职状态
    /// </summary>
    [HttpPut("employment-statuses/{id}")]
    public async Task<ApiResponse<EmploymentStatus>> UpdateEmploymentStatus(int id, [FromBody] EmploymentStatus model)
    {
        return await _service.UpdateEmploymentStatusAsync(id, model);
    }

    /// <summary>
    /// 删除在职状态
    /// </summary>
    [HttpDelete("employment-statuses/{id}")]
    public async Task<ApiResponse> DeleteEmploymentStatus(int id)
    {
        return await _service.DeleteEmploymentStatusAsync(id);
    }

    #endregion

    #region 员工班组 (Team)

    /// <summary>
    /// 获取班组列表
    /// </summary>
    [HttpGet("teams")]
    public async Task<ApiResponse<PagedResult<Team>>> GetTeams(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetTeamsAsync(keyword, page, pageSize);
        return ApiResponse<PagedResult<Team>>.Ok(result);
    }

    /// <summary>
    /// 获取班组详情
    /// </summary>
    [HttpGet("teams/{id}")]
    public async Task<ApiResponse<Team?>> GetTeam(int id)
    {
        var result = await _service.GetTeamByIdAsync(id);
        return ApiResponse<Team?>.Ok(result);
    }

    /// <summary>
    /// 新增班组
    /// </summary>
    [HttpPost("teams")]
    public async Task<ApiResponse<Team>> CreateTeam([FromBody] Team model)
    {
        var result = await _service.CreateTeamAsync(model);
        return result;
    }

    /// <summary>
    /// 更新班组
    /// </summary>
    [HttpPut("teams/{id}")]
    public async Task<ApiResponse<Team>> UpdateTeam(int id, [FromBody] Team model)
    {
        var result = await _service.UpdateTeamAsync(id, model);
        return result;
    }

    /// <summary>
    /// 删除班组
    /// </summary>
    [HttpDelete("teams/{id}")]
    public async Task<ApiResponse> DeleteTeam(int id)
    {
        var result = await _service.DeleteTeamAsync(id);
        return result;
    }

    #endregion
}
