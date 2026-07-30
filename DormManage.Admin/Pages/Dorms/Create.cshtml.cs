using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using DormManage.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Dorms;

/// <summary>
/// 住宿新增页面模型
/// </summary>
public class CreateModel : PageModel
{
    private readonly DormDbContext _db;
    private readonly IBasicsService _basicsService;

    public CreateModel(DormDbContext db, IBasicsService basicsService)
    {
        _db = db;
        _basicsService = basicsService;
    }

    [BindProperty]
    public DormCreateDto Dorm { get; set; } = new();

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

    public async Task OnGetAsync()
    {
        await LoadBasicsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadBasicsAsync();
            return Page();
        }

        // 检查住宿号是否已存在
        if (await _db.Dorms.AnyAsync(d => d.DormCode == Dorm.DormCode))
        {
            ModelState.AddModelError("Dorm.DormCode", "该住宿号已存在");
            await LoadBasicsAsync();
            return Page();
        }

        // 获取楼栋名称
        var building = await _basicsService.GetBuildingByIdAsync(Dorm.BuildingId);
        var address = await _basicsService.GetAddressByIdAsync(Dorm.AddressId);

        var dorm = new Dorm
        {
            DormCode = Dorm.DormCode,
            BuildingId = Dorm.BuildingId,
            BuildingName = building?.Name ?? "",
            FloorId = Dorm.FloorId,
            AddressId = Dorm.AddressId,
            AddressText = address?.AddressText ?? "",
            Capacity = Dorm.Capacity,
            RoomCount = Dorm.RoomCount,
            Gender = Dorm.Gender,
            Remark = Dorm.Remark,
            IsActive = Dorm.IsActive,
            CreatedAt = DateTime.Now
        };

        _db.Dorms.Add(dorm);
        await _db.SaveChangesAsync();

        TempData["Success"] = "新增成功";
        // v2.13.11 inModal=1 时返回 JavaScript 关闭弹窗并刷新父页
        if (Request.Query.ContainsKey("inModal"))
        {
            return Content("<script>parent.location.reload();</script>", "text/html");
        }
        return RedirectToPage("/Dorms/Details", new { id = dorm.Id });
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
/// 住宿新增数据传输对象
/// </summary>
public class DormCreateDto
{
    public string DormCode { get; set; } = "";

    public int BuildingId { get; set; }

    public int FloorId { get; set; }

    public int AddressId { get; set; }

    public int Capacity { get; set; } = 4;

    public int RoomCount { get; set; } = 1;  // v2.12.37 新增房间数字段

    public int Gender { get; set; } = 1;

    public string? Remark { get; set; }

    public bool IsActive { get; set; } = true;
}
