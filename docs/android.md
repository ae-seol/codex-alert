# Android App Setup

The Android app is a small native Kotlin app. It displays the phone's FCM registration token, stores received Codex alerts in a local inbox, and shows Android system notifications.

## Requirements

- JDK 17
- Android Studio or Android SDK command line tools
- Android SDK platform for the configured `compileSdk`
- A physical Android phone or emulator with Google Play services
- `android/app/google-services.json` from Firebase

Run:

```powershell
.\scripts\check-env.ps1
```

## Build

From the Android project:

```powershell
cd android
.\gradlew.bat :app:assembleDebug
```

If the Gradle wrapper jar is missing, install Gradle or run the project once from Android Studio to regenerate the wrapper.

## Install

With a device connected:

```powershell
cd android
.\gradlew.bat :app:installDebug
```

Open the app, allow notification permission on Android 13 or newer, then copy the displayed FCM token into `config/pc.config.json`.

## Runtime Behavior

- Foreground and background FCM messages are saved to the local inbox.
- The app creates notification channel `codex_alerts`.
- If notification permission is denied, inbox storage still works but Android system notifications are not displayed.
- If FCM rotates the registration token, the app displays the new token and marks that the PC config needs updating.

## Reference

- Android notification runtime permission: https://developer.android.com/guide/topics/ui/notifiers/notification-permission
