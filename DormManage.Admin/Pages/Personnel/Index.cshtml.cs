using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;
using DormManage.Admin.Pages.Shared;

namespace DormManage.Admin.Pages.Personnel;

/// <summary>
/// 人员清单页面模型（v2.11.7.CORRECT → v2.11.24 升级）
/// 员工类型数据来源于基础资料-员工类型表（EmployeeType），仅 5 种类型
///
/// v2.11.24 修订（2026-07-13）：
/// - 页面后台数据天然无脏 ID（生产 SQL Server 数据库由 DataCleanupHostedService 启动清洗保证，v2.13.109 起 SQLite 已移除）
/// - 仅 5 种员工类型（ID 1-5：合同工/临时工/外包/实习生/驻场）
/// - 若发现 employeeTypeId > 5 的历史脏数据，说明数据清洗未执行，应手动调用
///   <c>DormManage.Shared.Services.DictionaryFallbackService.BatchNormalizeEmployeesAsync</c>
/// - 关联规范文档：<c>00-方案文档/43-无效FK归一通用规范-v2.11.24.md</c>
/// </summary>
public class IndexModel : PaginatedPageModel
{
    private readonly DormDbContext _db;
    private readonly IPersonnelService _svc;

    public IndexModel(DormDbContext db, IPersonnelService svc)
    {
        _db = db;
        _svc = svc;
    }

    /// <summary>标记离职（项1）</summary>
    public async Task<IActionResult> OnPostMarkLeftAsync(int id)
    {
        var (ok, msg) = await _svc.MarkLeftAsync(id, DateOnly.FromDateTime(DateTime.Today));
        TempData[ok ? "Success" : "ErrorMessage"] = msg;
        return RedirectToPage("/Personnel/Index");
    }

    /// <summary>删除员工（项1）</summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp == null)
        {
            TempData["ErrorMessage"] = "员工不存在";
            return RedirectToPage("/Personnel/Index");
        }
        _db.Employees.Remove(emp);
        await _db.SaveChangesAsync();
        TempData["Success"] = "删除成功";
        return RedirectToPage("/Personnel/Index");
    }

    /// <summary>
    /// 员工列表（含 EmployeeType 导航属性）
    /// </summary>
    public PagedResult<PersonnelDto>? Result { get; set; }

    /// <summary>总数（供 PageHeader 组件使用）</summary>
    public int Total => Result?.TotalCount ?? 0;

    /// <summary>
    /// 员工类型列表（用于筛选下拉）
    /// </summary>
    public List<EmployeeTypeDropdownItem> EmployeeTypes { get; set; } = new();

    /// <summary>
    /// 部门列表（用于筛选下拉）
    /// </summary>
    public List<DepartmentDropdownItem> Departments { get; set; } = new();

    /// <summary>员工班组列表（用于筛选下拉）</summary>
    public List<TeamDropdownItem> Teams { get; set; } = new();

    /// <summary>在职状态列表（用于筛选下拉）</summary>
    public List<EmploymentStatusDropdownItem> EmploymentStatuses { get; set; } = new();

    /// <summary>
    /// 考勤班次列表（用于筛选下拉）
    /// </summary>
    public List<AttendanceTypeDropdownItem> AttendanceTypes { get; set; } = new();

    /// <summary>住宿房号候选（datalist 自动完成用）</summary>
    public List<string> DormCodes { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? DepartmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EmployeeTypeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? TeamId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EmploymentStatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DormCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? AttendanceTypeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    // PageIndex / PageSize 继承自 v2.13.104 PaginatedPageModel 基类（含白名单校验）

    public async Task OnGetAsync()
    {
        // v2.13.104：强制 PageSize 白名单校验（确保主菜单导航不带参数时默认 20）
        EnsureValidPagination();
        // 加载筛选下拉数据
        EmployeeTypes = await _db.EmployeeTypes
            .Where(e => e.IsActive)
            .OrderBy(e => e.Id)
            .Select(e => new EmployeeTypeDropdownItem { Id = e.Id, Name = e.Name, Code = e.Code })
            .ToListAsync();

        Departments = await _db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .Select(d => new DepartmentDropdownItem { Id = d.Id, Name = d.Name })
            .ToListAsync();

        Teams = await _db.Teams
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new TeamDropdownItem { Id = t.Id, Name = t.Name })
            .ToListAsync();

        EmploymentStatuses = await _db.EmploymentStatuses
            .Where(es => es.IsActive)
            .OrderBy(es => es.Id)
            .Select(es => new EmploymentStatusDropdownItem { Id = es.Id, Name = es.Name })
            .ToListAsync();

        AttendanceTypes = await _db.AttendanceTypes
            .Where(a => a.IsActive)
            .OrderBy(a => a.Id)
            .Select(a => new AttendanceTypeDropdownItem { Id = a.Id, Name = a.Name, Code = a.Code })
            .ToListAsync();

        // 住宿房号候选（Dorms + Personnel.DormCode 出现过的去重）
        DormCodes = await _db.Dorms
            .Select(d => d.DormCode)
            .Union(_db.Employees.Where(e => e.DormCode != null).Select(e => e.DormCode!))
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        // 查询员工列表（v2.11.7.CORRECT 强制 Include EmployeeType 导航属性）
        var query = _db.Employees
            .Include(e => e.EmployeeType)      // 员工类型 FK 关联
            .Include(e => e.AttendanceType)    // 考勤班次 FK 关联
            .Include(e => e.Team)              // v2.13.78 BUG 修复：班组 FK 关联（之前缺 Include 导致班组列显示"-"）
            .Include(e => e.EmploymentStatus)  // v2.13.208 BUG 修复：在职状态 FK 关联（DTO 投影用到 .EmploymentStatus.Name 但未 Include → EF Core 抛 InvalidOperationException → 页面 INTERNAL_ERROR）
            .AsQueryable();

        if (DepartmentId.HasValue)
            query = query.Where(e => e.DepartmentId == DepartmentId.Value);

        if (EmployeeTypeId.HasValue)
            query = query.Where(e => e.EmployeeTypeId == EmployeeTypeId.Value);

        if (TeamId.HasValue)
            query = query.Where(e => e.TeamId == TeamId.Value);

        if (EmploymentStatusId.HasValue)
            query = query.Where(e => e.EmploymentStatusId == EmploymentStatusId.Value);

        if (!string.IsNullOrWhiteSpace(DormCode))
            query = query.Where(e => e.DormCode != null && e.DormCode.Contains(DormCode));

        if (AttendanceTypeId.HasValue)
            query = query.Where(e => e.AttendanceTypeId == AttendanceTypeId.Value);

        if (Status.HasValue)
            query = query.Where(e => e.EmploymentStatusId == Status.Value);

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var kw = Keyword.ToLower();
            query = query.Where(e =>
                e.EmployeeCode.ToLower().Contains(kw) ||
                e.RealName.ToLower().Contains(kw) ||
                (e.Phone != null && e.Phone.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(e => e.EmployeeCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .Select(e => new PersonnelDto
            {
                Id = e.Id,
                EmployeeCode = e.EmployeeCode,
                RealName = e.RealName,
                Phone = e.Phone ?? "-",
                // v2.13.208 BUG 修复：DB 列名是 IdNumber，投影用 .IdNumber（不再是 .IdCard 否则 EF Core 抛 Invalid column name）
                IdNumber = e.IdNumber ?? "",
                // v2.13.83 性别字段（SysEmployee.Gender：1=男 2=女 0=未知）
                Gender = e.Gender,
                DepartmentId = e.DepartmentId,
                Department = e.Department ?? "-",
                // v2.11.7 员工类型 FK 关联：通过 EmployeeTypeId 关联 EmployeeType 主键
                EmployeeTypeId = e.EmployeeTypeId,
                EmployeeTypeName = e.EmployeeType != null ? e.EmployeeType.Name : "-",
                EmployeeTypeCode = e.EmployeeType != null ? e.EmployeeType.Code : "-",
                AttendanceTypeId = e.AttendanceTypeId ?? 0,
                AttendanceTypeName = e.AttendanceType != null ? e.AttendanceType.Name : "-",
                AttendanceTypeCode = e.AttendanceType != null ? e.AttendanceType.Code : "-",
                // v2.11.18：在职状态 FK 关联引用基础资料-在职状态表 EmploymentStatus
                Status = e.EmploymentStatusId,
                StatusName = e.EmploymentStatus != null ? e.EmploymentStatus.Name : GetStatusName(e.EmploymentStatusId),
                HireDate = e.HireDate != null ? e.HireDate.Value.ToString("yyyy-MM-dd") : "-",
                LeaveDate = e.LeaveDate != null ? e.LeaveDate.Value.ToString("yyyy-MM-dd") : "-",
                DormCode = e.DormCode ?? "-",
                BedNo = e.BedNo ?? 0,
                TeamId = e.TeamId,
                // v2.13.78 BUG 修复：班组名称走 FK 关联（之前 e.Team 字符串字段被 DbContext.Ignore，永远为 null → 显示"-"）
                TeamName = e.Team != null ? e.Team.Name : "-",
                IsActive = e.IsActive
            })
            .ToListAsync();

        Result = new PagedResult<PersonnelDto>
        {
            Items = items,
            TotalCount = total,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }

    /// <summary>
    /// 获取在职状态中文名称
    /// </summary>
    private static string GetStatusName(int status)
    {
        switch (status)
        {
            case 1: return "在职";
            case 2: return "待入职";
            case 3: return "已离职";
            default: return "未知";
        }
    }
}

/// <summary>
/// 人员清单数据传输对象
/// </summary>
public class PersonnelDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string RealName { get; set; } = "";
    public string Phone { get; set; } = "";
    /// <summary>v2.13.208 BUG 修复：字段名 IdCard → IdNumber（与 EF 模型 + DB 列名一致）；v2.13.180 已添加 18 位中国大陆居民身份证号列；默认隐私字段，受字段权限控制</summary>
    public string IdNumber { get; set; } = "";
    /// <summary>v2.13.83 新增：性别（1=男 2=女 0=未知）</summary>
    public int Gender { get; set; }
    public string GenderName => Gender == 1 ? "男" : Gender == 2 ? "女" : "未知";
    public int DepartmentId { get; set; }
    public string Department { get; set; } = "";

    // v2.11.7.CORRECT 员工类型 FK 关联字段（仅 5 种：合同工/临时工/外包/实习生/驻场）
    public int EmployeeTypeId { get; set; }
    public string EmployeeTypeName { get; set; } = "";
    public string EmployeeTypeCode { get; set; } = "";

    // v2.11.7 考勤班次 FK 关联字段
    public int AttendanceTypeId { get; set; }
    public string AttendanceTypeName { get; set; } = "";
    public string AttendanceTypeCode { get; set; } = "";

    public int Status { get; set; }
    public string StatusName { get; set; } = "";
    public string HireDate { get; set; } = "";
    public string LeaveDate { get; set; } = "";
    public string DormCode { get; set; } = "";
    public int BedNo { get; set; }
    public int? TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public bool IsActive { get; set; }
}

/// <summary>
/// 员工类型下拉项
/// </summary>
public class EmployeeTypeDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

/// <summary>
/// 部门下拉项
/// </summary>
public class DepartmentDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// 考勤班次下拉项
/// </summary>
public class AttendanceTypeDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

/// <summary>
/// 班组下拉项
/// </summary>
public class TeamDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// 在职状态下拉项
/// </summary>
public class EmploymentStatusDropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
