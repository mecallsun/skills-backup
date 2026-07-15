using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DormManage.Admin.Services;

namespace DormManage.Admin.ViewComponents;

public class MenuViewComponent : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var menu = new List<AuthHelperExtensions.MenuNode>
        {
            new() { Id = 1, PermissionCode = "home:view", PermissionName = "首页看板", Route = "/", Icon = "bi-speedometer2" }
        };
        return View("Default", menu);
    }
}
