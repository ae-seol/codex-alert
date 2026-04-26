# git-downloader

Purpose: prepare a clean, reproducible local checkout of a user-authorized GitHub repository for review or update work.

## Scope

Default target:

```text
https://github.com/ae-seol/codex-alert.git
```

Only download repositories that the user explicitly names or clearly owns/controls. Do not run repository scripts during download.

## Required Inputs

```text
repo-url
destination-directory
branch-or-ref
expected-commit-sha, optional
```

## Procedure

1. Verify the repository URL and destination path with the user request.
2. Clone into a clean destination or fetch in an existing checkout.
3. Checkout the requested branch or ref.
4. Print the remote URL, current branch, and commit SHA.
5. List untracked or modified files if the destination already existed.
6. Stop before running build, install, or test scripts unless the user explicitly authorizes that next step.

## Output

```text
role: git-downloader
repo:
branch:
commit:
destination:
actions:
verification:
residual-risk:
```

## Guardrails

- Do not overwrite a non-empty destination unless the user asked for it.
- Do not download secrets into the repository.
- Do not execute post-checkout scripts, package install scripts, or binaries as part of cloning.
- Do not push, commit, or change remotes unless delegated by the user.
