using Microsoft.AspNetCore.Mvc;

namespace DormManage.Admin.ViewComponents;

/// <summary>
/// 导航栏菜单组件（v2.13.14 已废弃：Tab 栏改为硬编码 10 个固定 Tab）
/// 保留此组件以兼容旧引用，当前返回空视图。
/// </summary>
public class MenuViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        // v2.13.14：Tab 栏已改为硬编码，此组件不再渲染菜单
        return View("_MenuStub");
    }
}
