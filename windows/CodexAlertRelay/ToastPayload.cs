namespace CodexAlertRelay;

public sealed class ToastPayload
{
    public string PcId { get; init; } = "";
    public string PcName { get; init; } = "";
    public string SourceAppId { get; init; } = "";
    public string SourceAppName { get; init; } = "";
    public string ToastId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTimeOffset ReceivedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public static ToastPayload Test(PcConfig config)
    {
        return new ToastPayload
        {
            PcId = config.PcId,
            PcName = config.PcName,
            SourceAppId = "manual-test",
            SourceAppName = "Codex Alert Relay",
            ToastId = "manual-test-" + Guid.NewGuid().ToString("N"),
            Title = "Codex Alert test",
            Body = "FCM test message from Windows relay.",
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public static ToastPayload FromCodexTaskComplete(CodexTaskCompleteEvent task, PcConfig config)
    {
        var title = string.IsNullOrWhiteSpace(task.ThreadTitle)
            ? "Codex turn complete"
            : $"Codex: {task.ThreadTitle}";

        return new ToastPayload
        {
            PcId = config.PcId,
            PcName = config.PcName,
            SourceAppId = "codex-internal-task-complete",
            SourceAppName = "Codex Desktop",
            ToastId = $"codex-session:{task.ThreadId}:{task.TurnId}",
            Title = Limit(title, 96),
            Body = NormalizeMessage(task.LastAgentMessage, config.Relay.CodexCompletionBodyMaxChars),
            ReceivedAtUtc = task.TimestampUtc
        };
    }

    private static string NormalizeMessage(string value, int maxChars)
    {
        var normalized = string.Join(
            " ",
            value.Replace("\r", "\n")
                .Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Codex finished a turn.";
        }

        return Limit(normalized, maxChars <= 0 ? 600 : maxChars);
    }

    private static string Limit(string value, int maxChars)
    {
        if (maxChars <= 0 || value.Length <= maxChars)
        {
            return value;
        }

        return value[..Math.Max(0, maxChars - 3)] + "...";
    }
}
