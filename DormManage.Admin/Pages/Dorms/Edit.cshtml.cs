using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 宿舍编辑页面模型
/// </summary>
public class EditModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBasicsService _basicsService;

    public EditModel(DormDbContext db, IBasicsService basicsService)
    {
        _db = db;
        _basicsService = basicsService;
    }

    [BindProperty]
    public DormEditDto Dorm { get; set; } = new();

    /// <summary>
    /// 楼栋列表
    /// </summary>
    public List<Building> Buildings { get; set; } = new();

    /// <summary>
    /// 楼层列表
    /// </summary>
    public List<Floor> Floors { get; set; } = new();

    /// <summary>
    /// 地址列表
    /// </summary>
    public List<Address> Addresses { get; set; } = new();

    /// <summary>
    /// v2.13.82 业务约束：当前在宿人数（动态计算：Status=Staying 的 DormBookings 数）
    /// </summary>
    public int CurrentCount { get; set; }

    /// <summary>
    /// v2.13.82 业务约束：CurrentCount > 0 时锁定 IsActive 复选框
    /// </summary>
    public bool IsActiveLocked => CurrentCount > 0;

    /// <summary>
    /// v2.13.82 业务约束：原始 IsActive 值（用于 OnPost 校验失败的回显）
    /// </summary>
    public bool IsActiveOriginal { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
        {
            return NotFound();
        }

        // v2.13.82 业务约束：当前在宿人数 > 0 时锁定 IsActive
        CurrentCount = await _db.DormBookings
            .CountAsync(b => b.DormCode == dorm.DormCode && b.Status == BookingStatus.Staying);

        Dorm = new DormEditDto
        {
            Id = dorm.Id,
            DormCode = dorm.DormCode,
            BuildingId = dorm.BuildingId,
            FloorId = dorm.FloorId,
            AddressId = dorm.AddressId,
            Capacity = dorm.Capacity,
            RoomCount = dorm.RoomCount,
            Gender = dorm.Gender,
            Remark = dorm.Remark,
            IsActive = dorm.IsActive
        };
        IsActiveOriginal = dorm.IsActive;

        await LoadBasicsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // v2.13.82 即便 ModelState 失效也要重新计算 CurrentCount 以便锁定提示继续显示
        if (await _db.Dorms.FindAsync(Dorm.Id) is { } dormForCount)
        {
            CurrentCount = await _db.DormBookings
                .CountAsync(b => b.DormCode == dormForCount.DormCode && b.Status == BookingStatus.Staying);
        }

        if (!ModelState.IsValid)
        {
            await LoadBasicsAsync();
            return Page();
        }

        var dorm = await _db.Dorms.FindAsync(Dorm.Id);
        if (dorm == null)
        {
            return NotFound();
        }

        // v2.13.82 业务约束：在宿人数 > 0 时禁止取消启用
        // 锁定条件：当前 dorm.IsActive=true 且 表单提交 Dorm.IsActive=false 且 CurrentCount > 0
        if (dorm.IsActive && !Dorm.IsActive && CurrentCount > 0)
        {
            IsActiveOriginal = dorm.IsActive;
            ModelState.AddModelError("Dorm.IsActive",
                $"该宿舍当前在宿 {CurrentCount} 人，禁止停用。请先办理所有人员退宿手续后再操作。");
            await LoadBasicsAsync();
            return Page();
        }

        // 获取楼栋名称
        var building = await _basicsService.GetBuildingByIdAsync(Dorm.BuildingId);
        var address = await _basicsService.GetAddressByIdAsync(Dorm.AddressId);

        dorm.DormCode = Dorm.DormCode;
        dorm.BuildingId = Dorm.BuildingId;
        dorm.BuildingName = building?.Name ?? "";
        dorm.FloorId = Dorm.FloorId;
        dorm.AddressId = Dorm.AddressId;
        dorm.AddressText = address?.AddressText ?? "";
        dorm.Capacity = Dorm.Capacity;
        dorm.RoomCount = Dorm.RoomCount;
        dorm.Gender = Dorm.Gender;
        dorm.Remark = Dorm.Remark;
        dorm.IsActive = Dorm.IsActive;
        dorm.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        TempData["Success"] = "保存成功";
        return RedirectToPage("/Dorms/Details", new { id = Dorm.Id });
    }

    private async Task LoadBasicsAsync()
    {
        var buildings = await _basicsService.GetBuildingsAsync(null, 1, 100);
        var floors = await _basicsService.GetFloorsAsync(null, 1, 100);
        var addresses = await _basicsService.GetAddressesAsync(null, 1, 100);

        Buildings = buildings.Items.ToList();
        Floors = floors.Items.ToList();
        Addresses = addresses.Items.ToList();
    }
}

/// <summary>
/// 宿舍编辑数据传输对象
/// </summary>
public class DormEditDto
{
    public int Id { get; set; }

    public string DormCode { get; set; } = "";

    public int BuildingId { get; set; }

    public int FloorId { get; set; }

    public int AddressId { get; set; }

    public int Capacity { get; set; } = 4;

    public int RoomCount { get; set; } = 1;  // v2.12.38 新增房间数字段

    public int Gender { get; set; } = 1;

    public string? Remark { get; set; }

    public bool IsActive { get; set; } = true;
}
