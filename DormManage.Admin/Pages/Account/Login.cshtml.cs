using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using DormManage.Admin.Services;

namespace DormManage.Admin.Pages.Account;

/// <summary>
/// 登录页面模型
/// </summary>
public class LoginModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; set; }

    public LoginModel(IAuthService authService, ILogger<LoginModel> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入用户名和密码";
            return Page();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var (success, message, principal) = await _authService.LoginAsync(UserName, Password, ip);

        if (!success)
        {
            ErrorMessage = message;
            _logger.LogWarning("登录失败: 用户名={UserName}, 原因={Message}", UserName, message);
            return Page();
        }

        // 登录成功：写入 Cookie
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal!,
            new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            });

        _logger.LogInformation("用户登录成功: {UserName}", UserName);
        return LocalRedirect("~/");
    }
}
