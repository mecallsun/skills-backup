using DormManage.TrayApp.Models;

namespace DormManage.TrayApp;

/// <summary>
/// 托盘图标 + 右键菜单管理器。
///
/// 菜单项（按 F1 需求规格）：
/// - 打开管理后台 / 打开 API 文档
/// - 服务状态（Api / Admin 子项）
/// - 重启所有服务
/// - 设置...
/// - 查看日志
/// - 关于
/// - 退出
///
/// 托盘图标状态：
/// - 全绿：Api + Admin 均运行中
/// - 全灰：未启动
/// - 黄三角：某一服务异常
/// - 红 X：两服务都异常
/// </summary>
public sealed class NotifyIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _miApiStatus;
    private readonly ToolStripMenuItem _miAdminStatus;
    private readonly ToolStripMenuItem _miExit;
    private readonly SynchronizationContext? _uiContext;

    private ServiceState _apiState = ServiceState.Stopped;
    private ServiceState _adminState = ServiceState.Stopped;

    public NotifyIconManager(
        Action onOpenAdmin,
        Action onOpenApi,
        Action onSettings,
        Func<Task> onRestartAll,
        Action onViewLogs,
        Action onAbout,
        Func<Task> onExit)
    {
        _uiContext = SynchronizationContext.Current;
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "金戈宿舍管理系统 v2.13.2",
            Visible = true
        };

        // 左键单击：打开管理后台
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) onOpenAdmin();
        };

        // 状态子菜单
        _miApiStatus = new ToolStripMenuItem("Api：已停止") { Enabled = false };
        _miAdminStatus = new ToolStripMenuItem("Admin：已停止") { Enabled = false };

        var ctx = new ContextMenuStrip();
        ctx.Items.Add(new ToolStripMenuItem("打开管理后台", null, (_, _) => onOpenAdmin()) { Font = new Font(SystemFonts.MenuFont, FontStyle.Bold) });
        ctx.Items.Add(new ToolStripMenuItem("打开 API 文档", null, (_, _) => onOpenApi()));
        ctx.Items.Add(new ToolStripSeparator());

        var statusMenu = new ToolStripMenuItem("服务状态");
        statusMenu.DropDownItems.Add(_miApiStatus);
        statusMenu.DropDownItems.Add(_miAdminStatus);
        ctx.Items.Add(statusMenu);

        ctx.Items.Add(new ToolStripMenuItem("重启所有服务", null, async (_, _) =>
        {
            try { await onRestartAll(); }
            catch (Exception ex) { ShowError($"重启失败：{ex.Message}"); }
        }));
        ctx.Items.Add(new ToolStripSeparator());

        ctx.Items.Add(new ToolStripMenuItem("设置...", null, (_, _) => onSettings()));
        ctx.Items.Add(new ToolStripMenuItem("查看日志", null, (_, _) => onViewLogs()));
        ctx.Items.Add(new ToolStripMenuItem("关于", null, (_, _) => onAbout()));

        ctx.Items.Add(new ToolStripSeparator());
        _miExit = new ToolStripMenuItem("退出", null, async (_, _) =>
        {
            try { await onExit(); }
            catch (Exception ex) { ShowError($"退出失败：{ex.Message}"); }
        });
        ctx.Items.Add(_miExit);

        _notifyIcon.ContextMenuStrip = ctx;
    }

    /// <summary>外部更新服务状态（来自 ProcessManager / HealthChecker）
    /// 注意：托盘事件/菜单回调均在 UI 线程触发，但 ProcessManager.Exited 事件可能来自其他线程，
    /// 因此通过 _uiContext.Post 投递到 UI 线程以保证线程安全。
    /// </summary>
    public void UpdateServiceState(string name, ServiceState state)
    {
        var ctx = _uiContext;
        if (ctx is not null)
            ctx.Post(_ => UpdateServiceStateCore(name, state), null);
        else
            UpdateServiceStateCore(name, state);
    }

    private void UpdateServiceStateCore(string name, ServiceState state)
    {
        if (name == "Api")
        {
            _apiState = state;
            _miApiStatus.Text = $"Api：{StateText(state)}";
        }
        else if (name == "Admin")
        {
            _adminState = state;
            _miAdminStatus.Text = $"Admin：{StateText(state)}";
        }

        _notifyIcon.Text = $"金戈宿舍管理系统 v2.13.2\nApi: {StateText(_apiState)}\nAdmin: {StateText(_adminState)}";
    }

    private static string StateText(ServiceState state) => state switch
    {
        ServiceState.Running => "● 运行中",
        ServiceState.Starting => "◐ 启动中",
        ServiceState.Stopping => "◐ 停止中",
        ServiceState.Stopped => "○ 已停止",
        ServiceState.Crashed => "✕ 异常",
        _ => "未知"
    };

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    private static Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "tray-icon.ico");
        if (File.Exists(iconPath))
        {
            try { return new Icon(iconPath); }
            catch { /* 损坏时回退 */ }
        }
        // 回退：使用系统图标
        return SystemIcons.Application;
    }

    private static void ShowError(string msg)
        => MessageBox.Show(msg, "托盘错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}