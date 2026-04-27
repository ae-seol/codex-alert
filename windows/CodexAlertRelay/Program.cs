using System.Text;

namespace CodexAlertRelay;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Console encoding can be unavailable when the tray app is launched without a console.
        }

        ApplicationConfiguration.Initialize();
        if (args.Length > 0)
        {
            await RunCliAsync(args);
            return;
        }

        using var context = new RelayApplicationContext();
        Application.Run(context);
    }

    private static async Task RunCliAsync(string[] args)
    {
        var logger = new FileLogger();
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--run-headless":
                    await RunHeadlessRelayAsync(logger, args);
                    return;

                case "--list-codex-completions":
                    await ListCodexCompletionsAsync(logger, args);
                    return;

                case "--send-latest-codex-completion":
                    await SendLatestCodexCompletionAsync(logger, args);
                    return;

                default:
                    PrintAndLog(logger, HelpText());
                    Environment.ExitCode = 2;
                    return;
            }
        }
        catch (Exception exception)
        {
            logger.Error("CLI command failed.", exception, new { args });
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
        }
    }

    private static async Task ListCodexCompletionsAsync(FileLogger logger, string[] args)
    {
        var sinceMinutes = GetIntOption(args, "--since-minutes", 1440);
        var cwdContains = GetStringOption(args, "--cwd-contains", "");
        var limit = GetIntOption(args, "--limit", 10);
        var configStore = new ConfigStore();
        var config = configStore.LoadOrCreate();
        var relay = new NotificationRelayService(new FcmSender(), logger);
        var completions = await relay.FindRecentCodexTaskCompletionsAsync(
            config,
            TimeSpan.FromMinutes(Math.Max(1, sinceMinutes)),
            cwdContains,
            Math.Max(1, limit),
            CancellationToken.None);

        if (completions.Count == 0)
        {
            PrintAndLog(logger, "No matching Codex task_complete events found.");
            return;
        }

        PrintAndLog(logger, $"Found {completions.Count} Codex task_complete event(s):");
        for (var index = 0; index < completions.Count; index++)
        {
            var item = completions[index];
            var body = item.LastAgentMessage.Replace("\r", " ").Replace("\n", " ").Trim();
            if (body.Length > 160)
            {
                body = body[..157] + "...";
            }

            PrintAndLog(logger,
                $"[{index + 1}] {item.TimestampUtc:O}" + Environment.NewLine +
                $"    Title: {Fallback(item.ThreadTitle, "(untitled)")}" + Environment.NewLine +
                $"    CWD: {Fallback(item.Cwd, "(unknown)")}" + Environment.NewLine +
                $"    Thread: {Fallback(item.ThreadId, "(unknown)")}" + Environment.NewLine +
                $"    Turn: {item.TurnId}" + Environment.NewLine +
                $"    Message: {Fallback(body, "(empty)")}");
        }
    }

    private static async Task SendLatestCodexCompletionAsync(FileLogger logger, string[] args)
    {
        var sinceMinutes = GetIntOption(args, "--since-minutes", 1440);
        var cwdContains = GetStringOption(args, "--cwd-contains", "");
        var configStore = new ConfigStore();
        var config = configStore.LoadOrCreate();
        var relay = new NotificationRelayService(new FcmSender(), logger);
        await relay.SendLatestCodexTaskCompleteAsync(
            config,
            TimeSpan.FromMinutes(Math.Max(1, sinceMinutes)),
            cwdContains,
            CancellationToken.None);
        PrintAndLog(logger, "Latest Codex task_complete event sent through FCM.");
    }

    private static async Task RunHeadlessRelayAsync(FileLogger logger, string[] args)
    {
        var seconds = GetIntOption(args, "--seconds", 30);
        var configStore = new ConfigStore();
        var config = configStore.LoadOrCreate();
        var sender = new FcmSender();
        var relay = new NotificationRelayService(sender, logger);
        relay.StatusChanged += status => PrintAndLog(logger, "Headless relay: " + status);

        PrintAndLog(logger, $"Headless relay starting for {seconds} seconds. Config: {configStore.ConfigPath}");
        await relay.StartAsync(config, CancellationToken.None);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, seconds)));
        }
        finally
        {
            relay.Stop();
            PrintAndLog(logger, "Headless relay stopped.");
        }
    }

    private static int GetIntOption(string[] args, string name, int fallback)
    {
        var value = GetStringOption(args, name, "");
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string GetStringOption(string[] args, string name, string fallback)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }
        return fallback;
    }

    private static string HelpText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("CodexAlertRelay CLI");
        builder.AppendLine("  --run-headless --seconds 30");
        builder.AppendLine("  --list-codex-completions --since-minutes 1440 --cwd-contains <path text> --limit 10");
        builder.AppendLine("  --send-latest-codex-completion --since-minutes 1440 --cwd-contains <path text>");
        return builder.ToString();
    }

    private static string Fallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static void PrintAndLog(FileLogger logger, string message)
    {
        Console.WriteLine(message);
        logger.Info(message);
    }
}
