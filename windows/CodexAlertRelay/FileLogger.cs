namespace CodexAlertRelay;

public sealed class FileLogger
{
    public string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexAlert",
        "logs");

    public string CurrentLogPath => Path.Combine(LogDirectory, $"relay-{DateTimeOffset.Now:yyyyMMdd}.jsonl");

    public void Info(string message, object? data = null) => Write("info", message, data);

    public void Error(string message, Exception exception, object? data = null)
    {
        Write("error", message, new
        {
            exception = exception.ToString(),
            data
        });
    }

    public void OpenDirectory(Action<Exception>? onError = null)
    {
        Directory.CreateDirectory(LogDirectory);
        ShellLauncher.OpenPath(LogDirectory, onError);
    }

    private void Write(string level, string message, object? data)
    {
        Directory.CreateDirectory(LogDirectory);
        var line = System.Text.Json.JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level,
            message,
            data
        });
        File.AppendAllText(CurrentLogPath, line + Environment.NewLine);
    }
}
