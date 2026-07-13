using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Data;

namespace DormManage.Shared.Services;

/// <summary>
/// 通用字典兜底归一服务（v2.11.24）
/// </summary>
/// <remarks>
/// 规范文档：<c>00-方案文档/43-无效FK归一通用规范-v2.11.24.md</c>
///
/// 用途：当业务表 FK 字段的值不在基础资料字典有效范围内时，
///       统一更新为该字典列表最后一个选项的 ID 主键（即按 Id DESC 取首条）。
///
/// 应用范围：
///   <list type="bullet">
///     <item><description><c>SysEmployee.EmployeeTypeId</c>（员工类型，末项 = 5 / ONSITE）</description></item>
///     <item><description><c>SysEmployee.AttendanceTypeId</c>（考勤班次）</description></item>
///     <item><description><c>SysEmployee.DepartmentId</c>（部门）</description></item>
///     <item><description><c>SysEmployee.EmploymentStatusId</c>（在职状态）</description></item>
///     <item><description><c>SysEmployee.ResidenceStatusId</c>（住宿状态）</description></item>
///   </list>
///
/// 取代规范：v2.11.23 §2.2 规则 2b（部门语义映射，DEPRECATED）。
///
/// 设计意图（三层防护）：
/// <list type="number">
///   <item><description>写入层：创建/更新/导入路径调用本服务的 5 个方法之一</description></item>
///   <item><description>读取层：前端 mock-data.js 已实现 <c>getLastDictId()</c> / <c>normalizeFK()</c></description></item>
///   <item><description>修复层：<c>DataCleanupHostedService</c> 启动时一次性存量清洗</description></item>
/// </list>
/// </remarks>
public static class DictionaryFallbackService
{
    /// <summary>
    /// 员工类型 ID 归一。
    /// </summary>
    /// <param name="currentId">当前值；<c>null</c> 或不在字典范围内时返回末项 ID。</param>
    /// <param name="db">EF Core 数据库上下文。</param>
    /// <returns>有效 ID。</returns>
    public static async Task<int> NormalizeEmployeeTypeIdAsync(int? currentId, DormDbContext db)
    {
        var validIds = await db.EmployeeTypes.Select(e => e.Id).ToListAsync();
        if (currentId.HasValue && validIds.Contains(currentId.Value)) return currentId.Value;
        return await db.EmployeeTypes.OrderByDescending(e => e.Id).Select(e => e.Id).FirstAsync();
    }

    /// <summary>
    /// 考勤班次 ID 归一。
    /// </summary>
    /// <param name="currentId">当前值；<c>null</c> 或不在字典范围内时返回末项 ID。</param>
    /// <param name="db">EF Core 数据库上下文。</param>
    /// <returns>有效 ID。</returns>
    public static async Task<int?> NormalizeAttendanceTypeIdAsync(int? currentId, DormDbContext db)
    {
        if (!currentId.HasValue) return currentId;
        var validIds = await db.AttendanceTypes.Select(a => a.Id).ToListAsync();
        if (validIds.Contains(currentId.Value)) return currentId.Value;
        return await db.AttendanceTypes.OrderByDescending(a => a.Id).Select(a => a.Id).FirstOrDefaultAsync();
    }

    /// <summary>
    /// 部门 ID 归一。
    /// </summary>
    /// <param name="currentId">当前值；<c>null</c> 或不在字典范围内时返回末项 ID。</param>
    /// <param name="db">EF Core 数据库上下文。</param>
    /// <returns>有效 ID。</returns>
    public static async Task<int> NormalizeDepartmentIdAsync(int? currentId, DormDbContext db)
    {
        var validIds = await db.Departments.Select(d => d.Id).ToListAsync();
        if (currentId.HasValue && validIds.Contains(currentId.Value)) return currentId.Value;
        return await db.Departments.OrderByDescending(d => d.Id).Select(d => d.Id).FirstAsync();
    }

    /// <summary>
    /// 在职状态 ID 归一。
    /// </summary>
    /// <param name="currentId">当前值；<c>null</c> 或不在字典范围内时返回末项 ID。</param>
    /// <param name="db">EF Core 数据库上下文。</param>
    /// <returns>有效 ID。</returns>
    public static async Task<int> NormalizeEmploymentStatusIdAsync(int? currentId, DormDbContext db)
    {
        var validIds = await db.EmploymentStatuses.Select(s => s.Id).ToListAsync();
        if (currentId.HasValue && validIds.Contains(currentId.Value)) return currentId.Value;
        return await db.EmploymentStatuses.OrderByDescending(s => s.Id).Select(s => s.Id).FirstAsync();
    }

    /// <summary>
    /// 住宿状态 ID 归一。
    /// </summary>
    /// <param name="currentId">当前值；<c>null</c> 或不在字典范围内时返回末项 ID。</param>
    /// <param name="db">EF Core 数据库上下文。</param>
    /// <returns>有效 ID。</returns>
    public static async Task<int> NormalizeResidenceStatusIdAsync(int? currentId, DormDbContext db)
    {
        var validIds = await db.ResidenceStatuses.Select(r => r.Id).ToListAsync();
        if (currentId.HasValue && validIds.Contains(currentId.Value)) return currentId.Value;
        return await db.ResidenceStatuses.OrderByDescending(r => r.Id).Select(r => r.Id).FirstAsync();
    }

    /// <summary>
    /// 批量归一 SysEmployee 的全部 5 个 FK 字段。
    /// </summary>
    /// <remarks>
    /// 高频路径优化：先同步把 4 个字典表 ToList 内存化，再遍历员工记录；避免 N+1 查询。
    /// 主用于 <c>DataCleanupHostedService</c> 启动修复，调用方需自行 SaveChangesAsync。
    /// </remarks>
    /// <returns>各项修复条数。</returns>
    public static async Task<Dictionary<string, int>> BatchNormalizeEmployeesAsync(DormDbContext db)
    {
        // 1. 一次性加载字典到内存
        var employeeTypeIds = await db.EmployeeTypes.Select(e => e.Id).ToListAsync();
        var attendanceTypeIds = await db.AttendanceTypes.Select(a => a.Id).ToListAsync();
        var departmentIds = await db.Departments.Select(d => d.Id).ToListAsync();
        var employmentStatusIds = await db.EmploymentStatuses.Select(s => s.Id).ToListAsync();
        var residenceStatusIds = await db.ResidenceStatuses.Select(r => r.Id).ToListAsync();

        var lastEmployeeTypeId = employeeTypeIds.Count > 0 ? employeeTypeIds.Max() : 0;
        var lastAttendanceTypeId = attendanceTypeIds.Count > 0 ? attendanceTypeIds.Max() : 0;
        var lastDepartmentId = departmentIds.Count > 0 ? departmentIds.Max() : 0;
        var lastEmploymentStatusId = employmentStatusIds.Count > 0 ? employmentStatusIds.Max() : 0;
        var lastResidenceStatusId = residenceStatusIds.Count > 0 ? residenceStatusIds.Max() : 0;

        // 2. 加载员工（仅必要字段）
        var employees = await db.Employees.ToListAsync();

        int employeeTypeFixed = 0;
        int attendanceTypeFixed = 0;
        int departmentFixed = 0;
        int employmentStatusFixed = 0;
        int residenceStatusFixed = 0;

        // 3. 内存归一
        foreach (var emp in employees)
        {
            if (!employeeTypeIds.Contains(emp.EmployeeTypeId))
            {
                emp.EmployeeTypeId = lastEmployeeTypeId;
                employeeTypeFixed++;
            }

            if (emp.AttendanceTypeId.HasValue && !attendanceTypeIds.Contains(emp.AttendanceTypeId.Value))
            {
                emp.AttendanceTypeId = lastAttendanceTypeId;
                attendanceTypeFixed++;
            }
            else if (!emp.AttendanceTypeId.HasValue && attendanceTypeIds.Count > 0)
            {
                // 仅在字段完全缺失时兜底，保留原有 null 语义
                // 注：考勤班次是可空字段，本期不强制归一
                // emp.AttendanceTypeId = lastAttendanceTypeId;
                // attendanceTypeFixed++;
            }

            if (!departmentIds.Contains(emp.DepartmentId))
            {
                emp.DepartmentId = lastDepartmentId;
                departmentFixed++;
            }

            if (!employmentStatusIds.Contains(emp.EmploymentStatusId))
            {
                emp.EmploymentStatusId = lastEmploymentStatusId;
                employmentStatusFixed++;
            }

            if (!residenceStatusIds.Contains(emp.ResidenceStatusId))
            {
                emp.ResidenceStatusId = lastResidenceStatusId;
                residenceStatusFixed++;
            }
        }

        await db.SaveChangesAsync();

        return new Dictionary<string, int>
        {
            ["EmployeeType"] = employeeTypeFixed,
            ["AttendanceType"] = attendanceTypeFixed,
            ["Department"] = departmentFixed,
            ["EmploymentStatus"] = employmentStatusFixed,
            ["ResidenceStatus"] = residenceStatusFixed
        };
    }
}
