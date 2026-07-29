using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DormManage.Admin.Pages.Booking;

/// <summary>
/// v2.13.214 办理入住/退房 独立页面 PageModel
///
/// 支持通过 query string ?opType=1 (入住) 或 ?opType=2 (退房) 切换初始操作类型：
/// - opType=1 (默认)：入住办理
/// - opType=2：退房办理
/// </summary>
public class CheckInModel : PageModel
{
    /// <summary>
    /// 操作类型：1 = 入住，2 = 退房
    /// 与 CheckIn.cshtml 中的 radio 控件同步（id=opIn value="1" / id=opOut value="2"）
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int OpType { get; set; } = 1; // 默认入住

    public void OnGet()
    {
        // v2.13.214：从 query string 读取 OpType 参数（如 ?opType=2 表示退房）
        // 如果 OpType 不在合法范围内（1或2），默认为 1（入住）
        if (OpType != 1 && OpType != 2)
        {
            OpType = 1;
        }
    }
}