using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 宿舍档案页面模型（v2.13.10 与原型 dorms/list.html 对齐）
/// 筛选条件：楼栋/楼层/状态/关键词（房号/地址）
/// </summary>
public class IndexModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBasicsService _basicsService;

    public IndexModel(DormDbContext db, IBasicsService basicsService)
    {
        _db = db;
        _basicsService = basicsService;
    }

    /// <summary>宿舍列表</summary>
    public PagedResult<DormDto>? Result { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    /// <summary>楼栋ID</summary>
    [BindProperty(SupportsGet = true)]
    public int? BuildingId { get; set; }

    /// <summary>楼层号（v2.13.10 改为直接 FloorNo int 输入框）</summary>
    [BindProperty(SupportsGet = true)]
    public int? FloorNo { get; set; }

    /// <summary>状态（启用/停用）</summary>
    [BindProperty(SupportsGet = true)]
    public bool? IsActive { get; set; }

    /// <summary>关键词（房号/地址模糊匹配）</summary>
    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    /// <summary>楼栋列表</summary>
    public List<Building> Buildings { get; set; } = new();

    public async Task OnGetAsync()
    {
        // 加载基础资料 - 仅楼栋（v2.13.10 简化为 4 项筛选）
        var buildings = await _basicsService.GetBuildingsAsync(null, 1, 100);
        Buildings = buildings.Items.ToList();

        // 查询宿舍列表
        var query = _db.Dorms.AsQueryable();

        if (BuildingId.HasValue && BuildingId.Value > 0)
            query = query.Where(d => d.BuildingId == BuildingId.Value);

        // v2.13.10：楼层号筛选（简化：与 Dorm.FloorId 关联，FloorId 直接当楼层号使用）
        if (FloorNo.HasValue && FloorNo.Value > 0)
            query = query.Where(d => d.FloorId == FloorNo.Value);

        if (IsActive.HasValue)
            query = query.Where(d => d.IsActive == IsActive.Value);

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var kw = Keyword.Trim();
            query = query.Where(d => d.DormCode.Contains(kw) || (d.AddressText != null && d.AddressText.Contains(kw)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.DormCode)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .Select(d => new DormDto
            {
                Id = d.Id,
                DormCode = d.DormCode,
                BuildingName = d.BuildingName ?? "",
                FloorNo = d.FloorId,
                AddressText = d.AddressText ?? "",
                RoomCount = d.RoomCount,
                Capacity = d.Capacity,
                Gender = d.Gender,
                CurrentCount = _db.DormBookings.Count(b => b.DormCode == d.DormCode && b.Status == 2),
                IsActive = d.IsActive,
                HasBookingHistory = _db.DormBookings.Any(b => b.DormCode == d.DormCode)
            })
            .ToListAsync();

        // v2.12.41 计算 CanDelete：仅当当前无在宿人员 且 无办理登记历史时才允许删除
        foreach (var item in items)
        {
            item.CanDelete = (item.CurrentCount == 0 && !item.HasBookingHistory);
        }

        Result = new PagedResult<DormDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }

    /// <summary>删除宿舍（v2.12.41 删除约束）</summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
        {
            TempData["ErrorMessage"] = "宿舍不存在";
            return RedirectToPage("/Dorms/Index");
        }
        // 校验：在宿人数
        var current = await _db.DormBookings.CountAsync(b => b.DormCode == dorm.DormCode && b.Status == 2);
        if (current > 0)
        {
            TempData["ErrorMessage"] = $"该宿舍当前有 {current} 人在宿，禁止删除";
            return RedirectToPage("/Dorms/Index");
        }
        // 校验：办理登记历史
        var hasHistory = await _db.DormBookings.AnyAsync(b => b.DormCode == dorm.DormCode);
        if (hasHistory)
        {
            TempData["ErrorMessage"] = $"该宿舍 \"{dorm.DormCode}\" 有历史办理登记记录，禁止删除";
            return RedirectToPage("/Dorms/Index");
        }
        _db.Dorms.Remove(dorm);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"宿舍 {dorm.DormCode} 已删除";
        return RedirectToPage("/Dorms/Index");
    }
}

/// <summary>宿舍数据传输对象（v2.13.10 字段对齐原型：房号/楼栋/楼层/地址/房间数/容量/在住人数/使用率/状态）</summary>
public class DormDto
{
    public int Id { get; set; }
    public string DormCode { get; set; } = "";
    public string BuildingName { get; set; } = "";
    public int FloorNo { get; set; }
    public string AddressText { get; set; } = "";
    public int RoomCount { get; set; }
    public int Capacity { get; set; }
    public int Gender { get; set; }
    public int CurrentCount { get; set; }
    public bool IsActive { get; set; }
    /// <summary>是否存在办理登记历史（v2.12.41）</summary>
    public bool HasBookingHistory { get; set; }
    /// <summary>是否可删除（v2.12.41）</summary>
    public bool CanDelete { get; set; }
}