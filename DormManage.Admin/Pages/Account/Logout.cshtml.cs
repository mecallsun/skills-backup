using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using DormManage.Admin.Services;

namespace DormManage.Admin.Pages.Account;

/// <summary>
/// 登出页面模型
/// </summary>
public class LogoutModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(IAuthService authService, ILogger<LogoutModel> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await _authService.SignOutAsync(HttpContext);

        _logger.LogInformation("用户退出登录");
        return LocalRedirect("/Account/Login");
    }
}
