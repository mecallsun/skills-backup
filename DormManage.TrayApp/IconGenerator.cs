using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DormManage.TrayApp;

/// <summary>
/// 托盘图标动态生成器（v2.13.19）。
/// 根据颜色状态生成 32×32 实心圆点图标，避免维护多套 ICO 资源。
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
        _ => Color.Gray
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
