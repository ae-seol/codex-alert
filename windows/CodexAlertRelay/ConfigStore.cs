using System.Text.Json;

namespace CodexAlertRelay;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ConfigPath { get; }

    public ConfigStore()
    {
        ConfigPath = ResolveConfigPath();
    }

    public PcConfig LoadOrCreate()
    {
        if (!File.Exists(ConfigPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var config = CreateDefault();
            Save(config);
            return config;
        }

        var raw = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<PcConfig>(raw, JsonOptions) ?? CreateDefault();
    }

    public void Save(PcConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public void OpenInEditor(Action<Exception>? onError = null)
    {
        if (!File.Exists(ConfigPath))
        {
            Save(CreateDefault());
        }
        ShellLauncher.Open(ConfigPath, onError);
    }

    private static PcConfig CreateDefault()
    {
        return new PcConfig
        {
            PcId = Environment.MachineName.ToLowerInvariant(),
            PcName = Environment.MachineName,
            AllowedAppIds = [],
            Firebase = new FirebaseConfig
            {
                ProjectId = "your-firebase-project-id",
                ServiceAccountPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodexAlert",
                    "service-account.json"),
                TargetToken = "android-fcm-registration-token",
                TargetTokens = []
            },
            Relay = new RelayConfig
            {
                DedupeWindowSeconds = 30,
                SendRetries = 3,
                EnableWindowsToastRelay = false,
                EnableCodexSessionWatcher = true,
                CodexHomePath = "",
                CodexSessionScanIntervalSeconds = 2,
                CodexSessionMaxFiles = 80,
                CodexCompletionBodyMaxChars = 600
            }
        };
    }

    private static string ResolveConfigPath()
    {
        var envPath = Environment.GetEnvironmentVariable("CODEX_ALERT_CONFIG");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return Path.GetFullPath(envPath);
        }

        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrWhiteSpace(current); i++)
        {
            var candidate = Path.Combine(current, "config", "pc.config.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexAlert",
            "pc.config.json");
        return local;
    }
}
