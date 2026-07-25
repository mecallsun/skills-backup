using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Admin.Pages.Shared;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

namespace DormManage.Admin.Pages.Basics;

/// <summary>
/// v2.13.158 基础资料页 PageModel（服务端渲染 + 标准分页）
///
/// 架构升级（v2.13.158）：
/// - 全量切到 Razor Pages 服务端渲染（替代早期 AJAX 内联脚本）
/// - 服务端 12 个二级菜单全部用同一组分页参数 + _PaginationPartial
/// - 与 Personnel/Index 共享 PaginationModel + PaginatedPageModel 规范
/// - URL 携带 ?tab=dept&page=2&pageSize=20&kw=xxx 可分享/书签
/// - 配合 PermissionCode 控制每 tab 的增/改/删按钮显隐
/// </summary>
public class IndexModel : PaginatedPageModel
{
    private readonly IBasicsService _svc;

    public IndexModel(IBasicsService svc) { _svc = svc; }

    // ── Tab & 筛选状态 ───────────────────────────────────────────────
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    public string ActiveTab => Tab ?? "dept";

    /// <summary>v2.13.158：每 tab 独立的关键词筛选（URL 持久化）</summary>
    [BindProperty(SupportsGet = true, Name = "kw")] public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true, Name = "type")] public byte? EquipmentType { get; set; }

    /// <summary>设备记录时间区间筛选</summary>
    [BindProperty(SupportsGet = true, Name = "start")] public DateTime? StartTime { get; set; }
    [BindProperty(SupportsGet = true, Name = "end")] public DateTime? EndTime { get; set; }

    // ── 12 个 tab 的数据 + 总数 ──────────────────────────────────────
    public List<Department> Departments { get; set; } = new();
    public int DepartmentsCount { get; set; }

    public List<Building> Buildings { get; set; } = new();
    public int BuildingsCount { get; set; }

    public List<Floor> Floors { get; set; } = new();
    public int FloorsCount { get; set; }

    public List<Address> Addresses { get; set; } = new();
    public int AddressesCount { get; set; }

    public List<EmployeeType> EmployeeTypes { get; set; } = new();
    public int EmployeeTypesCount { get; set; }

    public List<AttendanceType> AttendanceTypes { get; set; } = new();
    public int AttendanceTypesCount { get; set; }

    public List<Team> Teams { get; set; } = new();
    public int TeamsCount { get; set; }

    public List<MeterUnit> MeterUnits { get; set; } = new();
    public int MeterUnitsCount { get; set; }

    public List<ResidenceStatus> ResidenceStatuses { get; set; } = new();
    public int ResidenceStatusesCount { get; set; }

    public List<EmploymentStatus> EmploymentStatuses { get; set; } = new();
    public int EmploymentStatusesCount { get; set; }

    public List<DormMeterDto> DeviceMeters { get; set; } = new();
    public int DeviceMetersCount { get; set; }

    public List<EquipmentReadingDto> EquipmentReadings { get; set; } = new();
    public int EquipmentReadingsCount { get; set; }

    /// <summary>v2.13.120 设备档案，房号下拉</summary>
    public List<DormOptionDto> DormOptions { get; set; } = new();

    /// <summary>v2.13.158：当前 tab 的总记录数（视图引用以渲染分页器）</summary>
    public int CurrentTabTotalCount => ActiveTab switch
    {
        "dept" => DepartmentsCount,
        "building" => BuildingsCount,
        "floor" => FloorsCount,
        "address" => AddressesCount,
        "emptype" => EmployeeTypesCount,
        "attendance" => AttendanceTypesCount,
        "team" => TeamsCount,
        "unit" => MeterUnitsCount,
        "residence" => ResidenceStatusesCount,
        "employment" => EmploymentStatusesCount,
        "device" => DeviceMetersCount,
        "equipmentreading" => EquipmentReadingsCount,
        _ => 0
    };

    public async Task OnGetAsync()
    {
        // 用户需求：列表默认显示 10 条（PaginatedPageModel.DefaultPageSize = 10）
        // URL 不带 pageSize → 默认 10；带 pageSize → 校验白名单 10/20/50/100
        EnsureValidPagination();

        // 加载所有 tab 的真实总数（page=1, pageSize=最大）
        var deptFull = await _svc.GetDepartmentsAsync(null, 1, int.MaxValue);
        DepartmentsCount = deptFull.TotalCount;
        var bldFull = await _svc.GetBuildingsAsync(null, 1, int.MaxValue);
        BuildingsCount = bldFull.TotalCount;
        var flrFull = await _svc.GetFloorsAsync(null, 1, int.MaxValue);
        FloorsCount = flrFull.TotalCount;
        var adrFull = await _svc.GetAddressesAsync(null, 1, int.MaxValue);
        AddressesCount = adrFull.TotalCount;
        var empTFull = await _svc.GetEmployeeTypesAsync(null, 1, int.MaxValue);
        EmployeeTypesCount = empTFull.TotalCount;
        var attFull = await _svc.GetAttendanceTypesAsync(null, 1, int.MaxValue);
        AttendanceTypesCount = attFull.TotalCount;
        var teamFull = await _svc.GetTeamsAsync(null, 1, int.MaxValue);
        TeamsCount = teamFull.TotalCount;
        var unitFull = await _svc.GetMeterUnitsAsync(null, 1, int.MaxValue);
        MeterUnitsCount = unitFull.TotalCount;
        var resFull = await _svc.GetResidenceStatusesAsync(null, 1, int.MaxValue);
        ResidenceStatusesCount = resFull.TotalCount;
        var empFull = await _svc.GetEmploymentStatusesAsync(null, 1, int.MaxValue);
        EmploymentStatusesCount = empFull.TotalCount;
        var dmFull = await _svc.GetDeviceMetersAsync(null, 1, int.MaxValue);
        DeviceMetersCount = dmFull.TotalCount;
        var erFull = await _svc.GetEquipmentReadingsAsync(new EquipmentReadingQuery
        {
            EquipmentId = null, EquipmentType = null, StartTime = null, EndTime = null,
            PageIndex = 1, PageSize = int.MaxValue
        });
        EquipmentReadingsCount = erFull.TotalCount;

        // 当前 tab 的分页数据
        switch (ActiveTab)
        {
            case "dept":
                var deptPage = await _svc.GetDepartmentsAsync(Keyword, PageIndex, PageSize);
                Departments = deptPage.Items;
                break;
            case "building":
                Buildings = (await _svc.GetBuildingsAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "floor":
                Floors = (await _svc.GetFloorsAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "address":
                Addresses = (await _svc.GetAddressesAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "emptype":
                EmployeeTypes = (await _svc.GetEmployeeTypesAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "attendance":
                AttendanceTypes = (await _svc.GetAttendanceTypesAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "team":
                Teams = (await _svc.GetTeamsAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "unit":
                MeterUnits = (await _svc.GetMeterUnitsAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "residence":
                ResidenceStatuses = (await _svc.GetResidenceStatusesAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "employment":
                EmploymentStatuses = (await _svc.GetEmploymentStatusesAsync(Keyword, PageIndex, PageSize)).Items;
                break;
            case "device":
                DeviceMeters = (await _svc.GetDeviceMetersAsync(Keyword, PageIndex, PageSize)).Items;
                DormOptions = await _svc.GetDormsForDeviceAsync();
                break;
            case "equipmentreading":
                var erQuery = new EquipmentReadingQuery
                {
                    EquipmentId = Keyword,
                    EquipmentType = EquipmentType,
                    StartTime = StartTime,
                    EndTime = EndTime,
                    PageIndex = PageIndex,
                    PageSize = PageSize
                };
                EquipmentReadings = (await _svc.GetEquipmentReadingsAsync(erQuery)).Items;
                break;
        }
    }
}
