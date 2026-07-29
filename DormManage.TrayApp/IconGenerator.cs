using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DormManage.TrayApp;

/// <summary>
/// v2.13.212 托盘图标动态生成器（强约束：仅生成 ⚪ 圆形图标）
///
/// 根据颜色状态生成 32×32 实心圆点图标，避免维护多套 ICO 资源。
///
/// v2.13.200 新增：
/// - TrayIconColor 枚举增加 StartupGray / StartupWhite / StartupOrange 三个启动动画色
/// - 这些颜色仅用于托盘启动阶段（双服务均未启动）的轮询动画
/// - 当任一服务启动成功或失败时，轮询动画停止，颜色切换到正常的红/黄/绿
///
/// v2.13.212 强约束：
/// - 100% 使用 `FillEllipse` 绘制圆形
/// - 严禁使用 FillRectangle/FillPolygon/FillPie 等任何非圆形绘制方法
/// - 严禁加载 ICO 静态资源（tray-icon.ico 已删除）
/// - 如果在生产环境发现非圆形图标：
///   1) 确认已部署 v2.13.211+ 版本
///   2) Windows 任务栏会缓存图标，重启或注销可清除缓存
///   3) 检查托盘日志（log/）是否触发了异常回退到 SystemIcons.Application
/// </summary>
public static class IconGenerator
{
    private const int IconSize = 32;
    private const int CircleDiameter = 24;

    public static Icon CreateSolidCircle(TrayIconColor color)
    {
        using var bitmap = new Bitmap(IconSize, IconSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(MapColor(color));
            var x = (IconSize - CircleDiameter) / 2;
            var y = (IconSize - CircleDiameter) / 2;
            g.FillEllipse(brush, x, y, CircleDiameter, CircleDiameter);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            DestroyIcon(hIcon);
            throw;
        }
    }

    private static Color MapColor(TrayIconColor color) => color switch
    {
        TrayIconColor.Green => Color.FromArgb(40, 167, 69),
        TrayIconColor.Yellow => Color.FromArgb(255, 193, 7),
        TrayIconColor.Red => Color.FromArgb(220, 53, 69),
        // v2.13.200 启动轮询动画色
        (TrayIconColor)100 => Color.FromArgb(108, 117, 125),   // Bootstrap secondary - Gray（#6c757d）
        (TrayIconColor)101 => Color.FromArgb(248, 249, 250),   // Bootstrap light - White（#f8f9fa）
        (TrayIconColor)102 => Color.FromArgb(253, 126, 20),    // Bootstrap orange（#fd7e14）
        _ => Color.Gray
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
