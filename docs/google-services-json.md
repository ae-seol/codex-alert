# google-services.json 받는 위치

이 문서는 Codex Alert Android 앱 빌드에 필요한 Firebase 설정 파일을 어디서 받는지 설명합니다.

Firebase Console은 로그인과 프로젝트 권한이 필요한 화면이라 실제 스크린샷을 문서에 고정해 두기 어렵습니다. 대신 화면에서 어디를 봐야 하는지 스크린샷처럼 따라갈 수 있게 위치를 적었습니다.

가장 빠른 시작 링크는 아래입니다:

```text
https://console.firebase.google.com/project/codex-alert/settings/general/android:com.codexalert
```

이 링크가 바로 열리면 `Project settings > General` 화면의 `com.codexalert` Android 앱 위치로 이동합니다. 로그인이나 권한 때문에 다른 화면이 나오면 아래 순서대로 들어가면 됩니다.

## 받아야 하는 파일

받아야 하는 파일 이름은 정확히 이것입니다:

```text
google-services.json
```

이 파일은 아래 Android 앱용 Firebase 설정 파일입니다:

```text
Firebase project: codex-alert
Android package: com.codexalert
```

주의할 점:

- `google-services.json`은 Android 앱 빌드에 들어가는 Firebase 앱 설정 파일입니다.
- `firebase-adminsdk-...json`처럼 생긴 서비스 계정 키와 다릅니다.
- 서비스 계정 키는 Android 앱에 넣으면 안 됩니다.
- 이 repo에서는 `android/app/google-services.json`도 Git에 올리지 않도록 ignore합니다. 로컬 빌드용으로만 둡니다.

## 화면에서 찾는 위치

Firebase Console 안에서 이동 경로는 이렇습니다:

```text
Firebase Console
-> codex-alert 프로젝트
-> Project settings
-> General 탭
-> Your apps
-> Android app: com.codexalert
-> google-services.json 다운로드 버튼
```

화면에서는 대략 아래 영역을 찾으면 됩니다:

```text
Project settings

[ General ] [ Cloud Messaging ] [ Integrations ] [ Service accounts ] ...

Your apps

Android apps
+-------------------------------------------------------------+
| com.codexalert                                              |
| App ID: 1:...:android:...                                   |
|                                                             |
| SDK setup and configuration                                 |
|                                                             |
| [ google-services.json ]   <- 이 버튼/파일명을 클릭         |
+-------------------------------------------------------------+
```

대부분 화면의 아래쪽에 `Your apps` 카드가 있습니다. Android 앱 카드가 접혀 있으면 `com.codexalert` 줄이나 카드를 먼저 눌러 펼치면 됩니다.

## 단계별 안내

1. Firebase Console을 엽니다:

   ```text
   https://console.firebase.google.com/
   ```

2. 프로젝트 목록에서 아래 프로젝트를 선택합니다:

   ```text
   codex-alert
   ```

3. 프로젝트 설정으로 들어갑니다:

   ```text
   왼쪽 위 톱니바퀴 아이콘 -> Project settings
   ```

   보통 Firebase 로고/프로젝트 이름 근처에 톱니바퀴가 있습니다.

4. 상단 탭에서 `General`이 선택되어 있는지 확인합니다.

5. 아래쪽으로 스크롤해서 `Your apps` 섹션을 찾습니다.

6. Android 앱 목록에서 아래 앱을 찾습니다:

   ```text
   com.codexalert
   ```

7. 앱 카드 안에서 아래 버튼 또는 파일명을 클릭합니다:

   ```text
   google-services.json
   ```

   Firebase UI에 따라 `Download google-services.json`, `google-services.json`, 또는 다운로드 아이콘으로 보일 수 있습니다.

8. 받은 파일을 이 위치에 둡니다:

   ```text
   D:\Documents\codex-alert\android\app\google-services.json
   ```

## com.codexalert 앱이 안 보일 때

`Your apps`에 `com.codexalert`가 없다면 Android 앱을 아직 등록하지 않은 상태입니다.

1. `Project settings > General > Your apps`에서 `Add app`을 누릅니다.
2. Android 아이콘을 선택합니다.
3. Android package name에 아래 값을 넣습니다:

   ```text
   com.codexalert
   ```

4. App nickname은 선택 사항입니다. 넣고 싶으면 이렇게 넣습니다:

   ```text
   Codex Alert
   ```

5. SHA-1은 이 프로젝트의 기본 FCM 알림 전송에는 필수값이 아닙니다. 비워도 됩니다.
6. `Register app`을 누릅니다.
7. 다음 단계에서 `google-services.json`을 다운로드합니다.

## Setup GUI에서 쓰는 방법

`CodexAlertSetup.exe`를 쓰는 경우:

1. `google-services.json` 옆의 `Browse`를 누릅니다.
2. 방금 다운로드한 `google-services.json`을 선택합니다.
3. `Validate Firebase JSON`을 누릅니다.
4. `Copy Firebase JSON`을 누릅니다.
5. `Build APK`를 누릅니다.

GUI는 파일을 아래 위치로 복사합니다:

```text
D:\Documents\codex-alert\android\app\google-services.json
```

## 맞는 파일인지 빠르게 확인

다운로드한 파일을 메모장으로 열어서 아래 값이 있는지만 확인하면 됩니다:

```json
{
  "project_info": {
    "project_id": "codex-alert"
  },
  "client": [
    {
      "client_info": {
        "android_client_info": {
          "package_name": "com.codexalert"
        }
      }
    }
  ]
}
```

실제 파일에는 더 많은 항목이 들어 있습니다. 중요한 확인 포인트는 세 가지입니다:

- `project_id`가 `codex-alert`인지
- `package_name`이 `com.codexalert`인지
- 파일명이 정확히 `google-services.json`인지

## 자주 헷갈리는 부분

- 다른 Firebase 프로젝트에서 받은 파일이면 안 됩니다.
- 다른 Android package name으로 등록된 앱에서 받은 파일이면 안 됩니다.
- `firebase-adminsdk-...json`은 `google-services.json`이 아닙니다.
- `Downloads` 폴더에만 두고 APK를 빌드하면 Gradle이 못 찾습니다. `android/app/` 아래에 있어야 합니다.
- 브라우저가 `google-services (1).json`처럼 이름을 바꾸면, 최종 위치에서는 `google-services.json`으로 바꿔야 합니다.

## 참고

- Firebase Help: Download Firebase config file or object: https://support.google.com/firebase/answer/7015592
- Firebase Help: Project general settings and app config files: https://support.google.com/firebase/answer/7000104
- Firebase Android setup: https://firebase.google.com/docs/android/setup
