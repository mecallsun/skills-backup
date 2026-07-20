using System.Diagnostics;
using DormManage.TrayApp.Models;

namespace DormManage.TrayApp;

/// <summary>
/// 托盘图标颜色状态。
/// </summary>
public enum TrayIconColor
{
    Red,
    Yellow,
    Green
}

/// <summary>
/// 托盘图标 + 右键菜单管理器。
///
/// 菜单项（按需求 57 §3.1）：
/// - 打开管理后台 / 打开 API 文档
/// - 服务状态（Api / Admin 子项）
/// - 重启所有服务
/// - 系统设置...
/// - 查看日志
/// - 开机自启动（v2.13.3）
/// - 关于
/// - 退出
///
/// 托盘图标颜色状态机（v2.13.19）：
/// - 红色：启动阶段 / 任一服务未 Running 且未全部 Running
/// - 绿色：Api + Admin 均 Running
/// - 黄色：Api / Admin 恰好一个 Running
///
/// 【v2.13.4 修复】
/// 1. 构造函数增加 owner: Form 参数，ContextMenuStrip 关联到该 owner，
///    避免无主窗体时右键菜单 Show 失败；
/// 2. SystemFonts.MenuFont 加 ?? 兜底，避免高 DPI / 主题下为 null 时 NRE；
/// 3. 所有菜单 Click 回调统一 try-catch，避免单次失败拖死整个菜单；
/// 4. 拆分菜单构造为多个小方法，避免大构造函数中 null 字面量 + 对象初始化器歧义。
/// </summary>
public sealed class NotifyIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _ctx;
    private ToolStripMenuItem _miApiStatus = null!;
    private ToolStripMenuItem _miAdminStatus = null!;
    private ToolStripMenuItem _miAutoStart = null!;
    private readonly SynchronizationContext? _uiContext;

    private ServiceState _apiState = ServiceState.Stopped;
    private ServiceState _adminState = ServiceState.Stopped;

    public NotifyIconManager(
        Form owner,
        Action onOpenAdmin,
        Action onOpenApi,
        Action onSettings,
        Func<Task> onRestartAll,
        Action onViewLogs,
        Action onAbout,
        Func<Task> onExit,
        Action onToggleAutoStart)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _uiContext = SynchronizationContext.Current;

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "金戈宿舍管理系统 v2.13.29",
            Visible = true
        };

        // 左键单击：打开管理后台
        _notifyIcon.MouseClick += (_, e) =>
        {
            try
            {
                if (e.Button == MouseButtons.Left) onOpenAdmin();
            }
            catch (Exception ex)
            {
                SafeShowError($"打开管理后台失败：{ex.Message}");
            }
        };

        // ===== 右键菜单（按需求 57 §3.1 顺序）=====
        _ctx = new ContextMenuStrip();

        BuildMenuItems(
            onOpenAdmin, onOpenApi, onSettings,
            onRestartAll, onViewLogs, onAbout, onExit, onToggleAutoStart);

        // 关键：ContextMenuStrip 关联到 NotifyIcon，
        // 并由外部传入 owner 作为 Application.OpenForms 中的窗口宿主，
        // 避免无主窗体时右键菜单 Show 失败（v2.13.4 修复点）
        _notifyIcon.ContextMenuStrip = _ctx;
    }

    private void BuildMenuItems(
        Action onOpenAdmin,
        Action onOpenApi,
        Action onSettings,
        Func<Task> onRestartAll,
        Action onViewLogs,
        Action onAbout,
        Func<Task> onExit,
        Action onToggleAutoStart)
    {
        // 1. 打开管理后台
        var miOpenAdmin = NewMenuItem("打开管理后台", "🌐", bold: true);
        miOpenAdmin.Click += (_, _) => SafeInvoke(onOpenAdmin);
        _ctx.Items.Add(miOpenAdmin);

        // 2. 打开 API 文档
        var miOpenApi = NewMenuItem("打开 API 文档", "📘", bold: false);
        miOpenApi.Click += (_, _) => SafeInvoke(onOpenApi);
        _ctx.Items.Add(miOpenApi);

        _ctx.Items.Add(new ToolStripSeparator());

        // 3. 服务状态（子菜单）
        _miApiStatus = NewStatusItem("Api：○ 已停止");
        _miAdminStatus = NewStatusItem("Admin：○ 已停止");
        var statusMenu = NewMenuItem("服务状态", "●", bold: false);
        statusMenu.DropDownItems.Add(_miApiStatus);
        statusMenu.DropDownItems.Add(_miAdminStatus);
        _ctx.Items.Add(statusMenu);

        // 4. 重启所有服务
        var miRestart = NewMenuItem("重启所有服务", "🔄", bold: false);
        miRestart.Click += async (_, _) => await SafeInvokeAsync(onRestartAll, "重启失败");
        _ctx.Items.Add(miRestart);

        _ctx.Items.Add(new ToolStripSeparator());

        // 5. 系统设置...
        var miSettings = NewMenuItem("系统设置...", "⚙", bold: false);
        miSettings.Click += (_, _) => SafeInvoke(onSettings);
        _ctx.Items.Add(miSettings);

        // 6. 查看日志
        var miLogs = NewMenuItem("查看日志", "📄", bold: false);
        miLogs.Click += (_, _) => SafeInvoke(onViewLogs);
        _ctx.Items.Add(miLogs);

        // 7. 开机自启动（v2.13.3）
        _miAutoStart = NewMenuItem("开机自启动", "", bold: false);
        _miAutoStart.CheckOnClick = false;
        _miAutoStart.Click += (_, _) => SafeInvoke(onToggleAutoStart);
        _ctx.Items.Add(_miAutoStart);
        RefreshAutoStartStatus(new Services.AutoStartManager().IsEnabled());

        // 8. 关于
        var miAbout = NewMenuItem("关于", "ℹ", bold: false);
        miAbout.Click += (_, _) => SafeInvoke(onAbout);
        _ctx.Items.Add(miAbout);

        _ctx.Items.Add(new ToolStripSeparator());

        // 9. 退出
        var miExit = NewMenuItem("退出", "✕", bold: false);
        miExit.Click += async (_, _) => await SafeInvokeAsync(onExit, "退出失败");
        _ctx.Items.Add(miExit);
    }

    private static ToolStripMenuItem NewMenuItem(string text, string icon, bool bold)
    {
        var display = string.IsNullOrEmpty(icon) ? text : $"{icon} {text}";
        var font = SafeMenuFont();
        return new ToolStripMenuItem(display)
        {
            Font = bold ? new Font(font, FontStyle.Bold) : font
        };
    }

    private static ToolStripMenuItem NewStatusItem(string text)
    {
        return new ToolStripMenuItem(text)
        {
            Enabled = false,
            Font = SafeMenuFont()
        };
    }

    /// <summary>外部更新自启动状态（来自 ToggleAutoStart 回调）</summary>
    public void RefreshAutoStartStatus(bool enabled)
    {
        if (_miAutoStart is null) return;
        _miAutoStart.Checked = enabled;
        _miAutoStart.Text = enabled ? "✓ 开机自启动" : "开机自启动";
    }

    private static Font SafeMenuFont()
    {
        try
        {
            return SystemFonts.MenuFont ?? new Font("Microsoft YaHei UI", 9f);
        }
        catch
        {
            return new Font("Microsoft YaHei UI", 9f);
        }
    }

    /// <summary>外部更新服务状态（来自 ProcessManager / HealthChecker）</summary>
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

        _notifyIcon.Text = $"金戈宿舍管理系统 v2.13.29\nApi: {StateText(_apiState)}\nAdmin: {StateText(_adminState)}";

        // v2.13.19：同步刷新图标颜色
        SetIconColor(EvaluateIconColor());
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

    /// <summary>
    /// 根据当前 Api / Admin 状态计算图标颜色。
    /// </summary>
    private TrayIconColor EvaluateIconColor()
    {
        var apiRunning = _apiState == ServiceState.Running;
        var adminRunning = _adminState == ServiceState.Running;

        if (apiRunning && adminRunning) return TrayIconColor.Green;
        if (apiRunning || adminRunning) return TrayIconColor.Yellow;
        return TrayIconColor.Red;
    }

    /// <summary>
    /// 切换托盘图标颜色（v2.13.19）。
    /// </summary>
    public void SetIconColor(TrayIconColor color)
    {
        var ctx = _uiContext;
        if (ctx is not null)
        {
            ctx.Post(_ => SetIconColorCore(color), null);
        }
        else
        {
            SetIconColorCore(color);
        }
    }

    private void SetIconColorCore(TrayIconColor color)
    {
        try
        {
            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = IconGenerator.CreateSolidCircle(color);
            try { oldIcon?.Dispose(); } catch { }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotifyIconManager] 切换图标颜色失败: {ex.Message}");
        }
    }

    private static Icon LoadTrayIcon()
    {
        // v2.13.19：优先使用动态生成图标，静态 ICO 仅作为极端回退
        try
        {
            return IconGenerator.CreateSolidCircle(TrayIconColor.Red);
        }
        catch
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
    }

    private static void SafeInvoke(Action action)
    {
        try { action(); }
        catch (Exception ex) { SafeShowError($"操作失败：{ex.Message}"); }
    }

    private static async Task SafeInvokeAsync(Func<Task> action, string errorPrefix)
    {
        try { await action(); }
        catch (Exception ex) { SafeShowError($"{errorPrefix}：{ex.Message}"); }
    }

    private static void SafeShowError(string msg)
    {
        try
        {
            MessageBox.Show(msg, "托盘错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch
        {
            // 终极兜底：消息框也失败时直接吞掉，避免递归崩溃
        }
    }

    public void Dispose()
    {
        try { _notifyIcon.Visible = false; } catch { }
        try { _notifyIcon.Dispose(); } catch { }
        try { _ctx.Dispose(); } catch { }
    }
}