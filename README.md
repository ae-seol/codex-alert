# Codex Alert

Codex Alert relays Codex Desktop completion events from a Windows PC to Android through Firebase Cloud Messaging (FCM). It can also relay Windows Codex toast notifications when the optional toast listener is enabled.

## Why This Exists

요새 다들 사람이 병목이라고 합니다. Codex나 GPT가 답변을 낼 때까지 다른 일을 하면 좋겠지만, 저는 보통 기다리다가 그대로 쉬게 되더라구요.

그래서 GPT 앱처럼 Codex 작업도 휴대폰으로 알림을 보내면 어떨까 생각했습니다. 폰을 보면서 쉬다가 알림이 뜨면, 이제 병목은 모델이 아니라 다시 제가 되는 느낌입니다.

물론 이 작업도 vibe coding으로 만들었습니다.

The v1 flow is intentionally small:

1. Install the Android app and copy its FCM registration token.
2. Configure the Windows relay with Firebase project details, a service account JSON path, the phone token, and the Codex Windows AppID for optional toast relay.
3. Run the Windows tray relay.
4. When Codex Desktop writes an internal `task_complete` session event, the relay forwards it to the Android phone as a push notification and stores it in the app inbox.

The default v1 source is the Codex Desktop session JSONL under `%USERPROFILE%\.codex\sessions`, so it does not depend on whether Windows decides to show a toast. Windows toast relay remains available as an optional fallback by setting `relay.enableWindowsToastRelay` to `true`.

## Easiest Distribution

Android APK:

```text
D:\Documents\codex-alert\dist\codex-alert-v1-debug.apk
```

Windows relay single-EXE ZIP:

```text
D:\Documents\codex-alert\dist\codex-alert-relay-single-exe-win-x64.zip
```

Extract the ZIP and run `CodexAlertRelay.exe`. The relay opens a setup GUI by default and can be configured with only the Firebase project ID, service account JSON path, Android FCM token, and PC name. The EXE does not include secrets.

## Repository Layout

- `android/` - Native Kotlin Android app.
- `windows/` - .NET Windows tray relay app.
- `config/` - Example PC relay configuration.
- `scripts/` - Environment checks, Codex AppID detection, and FCM test send helpers.
- `docs/` - Setup guides for Firebase, Android, and the Windows relay.
- `.codex/subagents/` - Project-local Codex subagent role specs for download, defensive security testing, and security fixes.

## Current Machine Notes

This workspace was initialized on a Windows 10 PC where Codex is registered as:

```text
OpenAI.Codex_2p2nqsd0c76g0!App
```

Do not hardcode that value for other PCs. Run:

```powershell
.\scripts\detect-codex-appid.ps1
```

and put the detected AppID in `allowedAppIds` in your local `pc.config.json`.

## Quick Start

Check local tooling:

```powershell
.\scripts\check-env.ps1
```

Create a local Windows relay config:

```powershell
Copy-Item .\config\pc.example.json .\config\pc.config.json
notepad .\config\pc.config.json
```

Follow the guides:

- [Firebase setup](docs/firebase-setup.md)
- [Setup GUI](docs/setup-gui.md)
- [External network and Firebase checklist](docs/external-network-and-firebase.md)
- [GitHub publish guide](docs/github-publish.md)
- [Subagents security workflow](docs/subagents-security-workflow.md)
- [Connect another Windows PC](docs/connect-another-pc.md)
- [Android app setup](docs/android.md)
- [Windows relay setup](docs/windows-relay.md)
- [Testing](docs/testing.md)

## Security Notes

- Keep `android/app/google-services.json` out of source control.
- Keep the Firebase service account JSON outside this repo, for example under `%LOCALAPPDATA%\CodexAlert\service-account.json`.
- v1 forwards only configured `allowedAppIds`; keep that list narrow.
