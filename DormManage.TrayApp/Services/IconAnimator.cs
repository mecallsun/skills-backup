using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DormManage.TrayApp.Services;

/// <summary>
/// v2.13.212 托盘图标启动轮询动画器（任务栏图标统一规范：⚪ 圆形实心点）
///
/// 业务背景：
/// 旧版 v2.13.199 采用"旋转 loading 圆弧"动画（视觉冲击较强），
/// 但用户反馈"在 t0 阶段（双服务均未启动）时图标固定红色，给用户造成'卡死'的困惑"。
///
/// v2.13.200 改用"三色轮询"动画：
/// - 灰色（Gray）：   托盘程序初始化中（加载配置、初始化菜单）
/// - 白灰（White）：  准备拉起 Web/Api 服务
/// - 橙色（Orange）： 等待数据库连接 + 健康检查（持续等待中）
///
/// v2.13.212 任务栏图标统一规范（强约束）：
/// ============================================
/// **任务栏图标的形状必须是圆形 ⚪**，**禁止任何其他形状**
/// （方形、三角形、心形、菱形、星形等）。
///
/// 三种颜色（灰/白灰/橙）都是同一个 ⚪ 圆形的不同颜色填充：
///   🩶 Gray   → ⚪ 圆形（#6c757d）
///   ⚪ White → ⚪ 圆形（#f8f9fa）  ← "白灰"
///   🟧 Orange → ⚪ 圆形（#fd7e14）
///
/// 三色切换时**仅颜色变化，形状完全一致**（都是 ⚪ 圆形）。
/// 切换周期：500ms（每半秒切换一种颜色）。
///
/// 运行期（轮询停止后）也使用同样的 ⚪ 圆形：
///   🟢 Green   → ⚪ 圆形（双服务均 Running）
///   🟡 Yellow  → ⚪ 圆形（仅一个服务 Running）
///   🔴 Red     → ⚪ 圆形（双服务均异常/启动失败）
/// ============================================
///
/// v2.13.212 防御性强化：
/// - 代码 100% 使用 `FillEllipse` 绘制圆形，**不存在任何其他图形调用**
/// - 三角形/方形/菱形/星形/心形等形状的绘制方法（FillPolygon/FillRectangle/Heart 等）
///   **都不在本类中使用**，确保生成图标一定是圆形
/// - 如发现实际部署后仍有非圆形图标，请检查：
///   1) 是否部署了最新版本（v2.13.211+）
///   2) Windows 任务栏图标缓存（重启 Windows 或注销重登录刷新）
///   3) 如果是 SystemIcons.Application 回退路径触发，应在 logs/日志中查找异常
///
/// 触发规则：
/// - 启动时双服务均为 Stopped → 启动三色轮询（灰/白灰/橙，⚪ 圆形）
/// - 任一服务从 Stopped 进入其他状态（Starting/Running/Crashed/Stopping）→ 停止轮询，切换到运行期三色状态机
/// - 关闭服务后所有服务回到 Stopped → 重启三色轮询
///
/// 实现参数：
/// - 轮询周期：500ms（每 500ms 切换一种颜色）
/// - 颜色序列：Gray(100) → White(101) → Orange(102) → 回到 Gray 循环
/// - 图标尺寸：16x16 像素
/// - 圆形直径：14px（与 IconGenerator 保持视觉一致）
/// </summary>
public sealed class IconAnimator : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly Icon _idleIcon;        // 动画停止后显示的静态图标（备用）
    private bool _isAnimating = false;
    private int _tick = 0;

    // 三色轮询序列（每次 Tick 切换到下一个）
    private static readonly int[] StartupColorSequence = { 100, 101, 102 };

    /// <summary>
    /// 创建图标动画器
    /// </summary>
    /// <param name="notifyIcon">要控制动画的 NotifyIcon</param>
    /// <param name="idleIcon">动画停止后显示的静态图标（备用）</param>
    public IconAnimator(NotifyIcon notifyIcon, Icon idleIcon)
    {
        _notifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
        _idleIcon = idleIcon ?? throw new ArgumentNullException(nameof(idleIcon));

        // 500ms 切换一次颜色（用户的明确要求：每半秒 = 500ms 切换一种颜色）
        _animationTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _animationTimer.Tick += (_, _) => OnTick();
    }

    /// <summary>
    /// 开始显示启动轮询动画（启动阶段）
    /// </summary>
    public void StartAnimation()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        _tick = 0;
        _animationTimer.Start();
        ApplyTick();  // 立即显示第一个颜色（Gray），避免动画前有红色闪现
    }

    /// <summary>
    /// 停止动画并交由 NotifyIconManager 接管（红/黄/绿三色状态机）
    /// </summary>
    public void StopAnimation()
    {
        if (!_isAnimating) return;
        _animationTimer.Stop();
        _isAnimating = false;
        // 不在这里设置图标，让 NotifyIconManager.SetIconColor 接管
        // 因为 IconAnimator 启动的最终颜色可能不是 NotifyIconManager 期望的颜色
    }

    private void OnTick()
    {
        if (!_isAnimating) return;
        _tick = (_tick + 1) % StartupColorSequence.Length;
        ApplyTick();
    }

    private void ApplyTick()
    {
        try
        {
            var colorCode = StartupColorSequence[_tick];
            // 动态生成实心圆点图标
            _notifyIcon.Icon = CreateSolidCircleIcon(colorCode);
        }
        catch
        {
            // 切换失败不影响主流程
        }
    }

    /// <summary>
    /// v2.13.204 统一三色轮询图标形状：同一种圆形
    /// 颜色由传入的数值映射：
    /// - 100 → Gray  (#6c757d)
    /// - 101 → White (#f8f9fa)
    /// - 102 → Orange (#fd7e14)
    /// 三种颜色使用完全相同的圆形绘制参数（实心圆，无描边差异），
    /// 形状与运行期三色状态机（绿/黄/红）的实心圆点图标保持一致。
    /// </summary>
    private Icon CreateSolidCircleIcon(int colorCode)
    {
        // 标准托盘图标尺寸 16x16，与系统其他托盘图标保持一致
        const int size = 16;
        // 圆形直径 14（留 1 像素边距），与 IconGenerator.CreateSolidCircle 保持视觉一致
        const int diameter = 14;
        const int offset = 1;

        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            Color fillColor = colorCode switch
            {
                100 => Color.FromArgb(108, 117, 125),  // Gray   #6c757d
                101 => Color.FromArgb(248, 249, 250),  // White  #f8f9fa
                102 => Color.FromArgb(253, 126, 20),   // Orange #fd7e14
                _ => Color.Gray
            };

            // 统一填充：所有颜色都用相同的圆形（实心 FillEllipse），不画描边
            using var brush = new SolidBrush(fillColor);
            g.FillEllipse(brush, offset, offset, diameter, diameter);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _animationTimer?.Stop();
        _animationTimer?.Dispose();
    }
}
