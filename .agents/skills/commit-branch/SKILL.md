---
name: commit-branch
description: Slash command (/commit-branch) to automatically generate a descriptive new git branch name from the current changes, create and switch to that branch, and commit all changes there. Use when the user invokes /commit-branch or asks to commit current changes on a new branch.
---

# `/commit-branch` — Create New Branch & Commit

Analyzes current workspace changes, creates a **new git branch** with a descriptive agent-generated name, switches to it, and commits the changes cleanly.

---

## Steps

1. **Inspect Current Git State & Existing Branches:**
   Run in the repository root:
   ```powershell
   git branch --show-current
   git status --short
   git diff --stat
   git diff --cached --stat
   git branch -a
   git log -n 5 --oneline
   ```

2. **Check for Changes:**
   - If `git status --short` produces no output (no staged, unstaged, or untracked changes), inform the user that there are no changes to commit and stop without creating a new branch.

3. **Generate a Descriptive Branch Name & Commit Message:**
   - Inspect `git diff`, `git diff --cached`, and any untracked files to determine the intent and scope of the changes.
   - Generate a clean branch name following the repository's naming convention:
     - Format: `<type>/<short-kebab-summary>`
     - `<type>`: `feat`, `fix`, `docs`, `refactor`, `style`, `chore`, `test`, or `perf`
     - `<short-kebab-summary>`: 2–5 lowercase words separated by hyphens (a-z, 0-9, `-`), e.g. `feat/query-export-presets`, `fix/modal-backdrop-contrast`, `docs/slash-commit-skills`.
     - If the user passed a hint or topic after `/commit-branch`, use it to guide the branch name and commit message.
   - Verify the generated branch name does **not** collide with any branch listed by `git branch -a` (local or remote). If it collides, append a concise qualifier (e.g. `-v2` or a more specific scope).
   - Generate a matching Conventional Commit message: `<type>(<scope>): <imperative summary>`.

4. **Create Branch, Stage & Commit:**
   - Never stage secrets, `.env` files, credentials, or unintended build/database artifacts.
   ```powershell
   git checkout -b <type>/<short-kebab-summary>
   git add -A
   git commit -m "<type>(<scope>): <summary>"
   ```
   - Do **not** run `git push` unless the user explicitly requested pushing.

5. **Verify & Report:**
   ```powershell
   git status -sb
   git log -n 1 --oneline
   ```
   Report to the user:
   - Base branch and newly created branch name
   - Commit hash and commit message
   - List of committed files
