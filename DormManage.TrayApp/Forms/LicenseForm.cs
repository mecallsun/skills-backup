using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DormManage.Shared.Register;
using DormManage.Shared.Security;

namespace DormManage.TrayApp.Forms;

/// <summary>
/// v2.13.94 软件注册授权弹窗（托盘端）
/// 仿照原 NPGS.Register 项目 RegisterForm.cs 设计：
/// - 5 个字段：机器码（只读）+ 公司名称 + 注册码 + 注册状态 + 有效期
/// - 3 个按钮：注册/取消注册 + 关闭 + 清理
/// - 调用 DormManage.Shared.Register.RegisterSdk（与 Web 端共用同一算法）
/// - 数据存储：HKLM\Software\JINGE\DormManage\License（管理员权限）/ HKCU / 文件兜底
///
/// v2.13.142 机器码显示规则（用户原话 + NPGS.Register 规范验证）：
///   - 机器码 = 24 位 hex 连续显示，**禁止任何分隔符**（无连字符 - / 无空格 / 无下划线）
///   - 反例（错误）：078BF-BFF00-000F6-13C81-B56E（带连字符）
///   - 正例（正确）：078BFBFF00000F613C81B56E（连续 24 hex）
///   - 设计依据：
///     ① NPGS.Register 规范 §机器码生成规则"不得加入横线/格式化字符到机器码本身 —— 仅展示时格式化"
///     ② NPGS.Register RegisterForm.cs:62 `this.textSN.Text = SN` 直接显示 raw（无格式化）
///     ③ 用户 2026-07-24 明确指示「机器码显示没有连接符」
///   - 复制按钮：复制 raw 24 hex 给供应商生成 CDKEY
/// </summary>
public sealed class LicenseForm : Form
{
    // 字段
    private Label label1 = null!;             // 机器码
    private Label label_CDKEY = null!;        // 注册码
    private Label label3 = null!;             // 单位名称
    private Label label_Date = null!;         // 有效期
    private Label label_DateValue = null!;    // 有效期值
    private Label labRegInfo = null!;         // 注册状态
    private TextBox textSN = null!;           // 机器码值（24 位连续 hex 无分隔符）
    private TextBox text_CDKEY = null!;       // 注册码值（25 位连续 hex + 自动 5-5-5-5-5 分组）
    private TextBox textLTD = null!;          // 公司名称值
    private Label label_Trial = null!;        // 试用次数
    private Button btnReg = null!;            // 注册/取消注册
    private Button btnClear = null!;          // 清理
    private Button btnClose = null!;          // 关闭

    /// <summary>v2.13.142：缓存 raw 24 hex 机器码（用于 RegisterSdk.CheckRegCDKey 校验 + 复制按钮）</summary>
    private string _rawSN = string.Empty;

    public LicenseForm()
    {
        InitializeComponent();
        LoadRegState();
    }

    private void InitializeComponent()
    {
        this.Text = "软件注册授权 - 金戈宿舍管理系统";
        this.ClientSize = new Size(620, 360);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Microsoft YaHei UI", 9F);

        int x = 50, y = 20, w = 460, h = 25, gap = 38;

        // 1) 机器码（v2.13.142：24 位连续 hex 禁止任何分隔符）
        label1 = new Label { Text = "机器码（24 位）:", Location = new Point(x, y + 3), AutoSize = true };
        textSN = new TextBox { Location = new Point(x + 90, y), Size = new Size(w, h), ReadOnly = true, Font = new Font("Consolas", 9F) };
        var btnCopy = new Button { Text = "📋 复制", Location = new Point(x + 90 + w + 8, y - 1), Size = new Size(56, h + 4) };
        btnCopy.Click += (_, _) =>
        {
            try
            {
                // v2.13.142：复制原始 24 位 hex（供应商生成 CDKEY 必须用 raw 格式，无任何分隔符）
                Clipboard.SetText(_rawSN);
                MessageBox.Show($"机器码已复制到剪贴板（24 位原始 hex 无分隔符）：\n\n{_rawSN}\n\n请将此码发给软件供应商获取注册码。", "提示");
            }
            catch (Exception ex) { MessageBox.Show($"复制失败：{ex.Message}", "错误"); }
        };
        y += gap;

        // 2) 公司名称
        label3 = new Label { Text = "公司名称：", Location = new Point(x, y + 3), AutoSize = true };
        textLTD = new TextBox { Location = new Point(x + 90, y), Size = new Size(w, h), MaxLength = 100 };
        y += gap;

        // 3) 注册码（25 位 hex + 自动 5-5-5-5-5 视觉分组 → 实际可输入 29 字符含分隔符）
        label_CDKEY = new Label { Text = "注册码：", Location = new Point(x, y + 3), AutoSize = true };
        text_CDKEY = new TextBox { Location = new Point(x + 90, y), Size = new Size(w, h), MaxLength = 29, CharacterCasing = CharacterCasing.Upper, Font = new Font("Consolas", 9F) };
        text_CDKEY.KeyPress += Text_CDKEY_KeyPress;
        y += gap;

        // 4) 有效期
        label_Date = new Label { Text = "有效日期：", Location = new Point(x, y + 3), AutoSize = true };
        label_DateValue = new Label { Text = "—", Location = new Point(x + 60, y + 3), AutoSize = true, ForeColor = Color.Green, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold) };
        y += gap;

        // 5) 注册状态
        labRegInfo = new Label { Text = "未注册", Location = new Point(x + 60, y + 3), AutoSize = true, ForeColor = Color.Red, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };

        // 6) 试用次数
        label_Trial = new Label { Text = "", Location = new Point(x + 200, y + 3), AutoSize = true, ForeColor = Color.Gray };

        // 按钮
        btnReg = new Button { Text = "注册", Location = new Point(80, 240), Size = new Size(95, 32), BackColor = Color.FromArgb(25, 118, 210), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnReg.FlatAppearance.BorderSize = 0;
        btnReg.Click += BtnReg_Click;

        btnClear = new Button { Text = "清理", Location = new Point(245, 240), Size = new Size(95, 32) };
        btnClear.Click += BtnClear_Click;

        btnClose = new Button { Text = "关闭", Location = new Point(410, 240), Size = new Size(95, 32) };
        btnClose.Click += (_, _) => this.Close();

        // 控制
        this.Controls.Add(label1);
        this.Controls.Add(textSN);
        this.Controls.Add(btnCopy);
        this.Controls.Add(label3);
        this.Controls.Add(textLTD);
        this.Controls.Add(label_CDKEY);
        this.Controls.Add(text_CDKEY);
        this.Controls.Add(label_Date);
        this.Controls.Add(label_DateValue);
        this.Controls.Add(labRegInfo);
        this.Controls.Add(label_Trial);
        this.Controls.Add(btnReg);
        this.Controls.Add(btnClear);
        this.Controls.Add(btnClose);
    }

    /// <summary>注册码自动加分隔符（每 5 字符加 -，仅 UI 装饰，业务 raw 校验在 RegisterSdk 内完成）</summary>
    private void Text_CDKEY_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)8 || e.KeyChar == (char)46) return;  // 退格/删除
        var raw = text_CDKEY.Text.Replace("-", "");
        if (raw.Length > 0 && raw.Length % 5 == 0 && !text_CDKEY.Text.EndsWith("-"))
        {
            text_CDKEY.Text += "-";
            text_CDKEY.SelectionStart = text_CDKEY.Text.Length;
        }
    }

    /// <summary>注册码 raw 25 位校验（提取用户输入的 25 位 hex，校验格式：20+5 = 25 位大写 hex）</summary>
    private static bool TryNormalizeCDKey(string input, out string raw25, out string err)
    {
        raw25 = "";
        err = "";
        var cleaned = (input ?? "").Replace("-", "").Trim().ToUpperInvariant();
        if (cleaned.Length != 25)
        {
            err = $"注册码长度错误（应有 25 位 hex 实际 {cleaned.Length} 位）";
            return false;
        }
        foreach (var c in cleaned)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
            {
                err = $"注册码包含非法字符 '{c}'（仅允许 0-9 A-F）";
                return false;
            }
        }
        raw25 = cleaned;
        return true;
    }

    /// <summary>读取并展示注册状态</summary>
    private void LoadRegState()
    {
        var reg = RegisterSdk.CheckReg();
        // v2.13.142：RegisterSdk.GetSN() 返回 raw 24 hex；
        // 用户规则：机器码显示**禁止任何分隔符**（用户原话"机器码显示没有连接符"）
        // - 直接显示 raw 24 位连续 hex（参考 NPGS.Register RegisterForm.cs:62 `textSN.Text = SN`）
        // - 不调用任何 FormatCDKeyStyle/FormatMachineCodeDisplay 等格式化函数
        _rawSN = reg.SN ?? string.Empty;
        textSN.Text = _rawSN;  // raw 24 位连续大写 hex，无连字符
        text_CDKEY.Text = reg.CDKEY;
        textLTD.Text = reg.LTDName;
        label_Trial.Text = $"试用次数：{reg.UseTimes} / {RegisterSdk.TRIAL_LIMIT}";

        switch (reg.RegInt)
        {
            case 1:
                labRegInfo.Text = "✅ 此软件已注册";
                labRegInfo.ForeColor = Color.Green;
                btnReg.Text = "取消注册";
                text_CDKEY.ReadOnly = true;
                textLTD.ReadOnly = true;
                label_DateValue.Text = reg.RegDate?.ToString("yyyy年MM月dd日") ?? "—";
                if (reg.RegDate.HasValue)
                    label_Trial.Text += $"（剩 {(reg.RegDate.Value.Date - DateTime.Today).Days} 天）";
                break;
            case 0:
                labRegInfo.Text = "⚠️ 许可已过期";
                labRegInfo.ForeColor = Color.OrangeRed;
                btnReg.Text = "重新注册";
                text_CDKEY.ReadOnly = false;
                textLTD.ReadOnly = false;
                label_DateValue.Text = reg.RegDate?.ToString("yyyy年MM月dd日") ?? "—";
                break;
            default:
                labRegInfo.Text = "❌ 此软件尚未注册";
                labRegInfo.ForeColor = Color.Red;
                btnReg.Text = "注册";
                text_CDKEY.ReadOnly = false;
                textLTD.ReadOnly = false;
                label_DateValue.Text = "—";
                break;
        }
    }

    /// <summary>注册 / 取消注册</summary>
    private void BtnReg_Click(object? sender, EventArgs e)
    {
        if (btnReg.Text != "注册" && btnReg.Text != "重新注册")
        {
            // 取消注册
            var r = MessageBox.Show("确认取消注册？取消后需重新输入注册码。", "取消注册确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            if (RegisterSdk.DeleteRegItem())
            {
                LicenseGuard.ResetCache();  // v2.13.136 写入新状态后重置共享缓存
                MessageBox.Show("注册信息已取消！", "提示");
            }
            else
                MessageBox.Show("取消失败（请以管理员身份运行）", "错误");
            LoadRegState();
            return;
        }

        // 注册（v2.13.142：注册码 25 hex = 20 验证段 + 5 日期段；UI 允许 5-5-5-5-5 带连字符形式，业务 raw 25）
        if (!TryNormalizeCDKey(text_CDKEY.Text, out var cdkey, out var cdErr))
        {
            MessageBox.Show($"注册码格式错误：{cdErr}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(textLTD.Text))
        {
            MessageBox.Show("请输入公司/单位名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrEmpty(_rawSN) || _rawSN.Length != 24)
        {
            MessageBox.Show($"机器码格式错误：'{_rawSN}'（应 24 位连续 hex，无任何分隔符）。请重启托盘程序重新获取机器码。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var check = RegisterSdk.CheckRegCDKey(new RegItem { CDKEY = cdkey, SN = _rawSN, LTDName = textLTD.Text.Trim() });
        if (check.RegInt != 1)
        {
            MessageBox.Show("注册码校验失败！请核对机器码和公司名称是否匹配。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (check.RegDate < DateTime.Today)
        {
            MessageBox.Show($"注册码已过期！有效期至 {check.RegDate:yyyy-MM-dd}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var reg = new RegItem
        {
            SN = _rawSN,
            CDKEY = cdkey,  // v2.13.142：写入注册表使用 raw 25（不存带连字符的 display）
            LTDName = textLTD.Text.Trim(),
            RegDate = check.RegDate,
            RegInt = 1
        };
        if (RegisterSdk.WriteRegItem(reg))
        {
            LicenseGuard.ResetCache();  // v2.13.136 写入新状态后重置共享缓存
            MessageBox.Show($"🎉 注册成功！有效期至 {check.RegDate:yyyy年MM月dd日}", "注册成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
            MessageBox.Show("注册验证通过，但写入失败（请以管理员身份运行）", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        LoadRegState();
    }

    /// <summary>清理所有注册信息（含试用次数）</summary>
    private void BtnClear_Click(object? sender, EventArgs e)
    {
        var r = MessageBox.Show("确认清除所有注册信息（含试用次数）？此操作不可恢复！", "清理确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        RegisterSdk.DeleteRegAll();
        MessageBox.Show("注册信息已清除", "提示");
        LoadRegState();
    }
}