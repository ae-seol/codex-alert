# Codex Alert

Codex Alert relays Codex Desktop completion events from a Windows PC to Android through Firebase Cloud Messaging (FCM).

## Why This Exists

요새 다들 사람이 병목이라고 합니다. Codex나 GPT가 답변을 낼 때까지 다른 일을 하면 좋겠지만, 저는 보통 기다리다가 그대로 쉬게 되더라구요.

그래서 GPT 앱처럼 Codex 작업도 휴대폰으로 알림을 보내면 어떨까 생각했습니다.

물론 이것도 vibecoding으로 작성했습니다.

These days, people often say the human is the bottleneck. It would be better to do something else while Codex or GPT is working on an answer, but I usually end up just waiting and taking a break.

So I wondered: what if Codex could send a phone notification, like the GPT app does, when a task is done?

Of course, this was also written with vibecoding.

The v1 flow is intentionally small:

1. Install the Android app and copy its FCM registration token.
2. Configure the Windows relay with Firebase project details, a service account JSON path, the phone token, and a PC name.
3. Run the Windows tray relay.
4. When Codex Desktop writes an internal `task_complete` session event, the relay forwards it to the Android phone as a push notification and stores it in the app inbox.

The v1 source is the Codex Desktop session JSONL under `%USERPROFILE%\.codex\sessions`, so it does not depend on Windows desktop notification capture.

## Easiest Distribution

Download the latest release:

```text
https://github.com/ae-seol/codex-alert/releases/latest
```

Release assets:

```text
codex-alert-v1-debug.apk
codex-alert-relay-single-exe-win-x64.exe
```

Install the APK on Android, then run the Windows EXE:

```text
CodexAlertRelay.exe
```

The relay opens a setup GUI by default and can be configured with only the Firebase project ID, service account JSON path, Android FCM token, and PC name. The EXE does not include secrets.

## Repository Layout

- `android/` - Native Kotlin Android app.
- `windows/` - .NET Windows tray relay app.
- `config/` - Example PC relay configuration.
- `scripts/` - Environment checks and FCM test send helpers.
- `docs/` - Setup guides for Firebase, Android, and the Windows relay.
- `.codex/subagents/` - Project-local Codex subagent role specs for download, defensive security testing, and security fixes.

## Quick Start

Use the release files from GitHub. No local build is needed for normal use.

1. Download the latest release:

   ```text
   https://github.com/ae-seol/codex-alert/releases/latest
   ```

2. Install `codex-alert-v1-debug.apk` on Android.

3. Open the Android app and copy the FCM registration token shown in the app.

4. Run the Windows relay:

   ```text
   codex-alert-relay-single-exe-win-x64.exe
   ```

   If Windows SmartScreen asks for confirmation, choose to run it only if you downloaded it from this repository's release page.

5. In the setup window, enter:

   - Firebase project ID
   - Firebase service account JSON path
   - Android FCM token
   - PC name

6. Save the config, open `Diagnostics`, click `Send FCM test` to verify phone delivery, then click `Start relay`.

Local build outputs, when building from this workspace, are:

```text
D:\Documents\codex-alert\dist\codex-alert-v1-debug.apk
D:\Documents\codex-alert\dist\codex-alert-relay-single-exe-win-x64.exe
```

Keep the Firebase service account JSON outside this repo, for example:

```text
%LOCALAPPDATA%\CodexAlert\service-account.json
```

## Development Setup

Check local tooling before building from source:

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
- [Download google-services.json](docs/google-services-json.md)
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
