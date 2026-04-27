# GitHub Publish Guide

This project is safe to publish only after local secrets, generated artifacts, and machine-specific files are kept out of Git.

Target account:

```text
https://github.com/ae-seol
```

Recommended repository name:

```text
codex-alert
```

## File Separation

Commit these source files and project docs:

```text
.gitattributes
.gitignore
.codex/subagents/
.vscode/extensions.json
.vscode/tasks.json
README.md
android/build.gradle.kts
android/settings.gradle.kts
android/gradle.properties
android/gradlew
android/gradlew.bat
android/gradle/wrapper/gradle-wrapper.jar
android/gradle/wrapper/gradle-wrapper.properties
android/app/build.gradle.kts
android/app/src/
assets/
config/pc.example.json
docs/
scripts/
windows/CodexAlertRelay/
windows/CodexAlertSetup/
```

Keep these local-only files out of Git:

```text
config/pc.config.json
android/app/google-services.json
android/local.properties
android/.kotlin/
*.keystore
*.jks
*.p12
*.pfx
*.pem
*.key
key.properties
service-account*.json
*service-account*.json
firebase-adminsdk*.json
*firebase-adminsdk*.json
*-firebase-adminsdk-*.json
*.env
.env.*
```

Do not publish generated or machine-local output:

```text
.tools/
.tmp/
dist/
history/
logs/
*.log
*.jsonl
android/.gradle/
android/.kotlin/
android/**/build/
android/**/captures/
windows/**/bin/
windows/**/obj/
windows/**/AppPackages/
windows/**/BundleArtifacts/
windows/**/publish/
.vs/
TestResults/
*.apk
*.aab
*.zip
*.nupkg
*.binlog
```

## Public Setup

Install Git if `git` is not available in PowerShell:

```powershell
winget install Git.Git
```

Restart PowerShell, then configure identity:

```powershell
git config --global user.name "ae-seol"
git config --global user.email "YOUR_GITHUB_EMAIL"
```

Initialize the local repository:

```powershell
cd D:\Documents\codex-alert
git init
git branch -M main
```

## GitHub Repository Settings

Create the GitHub repository with these values:

```text
Owner: ae-seol
Repository name: codex-alert
Visibility: Private first, then switch to Public after the publish check passes
Initialize with README: No
Add .gitignore: No
Choose a license: No, unless you already know the license you want
Default branch: main
```

After the first push, recommended settings are:

```text
Settings > General > Features: disable Wikis and Projects unless you plan to use them
Settings > Pull Requests: enable squash merge if you want a clean history
Settings > Branches: add a branch protection rule for main after the first push
Releases: upload APK/ZIP artifacts here instead of committing dist/
```

Before staging, run the publish check:

```powershell
.\scripts\check-publish-files.ps1
```

Stage only the source tree:

```powershell
git add .gitattributes .gitignore .vscode README.md android assets config\pc.example.json docs scripts windows .codex
git status --short
```

Verify these files do not appear in `git status`:

```text
config/pc.config.json
android/app/google-services.json
android/local.properties
.tools/
.tmp/
dist/
history/
```

Create the first commit:

```powershell
git commit -m "Initial Codex Alert project"
```

Create an empty GitHub repository under `ae-seol` named `codex-alert`, then connect it:

```powershell
git remote add origin https://github.com/ae-seol/codex-alert.git
git push -u origin main
```

If you use SSH instead of HTTPS:

```powershell
git remote set-url origin git@github.com:ae-seol/codex-alert.git
git push -u origin main
```

## Secret Handling

The current local config may contain an Android FCM registration token. Treat it as private runtime state. Do not paste it into issues, commits, screenshots, or release notes.

Firebase service account JSON files contain private keys. Store them outside this repository, for example:

```text
%LOCALAPPDATA%\CodexAlert\service-account.json
```

If any secret was committed by mistake, remove it from Git history and rotate the Firebase key/token. Deleting it in a later commit is not enough for a public repository.

## Release Artifacts

Keep release artifacts out of the source tree. Upload APK and Windows EXE files to GitHub Releases instead of committing them:

```text
dist/codex-alert-v1-debug.apk
dist/codex-alert-relay-single-exe-win-x64.exe
```

Recommended release tag:

```text
v1.0.0
```

Recommended release assets:

```text
codex-alert-v1-debug.apk
codex-alert-relay-single-exe-win-x64.exe
SHA256SUMS.txt
```
