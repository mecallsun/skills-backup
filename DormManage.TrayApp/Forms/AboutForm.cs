using System.Reflection;

namespace DormManage.TrayApp.Forms;

/// <summary>
/// 关于窗口：版本信息、依赖、版权。
///
/// 【v2.13.4 修复】
/// 原版使用 <c>SystemFonts.MessageBoxFont!.FontFamily</c>（null-forgiving 强行通过），
/// 但在高 DPI / 主题加载未完成的极少数场景下，<see cref="SystemFonts.MessageBoxFont"/> 可能为 null，
/// 会抛出 NullReferenceException 导致窗口无法创建。
/// 改为 <see cref="SafeMessageBoxFont"/> 提供三层兜底：
/// SystemFonts.MessageBoxFont ?? SystemFonts.MenuFont ?? new Font("Microsoft YaHei UI", 9f)。
/// </summary>
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "关于";
        Size = new Size(460, 320);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        try
        {
            InitializeUI();
        }
        catch (Exception)
        {
            // 极端情况下 UI 初始化失败，至少保证窗口能弹出
            Controls.Clear();
            var lbl = new Label
            {
                Text = $"金智住宿管理系统托盘守护程序 v{Assembly.GetExecutingAssembly().GetName().Version}",
                AutoSize = true,
                Location = new Point(20, 20)
            };
            Controls.Add(lbl);
        }
    }

    private void InitializeUI()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.13.24";
        var msgFont = SafeMessageBoxFont();

        var lblTitle = new Label
        {
            Text = "金智住宿管理系统托盘守护程序",
            Font = new Font(msgFont.FontFamily, 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        var lblVersion = new Label
        {
            Text = $"版本：v{version}",
            Font = msgFont,
            AutoSize = true,
            Location = new Point(20, 58)
        };
        var lblFramework = new Label
        {
            Text = $"框架：.NET {Environment.Version}",
            Font = msgFont,
            AutoSize = true,
            Location = new Point(20, 86)
        };
        var lblAuthor = new Label
        {
            Text = "维护：金智项目组",
            Font = msgFont,
            AutoSize = true,
            Location = new Point(20, 114)
        };
        var lblDocs = new Label
        {
            Text = "需求文档：00-方案文档/57-DormManage.TrayApp需求规格-v2.13.2.md",
            Font = msgFont,
            AutoSize = true,
            Location = new Point(20, 142),
            MaximumSize = new Size(420, 0)
        };
        var lblFix = new Label
        {
            Text = "v2.13.4：修复右键 → 系统设置 \"创建窗口出错\" 问题",
            Font = new Font(msgFont.FontFamily, 9f, FontStyle.Italic),
            ForeColor = Color.FromArgb(0, 122, 204),
            AutoSize = true,
            Location = new Point(20, 180),
            MaximumSize = new Size(420, 0)
        };

        var btnOk = new Button
        {
            Text = "确定",
            Size = new Size(90, 32),
            Location = new Point(350, 230),
            DialogResult = DialogResult.OK
        };

        Controls.AddRange(new Control[] { lblTitle, lblVersion, lblFramework, lblAuthor, lblDocs, lblFix, btnOk });
        AcceptButton = btnOk;
        CancelButton = btnOk;
    }

    /// <summary>
    /// 三层兜底获取 MessageBox 字体，避免 SystemFonts.MessageBoxFont 为 null 时 NRE。
    /// </summary>
    private static Font SafeMessageBoxFont()
    {
        try
        {
            return SystemFonts.MessageBoxFont
                ?? SystemFonts.MenuFont
                ?? new Font("Microsoft YaHei UI", 9f);
        }
        catch
        {
            return new Font("Microsoft YaHei UI", 9f);
        }
    }
}