# Windows Relay Setup

The Windows relay is a .NET tray app that watches Codex Desktop internal session completion events and forwards them to FCM. It can also listen for Windows toast notifications when optional toast relay is enabled.

## Requirements

- Windows 10 build 14393 or newer, or Windows 11
- .NET SDK 8 or newer
- Firebase service account JSON

The default Codex internal watcher does not require Windows notification permission. Optional Windows toast relay requires user permission and the `userNotificationListener` capability in the package manifest.

## Codex Internal Completion Watcher

By default, the relay reads Codex Desktop session JSONL files under:

```text
%USERPROFILE%\.codex\sessions
```

When Codex Desktop appends an internal `task_complete` event, the relay sends a push with:

```text
sourceAppId = codex-internal-task-complete
sourceAppName = Codex Desktop
```

Relevant config:

```json
"relay": {
  "enableCodexSessionWatcher": true,
  "enableWindowsToastRelay": false,
  "codexHomePath": "",
  "codexSessionScanIntervalSeconds": 2,
  "codexSessionMaxFiles": 80,
  "codexCompletionBodyMaxChars": 600
}
```

Leave `codexHomePath` empty for the normal `%USERPROFILE%\.codex` location. Set it only if Codex uses a different home directory.

Useful CLI checks:

```powershell
.\relay\CodexAlertRelay.exe --list-codex-completions --since-minutes 1440 --limit 10
.\relay\CodexAlertRelay.exe --send-latest-codex-completion --since-minutes 1440
.\relay\CodexAlertRelay.exe --run-headless --seconds 60
```

`--send-latest-codex-completion` is a practical end-to-end replay test using a real Codex Desktop completion event already present in local session logs.

## Codex AppID Detection

AppID detection is only needed for optional Windows toast relay. It is not needed for the default internal completion watcher.

Run:

```powershell
.\scripts\detect-codex-appid.ps1
```

The script checks:

- Start menu registrations via `Get-StartApps`
- Running `Codex.exe` and `codex.exe` processes
- Microsoft Store package naming patterns under process paths

Put the detected AppID into:

```json
"allowedAppIds": ["OpenAI.Codex_2p2nqsd0c76g0!App"]
```

The current development PC reports:

```text
OpenAI.Codex_2p2nqsd0c76g0!App
```

Other PCs can have the same package family or a future value, so detect it on each PC.

## Config

Create:

```powershell
Copy-Item .\config\pc.example.json .\config\pc.config.json
```

Set:

- `pcId` and `pcName`
- `allowedAppIds` for optional Windows toast relay
- `firebase.projectId`
- `firebase.serviceAccountPath`
- `firebase.targetToken`

## Build

After installing the .NET SDK:

```powershell
dotnet build .\windows\CodexAlertRelay\CodexAlertRelay.csproj
```

The project includes `Package.appxmanifest` with the notification listener capability. If the listener returns denied or empty results in unpackaged debug runs, package the app with Visual Studio/MSIX tooling and install the package before using the relay.

For the default internal completion watcher, the unpackaged relay can run without MSIX packaging.

## Tray Actions

- `Open setup` - opens the setup/status window.
- `Start relay` - starts notification observation and FCM forwarding.
- `Stop relay` - stops forwarding.
- `Request notification access` - prompts Windows notification listener permission.
- `Detect Codex AppID` - finds Codex AppID candidates and optionally writes the first high-confidence candidate into config.
- `Observe notification sources` - lists visible toast sources and listens for new sources for 20 seconds while you trigger a Codex toast.
- `Validate filter` - checks configured `allowedAppIds` against recent toast sources.
- `Send test` - sends a synthetic Codex Alert message through FCM.
- `Send latest Codex completion` - replays the newest local Codex internal completion event through FCM.
- `Open config` and `Open logs` - opens local files/folders.

## Reference

- Microsoft UserNotificationListener: https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/notification-listener
