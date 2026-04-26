using System.Text.Json;

namespace CodexAlertRelay;

public sealed class CodexSessionWatcherService
{
    private readonly FileLogger _logger;
    private readonly Dictionary<string, SessionState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _threadTitles = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private DateTimeOffset _startedAtUtc;

    public CodexSessionWatcherService(FileLogger logger)
    {
        _logger = logger;
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void Start(
        PcConfig config,
        Func<ToastPayload, CancellationToken, Task> onPayload,
        CancellationToken cancellationToken)
    {
        Stop();

        var sessionsRoot = ResolveSessionsRoot(config);
        if (!Directory.Exists(sessionsRoot))
        {
            throw new DirectoryNotFoundException($"Codex sessions directory not found: {sessionsRoot}");
        }

        _startedAtUtc = DateTimeOffset.UtcNow;
        _states.Clear();
        RefreshThreadTitleCache(config);
        SeedExistingFiles(config, sessionsRoot);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;
        _loopTask = Task.Run(async () =>
        {
            _logger.Info("Codex session watcher started.", new { sessionsRoot });
            while (!token.IsCancellationRequested)
            {
                try
                {
                    RefreshThreadTitleCache(config);
                    foreach (var path in GetSessionFiles(sessionsRoot, config.Relay.CodexSessionMaxFiles))
                    {
                        if (!_states.TryGetValue(path, out var state))
                        {
                            state = CreateState(path);
                            _states[path] = state;
                        }

                        foreach (var task in ReadNewTaskCompletions(state, processHistorical: false))
                        {
                            if (task.TimestampUtc < _startedAtUtc.AddSeconds(-5))
                            {
                                continue;
                            }

                            var payload = ToastPayload.FromCodexTaskComplete(task, config);
                            _logger.Info("Codex internal task_complete observed.", new
                            {
                                task.ThreadId,
                                task.TurnId,
                                task.ThreadTitle,
                                task.Cwd
                            });
                            await onPayload(payload, token);
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.Error("Codex session watcher polling failed.", exception);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, config.Relay.CodexSessionScanIntervalSeconds)), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
            }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }

    public IReadOnlyList<CodexTaskCompleteEvent> FindRecentTaskCompletions(
        PcConfig config,
        TimeSpan maxAge,
        string cwdContains,
        int limit)
    {
        var sessionsRoot = ResolveSessionsRoot(config);
        if (!Directory.Exists(sessionsRoot))
        {
            throw new DirectoryNotFoundException($"Codex sessions directory not found: {sessionsRoot}");
        }

        RefreshThreadTitleCache(config);
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var matches = new List<CodexTaskCompleteEvent>();
        foreach (var path in GetSessionFiles(sessionsRoot, Math.Max(config.Relay.CodexSessionMaxFiles, 200)))
        {
            var state = CreateState(path);
            state.Position = 0;
            foreach (var task in ReadNewTaskCompletions(state, processHistorical: true))
            {
                if (task.TimestampUtc < cutoff)
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(cwdContains) &&
                    !NormalizePath(task.Cwd).Contains(cwdContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matches.Add(task);
            }
        }

        return matches
            .OrderByDescending(task => task.TimestampUtc)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    private void SeedExistingFiles(PcConfig config, string sessionsRoot)
    {
        foreach (var path in GetSessionFiles(sessionsRoot, config.Relay.CodexSessionMaxFiles))
        {
            var state = CreateState(path);
            PopulateMetadataFromFileStart(state);
            state.Position = new FileInfo(path).Length;
            _states[path] = state;
        }
    }

    private IEnumerable<CodexTaskCompleteEvent> ReadNewTaskCompletions(SessionState state, bool processHistorical)
    {
        var file = new FileInfo(state.Path);
        if (!file.Exists)
        {
            yield break;
        }

        if (file.Length < state.Position)
        {
            state.Position = 0;
        }

        using var stream = new FileStream(
            state.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(state.Position, SeekOrigin.Begin);

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryProcessLine(state, line, processHistorical, out var task))
            {
                yield return task;
            }
        }

        state.Position = new FileInfo(state.Path).Length;
    }

    private bool TryProcessLine(
        SessionState state,
        string line,
        bool processHistorical,
        out CodexTaskCompleteEvent task)
    {
        task = default!;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var topType = GetString(root, "type");
            var timestamp = ParseTimestamp(GetString(root, "timestamp")) ?? DateTimeOffset.UtcNow;

            if (topType == "session_meta" && root.TryGetProperty("payload", out var meta))
            {
                state.ThreadId = GetString(meta, "id") ?? state.ThreadId;
                state.Cwd = NormalizePath(GetString(meta, "cwd") ?? state.Cwd);
                ApplyCachedThreadTitle(state);
                return false;
            }

            if (topType != "event_msg" || !root.TryGetProperty("payload", out var payload))
            {
                return false;
            }

            var payloadType = GetString(payload, "type");
            if (payloadType == "thread_name_updated")
            {
                var threadId = GetString(payload, "thread_id");
                var threadName = GetString(payload, "thread_name");
                if (!string.IsNullOrWhiteSpace(threadId))
                {
                    state.ThreadId = threadId;
                }
                if (!string.IsNullOrWhiteSpace(threadName))
                {
                    state.ThreadTitle = threadName;
                    if (!string.IsNullOrWhiteSpace(state.ThreadId))
                    {
                        _threadTitles[state.ThreadId] = threadName;
                    }
                }
                return false;
            }

            if (payloadType != "task_complete")
            {
                return false;
            }

            var turnId = GetString(payload, "turn_id");
            if (string.IsNullOrWhiteSpace(turnId))
            {
                return false;
            }

            if (!processHistorical && timestamp < _startedAtUtc.AddSeconds(-5))
            {
                return false;
            }

            ApplyCachedThreadTitle(state);
            task = new CodexTaskCompleteEvent(
                state.Path,
                state.ThreadId,
                turnId,
                state.ThreadTitle,
                state.Cwd,
                GetString(payload, "last_agent_message") ?? "Codex finished a turn.",
                timestamp.ToUniversalTime());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void PopulateMetadataFromFileStart(SessionState state)
    {
        try
        {
            using var stream = new FileStream(
                state.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            for (var i = 0; i < 200; i++)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                TryProcessLine(state, line, processHistorical: true, out _);
                if (!string.IsNullOrWhiteSpace(state.Cwd) && !string.IsNullOrWhiteSpace(state.ThreadTitle))
                {
                    break;
                }
            }
        }
        catch (IOException)
        {
            // The polling loop will retry this file on the next pass.
        }
    }

    private SessionState CreateState(string path)
    {
        var state = new SessionState
        {
            Path = path,
            ThreadId = ParseThreadIdFromFileName(path),
            ThreadTitle = "",
            Cwd = ""
        };
        ApplyCachedThreadTitle(state);
        return state;
    }

    private void ApplyCachedThreadTitle(SessionState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ThreadTitle))
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(state.ThreadId) && _threadTitles.TryGetValue(state.ThreadId, out var title))
        {
            state.ThreadTitle = title;
        }
        else if (!string.IsNullOrWhiteSpace(state.Cwd))
        {
            state.ThreadTitle = Path.GetFileName(NormalizePath(state.Cwd).TrimEnd('\\', '/'));
        }
    }

    private void RefreshThreadTitleCache(PcConfig config)
    {
        var codexHome = ResolveCodexHome(config);
        var indexPath = Path.Combine(codexHome, "session_index.jsonl");
        if (!File.Exists(indexPath))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(
                indexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var id = GetString(root, "id");
                var title = GetString(root, "thread_name");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title))
                {
                    _threadTitles[id] = title;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.Error("Could not refresh Codex session title cache.", exception);
        }
    }

    private static IEnumerable<string> GetSessionFiles(string sessionsRoot, int maxFiles)
    {
        return Directory.EnumerateFiles(sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(Math.Max(1, maxFiles))
            .Select(file => file.FullName);
    }

    private static string ResolveSessionsRoot(PcConfig config)
    {
        return Path.Combine(ResolveCodexHome(config), "sessions");
    }

    private static string ResolveCodexHome(PcConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Relay.CodexHomePath))
        {
            return Environment.ExpandEnvironmentVariables(config.Relay.CodexHomePath);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
    }

    private static string NormalizePath(string path)
    {
        return path.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? path[4..]
            : path;
    }

    private static string ParseThreadIdFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Length >= 36 ? name[^36..] : name;
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private sealed class SessionState
    {
        public required string Path { get; init; }
        public long Position { get; set; }
        public string ThreadId { get; set; } = "";
        public string ThreadTitle { get; set; } = "";
        public string Cwd { get; set; } = "";
    }
}
