using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;

namespace DormManage.Admin.Pages.Meter;

/// <summary>
/// 批量导入抄表页面模型
/// </summary>
public class ImportModel : PageModel
{
    /// <summary>
    /// 当前步骤（1=下载模板, 2=上传文件, 3=查看结果）
    /// </summary>
    public int Step => ImportResult != null ? 3 : 2;

    /// <summary>
    /// 导入结果
    /// </summary>
    public ImportResultDto? ImportResult { get; set; }

    /// <summary>
    /// 下载模板（POST 处理）
    /// </summary>
    public Task<IActionResult> OnPostDownloadTemplateAsync()
    {
        // TODO: 实际项目中应生成 Excel 模板文件
        return Task.FromResult<IActionResult>(Redirect("/Meter"));
    }

    /// <summary>
    /// 上传并校验 Excel 文件
    /// </summary>
    public async Task<IActionResult> OnPostUploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "请选择要上传的文件");
            return Page();
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("file", "仅支持 .xlsx 格式的 Excel 文件");
            return Page();
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            ModelState.AddModelError("file", "文件大小不能超过 10MB");
            return Page();
        }

        await Task.CompletedTask; // 保持 async 语义以兼容 Page() 调用

        // TODO: 实际项目中应使用 EPPlus 或 ClosedXML 解析 Excel
        // 这里模拟校验结果
        ImportResult = new ImportResultDto
        {
            TotalRows = 200,
            ValidRows = 195,
            InvalidRows = 3,
            NeedCorrectionRows = 2,
            Errors = new List<ImportErrorDto>
            {
                new()
                {
                    RowNumber = 12,
                    DormCode = "D-001",
                    Month = "2026-07",
                    ColdMeter = "125.80",
                    HotMeter = "88.50",
                    ElectricMeter = "365.00",
                    ErrorMessage = "该记录已是正常状态（status=1），请先走修正流程"
                },
                new()
                {
                    RowNumber = 45,
                    DormCode = "D-050",
                    Month = "2026-07",
                    ColdMeter = "-10.00",
                    HotMeter = "62.40",
                    ElectricMeter = "280.50",
                    ErrorMessage = "冷水读数不能为负数"
                },
                new()
                {
                    RowNumber = 78,
                    DormCode = "D-100",
                    Month = "2026-07",
                    ColdMeter = "",
                    HotMeter = "",
                    ElectricMeter = "",
                    ErrorMessage = "表读数不能为空"
                }
            }
        };

        return Page();
    }

    /// <summary>
    /// 确认导入
    /// </summary>
    public Task<IActionResult> OnPostImportAsync()
    {
        // TODO: 实际项目中应执行批量导入逻辑
        TempData["SuccessMessage"] = "抄表数据导入成功";
        return Task.FromResult<IActionResult>(RedirectToPage("/Meter/Index"));
    }
}

/// <summary>
/// 导入结果数据传输对象
/// </summary>
public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int NeedCorrectionRows { get; set; }
    public List<ImportErrorDto>? Errors { get; set; }
}

/// <summary>
/// 导入错误信息
/// </summary>
public class ImportErrorDto
{
    public int RowNumber { get; set; }
    public string DormCode { get; set; } = "";
    public string Month { get; set; } = "";
    public string ColdMeter { get; set; } = "";
    public string HotMeter { get; set; } = "";
    public string ElectricMeter { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}
