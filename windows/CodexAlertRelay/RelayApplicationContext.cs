namespace CodexAlertRelay;

public sealed class RelayApplicationContext : ApplicationContext
{
    private readonly ConfigStore _configStore = new();
    private readonly FileLogger _logger = new();
    private readonly FcmSender _sender = new();
    private readonly AppIdDetector _detector = new();
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

    public async Task RequestNotificationAccessAsync()
    {
        await _relay.RequestAccessAsync();
    }

    public Task<IReadOnlyList<AppIdCandidate>> DetectCodexAppIdsAsync(CancellationToken cancellationToken)
    {
        return _detector.DetectAsync(cancellationToken);
    }

    public Task<IReadOnlyList<NotificationSource>> ObserveSourcesAsync(CancellationToken cancellationToken)
    {
        return _relay.ObserveSourcesAsync(cancellationToken);
    }

    public Task<string> ValidateFilterAsync(CancellationToken cancellationToken)
    {
        return _relay.ValidateFilterAsync(LoadConfig(), cancellationToken);
    }

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
            CheckLine("Codex internal watcher", config.Relay.EnableCodexSessionWatcher, config.Relay.EnableCodexSessionWatcher ? "enabled" : "disabled"),
            CheckLine("Windows toast relay", true, config.Relay.EnableWindowsToastRelay ? "enabled optional source" : "disabled")
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
        lines.Add("- Optional toast relay requires Windows notification access and Allowed AppIDs.");
        return string.Join(Environment.NewLine, lines);
    }

    public void OpenConfig() => _configStore.OpenInEditor();

    public void OpenLogs() => _logger.OpenDirectory();

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
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.GetFullPath(path),
            UseShellExecute = true
        });
    }

    public void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
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
        menu.Items.Add("Hide to tray", null, (_, _) => HideSetupWindow());
        menu.Items.Add("Start relay", null, async (_, _) => await RunUiActionAsync("Start relay", StartRelayAsync));
        menu.Items.Add("Stop relay", null, (_, _) => StopRelay());
        menu.Items.Add("Request notification access", null, async (_, _) => await RunUiActionAsync("Request access", async ct =>
        {
            await _relay.RequestAccessAsync();
        }));
        menu.Items.Add("Detect Codex AppID", null, async (_, _) => await RunDetectAndApplyAsync());
        menu.Items.Add("Observe notification sources", null, async (_, _) => await RunObserveSourcesAsync());
        menu.Items.Add("Validate filter", null, async (_, _) => await RunValidateFilterAsync());
        menu.Items.Add("Send test", null, async (_, _) => await RunUiActionAsync("Send test", SendTestAsync));
        menu.Items.Add("Send latest Codex completion", null, async (_, _) => await RunUiActionAsync("Send latest Codex completion", SendLatestCodexCompletionAsync));
        menu.Items.Add("Open config", null, (_, _) => OpenConfig());
        menu.Items.Add("Open logs", null, (_, _) => OpenLogs());
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

    private async Task RunDetectAndApplyAsync()
    {
        try
        {
            var candidates = await DetectCodexAppIdsAsync(CancellationToken.None);
            var best = candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.AppId));
            var text = candidates.Count == 0
                ? "No Codex AppID candidates found."
                : string.Join(Environment.NewLine + Environment.NewLine, candidates.Select((candidate, index) =>
                    $"[{index + 1}] {candidate.AppId}" + Environment.NewLine +
                    $"Name: {candidate.Name}" + Environment.NewLine +
                    $"Source: {candidate.Source}" + Environment.NewLine +
                    $"Confidence: {candidate.Confidence}" +
                    (string.IsNullOrWhiteSpace(candidate.Path) ? "" : Environment.NewLine + $"Path: {candidate.Path}")));

            if (best is not null)
            {
                var apply = MessageBox.Show(
                    text + Environment.NewLine + Environment.NewLine + $"Use this AppID in config?{Environment.NewLine}{best.AppId}",
                    "Codex AppID detection",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (apply == DialogResult.Yes)
                {
                    var config = LoadConfig();
                    config.AllowedAppIds = [best.AppId];
                    SaveConfig(config);
                    _setupForm?.Reload();
                    UpdateStatus("Codex AppID applied.");
                }
            }
            else
            {
                MessageBox.Show(text, "Codex AppID detection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            ShowError("Detect Codex AppID failed", exception);
        }
    }

    private async Task RunObserveSourcesAsync()
    {
        try
        {
            var sources = await ObserveSourcesAsync(CancellationToken.None);
            var text = sources.Count == 0
                ? "No visible toast notification sources found."
                : string.Join(Environment.NewLine, sources.Select(source => $"{source.AppId} ({source.DisplayName})"));
            MessageBox.Show(text, "Recent notification sources", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowError("Observe sources failed", exception);
        }
    }

    private async Task RunValidateFilterAsync()
    {
        try
        {
            var result = await ValidateFilterAsync(CancellationToken.None);
            MessageBox.Show(result, "Validate filter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowError("Validate filter failed", exception);
        }
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
