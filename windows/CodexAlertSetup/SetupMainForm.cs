using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodexAlertSetup;

public sealed class SetupMainForm : Form
{
    private readonly TextBox _repoRoot = new();
    private readonly TextBox _googleServicesPath = new();
    private readonly TextBox _projectId = new();
    private readonly TextBox _packageName = new();
    private readonly TextBox _serviceAccountSource = new();
    private readonly TextBox _serviceAccountDest = new();
    private readonly TextBox _pcId = new();
    private readonly TextBox _pcName = new();
    private readonly TextBox _allowedAppIds = new();
    private readonly TextBox _targetTokens = new();
    private readonly TextBox _log = new();

    public SetupMainForm()
    {
        Text = "Codex Alert Setup Builder";
        Width = 1040;
        Height = 780;
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;

        _repoRoot.Text = FindRepoRoot();
        _serviceAccountDest.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexAlert",
            "service-account.json");
        _pcId.Text = Environment.MachineName.ToLowerInvariant();
        _pcName.Text = Environment.MachineName;

        Controls.Add(BuildContent());
        TryLoadExistingConfig();
    }

    private Control BuildContent()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(14),
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(Header(), 0, 0);
        root.Controls.Add(RepoGroup(), 0, 1);
        root.Controls.Add(ApkGroup(), 0, 2);
        root.Controls.Add(PcGroup(), 0, 3);
        root.Controls.Add(LinkGroup(), 0, 4);
        root.Controls.Add(LogBox(), 0, 5);
        return root;
    }

    private static Control Header()
    {
        var panel = new Panel { Height = 92, Dock = DockStyle.Top };
        panel.Controls.Add(new Label
        {
            Text = "Codex Alert Setup Builder",
            Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif, 18, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        });
        panel.Controls.Add(new Label
        {
            Text = "Build the Android APK, prepare relay config for each PC, and verify Firebase push without opening inbound ports.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(2, 36)
        });
        panel.Controls.Add(new Label
        {
            Text = "One APK works for every Android device. Each installed phone shows its own FCM token; paste one or more tokens into each PC config.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(2, 60)
        });
        return panel;
    }

    private Control RepoGroup()
    {
        var group = new GroupBox { Text = "Project root", Dock = DockStyle.Top, Height = 78 };
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(10)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(Label("Repo root"), 0, 0);
        row.Controls.Add(TextBox(_repoRoot), 1, 0);
        row.Controls.Add(Button("Browse", BrowseRepoRoot), 2, 0);
        group.Controls.Add(row);
        return group;
    }

    private Control ApkGroup()
    {
        var group = new GroupBox { Text = "Android APK builder", Dock = DockStyle.Top, Height = 186 };
        var grid = Grid(4);
        AddField(grid, 0, "google-services.json", _googleServicesPath, Button("Browse", BrowseGoogleServices));
        AddField(grid, 1, "Firebase project ID", _projectId, null);
        AddField(grid, 2, "Android package", _packageName, null);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        actions.Controls.Add(Button("Validate Firebase JSON", ValidateGoogleServices));
        actions.Controls.Add(Button("Copy Firebase JSON", CopyGoogleServices));
        actions.Controls.Add(Button("Build APK", async () => await BuildApkAsync()));
        actions.Controls.Add(Button("Open APK folder", OpenApkFolder));
        actions.Controls.Add(Button("Publish relay package", async () => await PublishRelayPackageAsync()));
        grid.Controls.Add(actions, 1, 3);

        group.Controls.Add(grid);
        return group;
    }

    private Control PcGroup()
    {
        var group = new GroupBox { Text = "Per-PC relay config", Dock = DockStyle.Top, Height = 260 };
        var grid = Grid(7);
        AddField(grid, 0, "Service key source", _serviceAccountSource, Button("Browse", BrowseServiceAccount));
        AddField(grid, 1, "Service key local path", _serviceAccountDest, Button("Copy key", CopyServiceAccount));
        AddField(grid, 2, "PC ID", _pcId, null);
        AddField(grid, 3, "PC name", _pcName, null);
        _allowedAppIds.Multiline = true;
        _allowedAppIds.Height = 44;
        AddField(grid, 4, "Codex AppID(s)", _allowedAppIds, Button("Detect", async () => await DetectCodexAppIdAsync()));
        _targetTokens.Multiline = true;
        _targetTokens.Height = 58;
        AddField(grid, 5, "Android token(s)", _targetTokens, null);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        actions.Controls.Add(Button("Save PC config", SavePcConfig));
        actions.Controls.Add(Button("Check network/config", async () => await RunScriptAsync("check-fcm-network.ps1", "-ConfigPath", ConfigPath())));
        actions.Controls.Add(Button("Send FCM test", async () => await RunPwshScriptAsync("send-test.ps1", "-ConfigPath", ConfigPath(), "-Title", "Codex Alert GUI test", "-Body", "Push sent from Codex Alert Setup Builder.")));
        actions.Controls.Add(Button("Open config folder", OpenConfigFolder));
        grid.Controls.Add(actions, 1, 6);

        group.Controls.Add(grid);
        return group;
    }

    private Control LinkGroup()
    {
        var group = new GroupBox { Text = "Firebase tasks and links", Dock = DockStyle.Top, Height = 96 };
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(10)
        };
        panel.Controls.Add(Button("Firebase console", () => OpenUrl("https://console.firebase.google.com/")));
        panel.Controls.Add(Button("Service account key", OpenServiceAccountUrl));
        panel.Controls.Add(Button("FCM HTTP v1 docs", () => OpenUrl("https://firebase.google.com/docs/cloud-messaging/auth-server")));
        panel.Controls.Add(Button("Android FCM setup", () => OpenUrl("https://firebase.google.com/docs/cloud-messaging/android/client")));
        panel.Controls.Add(Button("Pricing", () => OpenUrl("https://firebase.google.com/pricing")));
        panel.Controls.Add(Button("Open setup docs", OpenSetupDocs));
        group.Controls.Add(panel);
        return group;
    }

    private Control LogBox()
    {
        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ScrollBars = ScrollBars.Both;
        _log.ReadOnly = true;
        _log.WordWrap = false;
        _log.BackColor = Color.White;
        return _log;
    }

    private static TableLayoutPanel Grid(int rows)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = rows,
            Padding = new Padding(10)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < rows; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        return grid;
    }

    private static void AddField(TableLayoutPanel grid, int row, string label, TextBox textBox, Control? action)
    {
        grid.Controls.Add(Label(label), 0, row);
        grid.Controls.Add(TextBox(textBox), 1, row);
        if (action is not null)
        {
            grid.Controls.Add(action, 2, row);
        }
    }

    private static Label Label(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 8, 0)
        };
    }

    private static TextBox TextBox(TextBox textBox)
    {
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        textBox.Width = 700;
        return textBox;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Button Button(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.Click += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                ShowError(text + " failed", exception);
            }
        };
        return button;
    }

    private void BrowseRepoRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Codex Alert project root",
            SelectedPath = Directory.Exists(_repoRoot.Text) ? _repoRoot.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _repoRoot.Text = dialog.SelectedPath;
            TryLoadExistingConfig();
        }
    }

    private void BrowseGoogleServices()
    {
        var path = BrowseFile("Select google-services.json", "JSON files (*.json)|*.json|All files (*.*)|*.*");
        if (path is null) return;
        _googleServicesPath.Text = path;
        ValidateGoogleServices();
    }

    private void BrowseServiceAccount()
    {
        var path = BrowseFile("Select Firebase service account JSON", "JSON files (*.json)|*.json|All files (*.*)|*.*");
        if (path is null) return;
        _serviceAccountSource.Text = path;
        ValidateServiceAccount(path);
    }

    private static string? BrowseFile(string title, string filter)
    {
        using var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    private void ValidateGoogleServices()
    {
        try
        {
            var info = ReadGoogleServices(_googleServicesPath.Text);
            _projectId.Text = info.ProjectId;
            _packageName.Text = info.PackageName;
            AppendLog($"Firebase JSON OK. projectId={info.ProjectId}, package={info.PackageName}");
            if (!string.Equals(info.PackageName, "com.codexalert", StringComparison.Ordinal))
            {
                AppendLog("Warning: Android package should be com.codexalert for this app.");
            }
        }
        catch (Exception exception)
        {
            ShowError("Validate Firebase JSON failed", exception);
        }
    }

    private void CopyGoogleServices()
    {
        try
        {
            ValidateGoogleServices();
            var destination = Path.Combine(RepoRoot(), "android", "app", "google-services.json");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(_googleServicesPath.Text, destination, true);
            AppendLog("Copied google-services.json to " + destination);
        }
        catch (Exception exception)
        {
            ShowError("Copy Firebase JSON failed", exception);
        }
    }

    private async Task BuildApkAsync()
    {
        try
        {
            CopyGoogleServices();
            var root = RepoRoot();
            var androidDir = Path.Combine(root, "android");
            var gradlew = Path.Combine(androidDir, "gradlew.bat");
            if (!File.Exists(gradlew))
            {
                throw new FileNotFoundException("Gradle wrapper not found.", gradlew);
            }

            await RunProcessAsync(gradlew, ["-p", androidDir, ":app:assembleDebug"], root, ConfigureBuildEnvironment);
            var apk = Path.Combine(androidDir, "app", "build", "outputs", "apk", "debug", "app-debug.apk");
            if (!File.Exists(apk))
            {
                throw new FileNotFoundException("APK build succeeded but output APK was not found.", apk);
            }

            var dist = Path.Combine(root, "dist");
            Directory.CreateDirectory(dist);
            var output = Path.Combine(dist, "codex-alert-v1-debug.apk");
            File.Copy(apk, output, true);
            AppendLog("APK ready: " + output);
        }
        catch (Exception exception)
        {
            ShowError("Build APK failed", exception);
        }
    }

    private async Task PublishRelayPackageAsync()
    {
        try
        {
            var root = RepoRoot();
            var dotnet = FindExecutable("dotnet.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"));
            var project = Path.Combine(root, "windows", "CodexAlertRelay", "CodexAlertRelay.csproj");
            var output = Path.Combine(root, "dist", "windows-relay-win-x64");
            await RunProcessAsync(dotnet, ["publish", project, "-c", "Release", "-r", "win-x64", "--self-contained", "false", "-o", output], root, null);
            AppendLog("Relay package ready: " + output);
        }
        catch (Exception exception)
        {
            ShowError("Publish relay package failed", exception);
        }
    }

    private void CopyServiceAccount()
    {
        try
        {
            ValidateServiceAccount(_serviceAccountSource.Text);
            Directory.CreateDirectory(Path.GetDirectoryName(_serviceAccountDest.Text)!);
            File.Copy(_serviceAccountSource.Text, _serviceAccountDest.Text, true);
            AppendLog("Copied service account key to " + _serviceAccountDest.Text);
        }
        catch (Exception exception)
        {
            ShowError("Copy service account failed", exception);
        }
    }

    private void SavePcConfig()
    {
        try
        {
            var tokens = _targetTokens.Lines
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var appIds = _allowedAppIds.Lines
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tokens.Count == 0)
            {
                throw new InvalidOperationException("Paste at least one Android FCM token.");
            }
            if (appIds.Count == 0)
            {
                throw new InvalidOperationException("Detect or paste at least one Codex AppID.");
            }

            var config = new
            {
                pcId = _pcId.Text.Trim(),
                pcName = _pcName.Text.Trim(),
                allowedAppIds = appIds,
                firebase = new
                {
                    projectId = _projectId.Text.Trim(),
                    serviceAccountPath = _serviceAccountDest.Text.Trim(),
                    targetToken = tokens[0],
                    targetTokens = tokens
                },
                relay = new
                {
                    dedupeWindowSeconds = 30,
                    sendRetries = 3
                }
            };

            var path = ConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            AppendLog("Saved PC config: " + path);
        }
        catch (Exception exception)
        {
            ShowError("Save PC config failed", exception);
        }
    }

    private async Task DetectCodexAppIdAsync()
    {
        try
        {
            var script = ScriptPath("detect-codex-appid.ps1");
            var shell = FindPowerShell();
            var output = await RunProcessCaptureAsync(shell, ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script], RepoRoot(), null);
            AppendLog(output);

            var match = Regex.Match(output, @"(?m)^\[\d+\]\s+(?<appId>\S+!App)\s*$");
            if (!match.Success)
            {
                match = Regex.Match(output, @"(?<appId>[A-Za-z0-9_.-]+_[A-Za-z0-9]+!App)");
            }
            if (match.Success)
            {
                _allowedAppIds.Text = match.Groups["appId"].Value;
                AppendLog("Detected Codex AppID: " + _allowedAppIds.Text);
            }
        }
        catch (Exception exception)
        {
            ShowError("Detect Codex AppID failed", exception);
        }
    }

    private async Task RunScriptAsync(string scriptName, params string[] args)
    {
        var script = ScriptPath(scriptName);
        var shell = FindPowerShell();
        var allArgs = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script };
        allArgs.AddRange(args);
        await RunProcessAsync(shell, allArgs, RepoRoot(), null);
    }

    private async Task RunPwshScriptAsync(string scriptName, params string[] args)
    {
        var script = ScriptPath(scriptName);
        var shell = FindExecutable("pwsh.exe", "pwsh.exe");
        var allArgs = new List<string> { "-NoProfile", "-File", script };
        allArgs.AddRange(args);
        await RunProcessAsync(shell, allArgs, RepoRoot(), null);
    }

    private void OpenApkFolder()
    {
        OpenFolder(Path.Combine(RepoRoot(), "dist"));
    }

    private void OpenConfigFolder()
    {
        OpenFolder(Path.Combine(RepoRoot(), "config"));
    }

    private void OpenSetupDocs()
    {
        var path = Path.Combine(RepoRoot(), "docs", "connect-another-pc.md");
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void OpenServiceAccountUrl()
    {
        var projectId = string.IsNullOrWhiteSpace(_projectId.Text) ? "_" : _projectId.Text.Trim();
        OpenUrl($"https://console.firebase.google.com/project/{projectId}/settings/serviceaccounts/adminsdk");
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void TryLoadExistingConfig()
    {
        try
        {
            var googleServices = Path.Combine(RepoRoot(), "android", "app", "google-services.json");
            if (File.Exists(googleServices))
            {
                _googleServicesPath.Text = googleServices;
                ValidateGoogleServices();
            }

            var config = ConfigPath();
            if (!File.Exists(config))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(config));
            var root = document.RootElement;
            _pcId.Text = ReadString(root, "pcId", _pcId.Text);
            _pcName.Text = ReadString(root, "pcName", _pcName.Text);
            _allowedAppIds.Text = ReadStringArray(root, "allowedAppIds");
            if (root.TryGetProperty("firebase", out var firebase))
            {
                _projectId.Text = ReadString(firebase, "projectId", _projectId.Text);
                _serviceAccountDest.Text = ReadString(firebase, "serviceAccountPath", _serviceAccountDest.Text);
                var tokens = ReadStringArray(firebase, "targetTokens");
                if (string.IsNullOrWhiteSpace(tokens))
                {
                    tokens = ReadString(firebase, "targetToken", "");
                }
                _targetTokens.Text = tokens;
            }
            AppendLog("Loaded existing config: " + config);
        }
        catch (Exception exception)
        {
            AppendLog("Could not load existing config: " + exception.Message);
        }
    }

    private static FirebaseAndroidInfo ReadGoogleServices(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("google-services.json not found.", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var projectId = root.GetProperty("project_info").GetProperty("project_id").GetString() ?? "";
        var client = root.GetProperty("client")[0];
        var packageName = client.GetProperty("client_info")
            .GetProperty("android_client_info")
            .GetProperty("package_name")
            .GetString() ?? "";
        return new FirebaseAndroidInfo(projectId, packageName);
    }

    private void ValidateServiceAccount(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("Service account JSON not found.", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var projectId = ReadString(root, "project_id", "");
        var clientEmail = ReadString(root, "client_email", "");
        var hasPrivateKey = !string.IsNullOrWhiteSpace(ReadString(root, "private_key", ""));
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(clientEmail) || !hasPrivateKey)
        {
            throw new InvalidOperationException("Service account JSON must include project_id, client_email, and private_key.");
        }
        if (!string.IsNullOrWhiteSpace(_projectId.Text) && !string.Equals(projectId, _projectId.Text.Trim(), StringComparison.Ordinal))
        {
            AppendLog($"Warning: service account project_id '{projectId}' differs from Firebase project '{_projectId.Text.Trim()}'.");
        }
        AppendLog($"Service account OK. projectId={projectId}, client={clientEmail}");
    }

    private string RepoRoot()
    {
        var root = _repoRoot.Text.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("Project root not found: " + root);
        }
        return root;
    }

    private string ConfigPath() => Path.Combine(RepoRoot(), "config", "pc.config.json");

    private string ScriptPath(string name)
    {
        var path = Path.Combine(RepoRoot(), "scripts", name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Script not found.", path);
        }
        return path;
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrWhiteSpace(current); i++)
        {
            if (File.Exists(Path.Combine(current, "android", "gradlew.bat")) &&
                Directory.Exists(Path.Combine(current, "windows")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName ?? "";
        }

        var defaultRoot = @"D:\Documents\codex-alert";
        return Directory.Exists(defaultRoot) ? defaultRoot : Environment.CurrentDirectory;
    }

    private static string FindPowerShell()
    {
        return FindExecutable("powershell.exe", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"));
    }

    private static string FindExecutable(string command, string fallback)
    {
        if (File.Exists(fallback))
        {
            return fallback;
        }
        return command;
    }

    private static void ConfigureBuildEnvironment(ProcessStartInfo startInfo, string root)
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        var bundledJdk = @"C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot";
        if (string.IsNullOrWhiteSpace(javaHome) && Directory.Exists(bundledJdk))
        {
            startInfo.Environment["JAVA_HOME"] = bundledJdk;
            startInfo.Environment["Path"] = Path.Combine(bundledJdk, "bin") + ";" + startInfo.Environment["Path"];
        }

        var androidSdk = Path.Combine(root, ".tools", "android-sdk");
        if (Directory.Exists(androidSdk))
        {
            startInfo.Environment["ANDROID_HOME"] = androidSdk;
            startInfo.Environment["ANDROID_SDK_ROOT"] = androidSdk;
        }
    }

    private async Task RunProcessAsync(
        string fileName,
        IEnumerable<string> args,
        string workingDirectory,
        Action<ProcessStartInfo, string>? configure)
    {
        var output = await RunProcessCaptureAsync(fileName, args, workingDirectory, configure);
        AppendLog(output);
    }

    private async Task<string> RunProcessCaptureAsync(
        string fileName,
        IEnumerable<string> args,
        string workingDirectory,
        Action<ProcessStartInfo, string>? configure)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        configure?.Invoke(startInfo, workingDirectory);

        AppendLog("> " + fileName + " " + string.Join(" ", args.Select(QuoteIfNeeded)));
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start process: " + fileName);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        var combined = output + error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}:{Environment.NewLine}{combined}");
        }
        return combined;
    }

    private void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(text)));
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        _log.AppendText(text.TrimEnd() + Environment.NewLine + Environment.NewLine);
    }

    private void ShowError(string title, Exception exception)
    {
        AppendLog(title + ": " + exception.Message);
        MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static string ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        return string.Join(Environment.NewLine, property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string QuoteIfNeeded(string arg)
    {
        return arg.Contains(' ') ? '"' + arg + '"' : arg;
    }

    private sealed record FirebaseAndroidInfo(string ProjectId, string PackageName);
}
