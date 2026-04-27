# Firebase Setup

Codex Alert uses Firebase Cloud Messaging because the Windows PC can send outbound HTTPS requests without opening inbound ports.

## 1. Create Firebase Project

1. Open the Firebase console.
2. Create a project or reuse an existing project.
3. Add an Android app with package name:

   ```text
   com.codexalert
   ```

4. Download `google-services.json` from `Project settings > General > Your apps > com.codexalert`.
   For a more detailed click-by-click guide, see [google-services-json.md](google-services-json.md).
5. Put the downloaded file at:

   ```text
   android/app/google-services.json
   ```

The file is ignored by git.

## 2. Enable FCM HTTP v1

In Google Cloud Console for the same project:

1. Confirm the Firebase Cloud Messaging API is enabled.
2. Create a service account or use an existing one.
3. Grant the service account permission to send FCM messages. Firebase's HTTP v1 documentation recommends using OAuth 2.0 credentials from a service account.
4. Download the service account JSON.
5. Store it outside the repo, for example:

   ```text
   %LOCALAPPDATA%\CodexAlert\service-account.json
   ```

## 3. Configure Windows Relay

Copy the example config:

```powershell
Copy-Item .\config\pc.example.json .\config\pc.config.json
```

Edit:

- `firebase.projectId`
- `firebase.serviceAccountPath`
- `firebase.targetToken` for a single Android device
- `firebase.targetTokens` for one or more Android devices
- `allowedAppIds`

The Android app displays the FCM registration token on the first screen. Install the same APK on additional Android devices and copy each device's token into `firebase.targetTokens`.

## 4. Send Test Message

After adding `pc.config.json`, run:

```powershell
.\scripts\check-fcm-network.ps1 -ConfigPath .\config\pc.config.json
```

Then send a test push:

```powershell
pwsh .\scripts\send-test.ps1 -ConfigPath .\config\pc.config.json
```

`send-test.ps1` requires PowerShell 7 or another PowerShell runtime backed by a .NET version that supports `RSA.ImportFromPem`.

The Windows relay sends data-only, high-priority FCM messages. The Android app receives them in `FirebaseMessagingService`, stores them in the local inbox, and posts the visible notification on channel `codex_alerts`. This avoids Android's fallback FCM notification channel and keeps the inbox path reliable while the app is in the background.

## References

- Firebase FCM HTTP v1: https://firebase.google.com/docs/cloud-messaging/auth-server
- External network checklist: external-network-and-firebase.md
