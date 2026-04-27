# Setup GUI

`CodexAlertSetup.exe` is a Windows GUI helper for the setup/build work that should not be repeated by hand every time.

`CodexAlertRelay.exe` also now includes a beginner-friendly setup GUI. For most users who already have an APK and only need to connect a Windows PC to an Android phone, use the relay GUI first.

## Relay One-EXE GUI

Easiest Windows distribution:

```text
D:\Documents\codex-alert\dist\codex-alert-relay-single-exe-win-x64.zip
```

Extract it and run:

```text
CodexAlertRelay.exe
```

This is a self-contained single-file Windows relay. It does not require installing .NET Desktop Runtime.

The relay GUI has three tabs:

- `Setup`: enter Firebase project ID, service account JSON path, Android FCM token(s), and PC name, then click `Save config`, `Send FCM test`, `Start relay`.
- `Diagnostics`: run setup checks, send FCM tests, list recent Codex completions, and open logs/config.
- `Advanced`: change Codex home, enable optional Windows toast relay, detect Codex AppID, or stop the relay.

Important: the single EXE does not include secrets. Each trusted PC still needs a local Firebase Admin SDK service account JSON and one or more Android FCM tokens.

The screenshot-based distribution guide lives in Obsidian:

```text
D:\Documents\Obsidian Vault\codex-alert\Codex Alert - 배포 사용자 가이드.md
```

It does four practical jobs:

- Copies and validates `google-services.json`.
- Builds the Firebase-enabled Android APK.
- Copies and validates the Firebase service account JSON into a user-local path.
- Creates and tests per-PC `pc.config.json` files, including one or more Android FCM target tokens.

## Built GUI

Single-file self-contained GUI:

```text
D:\Documents\codex-alert\dist\setup-gui-single-win-x64\CodexAlertSetup.exe
```

Framework-dependent development build:

```text
D:\Documents\codex-alert\windows\CodexAlertSetup\bin\Debug\net8.0-windows\CodexAlertSetup.exe
```

The self-contained build is intended for convenience on Windows PCs that do not already have the .NET runtime. APK building still needs JDK 17, Android SDK, and the Android Gradle project on the machine doing the build.

## One-Time Project Setup

Do this once per Firebase project:

1. Open Firebase Console.
2. Create a Spark plan project.
3. Add Android app package:

   ```text
   com.codexalert
   ```

4. Download `google-services.json` from `Project settings > General > Your apps > com.codexalert`.
   If you are using the direct Firebase Console URL, open:

   ```text
   https://console.firebase.google.com/project/codex-alert/settings/general/android:com.codexalert
   ```

   A more detailed click-by-click guide is in:

   ```text
   docs/google-services-json.md
   ```

5. In the GUI, select that file and click:

   ```text
   Validate Firebase JSON
   Copy Firebase JSON
   Build APK
   ```

6. The APK is written to:

   ```text
   D:\Documents\codex-alert\dist\codex-alert-v1-debug.apk
   ```

This APK can be installed on any Android device that should receive Codex alerts. You do not need a different APK per phone.

## Per-Android Setup

For every Android phone or tablet:

1. Download and install the same APK.
2. Open the app once.
3. Allow notification permission.
4. Copy the FCM registration token shown in the app.
5. Paste that token into the GUI's `Android token(s)` field on every PC that should send alerts to that Android device.

Each Android device has its own FCM token. One APK, many tokens.

## Per-PC Setup

For every Windows PC that should forward Codex notifications:

1. Copy or clone this project, or extract the relay zip:

   ```text
   D:\Documents\codex-alert\dist\codex-alert-windows-relay-win-x64.zip
   ```

2. Run the GUI or edit `pc.config.json` manually.
3. Select the Firebase service account JSON.
4. Click `Copy key` to place it under:

   ```text
   %LOCALAPPDATA%\CodexAlert\service-account.json
   ```

5. Paste one or more Android FCM tokens.
6. For the default internal Codex completion watcher, leave Windows toast relay disabled. Codex AppID detection is not required.
7. Optional: click `Detect` next to Codex AppID only if you enabled Windows toast relay.
8. Click:

   ```text
   Save PC config
   Check network/config
   Send FCM test
   ```

9. Start the relay:

   ```powershell
   .\relay\CodexAlertRelay.exe
   ```

The relay can also be tested without opening the tray UI:

```powershell
.\relay\CodexAlertRelay.exe --list-codex-completions --since-minutes 1440 --limit 10
.\relay\CodexAlertRelay.exe --send-latest-codex-completion --since-minutes 1440
.\relay\CodexAlertRelay.exe --run-headless --seconds 120
```

## Multiple PC And Multiple Android Patterns

Multiple PCs to one Android:

```text
PC A config targetTokens = [Phone 1 token]
PC B config targetTokens = [Phone 1 token]
PC C config targetTokens = [Phone 1 token]
```

One PC to multiple Android devices:

```text
PC A config targetTokens = [
  Phone 1 token,
  Phone 2 token,
  Tablet token
]
```

Multiple PCs to multiple Android devices:

```text
Every PC config contains the token list for the Android devices that should receive that PC's alerts.
```

The Android app does not need to know the PC list in v1. PC identity travels in the FCM payload as `pcId` and `pcName`, and the Android inbox displays it. App-side PC pairing, QR pairing, and token upload are v2 improvements.

## Firebase Secret Handling

The service account key is not part of the APK and must not be copied to Android.

Do:

- Keep it under `%LOCALAPPDATA%\CodexAlert\service-account.json`.
- Copy it only to trusted PCs that may send alerts.
- Revoke the key from Firebase/Google Cloud if a PC is no longer trusted.

Do not:

- Commit it to Git.
- Put it in `dist/`.
- Put it in the APK.
- Upload it next to APK download links.
- Send it to phones.

## Firebase Links

- Firebase Console: https://console.firebase.google.com/
- Firebase service account key page: `https://console.firebase.google.com/project/<project-id>/settings/serviceaccounts/adminsdk`
- FCM HTTP v1 auth: https://firebase.google.com/docs/cloud-messaging/auth-server
- Android FCM client setup: https://firebase.google.com/docs/cloud-messaging/android/client
- Firebase pricing: https://firebase.google.com/pricing
