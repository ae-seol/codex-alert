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
    private readonly CheckBox _enableToastRelay = new();
    private readonly TextBox _codexHomePath = new();
    private readonly TextBox _allowedAppIds = new();
    private readonly TextBox _diagnostics = new();
    private readonly TextBox _status = new();

    public SetupForm(RelayApplicationContext context)
    {
        _context = context;
        Text = "Codex Alert Relay";
        Width = 980;
        Height = 760;
        MinimumSize = new Size(860, 660);
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
        _enableToastRelay.Checked = config.Relay.EnableWindowsToastRelay;
        _codexHomePath.Text = config.Relay.CodexHomePath;
        _allowedAppIds.Text = string.Join(Environment.NewLine, config.AllowedAppIds);
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
            Text = "Send Codex Desktop completion alerts to Android through Firebase Cloud Messaging.",
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
        tabs.TabPages.Add(Page("Beginner Help", BuildHelpTab()));
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
        body.Controls.Add(RelayModeBox());
        root.Controls.Add(body, 0, 1);
        return root;
    }

    private Control QuickStartBox()
    {
        var box = Group("Quick start", 150);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text =
                "1. Install the Android APK and copy the FCM token from the phone.\r\n" +
                "2. Paste Firebase project ID, service account JSON, and Android token below.\r\n" +
                "3. Use the top buttons: Save config, Check setup, Send FCM test, then Start relay.\r\n" +
                "4. Codex Desktop completion events are watched internally; Windows notification access is not required.",
            ForeColor = Color.FromArgb(40, 44, 52),
            AutoSize = false
        }, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        buttons.Controls.Add(WideButton("Open Firebase Console", () => _context.OpenUrl("https://console.firebase.google.com/")));
        buttons.Controls.Add(WideButton("Open Android APK folder", () => _context.OpenPath(Path.Combine(AppContext.BaseDirectory, ".."))));
        buttons.Controls.Add(WideButton("Open config file", () => _context.OpenConfig()));
        layout.Controls.Add(buttons, 1, 0);
        box.Controls.Add(layout);
        return box;
    }

    private Control FirebaseBox()
    {
        var box = Group("Firebase and Android", 196);
        var grid = FieldGrid(3);
        AddField(grid, 0, "Firebase project ID", _projectId);
        AddPathField(grid, 1, "Service account JSON", _serviceAccountPath, BrowseServiceAccount);
        _targetToken.Multiline = true;
        _targetToken.Height = 66;
        AddField(grid, 2, "Android FCM token(s)", _targetToken);
        box.Controls.Add(grid);
        return box;
    }

    private Control PcBox()
    {
        var box = Group("This PC", 112);
        var grid = FieldGrid(2);
        AddField(grid, 0, "PC ID", _pcId);
        AddField(grid, 1, "PC display name", _pcName);
        box.Controls.Add(grid);
        return box;
    }

    private Control RelayModeBox()
    {
        var box = Group("Relay mode", 130);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        _enableInternalWatcher.Text = "Watch Codex Desktop internal task_complete events (recommended)";
        _enableInternalWatcher.AutoSize = true;
        _enableToastRelay.Text = "Also relay optional Windows toast notifications";
        _enableToastRelay.AutoSize = true;
        layout.Controls.Add(_enableInternalWatcher, 0, 0);
        layout.Controls.Add(_enableToastRelay, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = "Default mode reads %USERPROFILE%\\.codex\\sessions and works even when Windows does not show a Codex toast.",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        }, 0, 2);
        box.Controls.Add(layout);
        return box;
    }

    private Control PrimaryActions()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 82,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(245, 247, 250),
            BorderStyle = BorderStyle.FixedSingle
        };
        panel.Controls.Add(Button("Save config", Save));
        panel.Controls.Add(Button("Check setup", async () => await RunText("Check setup", _context.CheckSetupAsync)));
        panel.Controls.Add(Button("Send FCM test", async () => await Run("Send FCM test", _context.SendTestAsync)));
        panel.Controls.Add(Button("Start relay", async () => await Run("Start relay", _context.StartRelayAsync)));
        panel.Controls.Add(Button("Send latest Codex completion", async () => await Run("Send latest Codex completion", _context.SendLatestCodexCompletionAsync)));
        panel.Controls.Add(Button("Hide to tray", () => _context.HideSetupWindow()));
        return panel;
    }

    private Control BuildHelpTab()
    {
        var root = ScrollPanel();
        root.Controls.Add(HelpText());
        root.Controls.Add(HelpActions());
        return root;
    }

    private Control HelpText()
    {
        var box = Group("Beginner guide", 410);
        var text = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Text =
                "What you need once:\r\n" +
                "- Firebase Spark project\r\n" +
                "- Android app registered with package com.codexalert\r\n" +
                "- google-services.json used when building the APK\r\n" +
                "- Firebase Admin SDK service account JSON kept on trusted Windows PCs only\r\n\r\n" +
                "What each Android phone needs:\r\n" +
                "- Install the APK\r\n" +
                "- Allow notifications\r\n" +
                "- Copy the FCM token shown in the app\r\n\r\n" +
                "What each Windows PC needs:\r\n" +
                "- This relay EXE\r\n" +
                "- service-account.json path\r\n" +
                "- Firebase project ID\r\n" +
                "- One or more Android FCM tokens\r\n\r\n" +
                "Important security notes:\r\n" +
                "- Do not put service account JSON in the APK.\r\n" +
                "- Do not upload service account JSON to public cloud links.\r\n" +
                "- If a PC is no longer trusted, revoke its service account key in Firebase/Google Cloud.\r\n\r\n" +
                "Normal operation:\r\n" +
                "- Click Start relay.\r\n" +
                "- Complete a Codex Desktop turn.\r\n" +
                "- Android receives a Codex notification and stores it in the inbox."
        };
        box.Controls.Add(text);
        return box;
    }

    private Control HelpActions()
    {
        var box = Group("Useful links", 138);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), WrapContents = true };
        panel.Controls.Add(Button("Firebase Console", () => _context.OpenUrl("https://console.firebase.google.com/")));
        panel.Controls.Add(Button("Service account page", () =>
        {
            var project = _projectId.Text.Trim();
            var url = string.IsNullOrWhiteSpace(project) || project.Contains("your-firebase", StringComparison.OrdinalIgnoreCase)
                ? "https://console.firebase.google.com/project/_/settings/serviceaccounts/adminsdk"
                : $"https://console.firebase.google.com/project/{Uri.EscapeDataString(project)}/settings/serviceaccounts/adminsdk";
            _context.OpenUrl(url);
        }));
        panel.Controls.Add(Button("Firebase FCM docs", () => _context.OpenUrl("https://firebase.google.com/docs/cloud-messaging")));
        panel.Controls.Add(Button("Firebase pricing", () => _context.OpenUrl("https://firebase.google.com/pricing")));
        panel.Controls.Add(Button("Open local docs", () => _context.OpenLocalDocs()));
        box.Controls.Add(panel);
        return box;
    }

    private Control BuildDiagnosticsTab()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 96, WrapContents = true };
        buttons.Controls.Add(Button("Check setup", async () => await RunText("Check setup", _context.CheckSetupAsync)));
        buttons.Controls.Add(Button("Send FCM test", async () => await Run("Send FCM test", _context.SendTestAsync)));
        buttons.Controls.Add(Button("List Codex completions", ListCodexCompletions));
        buttons.Controls.Add(Button("Send latest Codex completion", async () => await Run("Send latest Codex completion", _context.SendLatestCodexCompletionAsync)));
        buttons.Controls.Add(Button("Open logs", () => _context.OpenLogs()));
        buttons.Controls.Add(Button("Open config", () => _context.OpenConfig()));
        root.Controls.Add(buttons, 0, 0);

        _diagnostics.Dock = DockStyle.Fill;
        _diagnostics.Multiline = true;
        _diagnostics.ReadOnly = true;
        _diagnostics.ScrollBars = ScrollBars.Vertical;
        _diagnostics.Font = new Font(FontFamily.GenericMonospace, 9);
        _diagnostics.Text = "Click Check setup or List Codex completions.";
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
        var box = Group("Optional Windows toast relay and Codex home", 226);
        var grid = FieldGrid(3);
        AddPathField(grid, 0, "Codex home path", _codexHomePath, BrowseCodexHome);
        _allowedAppIds.Multiline = true;
        _allowedAppIds.Height = 76;
        AddField(grid, 1, "Allowed AppIDs", _allowedAppIds);
        grid.Controls.Add(new Label
        {
            Text = "AppID is only needed when optional Windows toast relay is enabled. Internal watcher does not need it.",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        }, 1, 2);
        box.Controls.Add(grid);
        return box;
    }

    private Control AdvancedActions()
    {
        var box = Group("Advanced actions", 170);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), WrapContents = true };
        panel.Controls.Add(Button("Request notification access", async () => await Run("Request access", ct => _context.RequestNotificationAccessAsync())));
        panel.Controls.Add(Button("Detect Codex AppID", DetectCodexAppId));
        panel.Controls.Add(Button("Observe sources", ObserveSources));
        panel.Controls.Add(Button("Validate toast filter", async () => await RunText("Validate filter", _context.ValidateFilterAsync)));
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
        config.Relay.EnableWindowsToastRelay = _enableToastRelay.Checked;
        config.Relay.CodexHomePath = _codexHomePath.Text.Trim();
        config.AllowedAppIds = _allowedAppIds.Lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _context.SaveConfig(config);
        SetStatus("Saved config: " + _context.ConfigPath);
    }

    private void BrowseServiceAccount()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Select Firebase service account JSON"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _serviceAccountPath.Text = dialog.FileName;
        }
    }

    private void BrowseCodexHome()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Codex home folder. Leave empty for %USERPROFILE%\\.codex."
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _codexHomePath.Text = dialog.SelectedPath;
        }
    }

    private async void DetectCodexAppId()
    {
        await Run("Detect Codex AppID", async ct =>
        {
            var candidates = await _context.DetectCodexAppIdsAsync(ct);
            if (candidates.Count == 0)
            {
                MessageBox.Show("No Codex AppID candidates found.", "Detect Codex AppID", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var best = candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.AppId));
            var text = string.Join(Environment.NewLine + Environment.NewLine, candidates.Select((candidate, index) =>
                $"[{index + 1}] {candidate.AppId}" + Environment.NewLine +
                $"Name: {candidate.Name}" + Environment.NewLine +
                $"Source: {candidate.Source}" + Environment.NewLine +
                $"Confidence: {candidate.Confidence}" +
                (string.IsNullOrWhiteSpace(candidate.Path) ? "" : Environment.NewLine + $"Path: {candidate.Path}")));

            if (best is null)
            {
                MessageBox.Show(text, "Detect Codex AppID", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var apply = MessageBox.Show(
                text + Environment.NewLine + Environment.NewLine + $"Use this AppID?{Environment.NewLine}{best.AppId}",
                "Detect Codex AppID",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (apply == DialogResult.Yes)
            {
                _allowedAppIds.Text = best.AppId;
                Save();
            }
        });
    }

    private async void ObserveSources()
    {
        await RunText("Observe sources", async ct =>
        {
            var sources = await _context.ObserveSourcesAsync(ct);
            return sources.Count == 0
                ? "No visible toast notification sources found."
                : string.Join(Environment.NewLine, sources.Select(source => $"{source.AppId} ({source.DisplayName})"));
        });
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

    private static Button WideButton(string text, Action action)
    {
        var button = Button(text, action);
        button.Width = 210;
        button.AutoSize = false;
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
