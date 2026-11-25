---
name: commit
description: Slash command (/commit) to stage and commit current workspace changes directly on the active git branch with an auto-generated Conventional Commit message. Use when the user invokes /commit or asks to commit changes directly to the current branch without creating a new branch.
---

# `/commit` — Direct Commit on Current Branch

Commits the current working tree and staged changes directly onto the **currently checked-out branch** without creating a new branch.

---

## Steps

1. **Inspect Current Git State:**
   Run in the repository root:
   ```powershell
   git branch --show-current
   git status --short
   git diff --stat
   git diff --cached --stat
   git log -n 5 --oneline
   ```

2. **Check for Changes:**
   - If `git status --short` produces no output (no staged, unstaged, or untracked changes), inform the user that the working tree is clean and stop without creating an empty commit (unless the user explicitly requested an empty commit).

3. **Review Diff & Prepare Commit Message:**
   - Inspect `git diff` and `git diff --cached` (plus any untracked files) to understand the scope of changes.
   - Never stage secrets, `.env` files, credentials, or unintended build artifacts (`bin/`, `obj/`, temporary `.db` files).
   - Compose a concise **Conventional Commit** message matching the repository style:
     - Format: `<type>(<scope>): <imperative summary>` (e.g., `feat(editor): add query tab pinning`, `fix(modals): resolve backdrop focus leak`, `docs(skills): update component catalog`).
     - Allowed types: `feat`, `fix`, `docs`, `refactor`, `style`, `chore`, `test`, `perf`.
     - If the user supplied extra text or a preferred message after `/commit`, follow their intent.

4. **Stage & Commit on the Current Branch:**
   ```powershell
   git add -A
   git commit -m "<type>(<scope>): <summary>"
   ```
   - Do **not** switch branches.
   - Do **not** run `git push` unless the user explicitly requested pushing.

5. **Verify & Report:**
   ```powershell
   git status -sb
   git log -n 1 --oneline
   ```
   Report to the user:
   - Current branch name
   - Commit hash and commit message
   - List of committed files
