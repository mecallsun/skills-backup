using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 修正抄表读数页面模型
/// </summary>
public class EditModel : PageModel
{
    private readonly DormDbContext _db;

    public EditModel(DormDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>
    /// 原始抄表记录
    /// </summary>
    public MeterRecord? Record { get; set; }

    /// <summary>
    /// 修正原因
    /// </summary>
    [BindProperty]
    public string? Remark { get; set; }

    /// <summary>
    /// 上月读数参考文本
    /// </summary>
    public string? LastReadingRef { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Record = await _db.MeterRecords
            .FirstOrDefaultAsync(r => r.Id == Id);

        if (Record == null)
        {
            TempData["ErrorMessage"] = "抄表记录不存在";
            return RedirectToPage("/Meter/Index");
        }

        // 已修正的记录不允许再次修改
        if (Record.Status == 2)
        {
            TempData["ErrorMessage"] = "该记录已是修正状态，不可再次修改";
            return RedirectToPage("/Meter/Index");
        }

        // 加载上月读数参考
        var lastMonth = DateTime.Parse(Record.ReadMonth).AddMonths(-1).ToString("yyyy-MM");
        var lastRecord = await _db.MeterRecords
            .FirstOrDefaultAsync(r =>
                r.DormId == Record.DormId &&
                r.ReadMonth == lastMonth);

        if (lastRecord != null)
        {
            LastReadingRef = $"上月（{lastMonth}）：冷水 {lastRecord.ColdMeter:F2} / 热水 {lastRecord.HotMeter:F2} / 电 {lastRecord.ElectricMeter:F2}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(decimal ColdMeter, decimal HotMeter, decimal ElectricMeter)
    {
        if (Record == null)
        {
            return NotFound();
        }

        // 修正原因必填
        if (string.IsNullOrWhiteSpace(Remark))
        {
            ModelState.AddModelError("Remark", "修正原因必填");
            return Page();
        }

        Record.ColdMeter = ColdMeter;
        Record.HotMeter = HotMeter;
        Record.ElectricMeter = ElectricMeter;
        Record.Remark = $"【修正】{Remark}\n原数据：冷水 {Record.ColdMeter:F2} / 热水 {Record.HotMeter:F2} / 电 {Record.ElectricMeter:F2}";
        Record.ServerCreatedAt = DateTime.Now;
        Record.Status = 2; // 已修正

        try
        {
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "抄表记录修正成功";
            return RedirectToPage("/Meter/Index");
        }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError("", $"修正失败：{ex.Message}");
            return Page();
        }
    }
}
