using DormManage.Shared.Models;
using DormManage.Shared.Services;
using DormManage.TrayApp.Models;
using DormManage.TrayApp.Services;

namespace DormManage.TrayApp.Forms;

/// <summary>
/// 系统设置窗口（托盘右键 → 系统设置...）。
///
/// 字段与按钮严格按需求规格 57 §3.2 一一对应：
/// §3.2.1 字段（11 项）
/// §3.2.2 按钮（启动/停止/重启/保存/取消/浏览）
/// §3.2.3 服务状态显示（已停止/启动中/运行中/异常）
///
/// 【v2.13.4 修复】
/// 1. 构造函数整体 try-catch，单控件失败不影响整个窗口创建；
/// 2. 拆分 BuildUI() 方法，单步失败可定位；
/// 3. 状态定时器在 ctor 末尾启动，避免半初始化窗口被 Timer 访问；
/// 4. BrowseFolder 使用 FolderBrowserDialog 兼容包（避免 WinForms 原生 FBD 在 Win11 异常）；
/// 5. 所有用户操作路径加异常保护 + 友好提示。
///
/// 【双 UI 职责划分（CLAUDE.md 强制）】
/// 托盘系统配置窗口（SettingsForm）仅保留核心服务端参数（PDA/Web 端口、数据库、图片路径、服务启停、保存），无权限控制；
/// Web 端系统设置（/Settings/*）承载全部功能（用户角色/备份恢复/系统集成/筛选缓存等），受角色权限管控。
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly ProcessManager _process;
    private readonly HealthChecker _health;

    // 端口
    private NumericUpDown _numApiPort = null!;
    private NumericUpDown _numAdminPort = null!;

    // 可执行文件
    private TextBox _txtApiPath = null!;
    private Button _btnApiBrowse = null!;
    private TextBox _txtAdminPath = null!;
    private Button _btnAdminBrowse = null!;

    // 数据库（v2.13.19：字段式输入，与 Web 端 /Settings 风格一致）
    private ComboBox _cmbProvider = null!;
    private TextBox _txtDbServer = null!;
    private NumericUpDown _numDbPort = null!;
    private TextBox _txtDbName = null!;
    private TextBox _txtDbUser = null!;
    private TextBox _txtDbPassword = null!;
    private TextBox _txtSqlitePath = null!;
    private Button _btnSqliteBrowse = null!;

    // v2.13.32-hotfix: 数据库连接测试按钮 + 测试结果标签
    private Button _btnDbTest = null!;
    private Label _lblDbTestResult = null!;

    // 存储
    private TextBox _txtImageRoot = null!;
    private Button _btnImageBrowse = null!;

    // 行为
    private CheckBox _chkAutoStart = null!;
    private CheckBox _chkAutoRestart = null!;
    private NumericUpDown _numHealthInterval = null!;

    // 状态
    private Label _lblApiStatus = null!;
    private Label _lblAdminStatus = null!;
    private System.Windows.Forms.Timer? _statusTimer;

    public SettingsForm(ConfigService config, LogService log, ProcessManager process, HealthChecker health)
    {
        _config = config;
        _log = log;
        _process = process;
        _health = health;

        try
        {
            InitializeFormProperties();
            InitializeControls();
            BuildUI();
#pragma warning disable CS4014 // 构造函数中启动异步加载，无需等待
            LoadConfigAsync();
#pragma warning restore CS4014
            UpdateProviderVisibility();
            AttachEventHandlers();
            StartStatusTimer();
        }
        catch (Exception ex)
        {
            _log.Error("SettingsForm 构造失败", ex);
            throw new InvalidOperationException(
                $"系统设置窗口初始化失败：{ex.Message}", ex);
        }
    }

    #region 初始化

    private void InitializeFormProperties()
    {
        Text = "金戈宿舍管理系统 — 系统设置";
        Size = new Size(680, 620);
        MinimumSize = new Size(620, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = true;
        KeyPreview = true;
    }

    private void InitializeControls()
    {
        _numApiPort = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = 5100 };
        _numAdminPort = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = 5001 };

        _txtApiPath = new TextBox();
        _btnApiBrowse = new Button { Text = "浏览..." };
        _txtAdminPath = new TextBox();
        _btnAdminBrowse = new Button { Text = "浏览..." };

        _cmbProvider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbProvider.Items.AddRange(new object[] { "SqlServer", "Sqlite" });

        _txtDbServer = new TextBox { Text = "192.168.1.237" };
        _numDbPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 1433 };
        _txtDbName = new TextBox { Text = "WaterMeterDB" };
        _txtDbUser = new TextBox { Text = "__DB_USER__" };
        _txtDbPassword = new TextBox { Text = "__DB_PASSWORD__", PasswordChar = '*' };
        _txtSqlitePath = new TextBox();
        _btnSqliteBrowse = new Button { Text = "浏览..." };

        // v2.13.32-hotfix: 测试连接按钮（无破坏性，仅测试当前填写的字段，不保存任何东西）
        _btnDbTest = new Button { Text = "测试连接", Size = new Size(90, 32) };
        _lblDbTestResult = new Label { Text = "", AutoSize = true, ForeColor = Color.Gray };

        _txtImageRoot = new TextBox();
        _btnImageBrowse = new Button { Text = "浏览..." };

        _chkAutoStart = new CheckBox { Text = "托盘启动后自动拉起 Api + Admin" };
        _chkAutoRestart = new CheckBox { Text = "子进程异常退出时自动重启" };

        _numHealthInterval = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 10 };

        _lblApiStatus = new Label { Text = "Api：--", AutoSize = true };
        _lblAdminStatus = new Label { Text = "Admin：--", AutoSize = true };
    }

    private void BuildUI()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(14),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddLabeledRow(layout, "服务端口", BuildPortPanel(), rowHeight: 32);
        AddLabeledRow(layout, "Api 可执行文件", BuildPathRow(_txtApiPath, _btnApiBrowse, () => BrowseExe(_txtApiPath)));
        AddLabeledRow(layout, "Admin 可执行文件", BuildPathRow(_txtAdminPath, _btnAdminBrowse, () => BrowseExe(_txtAdminPath)));
        AddLabeledRow(layout, "数据库类型", _cmbProvider);
        AddLabeledRow(layout, "数据库服务器", _txtDbServer);
        AddLabeledRow(layout, "端口号", _numDbPort);
        AddLabeledRow(layout, "数据库名称", _txtDbName);
        AddLabeledRow(layout, "账号", _txtDbUser);
        AddLabeledRow(layout, "密码", _txtDbPassword);
        AddLabeledRow(layout, "SQLite 数据库路径", BuildSqlitePanel());
        // v2.13.32-hotfix: 在数据库字段下方加"测试连接"行（按钮 + 结果标签）
        AddLabeledRow(layout, "", BuildDbTestRow());
        AddLabeledRow(layout, "图片存储根路径", BuildPathRow(_txtImageRoot, _btnImageBrowse, BrowseImageFolder));
        AddLabeledRow(layout, "启动时自动启动服务", _chkAutoStart);
        AddLabeledRow(layout, "异常时自动重启", _chkAutoRestart);
        AddLabeledRow(layout, "健康检查间隔（秒）", _numHealthInterval);
        AddLabeledRow(layout, "服务状态", BuildStatusPanel());

        // 底部按钮区
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = Color.FromArgb(248, 248, 250)
        };

        var btnCancel = new Button { Text = "取消", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
        var btnSave = new Button
        {
            Text = "保存",
            Size = new Size(90, 32),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.Click += async (_, _) => await BtnSaveAsync();

        var btnRestart = new Button { Text = "重启", Size = new Size(90, 32) };
        btnRestart.Click += async (_, _) => await OnRestartClick();

        var btnStop = new Button { Text = "停止", Size = new Size(90, 32) };
        btnStop.Click += async (_, _) => await OnStopClick();

        var btnStart = new Button
        {
            Text = "启动",
            Size = new Size(90, 32),
            BackColor = Color.FromArgb(40, 167, 69),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnStart.Click += async (_, _) => await OnStartClick();

        btnPanel.Controls.AddRange(new Control[] { btnCancel, btnSave, btnRestart, btnStop, btnStart });

        // 顶部标题区
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(0, 122, 204),
            Padding = new Padding(14, 0, 14, 0)
        };
        var lblTitle = new Label
        {
            Text = "⚙  系统设置 — 核心服务端参数",
            Font = new Font(SafeMenuFont(), FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
        header.Controls.Add(lblTitle);

        // 注意顺序：先 Add(header) 后 Add(btnPanel) 后 Add(layout)
        // Dock=Top 与 Dock=Bottom 会按添加顺序布局
        Controls.Add(layout);
        Controls.Add(btnPanel);
        Controls.Add(header);

        CancelButton = btnCancel;
        AcceptButton = btnSave;
    }

    private void AttachEventHandlers()
    {
        _btnApiBrowse.Click += (_, _) => BrowseExe(_txtApiPath);
        _btnAdminBrowse.Click += (_, _) => BrowseExe(_txtAdminPath);
        _btnSqliteBrowse.Click += (_, _) => BrowseSqliteFile();
        _btnImageBrowse.Click += (_, _) => BrowseImageFolder();
        _btnDbTest.Click += async (_, _) => await BtnDbTestAsync();  // v2.13.32-hotfix
        _cmbProvider.SelectedIndexChanged += (_, _) => UpdateProviderVisibility();
    }

    private void StartStatusTimer()
    {
        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
    }

    private void AddLabeledRow(TableLayoutPanel layout, string labelText, Control inputControl, int? rowHeight = null)
    {
        var rowIndex = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lbl = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Font = SafeMenuFont()
        };
        layout.Controls.Add(lbl, 0, rowIndex);
        layout.Controls.Add(inputControl, 1, rowIndex);

        if (rowHeight.HasValue)
            lbl.Height = rowHeight.Value;
        else
            lbl.Height = Math.Max(28, inputControl.PreferredSize.Height + 6);
    }

    private Control BuildPortPanel()
    {
        var p = new TableLayoutPanel { ColumnCount = 4, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
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
        var p = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        textBox.Dock = DockStyle.Fill;
        browseButton.Dock = DockStyle.Fill;
        browseButton.Click += (_, _) =>
        {
            try { browseAction(); }
            catch (Exception ex) { ShowError($"浏览失败：{ex.Message}"); }
        };
        p.Controls.Add(textBox, 0, 0);
        p.Controls.Add(browseButton, 1, 0);
        return p;
    }

    private Control BuildSqlitePanel()
    {
        return BuildPathRow(_txtSqlitePath, _btnSqliteBrowse, BrowseSqliteFile);
    }

    private Control BuildStatusPanel()
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        p.Controls.Add(_lblApiStatus);
        p.Controls.Add(new Label { Text = "    ", AutoSize = true });
        p.Controls.Add(_lblAdminStatus);
        return p;
    }

    // v2.13.32-hotfix: 测试连接按钮行（按钮 + 实时结果标签）
    private Control BuildDbTestRow()
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false
        };
        p.Controls.Add(_btnDbTest);
        p.Controls.Add(new Label { Text = "  ", AutoSize = true });
        p.Controls.Add(_lblDbTestResult);
        return p;
    }

    #endregion

    #region 配置加载与保存

    private async Task LoadConfigAsync()
    {
        var c = _config.Current;
        _numApiPort.Value = SafeClamp(c.Tray.ApiPort, (int)_numApiPort.Minimum, (int)_numApiPort.Maximum);
        _numAdminPort.Value = SafeClamp(c.Tray.AdminPort, (int)_numAdminPort.Minimum, (int)_numAdminPort.Maximum);
        _txtApiPath.Text = c.Tray.ApiExecutable ?? "";
        _txtAdminPath.Text = c.Tray.AdminExecutable ?? "";
        _txtImageRoot.Text = c.Storage.ImageRoot ?? "";
        _chkAutoStart.Checked = c.Tray.AutoStartServices;
        _chkAutoRestart.Checked = c.Tray.AutoRestartOnCrash;
        _numHealthInterval.Value = SafeClamp(c.Tray.HealthCheckIntervalSeconds, (int)_numHealthInterval.Minimum, (int)_numHealthInterval.Maximum);

        // v2.13.19：从 AppConfigManager 读取字段式数据库配置
        var dbConfig = await AppConfigManager.Instance.LoadAsync();
        if (dbConfig is not null)
        {
            _cmbProvider.SelectedItem = dbConfig.Provider;
            if (_cmbProvider.SelectedIndex < 0) _cmbProvider.SelectedIndex = 0;
            _txtDbServer.Text = dbConfig.DbServer;
            _numDbPort.Value = SafeClamp(dbConfig.DbPort, (int)_numDbPort.Minimum, (int)_numDbPort.Maximum);
            _txtDbName.Text = dbConfig.DbName;
            _txtDbUser.Text = dbConfig.DbUser;
            _txtDbPassword.Text = "";
            _txtSqlitePath.Text = dbConfig.SqlitePath ?? "";
        }
        else
        {
            _cmbProvider.SelectedIndex = 0;
        }
    }

    private void UpdateProviderVisibility()
    {
        try
        {
            var isSqlite = _cmbProvider.SelectedItem?.ToString() == "Sqlite";
            _txtDbServer.Enabled = !isSqlite;
            _numDbPort.Enabled = !isSqlite;
            _txtDbName.Enabled = !isSqlite;
            _txtDbUser.Enabled = !isSqlite;
            _txtDbPassword.Enabled = !isSqlite;
            _txtSqlitePath.Enabled = isSqlite;
            _btnSqliteBrowse.Enabled = isSqlite;
        }
        catch
        {
            // Provider 切换异常不应阻塞窗口
        }
    }

    private void RefreshStatus()
    {
        try
        {
            var apiHealth = _health.LastApiHealth;
            var adminHealth = _health.LastAdminHealth;
            _lblApiStatus.Text = $"Api：{StateBadge(_health.ApiState)}{(apiHealth is null ? "" : $" {apiHealth.Detail}")}";
            _lblApiStatus.ForeColor = StateColor(_health.ApiState);
            _lblAdminStatus.Text = $"Admin：{StateBadge(_health.AdminState)}{(adminHealth is null ? "" : $" {adminHealth.Detail}")}";
            _lblAdminStatus.ForeColor = StateColor(_health.AdminState);
        }
        catch
        {
            // 状态刷新失败时静默，不影响其他 UI
        }
    }

    // v2.13.32-hotfix: 测试连接按钮（不保存任何东西，仅验证当前填写的字段）
    // 密码留空时使用哨兵 "unchanged"，由 AppConfigManager.ResolveUnchangedPasswordAsync 替换为旧密码
    private async Task BtnDbTestAsync()
    {
        try
        {
            _btnDbTest.Enabled = false;
            _lblDbTestResult.Text = "正在测试...";
            _lblDbTestResult.ForeColor = Color.Gray;

            var provider = _cmbProvider.SelectedItem?.ToString() ?? "SqlServer";
            var dbDto = new DatabaseConfigDto
            {
                Provider = provider,
                DbServer = _txtDbServer.Text.Trim(),
                DbPort = (int)_numDbPort.Value,
                DbName = _txtDbName.Text.Trim(),
                DbUser = _txtDbUser.Text.Trim(),
                DbPassword = string.IsNullOrEmpty(_txtDbPassword.Text) ? "unchanged" : _txtDbPassword.Text,
                SqlitePath = _txtSqlitePath.Text.Trim()
            };

            var (ok, msg) = await AppConfigManager.Instance.TestDbConnectionAsync(dbDto);
            if (ok)
            {
                _lblDbTestResult.Text = "✓ " + msg;
                _lblDbTestResult.ForeColor = Color.FromArgb(40, 167, 69);
                _log.Info($"[DB-TEST] 连接成功：Provider={provider}, Server={dbDto.DbServer}, Db={dbDto.DbName}");
            }
            else
            {
                _lblDbTestResult.Text = "✕ " + msg;
                _lblDbTestResult.ForeColor = Color.FromArgb(220, 53, 69);
                _log.Warn($"[DB-TEST] 连接失败：Provider={provider}, Server={dbDto.DbServer}, Db={dbDto.DbName}, Msg={msg}");
            }
        }
        catch (Exception ex)
        {
            _lblDbTestResult.Text = "✕ " + ex.Message;
            _lblDbTestResult.ForeColor = Color.FromArgb(220, 53, 69);
            _log.Error("[DB-TEST] 异常", ex);
        }
        finally
        {
            _btnDbTest.Enabled = true;
        }
    }

    private async Task BtnSaveAsync()
    {
        try
        {
            // 校验
            var baseDir = AppContext.BaseDirectory.TrimEnd('\\');
            var apiFull = ResolveFullPath(_txtApiPath.Text, baseDir);
            if (!File.Exists(apiFull))
            {
                ShowError($"Api 可执行文件不存在：{apiFull}");
                _txtApiPath.Focus();
                return;
            }
            var adminFull = ResolveFullPath(_txtAdminPath.Text, baseDir);
            if (!File.Exists(adminFull))
            {
                ShowError($"Admin 可执行文件不存在：{adminFull}");
                _txtAdminPath.Focus();
                return;
            }

            var provider = _cmbProvider.SelectedItem?.ToString() ?? "SqlServer";
            if (provider == "SqlServer")
            {
                if (string.IsNullOrWhiteSpace(_txtDbServer.Text))
                {
                    ShowError("请填写数据库服务器");
                    _txtDbServer.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(_txtDbName.Text))
                {
                    ShowError("请填写数据库名称");
                    _txtDbName.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(_txtDbUser.Text))
                {
                    ShowError("请填写数据库账号");
                    _txtDbUser.Focus();
                    return;
                }
            }
            if (provider == "Sqlite" && string.IsNullOrWhiteSpace(_txtSqlitePath.Text))
            {
                ShowError("请填写 SQLite 数据库路径");
                _txtSqlitePath.Focus();
                return;
            }

            // 端口占用检查（仅在用户主动修改时提示，不阻塞保存）
            var apiPort = (int)_numApiPort.Value;
            var adminPort = (int)_numAdminPort.Value;

            var dbDto = new DatabaseConfigDto
            {
                Provider = provider,
                DbServer = _txtDbServer.Text.Trim(),
                DbPort = (int)_numDbPort.Value,
                DbName = _txtDbName.Text.Trim(),
                DbUser = _txtDbUser.Text.Trim(),
                DbPassword = string.IsNullOrEmpty(_txtDbPassword.Text) ? "unchanged" : _txtDbPassword.Text,
                SqlitePath = _txtSqlitePath.Text.Trim()
            };

            // v2.13.19：保存数据库配置（双擎持久化 + 广播）
            var (dbOk, dbMsg) = await AppConfigManager.Instance.SaveConfigurationAsync(dbDto);
            if (!dbOk)
            {
                ShowError($"数据库配置保存失败：{dbMsg}");
                _txtDbServer.Focus();
                return;
            }

            // 同步到 appsettings.json（子进程环境变量来源）
            _config.UpdateDatabaseSection(dbDto);

            var newConfig = new AppConfig
            {
                Tray = new TraySection
                {
                    ApiPort = apiPort,
                    AdminPort = adminPort,
                    ApiExecutable = _txtApiPath.Text.Trim(),
                    AdminExecutable = _txtAdminPath.Text.Trim(),
                    AutoStartServices = _chkAutoStart.Checked,
                    AutoRestartOnCrash = _chkAutoRestart.Checked,
                    HealthCheckIntervalSeconds = (int)_numHealthInterval.Value
                },
                Database = new DatabaseSection
                {
                    Provider = provider,
                    ConnectionString = dbDto.Provider == "Sqlite"
                        ? $"Data Source={dbDto.SqlitePath}"
                        : dbDto.BuildConnectionString(),
                    SqlitePath = dbDto.SqlitePath ?? ""
                },
                Storage = new StorageSection
                {
                    ImageRoot = _txtImageRoot.Text.Trim(),
                    LogRoot = _config.Current.Storage.LogRoot
                }
            };

            _config.Update(newConfig);
            _log.Info($"配置已保存：ApiPort={apiPort}, AdminPort={adminPort}, Provider={provider}, DbServer={dbDto.DbServer}");

            // v2.13.32：热加载架构 - 保存后 Api/Admin 下次请求自动切换连接，无需重启
            // AppConfigManager.SaveConfigurationAsync 已自动触发 AppConfigRuntime.ApplyExternalConfiguration
            // 子进程通过 db_setting.json FileSystemWatcher 监听变更（DatabaseConfigFileWatcher.cs）
            MessageBox.Show(this,
                "数据库配置已热加载，无需重启服务。\n\n所有 Api/Admin 进程的下一次 HTTP 请求将自动切换到新连接。\nSysParameter 表已在后台任务中同步更新。",
                "保存成功 (v2.13.32 热加载)",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _log.Error("保存配置失败", ex);
            ShowError($"保存失败：{ex.Message}");
        }
    }

    private async Task OnStartClick()
    {
        try { await _process.StartAllAsync(); }
        catch (Exception ex)
        {
            _log.Error("启动失败", ex);
            ShowError($"启动失败：{ex.Message}");
        }
    }

    private async Task OnStopClick()
    {
        try { await _process.StopAllAsync(); }
        catch (Exception ex)
        {
            _log.Error("停止失败", ex);
            ShowError($"停止失败：{ex.Message}");
        }
    }

    private async Task OnRestartClick()
    {
        try { await _process.RestartAllAsync(); }
        catch (Exception ex)
        {
            _log.Error("重启失败", ex);
            ShowError($"重启失败：{ex.Message}");
        }
    }

    #endregion

    #region 浏览对话框

    private void BrowseExe(TextBox target)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            InitialDirectory = AppContext.BaseDirectory,
            Title = "选择可执行文件"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = ToRelativeIfUnderBase(dlg.FileName, AppContext.BaseDirectory.TrimEnd('\\'));
        }
    }

    private void BrowseSqliteFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "SQLite 数据库 (*.db;*.sqlite)|*.db;*.sqlite|所有文件 (*.*)|*.*",
            InitialDirectory = AppContext.BaseDirectory,
            Title = "选择 SQLite 数据库文件"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _txtSqlitePath.Text = ToRelativeIfUnderBase(dlg.FileName, AppContext.BaseDirectory.TrimEnd('\\'));
        }
    }

    private void BrowseImageFolder()
    {
        // .NET 8 WinForms 提供 FolderBrowserDialog（已逐步稳定）
        try
        {
            using var dlg = new FolderBrowserDialog
            {
                SelectedPath = AppContext.BaseDirectory,
                Description = "选择图片存储根目录",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _txtImageRoot.Text = ToRelativeIfUnderBase(dlg.SelectedPath, AppContext.BaseDirectory.TrimEnd('\\'));
            }
        }
        catch (Exception ex)
        {
            // Win11 高 DPI 下 FBD 可能抛 COMException，回退到手动输入
            ShowError($"无法打开文件夹选择对话框：{ex.Message}\n请直接输入路径。");
            _txtImageRoot.Focus();
        }
    }

    #endregion

    #region 工具方法

    private static int SafeClamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static string ResolveFullPath(string relativeOrAbsolute, string baseDir)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            return "";
        return Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(baseDir, relativeOrAbsolute);
    }

    private static string ToRelativeIfUnderBase(string full, string baseDir)
    {
        if (string.IsNullOrEmpty(full)) return full;
        if (full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            return full.Substring(baseDir.Length).TrimStart('\\', '/');
        return full;
    }

    private static Font SafeMenuFont()
    {
        try { return SystemFonts.MenuFont ?? new Font("Microsoft YaHei UI", 9f); }
        catch { return new Font("Microsoft YaHei UI", 9f); }
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

    private void ShowError(string msg)
    {
        try
        {
            MessageBox.Show(this, msg, "系统设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch
        {
            // 终极兜底
        }
    }

    #endregion

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
            _statusTimer = null;
        }
        catch { }
        base.OnFormClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // ESC 关闭
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        base.OnKeyDown(e);
    }
}