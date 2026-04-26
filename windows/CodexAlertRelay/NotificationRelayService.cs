using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace CodexAlertRelay;

public sealed class NotificationRelayService
{
    private readonly FcmSender _sender;
    private readonly FileLogger _logger;
    private readonly DedupeCache _dedupe = new();
    private readonly CodexSessionWatcherService _codexSessionWatcher;
    private PcConfig? _config;
    private bool _running;
    private bool _eventSubscribed;
    private bool _toastRelayActive;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    public event Action<string>? StatusChanged;

    public NotificationRelayService(FcmSender sender, FileLogger logger)
    {
        _sender = sender;
        _logger = logger;
        _codexSessionWatcher = new CodexSessionWatcherService(logger);
    }

    public bool IsRunning => _running;

    public async Task<UserNotificationListenerAccessStatus> RequestAccessAsync()
    {
        EnsureApiAvailable();
        var status = await UserNotificationListener.Current.RequestAccessAsync();
        SetStatus($"Notification access: {status}");
        return status;
    }

    public UserNotificationListenerAccessStatus GetAccessStatus()
    {
        EnsureApiAvailable();
        return UserNotificationListener.Current.GetAccessStatus();
    }

    public async Task StartAsync(PcConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        var startedAnyRelay = false;

        if (config.Relay.EnableWindowsToastRelay)
        {
            EnsureApiAvailable();
            var status = UserNotificationListener.Current.GetAccessStatus();
            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                _logger.Info("Windows toast relay disabled for this run because notification access is not allowed.", new { status });
                SetStatus($"Toast access is {status}; Codex internal watcher may still run.");
            }
            else
            {
                UserNotificationListener.Current.NotificationChanged -= OnNotificationChanged;
                _eventSubscribed = false;
                try
                {
                    UserNotificationListener.Current.NotificationChanged += OnNotificationChanged;
                    _eventSubscribed = true;
                }
                catch (COMException exception)
                {
                    _logger.Error("NotificationChanged subscription failed; falling back to notification polling.", exception);
                    SetStatus("Notification event unavailable. Polling relay running.");
                    StartPollingFallback(config);
                }

                startedAnyRelay = true;
                _toastRelayActive = true;
                await ProcessExistingRecentNotificationsAsync(cancellationToken);
            }
        }

        if (config.Relay.EnableCodexSessionWatcher)
        {
            _codexSessionWatcher.Start(config, ProcessPayloadAsync, cancellationToken);
            startedAnyRelay = true;
        }

        if (!startedAnyRelay)
        {
            throw new InvalidOperationException("No relay source is enabled. Enable relay.enableCodexSessionWatcher or relay.enableWindowsToastRelay.");
        }

        _running = true;
        SetStatus(BuildRunningStatus(config));
        _logger.Info("Relay started", new
        {
            config.PcId,
            config.PcName,
            config.AllowedAppIds,
            config.Relay.EnableWindowsToastRelay,
            config.Relay.EnableCodexSessionWatcher
        });
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        if (_eventSubscribed)
        {
            UserNotificationListener.Current.NotificationChanged -= OnNotificationChanged;
            _eventSubscribed = false;
        }
        _pollingCts?.Cancel();
        _pollingTask = null;
        _pollingCts?.Dispose();
        _pollingCts = null;
        _codexSessionWatcher.Stop();
        _toastRelayActive = false;
        _running = false;
        SetStatus("Relay stopped.");
        _logger.Info("Relay stopped");
    }

    public async Task<IReadOnlyList<NotificationSource>> ObserveSourcesAsync(CancellationToken cancellationToken)
    {
        EnsureApiAvailable();
        var status = UserNotificationListener.Current.GetAccessStatus();
        if (status != UserNotificationListenerAccessStatus.Allowed)
        {
            throw new InvalidOperationException($"Notification listener access is {status}. Request access first.");
        }

        var listener = UserNotificationListener.Current;
        var sources = new ConcurrentDictionary<string, NotificationSource>(StringComparer.OrdinalIgnoreCase);

        void AddSource(UserNotification notification)
        {
            var source = SourceFromNotification(notification);
            if (string.IsNullOrWhiteSpace(source.AppId))
            {
                return;
            }
            sources.AddOrUpdate(source.AppId, source, (_, existing) =>
                source.CreationTime > existing.CreationTime ? source : existing);
        }

        void Handler(UserNotificationListener sender, UserNotificationChangedEventArgs args)
        {
            if (args.ChangeKind != UserNotificationChangedKind.Added)
            {
                return;
            }

            try
            {
                var notification = sender.GetNotification(args.UserNotificationId);
                if (notification is not null)
                {
                    AddSource(notification);
                }
            }
            catch (Exception exception)
            {
                _logger.Error("Observe mode could not inspect notification.", exception);
            }
        }

        foreach (var notification in await listener.GetNotificationsAsync(NotificationKinds.Toast))
        {
            AddSource(notification);
        }

        SetStatus("Observe mode active for 20 seconds. Trigger a Codex toast now.");
        listener.NotificationChanged += Handler;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
        }
        finally
        {
            listener.NotificationChanged -= Handler;
        }

        SetStatus("Observe mode complete.");
        return sources.Values
            .OrderBy(source => source.DisplayName)
            .ThenBy(source => source.AppId)
            .ToList();
    }

    public async Task<string> ValidateFilterAsync(PcConfig config, CancellationToken cancellationToken)
    {
        var sources = await ObserveSourcesAsync(cancellationToken);
        var configured = config.AllowedAppIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matching = sources.Where(source => configured.Contains(source.AppId)).ToList();
        if (configured.Count == 0)
        {
            return "No allowedAppIds configured.";
        }
        if (matching.Count == 0)
        {
            var visible = string.Join(Environment.NewLine, sources.Select(source => $"  - {source.AppId} ({source.DisplayName})"));
            return "No recent notification source matches allowedAppIds." + Environment.NewLine + visible;
        }

        return "Filter matches recent source(s):" + Environment.NewLine +
               string.Join(Environment.NewLine, matching.Select(source => $"  - {source.AppId} ({source.DisplayName})"));
    }

    public async Task SendTestAsync(PcConfig config, CancellationToken cancellationToken)
    {
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

    private async void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        if (!_running || args.ChangeKind != UserNotificationChangedKind.Added)
        {
            return;
        }

        try
        {
            var notification = sender.GetNotification(args.UserNotificationId);
            if (notification is null)
            {
                return;
            }
            await ProcessNotificationAsync(notification, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("Failed to process notification changed event.", exception);
            SetStatus("Relay error: " + exception.Message);
        }
    }

    private async Task ProcessExistingRecentNotificationsAsync(CancellationToken cancellationToken)
    {
        var notifications = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
        foreach (var notification in notifications.Where(n => DateTimeOffset.Now - n.CreationTime < TimeSpan.FromSeconds(5)))
        {
            await ProcessNotificationAsync(notification, cancellationToken);
        }
    }

    private void StartPollingFallback(PcConfig config)
    {
        _pollingCts?.Cancel();
        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;
        _pollingTask = Task.Run(async () =>
        {
            _config = config;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var notifications = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
                    foreach (var notification in notifications.Where(n => DateTimeOffset.Now - n.CreationTime < TimeSpan.FromSeconds(90)))
                    {
                        await ProcessNotificationAsync(notification, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.Error("Polling fallback failed to inspect notifications.", exception);
                    SetStatus("Polling relay error: " + exception.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
            }
        }, token);
    }

    private async Task ProcessNotificationAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        var config = _config ?? throw new InvalidOperationException("Relay config is not loaded.");
        var payload = ToastPayload.FromUserNotification(notification, config);
        if (!config.AllowedAppIds.Contains(payload.SourceAppId, StringComparer.OrdinalIgnoreCase))
        {
            _logger.Info("Notification ignored by source filter.", new
            {
                payload.SourceAppId,
                payload.SourceAppName,
                payload.Title
            });
            return;
        }

        await ProcessPayloadAsync(payload, cancellationToken);
    }

    private static NotificationSource SourceFromNotification(UserNotification notification)
    {
        return new NotificationSource(
            notification.AppInfo.AppUserModelId ?? "",
            notification.AppInfo.DisplayInfo.DisplayName ?? "",
            notification.Id,
            notification.CreationTime);
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

    private string BuildRunningStatus(PcConfig config)
    {
        var sources = new List<string>();
        if (config.Relay.EnableCodexSessionWatcher)
        {
            sources.Add("Codex internal watcher");
        }
        if (config.Relay.EnableWindowsToastRelay)
        {
            if (_toastRelayActive)
            {
                sources.Add(_eventSubscribed ? "Windows toast events" : "Windows toast polling");
            }
            else
            {
                sources.Add("Windows toast unavailable");
            }
        }

        return "Relay running: " + string.Join(", ", sources);
    }

    private static void EnsureApiAvailable()
    {
        if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
        {
            throw new PlatformNotSupportedException("UserNotificationListener is not available on this Windows build.");
        }
    }

    private void SetStatus(string status)
    {
        StatusChanged?.Invoke(status);
    }
}
