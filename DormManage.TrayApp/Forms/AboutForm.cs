using System.Reflection;

namespace DormManage.TrayApp.Forms;

/// <summary>
/// 关于窗口：版本信息、依赖、版权。
/// </summary>
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "关于";
        Size = new Size(420, 260);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.13.2";

        var lblTitle = new Label
        {
            Text = "金戈宿舍管理系统托盘守护程序",
            Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 12f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        var lblVersion = new Label
        {
            Text = $"版本：v{version}",
            AutoSize = true,
            Location = new Point(20, 55)
        };
        var lblFramework = new Label
        {
            Text = $"框架：.NET {Environment.Version}",
            AutoSize = true,
            Location = new Point(20, 80)
        };
        var lblAuthor = new Label
        {
            Text = "维护：金戈项目组",
            AutoSize = true,
            Location = new Point(20, 105)
        };
        var lblDocs = new Label
        {
            Text = "文档：00-方案文档/56-DormManage.TrayApp技术方案-v2.13.2.md",
            AutoSize = true,
            Location = new Point(20, 130)
        };
        var btnOk = new Button
        {
            Text = "确定",
            Size = new Size(90, 32),
            Location = new Point(310, 180),
            DialogResult = DialogResult.OK
        };

        Controls.AddRange(new Control[] { lblTitle, lblVersion, lblFramework, lblAuthor, lblDocs, btnOk });
        AcceptButton = btnOk;
        CancelButton = btnOk;
    }
}