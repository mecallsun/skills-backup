using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace DormManage.Admin.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    private readonly ILogger<ErrorModel> _logger;

    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        // 提取查询参数 code 和 msg
        var query = Request.Query;
        if (query.TryGetValue("code", out var codeValue))
        {
            Code = codeValue.ToString();
        }
        if (query.TryGetValue("msg", out var msgValue))
        {
            // 安全处理 null 值
            Message = !string.IsNullOrEmpty(msgValue) ? Uri.UnescapeDataString(msgValue.ToString()) : null;
        }
    }
}

