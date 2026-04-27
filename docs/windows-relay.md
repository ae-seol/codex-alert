# Windows Relay Setup

The Windows relay is a .NET tray app that watches Codex Desktop internal session completion events and forwards them to Android through Firebase Cloud Messaging.

It reads Codex Desktop session JSONL files directly, so it does not need Windows notification access or source filters.

## Requirements

- Windows 10 build 19041 or newer, or Windows 11
- .NET SDK 8 or newer for local builds
- Firebase service account JSON
- Android FCM token copied from the installed Codex Alert Android app

## Codex Completion Watcher

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
  "codexHomePath": "",
  "codexSessionScanIntervalSeconds": 2,
  "codexSessionMaxFiles": 80,
  "codexCompletionBodyMaxChars": 600
}
```

Leave `codexHomePath` empty for the normal `%USERPROFILE%\.codex` location. Set it only if Codex uses a different home directory.

## Config

Create:

```powershell
Copy-Item .\config\pc.example.json .\config\pc.config.json
```

Set:

- `pcId` and `pcName`
- `firebase.projectId`
- `firebase.serviceAccountPath`
- `firebase.targetToken` for one Android device
- `firebase.targetTokens` for one or more Android devices

## GUI Actions

The release EXE opens a setup window with three tabs:

- `Setup`: save the required values, start the relay, or hide it to tray.
- `Diagnostics`: validate setup, send an FCM test, list recent Codex completions, send the latest completion, show troubleshooting help, and open logs/config.
- `Advanced`: change the Codex home path or stop the relay.

The system tray menu is intentionally small:

- `Show window`
- `Start relay`
- `Stop relay`
- `Exit`

## CLI Checks

```powershell
.\relay\CodexAlertRelay.exe --list-codex-completions --since-minutes 1440 --limit 10
.\relay\CodexAlertRelay.exe --send-latest-codex-completion --since-minutes 1440
.\relay\CodexAlertRelay.exe --run-headless --seconds 60
```

`--send-latest-codex-completion` is a practical end-to-end replay test using a real Codex Desktop completion event already present in local session logs.

## Troubleshooting

For detailed fixes, see [troubleshooting.md](troubleshooting.md).

Common messages:

- `no supported key formats were found`: the selected `Service account JSON` is not a Firebase Admin SDK private key JSON. Do not use `google-services.json` here.
- `Send FCM test failed`: check `Diagnostics > Validate setup`, then confirm the Firebase Project ID, service account JSON, Android FCM token, and APK all belong to the same Firebase project.
- `Codex session watcher is disabled`: open `Advanced`, check `Watch Codex Desktop completion events`, click `Save config`, then start the relay.
- Codex AppID or app finder: no key is needed. Current releases read `%USERPROFILE%\.codex\sessions` directly and do not inspect Windows toast notifications.

## Build

After installing the .NET SDK:

```powershell
dotnet build .\windows\CodexAlertRelay\CodexAlertRelay.csproj
```
