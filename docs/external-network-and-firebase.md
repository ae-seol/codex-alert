# External Network and Firebase Checklist

This project does not require the Android phone and Windows PC to be on the same LAN. The Windows relay sends outbound HTTPS requests to Firebase Cloud Messaging (FCM), and FCM delivers the push notification to the phone.

## Free Plan Boundary

Use the Firebase Spark plan and only Firebase Cloud Messaging for v1.

Free for this project:

- Firebase Cloud Messaging (FCM)
- FCM registration token generation in the Android app
- FCM HTTP v1 sends from the Windows relay
- Firebase App Distribution, if you choose it for APK distribution

Avoid for a zero-cost test:

- Cloud Functions
- Cloud Firestore
- Realtime Database
- Cloud Storage
- Firebase Hosting for public downloads at scale
- Phone or SMS Authentication
- Cloud Run, Pub/Sub, Maps, or other paid Google Cloud services

## PC Network Requirements

The PC needs outbound HTTPS access only. It does not need inbound firewall rules, port forwarding, a static IP, DDNS, VPN, or the same LAN as the phone.

Required outbound destinations:

```text
https://oauth2.googleapis.com
https://fcm.googleapis.com
```

Run the network check:

```powershell
cd D:\Documents\codex-alert
.\scripts\check-fcm-network.ps1 -SkipConfig
```

After `config/pc.config.json` exists, run:

```powershell
cd D:\Documents\codex-alert
.\scripts\check-fcm-network.ps1 -ConfigPath .\config\pc.config.json
```

The script checks:

- DNS resolution for Google OAuth and FCM hosts
- TCP 443 connectivity
- HTTPS/TLS reachability
- Optional local config fields and service account JSON shape

Any HTTP response from Google, including 401, 403, 404, or 405, means the HTTPS path reached Google. Authentication is verified later by `send-test.ps1`.

## Firebase Account Tasks

1. Create a Firebase project on the Spark plan.
2. Add an Android app with package name:

   ```text
   com.codexalert
   ```

3. Download `google-services.json`.
4. Save it here:

   ```text
   D:\Documents\codex-alert\android\app\google-services.json
   ```

5. Confirm Firebase Cloud Messaging API is enabled in the same Google Cloud project.
6. Create or choose a service account for the Windows relay.
7. Grant it permission to send FCM messages, such as Firebase Cloud Messaging API Admin for this project.
8. Download the service account JSON.
9. Save it outside the repo, for example:

   ```text
   C:\Users\Admin\AppData\Local\CodexAlert\service-account.json
   ```

10. Do not put the service account JSON in the APK, GitHub, shared Drive folder, or this repository.

## Android APK Tasks

After placing `google-services.json`, rebuild the APK:

```powershell
cd D:\Documents\codex-alert
.\android\gradlew.bat -p .\android :app:assembleDebug
```

Distribute this file to the phone:

```text
D:\Documents\codex-alert\android\app\build\outputs\apk\debug\app-debug.apk
```

Install it from a trusted private link, open the app, allow notifications, and copy the FCM registration token shown on the first screen.

## Windows Relay Config Tasks

Create the local config:

```powershell
cd D:\Documents\codex-alert
Copy-Item .\config\pc.example.json .\config\pc.config.json
notepad .\config\pc.config.json
```

Fill these fields:

```text
firebase.projectId
firebase.serviceAccountPath
firebase.targetToken
firebase.targetTokens
```

## End-to-End Free Test

1. Check PC network:

   ```powershell
   .\scripts\check-fcm-network.ps1 -SkipConfig
   ```

2. Rebuild and install the Firebase-enabled APK.
3. Copy the Android FCM token.
4. Fill `config/pc.config.json`.
   - Use `firebase.targetToken` for one Android device.
   - Use `firebase.targetTokens` for one or more Android devices.
5. Check config and network together:

   ```powershell
   .\scripts\check-fcm-network.ps1 -ConfigPath .\config\pc.config.json
   ```

6. Send a test push:

   ```powershell
   pwsh .\scripts\send-test.ps1 -ConfigPath .\config\pc.config.json
   ```

7. Confirm the Android status bar notification and app inbox entry.
8. Start the Windows relay and complete a Codex Desktop turn.

## Troubleshooting

If DNS or TCP fails:

- Check whether the PC network blocks Google APIs.
- Try another network or hotspot.
- Check corporate proxy or firewall policy.

If HTTPS succeeds but `send-test.ps1` fails:

- Verify `firebase.projectId`.
- Verify the service account JSON belongs to the same project.
- Verify the service account has FCM send permission.
- Verify the Android FCM token is from the newly rebuilt APK.

If the send succeeds but no status bar notification appears:

- Open the Android app once after installation.
- Allow notification permission.
- Check Android app notification settings for Codex Alert.
- Disable aggressive battery restrictions for the app if notifications are delayed.

## References

- Firebase pricing: https://firebase.google.com/pricing
- Firebase pricing plans: https://firebase.google.com/docs/projects/billing/firebase-pricing-plans
- Firebase FCM HTTP v1 auth: https://firebase.google.com/docs/cloud-messaging/auth-server
- Android notification permission: https://developer.android.com/guide/topics/ui/notifiers/notification-permission
