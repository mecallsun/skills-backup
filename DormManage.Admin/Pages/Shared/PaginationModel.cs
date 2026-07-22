using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DormManage.Admin.Pages.Shared;

/// <summary>
/// v2.13.71 列表分页组件参数模型
/// </summary>
public class PaginationModel : PageModel
{
    /// <summary>当前页码（从 1 开始）</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>每页条数（默认 10）</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>总记录数</summary>
    public int TotalCount { get; set; }

    /// <summary>基础 URL（含现有 query string 但不含 pageIndex/pageSize）</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>筛选表单 ID（可选）</summary>
    public string FormId { get; set; } = "";
}
