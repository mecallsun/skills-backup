using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;

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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var dorm = await _db.Dorms.FindAsync(id);
        if (dorm == null)
        {
            return NotFound();
        }

        Dorm = new DormEditDto
        {
            Id = dorm.Id,
            DormCode = dorm.DormCode,
            BuildingId = dorm.BuildingId,
            FloorId = dorm.FloorId,
            AddressId = dorm.AddressId,
            Capacity = dorm.Capacity,
            Gender = dorm.Gender,
            Remark = dorm.Remark,
            IsActive = dorm.IsActive
        };

        await LoadBasicsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
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

    public int Gender { get; set; } = 1;

    public string? Remark { get; set; }

    public bool IsActive { get; set; } = true;
}
