# Subagents Security Workflow

This repository defines three Codex subagent role specs under `.codex/subagents/`.

The workflow is defensive:

```text
git-downloader -> security-attacker -> security-updater
```

## Roles

`git-downloader` prepares a clean local copy of a GitHub repository. It verifies the remote URL, branch, and commit SHA before any review. It does not run downloaded scripts until the repository has been inspected.

`security-attacker` performs authorized security testing against this project or another repository that the user explicitly owns or controls. The role is limited to static analysis, dependency review, local test cases, and controlled proof-of-concept inputs. It must not target public third-party systems, collect credentials, persist access, or perform destructive actions.

`security-updater` receives findings from `security-attacker`, applies minimal fixes, adds or updates tests where practical, and documents residual risk. It does not hide unresolved findings.

## Handoff Contract

Each subagent should produce a short Markdown report:

```text
role:
repo:
branch:
commit:
scope:
actions:
findings:
changed-files:
verification:
residual-risk:
```

`security-attacker` findings should include severity, affected file/path, reproduction steps against local code only, and a recommended fix direction.

`security-updater` should link every fix to an attacker finding or explain why a finding was not changed.

## Boundaries

Allowed:

```text
Static code review
Dependency and configuration review
Local-only tests
Defensive fuzz or malformed-input tests against local code
Secret-scanning for files in the authorized workspace
GitHub issue/PR review for repositories the user controls
```

Not allowed:

```text
Scanning or exploiting third-party targets
Credential theft or token exfiltration
Persistence, evasion, or stealth behavior
Destructive payloads
Bypassing access controls outside an owned test environment
Publishing secrets found during review
```

## Local Use

When asking Codex to use these roles, keep the scope explicit:

```text
Use .codex/subagents/git-downloader.md to fetch ae-seol/codex-alert into a clean folder.
Use .codex/subagents/security-attacker.md to review only that local checkout.
Use .codex/subagents/security-updater.md to patch findings in the working tree.
```

The downloader role can use the helper script:

```powershell
.\scripts\download-authorized-repo.ps1 -Destination ..\codex-alert-review
```

For this repository, the first security review should focus on:

```text
config/pc.config.json and Firebase token handling
android/app/google-services.json handling
FCM service account path handling
Codex session JSONL parsing
Windows notification relay filtering
Release artifact separation
```
