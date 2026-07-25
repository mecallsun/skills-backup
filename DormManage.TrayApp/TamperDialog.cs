using System;
using System.Drawing;
using System.Windows.Forms;
using DormManage.Shared.Security;

namespace DormManage.TrayApp;

/// <summary>
/// v2.13.135 暗桩：伪装系统内存错误对话框
///
/// 设计来源：复用「仓库物料汇总」Jinge.MaterialSummary FR-07 ShowTamperDialog 逻辑
/// - 窗口标题：「系统错误」
/// - 标题文本：「⚠ 内存访问冲突」
/// - 正文随机从 5 条内存错误信息中选取一条（无业务信息泄露）
/// - 隐藏键盘序列 5-2-0：匹配成功 → DialogResult = OK → return true（解锁）
/// - 点击「确认」按钮 → DialogResult = Cancel → return false（退出）
/// </summary>
public static class TamperDialog
{
    /// <summary>5 条内存相关错误信息（与仓库物料汇总 FR-07 完全一致）</summary>
    private static readonly string[] _messages = new[]
    {
        "应用程序无法正常启动 (0xc0000005)。尝试读取位置 0x00000000 时发生访问冲突。\n\n可能是内存模块故障或系统页面文件不足。请重新启动计算机后重试。",
        "系统检测到严重的内存异常：PAGE_FAULT_IN_NONPAGED_AREA (0x00000050)。\n\n请联系系统管理员检查物理内存或虚拟内存配置。",
        "异常代码：0xC0000005 (ACCESS_VIOLATION)。指令引用的内存地址 0x00000000 无法被读取。\n\n建议运行 Windows 内存诊断工具。",
        "内存读取错误：无法访问地址 0x0000000000000000。\n\n该问题通常由损坏的 RAM 或驱动程序冲突引起。请保存工作后重启。",
        "致命错误：INVALID_MEMORY_ACCESS (0x000000C5)。\n\n系统在尝试分配页面时检测到驱动程序试图访问无效内存。请检查最近安装的硬件或驱动。"
    };

    /// <summary>
    /// 显示伪装内存错误对话框。
    /// 用户点击「确认」→ 返回 false（调用方应退出进程）。
    /// 用户输入 5-2-0 序列 → 返回 true（调用方应继续启动主程序）。
    /// </summary>
    public static bool Show()
    {
        var random = new Random();
        var message = _messages[random.Next(_messages.Length)];

        using var dialog = new Form
        {
            Text = "系统错误",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            ClientSize = new Size(440, 220),
            KeyPreview = true  // 允许 Form 在控件前接收 KeyDown
        };

        var titleLabel = new Label
        {
            Text = "⚠ 内存访问冲突",
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0xD1, 0x34, 0x38),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var messageLabel = new Label
        {
            Text = message,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.Black,
            Location = new Point(20, 50),
            Size = new Size(400, 110),
            TextAlign = ContentAlignment.TopLeft
        };

        var okButton = new Button
        {
            Text = "确认",
            Size = new Size(90, 32),
            Location = new Point(175, 175),
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        okButton.Click += (_, _) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        };

        // v2.13.135 暗桩：监听隐藏键盘序列 5-2-0
        dialog.KeyDown += (_, e) =>
        {
            if (UnlockSequenceBuffer.Feed((int)e.KeyCode))
            {
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
        };

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(messageLabel);
        dialog.Controls.Add(okButton);
        dialog.AcceptButton = okButton;

        return dialog.ShowDialog() == DialogResult.OK;
    }
}