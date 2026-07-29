using System.Windows.Forms;

namespace DormManage.TrayApp.Forms;

/// <summary>
/// v2.13.196：试用次数超出强制确认窗口
/// 用于当未注册且 UseTimes >= TRIAL_LIMIT 时强制弹出，
/// 用户必须点击「我已知晓，继续」按钮确认后才能继续
/// 程序会以强制试用模式继续运行（受 CheckTrialRecordLimit 限制）
/// </summary>
public static class TrialExceedPrompt
{
    /// <summary>
    /// 显示强制确认窗口
    /// </summary>
    /// <param name="useTimes">当前已使用次数</param>
    /// <param name="trialLimit">试用次数上限</param>
    /// <returns>true = 用户已确认；false = 用户取消（应终止程序）</returns>
    public static bool Show(int useTimes, int trialLimit)
    {
        using var form = new Form
        {
            Text = "⚠ 试用次数已超出",
            Width = 480,
            Height = 280,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            TopMost = true,
            BackColor = System.Drawing.Color.FromArgb(255, 244, 230)
        };

        var lblTitle = new Label
        {
            Text = "软件未注册，已超出免费试用次数",
            Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(204, 102, 0),
            Location = new System.Drawing.Point(20, 20),
            Size = new System.Drawing.Size(430, 30),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        var lblDetail = new Label
        {
            Text = $"当前已使用 {useTimes} 次 / 上限 {trialLimit} 次\n\n您必须确认以下内容后才能继续使用：\n\n  •  程序将以强制试用模式运行\n  •  仅限3大基础模块（住宿登记/宿舍档案/人员清单）使用\n  •  其他模块将无法写入或保存\n  •  强烈建议联系信息科完成正式注册",
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
            Location = new System.Drawing.Point(20, 55),
            Size = new System.Drawing.Size(430, 150),
            BackColor = System.Drawing.Color.Transparent
        };

        var btnConfirm = new Button
        {
            Text = "✓ 我已知晓，继续使用（试用模式）",
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(60, 215),
            Size = new System.Drawing.Size(220, 35),
            BackColor = System.Drawing.Color.FromArgb(220, 53, 69),
            ForeColor = System.Drawing.Color.White,
            Cursor = Cursors.Hand
        };

        var btnAbort = new Button
        {
            Text = "取消并退出",
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
            Location = new System.Drawing.Point(290, 215),
            Size = new System.Drawing.Size(130, 35),
            Cursor = Cursors.Hand
        };

        bool confirmed = false;
        btnConfirm.Click += (_, _) =>
        {
            confirmed = true;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };
        btnAbort.Click += (_, _) =>
        {
            confirmed = false;
            form.DialogResult = DialogResult.Cancel;
            form.Close();
        };
        form.AcceptButton = btnConfirm;
        form.CancelButton = btnAbort;

        form.Controls.Add(lblTitle);
        form.Controls.Add(lblDetail);
        form.Controls.Add(btnConfirm);
        form.Controls.Add(btnAbort);

        form.ShowDialog();
        return confirmed;
    }
}
