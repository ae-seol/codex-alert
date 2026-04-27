namespace CodexAlertRelay;

public sealed class NotificationRelayService
{
    private readonly FcmSender _sender;
    private readonly FileLogger _logger;
    private readonly DedupeCache _dedupe = new();
    private readonly CodexSessionWatcherService _codexSessionWatcher;
    private PcConfig? _config;
    private bool _running;

    public event Action<string>? StatusChanged;

    public NotificationRelayService(FcmSender sender, FileLogger logger)
    {
        _sender = sender;
        _logger = logger;
        _codexSessionWatcher = new CodexSessionWatcherService(logger);
    }

    public bool IsRunning => _running;

    public Task StartAsync(PcConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        if (!config.Relay.EnableCodexSessionWatcher)
        {
            throw new InvalidOperationException("Codex session watcher is disabled.");
        }

        _codexSessionWatcher.Start(config, ProcessPayloadAsync, cancellationToken);
        _running = true;
        SetStatus("Relay running: Codex completion watcher");
        _logger.Info("Relay started", new
        {
            config.PcId,
            config.PcName,
            config.Relay.EnableCodexSessionWatcher
        });
        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _codexSessionWatcher.Stop();
        _running = false;
        SetStatus("Relay stopped.");
        _logger.Info("Relay stopped");
    }

    public async Task SendTestAsync(PcConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        await SendWithRetryAsync(config, ToastPayload.Test(config), cancellationToken);
    }

    public async Task<IReadOnlyList<CodexTaskCompleteEvent>> FindRecentCodexTaskCompletionsAsync(
        PcConfig config,
        TimeSpan maxAge,
        string cwdContains,
        int limit,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return _codexSessionWatcher.FindRecentTaskCompletions(config, maxAge, cwdContains, limit);
    }

    public async Task SendLatestCodexTaskCompleteAsync(
        PcConfig config,
        TimeSpan maxAge,
        string cwdContains,
        CancellationToken cancellationToken)
    {
        var latest = (await FindRecentCodexTaskCompletionsAsync(config, maxAge, cwdContains, 1, cancellationToken))
            .FirstOrDefault();
        if (latest is null)
        {
            throw new InvalidOperationException("No matching Codex task_complete event was found.");
        }

        _config = config;
        await ProcessPayloadAsync(ToastPayload.FromCodexTaskComplete(latest, config), cancellationToken);
    }

    private async Task SendWithRetryAsync(PcConfig config, ToastPayload payload, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, config.Relay.SendRetries);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await _sender.SendAsync(config, payload, cancellationToken);
                _logger.Info("Notification relayed.", new
                {
                    payload.SourceAppId,
                    payload.Title,
                    payload.ToastId
                });
                SetStatus($"Relayed: {payload.Title}");
                return;
            }
            catch (Exception exception) when (attempt < attempts)
            {
                _logger.Error($"Relay send attempt {attempt} failed.", exception, new { payload.ToastId });
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }

        await _sender.SendAsync(config, payload, cancellationToken);
    }

    private async Task ProcessPayloadAsync(ToastPayload payload, CancellationToken cancellationToken)
    {
        var config = _config ?? throw new InvalidOperationException("Relay config is not loaded.");
        var window = TimeSpan.FromSeconds(Math.Max(1, config.Relay.DedupeWindowSeconds));
        if (!_dedupe.ShouldSend(payload, window))
        {
            _logger.Info("Duplicate notification skipped.", new { payload.ToastId, payload.Title });
            return;
        }

        await SendWithRetryAsync(config, payload, cancellationToken);
    }

    private void SetStatus(string status)
    {
        StatusChanged?.Invoke(status);
    }
}
