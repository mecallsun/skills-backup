using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Shared.Services;

/// <summary>
/// 基础资料服务接口
/// </summary>
public interface IBasicsService
{
    // 部门
    Task<PagedResult<Department>> GetDepartmentsAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<Department?> GetDepartmentByIdAsync(int id);
    Task<ApiResponse<Department>> CreateDepartmentAsync(Department model);
    Task<ApiResponse<Department>> UpdateDepartmentAsync(int id, Department model);
    Task<ApiResponse> DeleteDepartmentAsync(int id);

    // 楼栋
    Task<PagedResult<Building>> GetBuildingsAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<Building?> GetBuildingByIdAsync(int id);
    Task<ApiResponse<Building>> CreateBuildingAsync(Building model);
    Task<ApiResponse<Building>> UpdateBuildingAsync(int id, Building model);
    Task<ApiResponse> DeleteBuildingAsync(int id);

    // 楼层
    Task<PagedResult<Floor>> GetFloorsAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<Floor?> GetFloorByIdAsync(int id);
    Task<ApiResponse<Floor>> CreateFloorAsync(Floor model);
    Task<ApiResponse<Floor>> UpdateFloorAsync(int id, Floor model);
    Task<ApiResponse> DeleteFloorAsync(int id);

    // 地址
    Task<PagedResult<Address>> GetAddressesAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<Address?> GetAddressByIdAsync(int id);
    Task<ApiResponse<Address>> CreateAddressAsync(Address model);
    Task<ApiResponse<Address>> UpdateAddressAsync(int id, Address model);
    Task<ApiResponse> DeleteAddressAsync(int id);

    // 员工类型
    Task<PagedResult<EmployeeType>> GetEmployeeTypesAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<EmployeeType?> GetEmployeeTypeByIdAsync(int id);
    Task<ApiResponse<EmployeeType>> CreateEmployeeTypeAsync(EmployeeType model);
    Task<ApiResponse<EmployeeType>> UpdateEmployeeTypeAsync(int id, EmployeeType model);
    Task<ApiResponse> DeleteEmployeeTypeAsync(int id);

    // 考勤班次
    Task<PagedResult<AttendanceType>> GetAttendanceTypesAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<AttendanceType?> GetAttendanceTypeByIdAsync(int id);
    Task<ApiResponse<AttendanceType>> CreateAttendanceTypeAsync(AttendanceType model);
    Task<ApiResponse<AttendanceType>> UpdateAttendanceTypeAsync(int id, AttendanceType model);
    Task<ApiResponse> DeleteAttendanceTypeAsync(int id);

    // 计量单位
    Task<PagedResult<MeterUnit>> GetMeterUnitsAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<MeterUnit?> GetMeterUnitByIdAsync(int id);
    Task<ApiResponse<MeterUnit>> CreateMeterUnitAsync(MeterUnit model);
    Task<ApiResponse<MeterUnit>> UpdateMeterUnitAsync(int id, MeterUnit model);
    Task<ApiResponse> DeleteMeterUnitAsync(int id);

    // 住宿状态
    Task<PagedResult<ResidenceStatus>> GetResidenceStatusesAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<ResidenceStatus?> GetResidenceStatusByIdAsync(int id);
    Task<ApiResponse<ResidenceStatus>> CreateResidenceStatusAsync(ResidenceStatus model);
    Task<ApiResponse<ResidenceStatus>> UpdateResidenceStatusAsync(int id, ResidenceStatus model);
    Task<ApiResponse> DeleteResidenceStatusAsync(int id);

    // 在职状态
    Task<PagedResult<EmploymentStatus>> GetEmploymentStatusesAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<EmploymentStatus?> GetEmploymentStatusByIdAsync(int id);
    Task<ApiResponse<EmploymentStatus>> CreateEmploymentStatusAsync(EmploymentStatus model);
    Task<ApiResponse<EmploymentStatus>> UpdateEmploymentStatusAsync(int id, EmploymentStatus model);
    Task<ApiResponse> DeleteEmploymentStatusAsync(int id);

    // 员工班组
    Task<PagedResult<Team>> GetTeamsAsync(string? keyword, int page = 1, int pageSize = 10);
    Task<Team?> GetTeamByIdAsync(int id);
    Task<ApiResponse<Team>> CreateTeamAsync(Team model);
    Task<ApiResponse<Team>> UpdateTeamAsync(int id, Team model);
    Task<ApiResponse> DeleteTeamAsync(int id);
}

/// <summary>
/// 基础资料服务实现
/// </summary>
public class BasicsService : IBasicsService
{
    private readonly DormDbContext _db;

    public BasicsService(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 检查字典是否被业务表引用（v2.13.12 FK 防护）
    /// </summary>
    private async Task<string?> CheckReferencedAsync(int id)
    {
        // Department → SysEmployee.DepartmentId
        if (await _db.Employees.AnyAsync(e => e.DepartmentId == id))
            return "有员工关联此部门";
        // Building → Dorm.BuildingId
        if (await _db.Dorms.AnyAsync(d => d.BuildingId == id))
            return "有宿舍关联此楼栋";
        // Floor → Dorm.FloorId
        if (await _db.Dorms.AnyAsync(d => d.FloorId == id))
            return "有宿舍关联此楼层";
        // Address → Dorm.AddressId
        if (await _db.Dorms.AnyAsync(d => d.AddressId == id))
            return "有宿舍关联此地址";
        // EmployeeType → SysEmployee.EmployeeTypeId
        if (await _db.Employees.AnyAsync(e => e.EmployeeTypeId == id))
            return "有员工关联此类型";
        // AttendanceType → SysEmployee.AttendanceTypeId
        if (await _db.Employees.AnyAsync(e => e.AttendanceTypeId == id))
            return "有员工关联此班次";
        // ResidenceStatus → SysEmployee.ResidenceStatusId
        if (await _db.Employees.AnyAsync(e => e.ResidenceStatusId == id))
            return "有员工关联此住宿状态";
        // EmploymentStatus → SysEmployee.EmploymentStatusId
        if (await _db.Employees.AnyAsync(e => e.EmploymentStatusId == id))
            return "有员工关联此在职状态";
        return null;
    }

    #region 部门

    public async Task<PagedResult<Department>> GetDepartmentsAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.Departments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(d => d.Code.Contains(keyword) || d.Name.Contains(keyword) || (d.Remark != null && d.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(d => d.SortOrder).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<Department> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<Department?> GetDepartmentByIdAsync(int id) => await _db.Departments.FindAsync(id);

    public async Task<ApiResponse<Department>> CreateDepartmentAsync(Department model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
            return ApiResponse<Department>.Fail("CODE_REQUIRED", "部门编码不能为空");

        if (await _db.Departments.AnyAsync(d => d.Code == model.Code))
            return ApiResponse<Department>.Fail("CODE_EXISTS", "部门编码已存在");

        if (await _db.Departments.AnyAsync(d => d.Name == model.Name))
            return ApiResponse<Department>.Fail("NAME_EXISTS", "部门名称已存在");

        model.CreatedAt = DateTime.Now;
        _db.Departments.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<Department>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<Department>> UpdateDepartmentAsync(int id, Department model)
    {
        var entity = await _db.Departments.FindAsync(id);
        if (entity == null) return ApiResponse<Department>.Fail("NOT_FOUND", "记录不存在");

        if (string.IsNullOrWhiteSpace(model.Code))
            return ApiResponse<Department>.Fail("CODE_REQUIRED", "部门编码不能为空");

        if (await _db.Departments.AnyAsync(d => d.Code == model.Code && d.Id != id))
            return ApiResponse<Department>.Fail("CODE_EXISTS", "部门编码已存在");

        if (await _db.Departments.AnyAsync(d => d.Name == model.Name && d.Id != id))
            return ApiResponse<Department>.Fail("NAME_EXISTS", "部门名称已存在");

        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.SortOrder = model.SortOrder;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<Department>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteDepartmentAsync(int id)
    {
        var entity = await _db.Departments.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        var refMsg = await CheckReferencedAsync(id);
        if (refMsg != null) return ApiResponse.Fail("REFERENCED", $"该部门被业务引用，无法删除。{refMsg}");

        _db.Departments.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 楼栋

    public async Task<PagedResult<Building>> GetBuildingsAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.Buildings.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(b => b.Name.Contains(keyword) || (b.Remark != null && b.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(b => b.SortOrder).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<Building> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<Building?> GetBuildingByIdAsync(int id) => await _db.Buildings.FindAsync(id);

    public async Task<ApiResponse<Building>> CreateBuildingAsync(Building model)
    {
        if (await _db.Buildings.AnyAsync(b => b.Name == model.Name))
            return ApiResponse<Building>.Fail("NAME_EXISTS", "楼栋名称已存在");

        model.CreatedAt = DateTime.Now;
        _db.Buildings.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<Building>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<Building>> UpdateBuildingAsync(int id, Building model)
    {
        var entity = await _db.Buildings.FindAsync(id);
        if (entity == null) return ApiResponse<Building>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.Buildings.AnyAsync(b => b.Name == model.Name && b.Id != id))
            return ApiResponse<Building>.Fail("NAME_EXISTS", "楼栋名称已存在");

        entity.Name = model.Name;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.SortOrder = model.SortOrder;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<Building>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteBuildingAsync(int id)
    {
        var entity = await _db.Buildings.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        var refMsg = await CheckReferencedAsync(id);
        if (refMsg != null) return ApiResponse.Fail("REFERENCED", $"该楼栋被业务引用，无法删除。{refMsg}");

        _db.Buildings.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 楼层

    public async Task<PagedResult<Floor>> GetFloorsAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.Floors.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(f => f.FloorNo.ToString().Contains(keyword) || (f.Remark != null && f.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(f => f.FloorNo).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<Floor> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<Floor?> GetFloorByIdAsync(int id) => await _db.Floors.FindAsync(id);

    public async Task<ApiResponse<Floor>> CreateFloorAsync(Floor model)
    {
        if (await _db.Floors.AnyAsync(f => f.FloorNo == model.FloorNo))
            return ApiResponse<Floor>.Fail("FLOOR_EXISTS", "楼层号已存在");

        model.CreatedAt = DateTime.Now;
        _db.Floors.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<Floor>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<Floor>> UpdateFloorAsync(int id, Floor model)
    {
        var entity = await _db.Floors.FindAsync(id);
        if (entity == null) return ApiResponse<Floor>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.Floors.AnyAsync(f => f.FloorNo == model.FloorNo && f.Id != id))
            return ApiResponse<Floor>.Fail("FLOOR_EXISTS", "楼层号已存在");

        entity.FloorNo = model.FloorNo;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<Floor>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteFloorAsync(int id)
    {
        var entity = await _db.Floors.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        var refMsg = await CheckReferencedAsync(id);
        if (refMsg != null) return ApiResponse.Fail("REFERENCED", $"该楼层被业务引用，无法删除。{refMsg}");

        _db.Floors.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 地址

    public async Task<PagedResult<Address>> GetAddressesAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.Addresses.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(a => a.AddressText.Contains(keyword) || (a.Remark != null && a.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<Address> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<Address?> GetAddressByIdAsync(int id) => await _db.Addresses.FindAsync(id);

    public async Task<ApiResponse<Address>> CreateAddressAsync(Address model)
    {
        if (await _db.Addresses.AnyAsync(a => a.AddressText == model.AddressText))
            return ApiResponse<Address>.Fail("ADDRESS_EXISTS", "地址已存在");

        model.CreatedAt = DateTime.Now;
        _db.Addresses.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<Address>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<Address>> UpdateAddressAsync(int id, Address model)
    {
        var entity = await _db.Addresses.FindAsync(id);
        if (entity == null) return ApiResponse<Address>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.Addresses.AnyAsync(a => a.AddressText == model.AddressText && a.Id != id))
            return ApiResponse<Address>.Fail("ADDRESS_EXISTS", "地址已存在");

        entity.AddressText = model.AddressText;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<Address>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteAddressAsync(int id)
    {
        var entity = await _db.Addresses.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        var refMsg = await CheckReferencedAsync(id);
        if (refMsg != null) return ApiResponse.Fail("REFERENCED", $"该地址被业务引用，无法删除。{refMsg}");

        _db.Addresses.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 员工类型

    public async Task<PagedResult<EmployeeType>> GetEmployeeTypesAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.EmployeeTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(e => e.Code.Contains(keyword) || e.Name.Contains(keyword) || (e.Remark != null && e.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<EmployeeType> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<EmployeeType?> GetEmployeeTypeByIdAsync(int id) => await _db.EmployeeTypes.FindAsync(id);

    public async Task<ApiResponse<EmployeeType>> CreateEmployeeTypeAsync(EmployeeType model)
    {
        if (await _db.EmployeeTypes.AnyAsync(e => e.Code == model.Code))
            return ApiResponse<EmployeeType>.Fail("CODE_EXISTS", "类型编码已存在");

        model.CreatedAt = DateTime.Now;
        _db.EmployeeTypes.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<EmployeeType>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<EmployeeType>> UpdateEmployeeTypeAsync(int id, EmployeeType model)
    {
        var entity = await _db.EmployeeTypes.FindAsync(id);
        if (entity == null) return ApiResponse<EmployeeType>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.EmployeeTypes.AnyAsync(e => e.Code == model.Code && e.Id != id))
            return ApiResponse<EmployeeType>.Fail("CODE_EXISTS", "类型编码已存在");

        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<EmployeeType>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteEmployeeTypeAsync(int id)
    {
        var entity = await _db.EmployeeTypes.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        var refMsg = await CheckReferencedAsync(id);
        if (refMsg != null) return ApiResponse.Fail("REFERENCED", $"该类型被业务引用，无法删除。{refMsg}");

        _db.EmployeeTypes.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 考勤班次

    public async Task<PagedResult<AttendanceType>> GetAttendanceTypesAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.AttendanceTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(a => a.Code.Contains(keyword) || a.Name.Contains(keyword) || (a.Remark != null && a.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<AttendanceType> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<AttendanceType?> GetAttendanceTypeByIdAsync(int id) => await _db.AttendanceTypes.FindAsync(id);

    public async Task<ApiResponse<AttendanceType>> CreateAttendanceTypeAsync(AttendanceType model)
    {
        if (await _db.AttendanceTypes.AnyAsync(a => a.Code == model.Code))
            return ApiResponse<AttendanceType>.Fail("CODE_EXISTS", "类型编码已存在");

        model.CreatedAt = DateTime.Now;
        _db.AttendanceTypes.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<AttendanceType>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<AttendanceType>> UpdateAttendanceTypeAsync(int id, AttendanceType model)
    {
        var entity = await _db.AttendanceTypes.FindAsync(id);
        if (entity == null) return ApiResponse<AttendanceType>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.AttendanceTypes.AnyAsync(a => a.Code == model.Code && a.Id != id))
            return ApiResponse<AttendanceType>.Fail("CODE_EXISTS", "类型编码已存在");

        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.WorkHours = model.WorkHours;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<AttendanceType>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteAttendanceTypeAsync(int id)
    {
        var entity = await _db.AttendanceTypes.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        var refMsg = await CheckReferencedAsync(id);
        if (refMsg != null) return ApiResponse.Fail("REFERENCED", $"该班次被业务引用，无法删除。{refMsg}");

        _db.AttendanceTypes.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 计量单位

    public async Task<PagedResult<MeterUnit>> GetMeterUnitsAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.MeterUnits.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m => m.Code.Contains(keyword) || m.Name.Contains(keyword) || (m.Remark != null && m.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<MeterUnit> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<MeterUnit?> GetMeterUnitByIdAsync(int id) => await _db.MeterUnits.FindAsync(id);

    public async Task<ApiResponse<MeterUnit>> CreateMeterUnitAsync(MeterUnit model)
    {
        if (await _db.MeterUnits.AnyAsync(m => m.Code == model.Code))
            return ApiResponse<MeterUnit>.Fail("CODE_EXISTS", "单位编码已存在");

        model.CreatedAt = DateTime.Now;
        _db.MeterUnits.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<MeterUnit>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<MeterUnit>> UpdateMeterUnitAsync(int id, MeterUnit model)
    {
        var entity = await _db.MeterUnits.FindAsync(id);
        if (entity == null) return ApiResponse<MeterUnit>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.MeterUnits.AnyAsync(m => m.Code == model.Code && m.Id != id))
            return ApiResponse<MeterUnit>.Fail("CODE_EXISTS", "单位编码已存在");

        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.Unit = model.Unit;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<MeterUnit>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteMeterUnitAsync(int id)
    {
        var entity = await _db.MeterUnits.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        _db.MeterUnits.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 住宿状态

    public async Task<PagedResult<ResidenceStatus>> GetResidenceStatusesAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.ResidenceStatuses.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(r => r.Code.Contains(keyword) || r.Name.Contains(keyword) || (r.Remark != null && r.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<ResidenceStatus> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<ResidenceStatus?> GetResidenceStatusByIdAsync(int id) => await _db.ResidenceStatuses.FindAsync(id);

    public async Task<ApiResponse<ResidenceStatus>> CreateResidenceStatusAsync(ResidenceStatus model)
    {
        if (await _db.ResidenceStatuses.AnyAsync(r => r.Code == model.Code))
            return ApiResponse<ResidenceStatus>.Fail("CODE_EXISTS", "状态编码已存在");

        model.CreatedAt = DateTime.Now;
        _db.ResidenceStatuses.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<ResidenceStatus>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<ResidenceStatus>> UpdateResidenceStatusAsync(int id, ResidenceStatus model)
    {
        var entity = await _db.ResidenceStatuses.FindAsync(id);
        if (entity == null) return ApiResponse<ResidenceStatus>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.ResidenceStatuses.AnyAsync(r => r.Code == model.Code && r.Id != id))
            return ApiResponse<ResidenceStatus>.Fail("CODE_EXISTS", "状态编码已存在");

        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<ResidenceStatus>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteResidenceStatusAsync(int id)
    {
        var entity = await _db.ResidenceStatuses.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        _db.ResidenceStatuses.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 在职状态

    public async Task<PagedResult<EmploymentStatus>> GetEmploymentStatusesAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.EmploymentStatuses.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(e => e.Code.Contains(keyword) || e.Name.Contains(keyword) || (e.Remark != null && e.Remark.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query.OrderBy(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<EmploymentStatus> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<EmploymentStatus?> GetEmploymentStatusByIdAsync(int id) => await _db.EmploymentStatuses.FindAsync(id);

    public async Task<ApiResponse<EmploymentStatus>> CreateEmploymentStatusAsync(EmploymentStatus model)
    {
        if (await _db.EmploymentStatuses.AnyAsync(e => e.Code == model.Code))
            return ApiResponse<EmploymentStatus>.Fail("CODE_EXISTS", "状态编码已存在");

        model.CreatedAt = DateTime.Now;
        _db.EmploymentStatuses.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<EmploymentStatus>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<EmploymentStatus>> UpdateEmploymentStatusAsync(int id, EmploymentStatus model)
    {
        var entity = await _db.EmploymentStatuses.FindAsync(id);
        if (entity == null) return ApiResponse<EmploymentStatus>.Fail("NOT_FOUND", "记录不存在");

        if (await _db.EmploymentStatuses.AnyAsync(e => e.Code == model.Code && e.Id != id))
            return ApiResponse<EmploymentStatus>.Fail("CODE_EXISTS", "状态编码已存在");

        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.Remark = model.Remark;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse<EmploymentStatus>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteEmploymentStatusAsync(int id)
    {
        var entity = await _db.EmploymentStatuses.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        _db.EmploymentStatuses.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion

    #region 员工班组 (Team)

    public async Task<PagedResult<Team>> GetTeamsAsync(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _db.Teams.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(t => t.Code.Contains(keyword) || t.Name.Contains(keyword));

        var total = await query.CountAsync();
        var items = await query.OrderBy(t => t.SortOrder).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<Team> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    public async Task<Team?> GetTeamByIdAsync(int id) => await _db.Teams.FindAsync(id);

    public async Task<ApiResponse<Team>> CreateTeamAsync(Team model)
    {
        model.CreatedAt = DateTime.Now;
        _db.Teams.Add(model);
        await _db.SaveChangesAsync();
        return ApiResponse<Team>.Ok(model, "创建成功");
    }

    public async Task<ApiResponse<Team>> UpdateTeamAsync(int id, Team model)
    {
        var entity = await _db.Teams.FindAsync(id);
        if (entity == null) return ApiResponse<Team>.Fail("NOT_FOUND", "记录不存在");

        entity.Name = model.Name;
        entity.Code = model.Code;
        entity.SortOrder = model.SortOrder;
        entity.IsActive = model.IsActive;
        await _db.SaveChangesAsync();
        return ApiResponse<Team>.Ok(entity, "更新成功");
    }

    public async Task<ApiResponse> DeleteTeamAsync(int id)
    {
        var entity = await _db.Teams.FindAsync(id);
        if (entity == null) return ApiResponse.Fail("NOT_FOUND", "记录不存在");

        var refMsg = await CheckReferencedAsync(id);
        if (refMsg != null) return ApiResponse.Fail("REFERENCED", $"该班组被业务引用，无法删除。{refMsg}");

        _db.Teams.Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }

    #endregion
}
