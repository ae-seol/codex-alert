# Testing

## What Was Tested Locally

On this PC, the following checks should pass after the environment setup:

```powershell
.\scripts\check-env.ps1
.\scripts\detect-codex-appid.ps1
.\android\gradlew.bat -p .\android :app:assembleDebug
dotnet build .\windows\CodexAlertRelay\CodexAlertRelay.csproj
dotnet build .\windows\CodexAlertSetup\CodexAlertSetup.csproj
```

The APK output is:

```text
android\app\build\outputs\apk\debug\app-debug.apk
```

The Windows relay output is:

```text
windows\CodexAlertRelay\bin\Debug\net8.0-windows10.0.19041.0\CodexAlertRelay.exe
```

The setup GUI output is:

```text
dist\setup-gui-single-win-x64\CodexAlertSetup.exe
```

## USB Phone Test

USB-C is the easiest way to test the Android app because it avoids LAN, port forwarding, and cloud push setup for the first UI check.

Phone setup:

1. Connect the phone with a USB-C data cable, not a charge-only cable.
2. Enable Developer options.
3. Enable USB debugging.
4. Accept the RSA debugging prompt on the phone.
5. Confirm the device appears:

   ```powershell
   $env:ANDROID_HOME=(Resolve-Path .\.tools\android-sdk).Path
   $env:Path="$env:ANDROID_HOME\platform-tools;$env:Path"
   adb devices -l
   ```

Install the debug APK:

```powershell
.\scripts\install-android-debug.ps1
```

Inject a local test alert into the app:

```powershell
.\scripts\inject-android-test-alert.ps1 -Title "Codex completed" -Body "Local adb test"
```

This validates the Android app UI/inbox path without LAN or FCM.

## USB Status Bar Notification Test

The debug APK includes a debug-only receiver named `com.codexalert.DEBUG_ALERT`. It is not part of release builds.

With the phone connected and USB debugging authorized:

```powershell
.\scripts\notify-android-debug-alert.ps1 -Title "Codex completed" -Body "Status bar test"
```

The script sends the app to the background, broadcasts a debug alert, stores it in the inbox, and asks Android to show a system notification. Check the phone status bar or notification shade.

To inspect posted notifications from the PC:

```powershell
$env:ANDROID_HOME=(Resolve-Path .\.tools\android-sdk).Path
$env:Path="$env:ANDROID_HOME\platform-tools;$env:Path"
adb shell dumpsys notification --noredact | Select-String "com.codexalert|Codex completed|Status bar test"
```

## Real FCM Test

After Firebase setup:

1. Put `android/app/google-services.json` in place.
2. Rebuild and reinstall the Android app.
3. Copy the FCM token shown in the app.
4. Create `config/pc.config.json` from `config/pc.example.json`.
5. Set `firebase.projectId`, `firebase.serviceAccountPath`, and `firebase.targetToken` or `firebase.targetTokens`.
6. Run:

   ```powershell
   pwsh .\scripts\send-test.ps1 -ConfigPath .\config\pc.config.json
   ```

This validates the cloud push path and does not require the phone to be on the same LAN.
The FCM payload is data-only/high-priority so the app service stores the inbox entry and posts on channel `codex_alerts`.

## Codex Internal Completion Test

The default relay path watches Codex Desktop internal session logs instead of waiting for a Windows toast.

List recent Codex completion events:

```powershell
.\windows\CodexAlertRelay\bin\Debug\net8.0-windows10.0.19041.0\CodexAlertRelay.exe --list-codex-completions --since-minutes 1440 --limit 10
```

Replay the newest local Codex completion event through FCM:

```powershell
.\windows\CodexAlertRelay\bin\Debug\net8.0-windows10.0.19041.0\CodexAlertRelay.exe --send-latest-codex-completion --since-minutes 1440
```

Run the watcher headlessly for a live Codex task:

```powershell
.\windows\CodexAlertRelay\bin\Debug\net8.0-windows10.0.19041.0\CodexAlertRelay.exe --run-headless --seconds 120
```

While that process is running, complete a Codex Desktop turn. The Android phone should receive a `Codex: <thread title>` notification and the app inbox should persist the message.

To inspect Android status bar notifications:

```powershell
$adb=".\.tools\android-sdk\platform-tools\adb.exe"
& $adb shell dumpsys notification --noredact | Select-String "pkg=com.codexalert|Codex:|codex_alerts"
```

To inspect the app inbox from USB debugging:

```powershell
$adb=".\.tools\android-sdk\platform-tools\adb.exe"
& $adb shell run-as com.codexalert cat shared_prefs/codex_alert_store.xml | Select-String "codex-internal-task-complete|Codex:"
```

## Optional Windows Toast Relay Test

Run:

```powershell
.\windows\CodexAlertRelay\bin\Debug\net8.0-windows10.0.19041.0\CodexAlertRelay.exe
```

Then in the setup window:

1. Set `relay.enableWindowsToastRelay` to `true` in config.
2. Request notification access.
3. Detect Codex AppID.
4. Paste Firebase settings and Android token.
5. Send test.
6. Start relay.

If Codex AppID detection fails on another PC, use `Observe sources`, trigger a Codex toast, then copy the AppID that appears.
