# Connect Another Windows PC

Firebase setup and APK build are one-time project tasks. After the Android APK is installed on the phone, each additional Windows PC only needs a local relay config, the Firebase service account JSON, and the Android FCM token.

## Connection Model

Each PC sends directly to FCM over outbound HTTPS:

```text
Windows PC A -> Firebase FCM -> Android phone
Windows PC B -> Firebase FCM -> Android phone
Windows PC C -> Firebase FCM -> Android phone
```

The PCs do not need to know each other's IP addresses. They do not need the same LAN, port forwarding, DDNS, VPN, or inbound firewall rules.

## What Is Reused

Reused for every PC:

- Firebase project id, for example `codex-alert`
- Android APK built with `android/app/google-services.json`
- Android FCM registration token(s) from the installed app(s)
- Firebase service account JSON, copied only to trusted PCs

Different on every PC:

- `pcId`
- `pcName`
- Local service account file path, if you choose a different path

## GUI Path

The easiest setup path is:

```text
D:\Documents\codex-alert\dist\setup-gui-single-win-x64\CodexAlertSetup.exe
```

Use the GUI to select `google-services.json`, build the APK, copy the service account key, paste Android token(s), save `pc.config.json`, run the network check, and send a test push.

## Security Boundary

The service account JSON lets a PC send FCM messages for the Firebase project. Treat it as a secret.

Do:

- Put it under `%LOCALAPPDATA%\CodexAlert\service-account.json`
- Copy it only to PCs you trust
- Remove it from a PC when that PC should no longer send alerts

Do not:

- Put it in the APK
- Put it in Git
- Put it in a shared APK download folder
- Send it to the Android phone

If a PC is lost or no longer trusted, revoke that key in Google Cloud / Firebase service accounts and generate a new one.

## Per-PC Setup

1. Copy or clone this project folder to the target PC. If you only need the relay, copy `dist/codex-alert-windows-relay-win-x64.zip` and extract it.
2. Make sure the target PC has outbound HTTPS access:

   ```powershell
   cd D:\Documents\codex-alert
   .\scripts\check-fcm-network.ps1 -SkipConfig
   ```

3. Install .NET Desktop Runtime 8 or .NET SDK 8 if needed:

   ```powershell
   winget install Microsoft.DotNet.DesktopRuntime.8
   ```

4. Copy the service account JSON to the target PC:

   ```powershell
   New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\CodexAlert"
   Copy-Item "C:\path\to\service-account.json" "$env:LOCALAPPDATA\CodexAlert\service-account.json"
   ```

5. Create the local config:

   ```powershell
   Copy-Item .\config\pc.example.json .\config\pc.config.json
   notepad .\config\pc.config.json
   ```

6. Fill these fields:

   ```text
   pcId
   pcName
   firebase.projectId
   firebase.serviceAccountPath
   firebase.targetToken
   firebase.targetTokens
   ```

7. Validate the config:

   ```powershell
   .\scripts\check-fcm-network.ps1 -ConfigPath .\config\pc.config.json
   ```

8. Send a push test:

   ```powershell
   pwsh .\scripts\send-test.ps1 -ConfigPath .\config\pc.config.json -Title "Codex test from PC B" -Body "Another PC is connected."
   ```

9. Start the relay. If you copied the full repo, run:

    ```powershell
    .\windows\CodexAlertRelay\bin\Debug\net8.0-windows10.0.19041.0\CodexAlertRelay.exe
    ```

    If you extracted the relay zip, run:

    ```powershell
    .\relay\CodexAlertRelay.exe
    ```

## Example Config For A Second PC

```json
{
  "pcId": "laptop-work",
  "pcName": "Work Laptop",
  "firebase": {
    "projectId": "codex-alert",
    "serviceAccountPath": "C:\\Users\\Admin\\AppData\\Local\\CodexAlert\\service-account.json",
    "targetToken": "ANDROID_FCM_TOKEN_FROM_THE_PHONE_APP",
    "targetTokens": [
      "ANDROID_FCM_TOKEN_FROM_THE_PHONE_APP",
      "OPTIONAL_SECOND_ANDROID_TOKEN"
    ]
  },
  "relay": {
    "dedupeWindowSeconds": 30,
    "sendRetries": 3
  }
}
```

## Token Changes

The Android FCM token normally stays stable, but it can change after:

- Reinstalling the app
- Clearing app data
- Restoring the phone
- Firebase rotating the token

If the app shows "PC config update needed", copy the new token into every PC's `pc.config.json`.

## Multiple Android Devices

The relay supports both old single-token config and new multi-token config.

To send to another phone or tablet:

1. Install the same APK on that Android device.
2. Open the app and copy that device's FCM token.
3. Add that token to the PC's `firebase.targetTokens` list.

The Android app does not need to register PC information in v1. The PC sends `pcId` and `pcName` in every push payload, so the inbox can show where the alert came from. App-side pairing and QR token exchange are v2 candidates.
