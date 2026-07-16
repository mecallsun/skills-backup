using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Admin.Pages.Personnel;

/// <summary>
/// 人员导入页面模型
/// </summary>
public class ImportModel : PageModel
{
    private readonly DormDbContext _db;

    public ImportModel(DormDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 当前步骤（1=下载模板, 2=上传文件, 3=查看结果）
    /// </summary>
    public int Step => ImportResult != null ? 3 : 2;

    /// <summary>
    /// 导入结果
    /// </summary>
    public ImportResultDto? ImportResult { get; set; }

    /// <summary>
    /// 下载导入模板
    /// </summary>
    public Task<IActionResult> OnGetDownloadTemplateAsync()
    {
        // TODO: 实际项目中应生成 Excel 模板文件
        // 这里返回一个占位响应
        return Task.FromResult<IActionResult>(Redirect("/"));
    }

    /// <summary>
    /// 上传并导入 Excel 文件
    /// </summary>
    public async Task<IActionResult> OnPostUploadAsync(IFormFile file, bool overwriteExisting = false)
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

        try
        {
            // TODO: 实际项目中应使用 EPPlus 或 ClosedXML 解析 Excel
            // 这里模拟导入结果
            ImportResult = new ImportResultDto
            {
                TotalRows = 200,
                SuccessRows = 195,
                FailedRows = 3,
                SkippedRows = 2,
                Errors = new List<ImportErrorDto>
                {
                    new() { RowNumber = 12, FieldName = "工号", ErrorMessage = "工号已存在：EMP-2026-001" },
                    new() { RowNumber = 78, FieldName = "入职日期", ErrorMessage = "日期格式错误" },
                    new() { RowNumber = 156, FieldName = "部门", ErrorMessage = "部门代码无效" }
                }
            };
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("upload", $"导入失败：{ex.Message}");
            return Page();
        }

        return Page();
    }

    /// <summary>
    /// 下载模板（POST 处理）
    /// </summary>
    public Task<IActionResult> OnPostDownloadTemplateAsync()
    {
        return Task.FromResult<IActionResult>(Redirect("/"));
    }
}

/// <summary>
/// 导入结果数据传输对象
/// </summary>
public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public int SkippedRows { get; set; }
    public List<ImportErrorDto>? Errors { get; set; }
}

/// <summary>
/// 导入错误信息
/// </summary>
public class ImportErrorDto
{
    public int RowNumber { get; set; }
    public string FieldName { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}
