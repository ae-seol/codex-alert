namespace CodexAlertRelay;

public sealed class SetupForm : Form
{
    private readonly RelayApplicationContext _context;
    private readonly TextBox _pcId = new();
    private readonly TextBox _pcName = new();
    private readonly TextBox _projectId = new();
    private readonly TextBox _serviceAccountPath = new();
    private readonly TextBox _targetToken = new();
    private readonly CheckBox _enableInternalWatcher = new();
    private readonly TextBox _codexHomePath = new();
    private readonly TextBox _diagnostics = new();
    private readonly TextBox _status = new();

    public SetupForm(RelayApplicationContext context)
    {
        _context = context;
        Text = "Codex Alert Relay";
        Width = 920;
        Height = 640;
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Icon = (Icon)_context.AppIcon.Clone();

        Controls.Add(BuildContent());
        Reload();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_context.IsExiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _context.HideSetupWindow();
            return;
        }

        base.OnFormClosing(e);
    }

    public void Reload()
    {
        var config = _context.LoadConfig();
        _pcId.Text = config.PcId;
        _pcName.Text = config.PcName;
        _projectId.Text = config.Firebase.ProjectId;
        _serviceAccountPath.Text = config.Firebase.ServiceAccountPath;
        _targetToken.Text = string.Join(Environment.NewLine, config.Firebase.GetTargetTokens());
        _enableInternalWatcher.Checked = config.Relay.EnableCodexSessionWatcher;
        _codexHomePath.Text = config.Relay.CodexHomePath;
        SetStatus("Config: " + _context.ConfigPath);
    }

    public void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetStatus(text)));
            return;
        }
        _status.Text = text;
    }

    private Control BuildContent()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(Header(), 0, 0);
        root.Controls.Add(Tabs(), 0, 1);
        root.Controls.Add(StatusBox(), 0, 2);
        return root;
    }

    private Control Header()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 86,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new PictureBox
        {
            Image = _context.AppIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 60,
            Height = 60,
            Margin = new Padding(0, 4, 14, 0)
        }, 0, 0);

        var text = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        text.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        text.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        text.Controls.Add(new Label
        {
            Text = "Codex Alert Relay",
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 20, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        }, 0, 0);
        text.Controls.Add(new Label
        {
            Text = "Windows relay for the Codex Alert Android app.",
            AutoSize = true,
            ForeColor = Color.DimGray
        }, 0, 1);
        panel.Controls.Add(text, 1, 0);
        return panel;
    }

    private Control Tabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("Setup", BuildSetupTab()));
        tabs.TabPages.Add(Page("Diagnostics", BuildDiagnosticsTab()));
        tabs.TabPages.Add(Page("Advanced", BuildAdvancedTab()));
        return tabs;
    }

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title);
        page.Controls.Add(content);
        return page;
    }

    private Control BuildSetupTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(PrimaryActions(), 0, 0);

        var body = ScrollPanel();
        body.Padding = new Padding(0, 8, 0, 0);
        body.Controls.Add(QuickStartBox());
        body.Controls.Add(FirebaseBox());
        body.Controls.Add(PcBox());
        root.Controls.Add(body, 0, 1);
        return root;
    }

    private Control QuickStartBox()
    {
        var box = Group("Release setup", 112);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Padding = new Padding(12) };

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text =
                "1. Install the release APK on Android and copy the FCM token from the app.\r\n" +
                "2. Run this Windows EXE and fill in only the fields below.\r\n" +
                "3. Save config. Use Diagnostics for the FCM test, then start the relay.",
            ForeColor = Color.FromArgb(40, 44, 52),
            AutoSize = false
        }, 0, 0);

        box.Controls.Add(layout);
        return box;
    }

    private Control FirebaseBox()
    {
        var box = Group("Required values", 196);
        var grid = FieldGrid(3);
        AddField(grid, 0, "Firebase project ID", _projectId);
        AddPathField(grid, 1, "Service account JSON", _serviceAccountPath, BrowseServiceAccount);
        _targetToken.Multiline = true;
        _targetToken.Height = 66;
        AddField(grid, 2, "Android app FCM token(s)", _targetToken);
        box.Controls.Add(grid);
        return box;
    }

    private Control PcBox()
    {
        var box = Group("Windows PC identity", 112);
        var grid = FieldGrid(2);
        AddField(grid, 0, "PC ID", _pcId);
        AddField(grid, 1, "PC display name", _pcName);
        box.Controls.Add(grid);
        return box;
    }

    private Control PrimaryActions()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(245, 247, 250),
            BorderStyle = BorderStyle.FixedSingle
        };
        panel.Controls.Add(Button("Save config", Save));
        panel.Controls.Add(Button("Start relay", async () => await Run("Start relay", _context.StartRelayAsync)));
        panel.Controls.Add(Button("Hide to tray", () => _context.HideSetupWindow()));
        return panel;
    }

    private Control BuildDiagnosticsTab()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, WrapContents = true };
        buttons.Controls.Add(Button("Validate setup", async () => await RunText("Validate setup", _context.CheckSetupAsync)));
        buttons.Controls.Add(Button("Send FCM test", async () => await Run("Send FCM test", _context.SendTestAsync)));
        buttons.Controls.Add(Button("List Codex completions", ListCodexCompletions));
        buttons.Controls.Add(Button("Send latest completion", async () => await Run("Send latest completion", _context.SendLatestCodexCompletionAsync)));
        buttons.Controls.Add(Button("Troubleshooting", ShowTroubleshooting));
        buttons.Controls.Add(Button("Open logs", () => _context.OpenLogs()));
        buttons.Controls.Add(Button("Open config", () => _context.OpenConfig()));
        root.Controls.Add(buttons, 0, 0);

        _diagnostics.Dock = DockStyle.Fill;
        _diagnostics.Multiline = true;
        _diagnostics.ReadOnly = true;
        _diagnostics.ScrollBars = ScrollBars.Vertical;
        _diagnostics.Font = new Font(FontFamily.GenericMonospace, 9);
        _diagnostics.Text = "Click Validate setup, List Codex completions, or Troubleshooting.";
        root.Controls.Add(_diagnostics, 0, 1);
        return root;
    }

    private Control BuildAdvancedTab()
    {
        var root = ScrollPanel();
        root.Controls.Add(AdvancedRelayBox());
        root.Controls.Add(AdvancedActions());
        return root;
    }

    private Control AdvancedRelayBox()
    {
        var box = Group("Advanced relay settings", 170);
        var grid = FieldGrid(3);
        _enableInternalWatcher.Text = "Watch Codex Desktop completion events";
        _enableInternalWatcher.AutoSize = true;
        grid.Controls.Add(_enableInternalWatcher, 1, 0);

        AddPathField(grid, 1, "Codex home path", _codexHomePath, BrowseCodexHome);

        grid.Controls.Add(new Label
        {
            Text = "Leave these as-is unless you store Codex sessions outside %USERPROFILE%\\.codex.",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        }, 1, 2);
        box.Controls.Add(grid);
        return box;
    }

    private Control AdvancedActions()
    {
        var box = Group("Relay control", 90);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), WrapContents = true };
        panel.Controls.Add(Button("Stop relay", () => _context.StopRelay()));
        box.Controls.Add(panel);
        return box;
    }

    private void Save()
    {
        var config = _context.LoadConfig();
        config.PcId = _pcId.Text.Trim();
        config.PcName = _pcName.Text.Trim();
        config.Firebase.ProjectId = _projectId.Text.Trim();
        config.Firebase.ServiceAccountPath = _serviceAccountPath.Text.Trim();
        var tokens = _targetToken.Lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        config.Firebase.TargetToken = tokens.FirstOrDefault() ?? "";
        config.Firebase.TargetTokens = tokens;
        config.Relay.EnableCodexSessionWatcher = _enableInternalWatcher.Checked;
        config.Relay.CodexHomePath = _codexHomePath.Text.Trim();
        _context.SaveConfig(config);
        SetStatus("Saved config: " + _context.ConfigPath);
    }

    private async void BrowseServiceAccount()
    {
        try
        {
            SetStatus("Opening service account file picker...");
            var path = await SafeFilePicker.PickFileAsync(
                "Select Firebase service account JSON",
                "JSON files (*.json)|*.json|All files (*.*)|*.*",
                _serviceAccountPath.Text);
            if (path is not null)
            {
                _serviceAccountPath.Text = path;
            }
            SetStatus("Config: " + _context.ConfigPath);
        }
        catch (Exception exception)
        {
            SetStatus("Service account file picker failed: " + exception.Message);
            MessageBox.Show(exception.Message, "Service account Browse failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BrowseCodexHome()
    {
        try
        {
            SetStatus("Opening Codex home folder picker...");
            var path = await SafeFilePicker.PickFolderAsync(
                "Select Codex home folder. Leave empty for %USERPROFILE%\\.codex.",
                _codexHomePath.Text);
            if (path is not null)
            {
                _codexHomePath.Text = path;
            }
            SetStatus("Config: " + _context.ConfigPath);
        }
        catch (Exception exception)
        {
            SetStatus("Codex home folder picker failed: " + exception.Message);
            MessageBox.Show(exception.Message, "Codex home Browse failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void ListCodexCompletions()
    {
        await RunText("List Codex completions", async ct =>
        {
            var completions = await _context.FindRecentCodexTaskCompletionsAsync(TimeSpan.FromHours(24), "", 10, ct);
            if (completions.Count == 0)
            {
                return "No Codex internal task_complete events found in the last 24 hours.";
            }

            return string.Join(Environment.NewLine + Environment.NewLine, completions.Select((item, index) =>
            {
                var body = item.LastAgentMessage.Replace("\r", " ").Replace("\n", " ").Trim();
                if (body.Length > 180)
                {
                    body = body[..177] + "...";
                }

                return $"[{index + 1}] {item.TimestampUtc:O}" + Environment.NewLine +
                       $"Title: {Fallback(item.ThreadTitle, "(untitled)")}" + Environment.NewLine +
                       $"CWD: {Fallback(item.Cwd, "(unknown)")}" + Environment.NewLine +
                       $"Message: {Fallback(body, "(empty)")}";
            }));
        });
    }

    private void ShowTroubleshooting()
    {
        _diagnostics.Text =
            "Common fixes" + Environment.NewLine +
            "============" + Environment.NewLine +
            Environment.NewLine +
            "Send FCM test failed" + Environment.NewLine +
            "- Open Diagnostics > Validate setup first." + Environment.NewLine +
            "- Firebase project ID must be the Firebase Project ID, not com.codexalert." + Environment.NewLine +
            "- Service account JSON must be a Firebase Admin SDK private key JSON." + Environment.NewLine +
            "- Android token(s) must be copied from the installed Android app." + Environment.NewLine +
            "- The APK, service account JSON, and project ID must belong to the same Firebase project." + Environment.NewLine +
            Environment.NewLine +
            "no supported key formats were found" + Environment.NewLine +
            "- You selected the wrong JSON file or a corrupted private key file." + Environment.NewLine +
            "- Do not use google-services.json here. That file is only for the Android APK." + Environment.NewLine +
            "- Use Firebase Console > Project settings > Service accounts > Generate new private key." + Environment.NewLine +
            "- The JSON must contain private_key starting with -----BEGIN PRIVATE KEY-----." + Environment.NewLine +
            Environment.NewLine +
            "Codex session watcher is disabled" + Environment.NewLine +
            "- Open Advanced." + Environment.NewLine +
            "- Check Watch Codex Desktop completion events." + Environment.NewLine +
            "- Click Save config, then Start relay." + Environment.NewLine +
            "- In pc.config.json, relay.enableCodexSessionWatcher must be true." + Environment.NewLine +
            Environment.NewLine +
            "Codex app finder / AppID" + Environment.NewLine +
            "- No Codex AppID key is needed in current releases." + Environment.NewLine +
            "- The relay reads %USERPROFILE%\\.codex\\sessions directly." + Environment.NewLine +
            "- It does not inspect Windows toast notifications or detect the Codex app package." + Environment.NewLine +
            Environment.NewLine +
            "More detail" + Environment.NewLine +
            "- Open logs to see the exact FCM/OAuth error." + Environment.NewLine +
            "- Open config to inspect the saved project ID, service account path, tokens, and watcher setting.";
        SetStatus("Troubleshooting help shown.");
    }

    private async Task Run(string name, Func<CancellationToken, Task> action)
    {
        try
        {
            SetStatus(name + "...");
            await action(CancellationToken.None);
            SetStatus(name + " complete.");
        }
        catch (Exception exception)
        {
            SetStatus(name + " failed: " + exception.Message);
            MessageBox.Show(exception.Message, name + " failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunText(string name, Func<CancellationToken, Task<string>> action)
    {
        try
        {
            SetStatus(name + "...");
            _diagnostics.Text = name + "...";
            var text = await action(CancellationToken.None);
            _diagnostics.Text = text;
            SetStatus(name + " complete.");
        }
        catch (Exception exception)
        {
            _diagnostics.Text = exception.ToString();
            SetStatus(name + " failed: " + exception.Message);
            MessageBox.Show(exception.Message, name + " failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static FlowLayoutPanel ScrollPanel()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10)
        };
    }

    private static GroupBox Group(string title, int height)
    {
        return new GroupBox
        {
            Text = title,
            Width = 870,
            Height = height,
            Margin = new Padding(0, 0, 0, 12)
        };
    }

    private static TableLayoutPanel FieldGrid(int rows)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows,
            Padding = new Padding(12)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rows; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        return grid;
    }

    private static void AddField(TableLayoutPanel grid, int row, string label, TextBox textBox)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 7, 8, 0)
        }, 0, row);

        textBox.Dock = DockStyle.Fill;
        grid.Controls.Add(textBox, 1, row);
    }

    private static void AddPathField(TableLayoutPanel grid, int row, string label, TextBox textBox, Action browse)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 7, 8, 0)
        }, 0, row);

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        textBox.Dock = DockStyle.Fill;
        panel.Controls.Add(textBox, 0, 0);
        panel.Controls.Add(Button("Browse", browse), 1, 0);
        grid.Controls.Add(panel, 1, row);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 34,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Control StatusBox()
    {
        _status.Dock = DockStyle.Bottom;
        _status.ReadOnly = true;
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.BackColor = Color.White;
        _status.Height = 28;
        return _status;
    }

    private static string Fallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
