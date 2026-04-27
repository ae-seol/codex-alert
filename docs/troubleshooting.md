# Troubleshooting

This page covers the setup errors most users hit first in the Windows relay GUI.

## Where To Start

In the relay EXE, open:

```text
Diagnostics > Validate setup
```

The required checks are:

- `Firebase project ID`
- `Service account JSON`
- `Android token(s)`
- `Codex completion watcher`
- `oauth2.googleapis.com:443`
- `fcm.googleapis.com:443`

If `Send FCM test` fails, open:

```text
Diagnostics > Open logs
```

The log usually contains the exact FCM or OAuth error.

## Send FCM Test Failed

Check these values first:

- `Firebase project ID` must be the Firebase Project ID, not the Android package name.
- `Service account JSON` must be a Firebase Admin SDK private key JSON.
- `Android app FCM token(s)` must be copied from the installed Android app.
- The APK, service account JSON, and project ID must belong to the same Firebase project.

Correct examples:

```text
Firebase project ID: codex-alert
Android package: com.codexalert
Service account path: %LOCALAPPDATA%\CodexAlert\service-account.json
```

`com.codexalert` is not the Firebase Project ID.

## No Supported Key Formats Were Found

This means the relay tried to import `private_key` from the selected service account JSON, but the value was not a supported PEM private key.

The most common cause is selecting this file by mistake:

```text
google-services.json
```

That file is only for the Android APK. It is not the Windows relay service account key.

The Windows relay needs a Firebase Admin SDK private key JSON. Download it from:

```text
Firebase Console > Project settings > Service accounts > Firebase Admin SDK > Generate new private key
```

The correct JSON contains fields like:

```json
{
  "type": "service_account",
  "project_id": "codex-alert",
  "private_key_id": "...",
  "private_key": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n",
  "client_email": "...@...iam.gserviceaccount.com",
  "token_uri": "https://oauth2.googleapis.com/token"
}
```

Do not paste the contents of this file into chat or commit it to Git. It is a secret.

## OAuth Token Request Failed

Typical causes:

- The service account JSON is not a valid Firebase Admin SDK key.
- The private key was edited or corrupted.
- The PC clock is far off, causing JWT `iat` or `exp` validation to fail.
- `oauth2.googleapis.com:443` is blocked by a firewall, proxy, VPN, or company network.

Fixes:

- Generate a fresh Firebase Admin SDK private key.
- Save it outside the repo, for example `%LOCALAPPDATA%\CodexAlert\service-account.json`.
- Select it again in the relay GUI and click `Save config`.
- Run `Diagnostics > Validate setup`.

## FCM Send Failed: 403 Or Sender ID Mismatch

Typical causes:

- `Firebase project ID` does not match the service account JSON project.
- The Android APK was built with a `google-services.json` from a different Firebase project.
- The Android FCM token came from an app install tied to a different Firebase project.
- The service account does not have permission to send FCM messages.

Fixes:

- Confirm the Firebase Project ID in `Project settings > General`.
- Confirm the service account JSON has the same `project_id`.
- Rebuild/reinstall the APK with the matching `google-services.json`.
- Open the Android app again and copy the new FCM token.
- Generate a new Firebase Admin SDK private key if unsure.

## FCM Send Failed: Invalid Or Unregistered Token

Typical errors include:

```text
INVALID_ARGUMENT
UNREGISTERED
Requested entity was not found
The registration token is not a valid FCM registration token
```

Fixes:

- Open the Android app and copy the current FCM token.
- Paste it into `Android app FCM token(s)`.
- Click `Save config`.
- If the token still fails, uninstall and reinstall the APK, then copy the new token.

FCM tokens can change after reinstalling the app, clearing app data, or changing Firebase projects.

## Codex Session Watcher Is Disabled

Current releases do not use Windows toast notifications or Codex AppID detection. The relay must watch Codex session logs.

Fix in the GUI:

```text
Advanced > Watch Codex Desktop completion events > checked
Setup > Save config
Setup > Start relay
```

Fix in `pc.config.json`:

```json
"relay": {
  "enableCodexSessionWatcher": true
}
```

## Codex App Finder Or AppID

There is no Codex AppID key to configure in current releases.

The relay does not find the Codex Windows app package. It reads Codex Desktop session JSONL files directly from:

```text
%USERPROFILE%\.codex\sessions
```

When Codex Desktop writes a `task_complete` event, the relay forwards that event to Android. This is why the setup no longer asks for:

- Windows notification access
- Codex AppID
- `allowedAppIds`
- Windows toast source filters

If Codex uses a nonstandard home directory, set:

```json
"relay": {
  "codexHomePath": "D:\\path\\to\\.codex"
}
```

Leave it empty for the normal `%USERPROFILE%\.codex` location.
