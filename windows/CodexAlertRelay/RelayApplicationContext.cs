namespace CodexAlertRelay;

public sealed class RelayApplicationContext : ApplicationContext
{
    private readonly ConfigStore _configStore = new();
    private readonly FileLogger _logger = new();
    private readonly FcmSender _sender = new();
    private readonly NotificationRelayService _relay;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _appIcon;
    private SetupForm? _setupForm;
    private bool _isExiting;

    public RelayApplicationContext()
    {
        _appIcon = LoadAppIcon();
        _relay = new NotificationRelayService(_sender, _logger);
        _relay.StatusChanged += UpdateStatus;

        _notifyIcon = new NotifyIcon
        {
            Text = "Codex Alert Relay",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                ShowSetup();
            }
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSetup();

        _configStore.LoadOrCreate();
        ShowSetup();
    }

    public string ConfigPath => _configStore.ConfigPath;

    public Icon AppIcon => _appIcon;

    public PcConfig LoadConfig() => _configStore.LoadOrCreate();

    public void SaveConfig(PcConfig config) => _configStore.Save(config);

    public async Task StartRelayAsync(CancellationToken cancellationToken)
    {
        var config = LoadConfig();
        await _relay.StartAsync(config, cancellationToken);
    }

    public void StopRelay() => _relay.Stop();

    public Task SendTestAsync(CancellationToken cancellationToken)
    {
        return _relay.SendTestAsync(LoadConfig(), cancellationToken);
    }

    public Task<IReadOnlyList<CodexTaskCompleteEvent>> FindRecentCodexTaskCompletionsAsync(
        TimeSpan maxAge,
        string cwdContains,
        int limit,
        CancellationToken cancellationToken)
    {
        return _relay.FindRecentCodexTaskCompletionsAsync(LoadConfig(), maxAge, cwdContains, limit, cancellationToken);
    }

    public Task SendLatestCodexCompletionAsync(CancellationToken cancellationToken)
    {
        return _relay.SendLatestCodexTaskCompleteAsync(
            LoadConfig(),
            TimeSpan.FromHours(24),
            "",
            cancellationToken);
    }

    public async Task<string> CheckSetupAsync(CancellationToken cancellationToken)
    {
        var config = LoadConfig();
        var lines = new List<string>
        {
            "Codex Alert setup check",
            "",
            CheckLine("Config file", File.Exists(ConfigPath), ConfigPath),
            CheckLine("PC ID", !string.IsNullOrWhiteSpace(config.PcId), config.PcId),
            CheckLine("PC name", !string.IsNullOrWhiteSpace(config.PcName), config.PcName),
            CheckLine("Firebase project ID", !string.IsNullOrWhiteSpace(config.Firebase.ProjectId) &&
                                             !config.Firebase.ProjectId.Contains("your-firebase", StringComparison.OrdinalIgnoreCase),
                config.Firebase.ProjectId),
            CheckLine("Service account JSON", !string.IsNullOrWhiteSpace(config.Firebase.ServiceAccountPath) &&
                                                  File.Exists(config.Firebase.ServiceAccountPath),
                config.Firebase.ServiceAccountPath),
            CheckLine("Android token(s)", config.Firebase.GetTargetTokens().Count > 0 &&
                                           config.Firebase.GetTargetTokens().All(token => !token.Contains("android-fcm", StringComparison.OrdinalIgnoreCase)),
                "configured: " + config.Firebase.GetTargetTokens().Count),
            CheckLine("Codex completion watcher", config.Relay.EnableCodexSessionWatcher, config.Relay.EnableCodexSessionWatcher ? "enabled" : "disabled")
        };

        var codexHome = ResolveCodexHome(config);
        var sessions = Path.Combine(codexHome, "sessions");
        lines.Add(CheckLine("Codex sessions folder", Directory.Exists(sessions), sessions));

        lines.Add("");
        lines.Add("Network:");
        lines.Add(await CheckTcpAsync("oauth2.googleapis.com", 443, cancellationToken));
        lines.Add(await CheckTcpAsync("fcm.googleapis.com", 443, cancellationToken));

        lines.Add("");
        lines.Add("Next:");
        lines.Add("- If all required checks passed, click Send FCM test.");
        lines.Add("- Then click Start relay and complete a Codex Desktop turn.");
        return string.Join(Environment.NewLine, lines);
    }

    public void OpenConfig() => _configStore.OpenInEditor(exception => PostToUi(() => ShowError("Open config failed", exception)));

    public void OpenLogs() => _logger.OpenDirectory(exception => PostToUi(() => ShowError("Open logs failed", exception)));

    public void OpenLocalDocs()
    {
        var docs = Path.Combine(AppContext.BaseDirectory, "docs");
        if (!Directory.Exists(docs))
        {
            docs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "docs"));
        }
        OpenPath(Directory.Exists(docs) ? docs : AppContext.BaseDirectory);
    }

    public void OpenPath(string path)
    {
        ShellLauncher.OpenPath(path, exception => PostToUi(() => ShowError("Open path failed", exception)));
    }

    public void OpenUrl(string url)
    {
        ShellLauncher.Open(url, exception => PostToUi(() => ShowError("Open browser failed", exception)));
    }

    public void HideSetupWindow()
    {
        if (_setupForm is null || _setupForm.IsDisposed)
        {
            return;
        }

        _setupForm.Hide();
        UpdateStatus("Hidden to tray. Click the tray icon to show.");
        try
        {
            _notifyIcon.BalloonTipTitle = "Codex Alert Relay";
            _notifyIcon.BalloonTipText = "Still running. Click the tray icon to show the window.";
            _notifyIcon.ShowBalloonTip(1500);
        }
        catch
        {
            // Some Windows notification settings suppress balloon tips.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isExiting = true;
            _relay.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _setupForm?.Dispose();
            _appIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show window", null, (_, _) => ShowSetup());
        menu.Items.Add("Start relay", null, async (_, _) => await RunUiActionAsync("Start relay", StartRelayAsync));
        menu.Items.Add("Stop relay", null, (_, _) => StopRelay());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _isExiting = true;
            ExitThread();
        });
        return menu;
    }

    private void ShowSetup()
    {
        if (_setupForm is null || _setupForm.IsDisposed)
        {
            _setupForm = new SetupForm(this);
        }

        _setupForm.Show();
        if (_setupForm.WindowState == FormWindowState.Minimized)
        {
            _setupForm.WindowState = FormWindowState.Normal;
        }
        _setupForm.BringToFront();
        _setupForm.Activate();
    }

    private async Task RunUiActionAsync(string name, Func<CancellationToken, Task> action)
    {
        try
        {
            await action(CancellationToken.None);
        }
        catch (Exception exception)
        {
            ShowError(name + " failed", exception);
        }
    }

    private void ShowError(string title, Exception exception)
    {
        _logger.Error(title, exception);
        UpdateStatus(title);
        MessageBox.Show(exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void PostToUi(Action action)
    {
        if (_setupForm is { IsDisposed: false, IsHandleCreated: true } form)
        {
            form.BeginInvoke(action);
            return;
        }

        action();
    }

    private void UpdateStatus(string status)
    {
        _notifyIcon.Text = status.Length > 63 ? status[..63] : status;
        _setupForm?.SetStatus(status);
        _logger.Info(status);
    }

    public bool IsExiting => _isExiting;

    private static Icon LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "CodexAlert.ico");
        if (File.Exists(path))
        {
            return new Icon(path);
        }

        var executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (executableIcon is not null)
        {
            return executableIcon;
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static string ResolveCodexHome(PcConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Relay.CodexHomePath))
        {
            return Environment.ExpandEnvironmentVariables(config.Relay.CodexHomePath);
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    }

    private static string CheckLine(string name, bool passed, string detail)
    {
        return $"{(passed ? "[PASS]" : "[FAIL]")} {name}: {detail}";
    }

    private static async Task<string> CheckTcpAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return CheckLine($"{host}:{port}", true, "connected");
        }
        catch (Exception exception)
        {
            return CheckLine($"{host}:{port}", false, exception.Message);
        }
    }
}
