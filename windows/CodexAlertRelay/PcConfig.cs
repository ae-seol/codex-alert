namespace CodexAlertRelay;

public sealed class PcConfig
{
    public string PcId { get; set; } = Environment.MachineName.ToLowerInvariant();
    public string PcName { get; set; } = Environment.MachineName;
    public FirebaseConfig Firebase { get; set; } = new();
    public RelayConfig Relay { get; set; } = new();
}

public sealed class FirebaseConfig
{
    public string ProjectId { get; set; } = "";
    public string ServiceAccountPath { get; set; } = "";
    public string TargetToken { get; set; } = "";
    public List<string> TargetTokens { get; set; } = [];

    public IReadOnlyList<string> GetTargetTokens()
    {
        return TargetTokens
            .Append(TargetToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

public sealed class RelayConfig
{
    public int DedupeWindowSeconds { get; set; } = 30;
    public int SendRetries { get; set; } = 3;
    public bool EnableCodexSessionWatcher { get; set; } = true;
    public string CodexHomePath { get; set; } = "";
    public int CodexSessionScanIntervalSeconds { get; set; } = 2;
    public int CodexSessionMaxFiles { get; set; } = 80;
    public int CodexCompletionBodyMaxChars { get; set; } = 600;
}

public sealed record CodexTaskCompleteEvent(
    string SessionFile,
    string ThreadId,
    string TurnId,
    string ThreadTitle,
    string Cwd,
    string LastAgentMessage,
    DateTimeOffset TimestampUtc
);
