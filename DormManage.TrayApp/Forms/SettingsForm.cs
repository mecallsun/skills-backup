using DormManage.TrayApp.Models;
using DormManage.TrayApp.Services;

namespace DormManage.TrayApp.Forms;

/// <summary>
/// 配置窗口：核心服务端参数（PDA/Web 端口、数据库、图片路径）+ 服务启停 + 保存。
///
/// 字段与需求规格 57 §3.2.1 一一对应。
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly ProcessManager _process;
    private readonly HealthChecker _health;

    private readonly NumericUpDown _numApiPort;
    private readonly NumericUpDown _numAdminPort;
    private readonly TextBox _txtApiPath;
    private readonly Button _btnApiBrowse;
    private readonly TextBox _txtAdminPath;
    private readonly Button _btnAdminBrowse;
    private readonly ComboBox _cmbProvider;
    private readonly TextBox _txtConnStr;
    private readonly TextBox _txtSqlitePath;
    private readonly Button _btnSqliteBrowse;
    private readonly TextBox _txtImageRoot;
    private readonly Button _btnImageBrowse;
    private readonly CheckBox _chkAutoStart;
    private readonly CheckBox _chkAutoRestart;
    private readonly NumericUpDown _numHealthInterval;
    private readonly Label _lblApiStatus;
    private readonly Label _lblAdminStatus;
    private readonly System.Windows.Forms.Timer _statusTimer;

    public SettingsForm(ConfigService config, LogService log, ProcessManager process, HealthChecker health)
    {
        _config = config;
        _log = log;
        _process = process;
        _health = health;

        Text = "金戈宿舍管理系统 — 托盘设置";
        Size = new Size(620, 580);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        // ===== 控件初始化 =====
        _numApiPort = new NumericUpDown { Minimum = 1024, Maximum = 65535 };
        _numAdminPort = new NumericUpDown { Minimum = 1024, Maximum = 65535 };
        _txtApiPath = new TextBox();
        _btnApiBrowse = new Button { Text = "浏览..." };
        _txtAdminPath = new TextBox();
        _btnAdminBrowse = new Button { Text = "浏览..." };
        _cmbProvider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbProvider.Items.AddRange(new object[] { "SqlServer", "Sqlite" });
        _txtConnStr = new TextBox();
        _txtSqlitePath = new TextBox();
        _btnSqliteBrowse = new Button { Text = "浏览..." };
        _txtImageRoot = new TextBox();
        _btnImageBrowse = new Button { Text = "浏览..." };
        _chkAutoStart = new CheckBox();
        _chkAutoRestart = new CheckBox();
        _numHealthInterval = new NumericUpDown { Minimum = 5, Maximum = 300 };
        _lblApiStatus = new Label { Text = "Api：--", AutoSize = true };
        _lblAdminStatus = new Label { Text = "Admin：--", AutoSize = true };

        // ===== 布局 =====
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 0,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        AddRow(layout, "服务端口", BuildPortPanel());
        AddRow(layout, "Api 可执行文件", BuildPathRow(_txtApiPath, _btnApiBrowse, BrowseExe));
        AddRow(layout, "Admin 可执行文件", BuildPathRow(_txtAdminPath, _btnAdminBrowse, BrowseExe));
        AddRow(layout, "数据库类型", _cmbProvider);

        var connStrPanel = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, AutoSize = true };
        connStrPanel.Controls.Add(BuildLabeledRow("SQL Server 连接串", _txtConnStr, null));
        connStrPanel.Controls.Add(BuildLabeledRow("SQLite 数据库路径", _txtSqlitePath, _btnSqliteBrowse));
        AddRow(layout, "数据库连接", connStrPanel);

        AddRow(layout, "图片存储根路径", BuildPathRow(_txtImageRoot, _btnImageBrowse, BrowseFolder));
        AddRow(layout, "启动时自动启动服务", _chkAutoStart);
        AddRow(layout, "异常时自动重启", _chkAutoRestart);
        AddRow(layout, "健康检查间隔（秒）", _numHealthInterval);

        var statusPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        statusPanel.Controls.Add(_lblApiStatus);
        statusPanel.Controls.Add(new Label { Text = "    " });
        statusPanel.Controls.Add(_lblAdminStatus);
        AddRow(layout, "服务状态", statusPanel);

        // 操作按钮
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var btnCancel = new Button { Text = "取消", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
        var btnSave = new Button { Text = "保存", Size = new Size(90, 32), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnSave.Click += async (_, _) => await BtnSaveAsync();
        var btnRestart = new Button { Text = "重启", Size = new Size(90, 32) };
        btnRestart.Click += async (_, _) => await OnRestartClick();
        var btnStop = new Button { Text = "停止", Size = new Size(90, 32) };
        btnStop.Click += async (_, _) => await OnStopClick();
        var btnStart = new Button { Text = "启动", Size = new Size(90, 32), BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnStart.Click += async (_, _) => await OnStartClick();

        btnPanel.Controls.AddRange(new Control[] { btnCancel, btnSave, btnRestart, btnStop, btnStart });

        Controls.Add(layout);
        Controls.Add(btnPanel);

        // 加载当前配置
        LoadConfig();

        // Provider 切换时显隐连接串/SQLite 路径
        _cmbProvider.SelectedIndexChanged += (_, _) => UpdateProviderVisibility();
        UpdateProviderVisibility();

        // 状态定时刷新
        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();

        CancelButton = btnCancel;
    }

    private void AddRow(TableLayoutPanel layout, string labelText, Control inputControl)
    {
        var rowIndex = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lbl = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false
        };
        layout.Controls.Add(lbl, 0, rowIndex);
        layout.Controls.Add(inputControl, 1, rowIndex);
        layout.SetColumnSpan(inputControl, 2);
        lbl.Height = Math.Max(28, inputControl.PreferredSize.Height + 4);
    }

    private Control BuildPortPanel()
    {
        var p = new TableLayoutPanel { ColumnCount = 4, Dock = DockStyle.Fill, AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        p.Controls.Add(new Label { Text = "Api 端口：", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, AutoSize = false }, 0, 0);
        p.Controls.Add(_numApiPort, 1, 0);
        p.Controls.Add(new Label { Text = "Admin 端口：", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, AutoSize = false }, 2, 0);
        p.Controls.Add(_numAdminPort, 3, 0);

        _numApiPort.Dock = DockStyle.Fill;
        _numAdminPort.Dock = DockStyle.Fill;
        return p;
    }

    private Control BuildPathRow(TextBox textBox, Button browseButton, Action browseAction)
    {
        var p = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        textBox.Dock = DockStyle.Fill;
        browseButton.Dock = DockStyle.Fill;
        browseButton.Click += (_, _) => browseAction();
        p.Controls.Add(textBox, 0, 0);
        p.Controls.Add(browseButton, 1, 0);
        return p;
    }

    private Control BuildLabeledRow(string label, TextBox textBox, Button? browseButton)
    {
        var p = new TableLayoutPanel { ColumnCount = browseButton is null ? 2 : 3, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (browseButton is not null) p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        var lbl = new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        textBox.Dock = DockStyle.Fill;
        p.Controls.Add(lbl, 0, 0);
        p.Controls.Add(textBox, 1, 0);
        if (browseButton is not null)
        {
            browseButton.Dock = DockStyle.Fill;
            p.Controls.Add(browseButton, 2, 0);
        }
        return p;
    }

    private void LoadConfig()
    {
        var c = _config.Current;
        _numApiPort.Value = Math.Clamp(c.Tray.ApiPort, (int)_numApiPort.Minimum, (int)_numApiPort.Maximum);
        _numAdminPort.Value = Math.Clamp(c.Tray.AdminPort, (int)_numAdminPort.Minimum, (int)_numAdminPort.Maximum);
        _txtApiPath.Text = c.Tray.ApiExecutable;
        _txtAdminPath.Text = c.Tray.AdminExecutable;
        _cmbProvider.SelectedItem = c.Database.Provider;
        _txtConnStr.Text = c.Database.ConnectionString;
        _txtSqlitePath.Text = c.Database.SqlitePath;
        _txtImageRoot.Text = c.Storage.ImageRoot;
        _chkAutoStart.Checked = c.Tray.AutoStartServices;
        _chkAutoRestart.Checked = c.Tray.AutoRestartOnCrash;
        _numHealthInterval.Value = Math.Clamp(c.Tray.HealthCheckIntervalSeconds, (int)_numHealthInterval.Minimum, (int)_numHealthInterval.Maximum);
    }

    private void UpdateProviderVisibility()
    {
        var isSqlite = _cmbProvider.SelectedItem?.ToString() == "Sqlite";
        _txtConnStr.Enabled = !isSqlite;
        _txtSqlitePath.Enabled = isSqlite;
        _btnSqliteBrowse.Enabled = isSqlite;
    }

    private void RefreshStatus()
    {
        var apiHealth = _health.LastApiHealth;
        var adminHealth = _health.LastAdminHealth;
        _lblApiStatus.Text = $"Api：{StateBadge(_health.ApiState)}{(apiHealth is null ? "" : $" {apiHealth.Detail}")}";
        _lblApiStatus.ForeColor = StateColor(_health.ApiState);
        _lblAdminStatus.Text = $"Admin：{StateBadge(_health.AdminState)}{(adminHealth is null ? "" : $" {adminHealth.Detail}")}";
        _lblAdminStatus.ForeColor = StateColor(_health.AdminState);
    }

    private static string StateBadge(ServiceState s) => s switch
    {
        ServiceState.Running => "●",
        ServiceState.Starting => "◐",
        ServiceState.Stopping => "◐",
        ServiceState.Stopped => "○",
        ServiceState.Crashed => "✕",
        _ => "?"
    };

    private static Color StateColor(ServiceState s) => s switch
    {
        ServiceState.Running => Color.FromArgb(40, 167, 69),
        ServiceState.Starting => Color.FromArgb(255, 193, 7),
        ServiceState.Stopping => Color.Gray,
        ServiceState.Stopped => Color.Gray,
        ServiceState.Crashed => Color.FromArgb(220, 53, 69),
        _ => Color.Black
    };

    private void BrowseExe()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            InitialDirectory = AppContext.BaseDirectory
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            // 转为相对路径（若在 BaseDirectory 下）
            var baseDir = AppContext.BaseDirectory.TrimEnd('\\');
            var full = Path.GetFullPath(dlg.FileName);
            var relative = ToRelativeIfUnderBase(full, baseDir);
            if (_txtApiPath.Focused)
                _txtApiPath.Text = relative;
            else
                _txtAdminPath.Text = relative;
        }
    }

    private static string ToRelativeIfUnderBase(string full, string baseDir)
    {
        if (full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            return full.Substring(baseDir.Length).TrimStart('\\');
        return full;
    }

    private void BrowseFolder()
    {
        // WinForms 没有内置 FolderBrowserDialog 的现代替代品，使用 SaveFileDialog 提示目录
        using var dlg = new FolderBrowserDialog { SelectedPath = AppContext.BaseDirectory };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _txtImageRoot.Text = ToRelativeIfUnderBase(dlg.SelectedPath, AppContext.BaseDirectory.TrimEnd('\\'));
        }
    }

    private async Task BtnSaveAsync()
    {
        // 校验
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, _txtApiPath.Text)))
        {
            MessageBox.Show("Api 可执行文件不存在", "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, _txtAdminPath.Text)))
        {
            MessageBox.Show("Admin 可执行文件不存在", "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_cmbProvider.SelectedItem?.ToString() == "SqlServer" && string.IsNullOrWhiteSpace(_txtConnStr.Text))
        {
            MessageBox.Show("请填写 SQL Server 连接串", "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_cmbProvider.SelectedItem?.ToString() == "Sqlite" && string.IsNullOrWhiteSpace(_txtSqlitePath.Text))
        {
            MessageBox.Show("请填写 SQLite 数据库路径", "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var newConfig = new AppConfig
        {
            Tray = new TraySection
            {
                ApiPort = (int)_numApiPort.Value,
                AdminPort = (int)_numAdminPort.Value,
                ApiExecutable = _txtApiPath.Text,
                AdminExecutable = _txtAdminPath.Text,
                AutoStartServices = _chkAutoStart.Checked,
                AutoRestartOnCrash = _chkAutoRestart.Checked,
                HealthCheckIntervalSeconds = (int)_numHealthInterval.Value
            },
            Database = new DatabaseSection
            {
                Provider = _cmbProvider.SelectedItem?.ToString() ?? "SqlServer",
                ConnectionString = _txtConnStr.Text,
                SqlitePath = _txtSqlitePath.Text
            },
            Storage = new StorageSection
            {
                ImageRoot = _txtImageRoot.Text,
                LogRoot = _config.Current.Storage.LogRoot
            }
        };

        _config.Update(newConfig);

        var ok = MessageBox.Show(
            "配置已保存。是否立即重启服务以使端口/数据库配置生效？",
            "保存成功",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (ok == DialogResult.Yes)
        {
            try
            {
                await _process.RestartAllAsync();
            }
            catch (Exception ex)
            {
                _log.Error("保存后重启失败", ex);
                MessageBox.Show($"重启失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task OnStartClick()
    {
        try
        {
            await _process.StartAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnStopClick()
    {
        try
        {
            await _process.StopAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"停止失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnRestartClick()
    {
        try
        {
            await _process.RestartAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重启失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statusTimer.Stop();
        _statusTimer.Dispose();
        base.OnFormClosing(e);
    }
}