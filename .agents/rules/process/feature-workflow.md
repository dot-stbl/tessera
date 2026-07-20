# Feature workflow — issue → plan → build, in an isolated worktree

> Captures the canonical Tessera feature-delivery lifecycle. This rule
> preserves the contract across sessions: a fresh agent loading this
> repo knows to start from an issue draft, plan commits on paper
> before opening the editor, isolate edits in a worktree, and gate
> every commit on format + build + tests.

## 1. Issue

- **Capture scope first** in an issue, not in code. Two forms:
  - **Draft issue**: `.github/ISSUE_DRAFT_<topic>.md` — local capture
    when the `gh` CLI is unavailable (matches the working tree's
    `.github/ISSUE_DRAFT_*` pattern established for Phase 7 Windows
    + diagnostics pattern). Drafts do not gate work; they're the
    permanent record of why a decision was made.
  - **Tracked issue**: when `gh` CLI is authenticated, prefer
    `gh issue create --body-file .github/ISSUE_DRAFT_<topic>.md`
    to record the decision in the GitHub issue tracker instead.
  - Both forms reference each other — `.github/ISSUE_DRAFT_*`
    carries the canonical narrative; `gh issue` references it via
    "see `.github/ISSUE_DRAFT_<topic>.md` for the working notes."
- Skim the issue body before opening the editor. Surface scope
  questions to the owner before locking in a plan if the body is
  ambiguous — replanning is cheap; rewriting a half-done commit
  series isn't.

## 2. Plan

- Convert the issue body into **1..N commits** matching
  `process/commit-format.md` (`[.stbl](feat/<area>): <subject>`).
- **One commit per logically isolated step.** Tangling unrelated
  changes into a single commit makes the audit trail painful and
  rebases fragile.
- Build the commit list **on paper before opening the editor.**
  The order of edits rarely matches the order of commits
  (reformatting ends up at the end, an `internal` helper moves
  before its first caller, etc.). Drafting the list first avoids
  scramble-commits that read as unrelated changes.
- Treat the plan as a checklist in the commit body of the
  **first** feature commit: `Implements: #<issue-id>` + the local
  commit-by-commit synopsis. Reviewers can match the diff against
  the plan instead of reverse-engineering intent.

## 3. Worktree

Open **a fresh worktree** before editing anything in `src/`:

```bash
# from the main checkout
git worktree add ../tessera-<short-branch> -b tessera/<short-branch>

cd ../tessera-<short-branch>

# edit -> format -> build -> test -> commit
```

Naming convention for `tessera-<short-branch>`:

- `tessera-mvp-2-platform` — long-running platform branches
- `tessera-feat-<short-desc>` — short-lived feature branches
- `tessera-fix-<bug-name>` — fix branches

Why a worktree, not a `git checkout` on the same clone:

- **Isolated build output.** Each worktree has its own `bin/` /
  `obj/`; running tests in one doesn't trash another's incremental
  build cache. Two open worktrees can both have `dotnet test`
  running simultaneously without `bin/*` overwriting each other.
- **Blast-radius isolation.** If the feature turns out to be a
  dead end, removing the worktree is one `git worktree remove`
  away. The main checkout's index / work-tree state is untouched,
  so opening a PR for the next feature doesn't have to wait for
  the cleanup.
- **Branch-name cost is near zero.** Each worktree is its own
  branch; `git worktree list` shows the active branches at a
  glance; `git branch --list` on the main checkout shows only the
  long-lived branches.

Cleanup at the end of the workstream:

```bash
# from the main checkout
git worktree remove ../tessera-<short-branch>
git branch -d tessera/<short-branch>
```

If the work is being merged, prefer `git merge --no-ff` (preserves
the feature-history bubble). If Phase 6+ requires a PR review
before merge, leave the worktree intact until the PR lands — the
worktree is the PR's local working copy.

## 4. Build

In the worktree, **before** each commit, the local gate is
identical to the agent's build-verification.md:

```bash
dotnet format tessera.slnx --verify-no-changes --severity hidden
dotnet build tessera.slnx -c Debug
dotnet test tessera.slnx --no-build
```

Plus the self-audit grep catalog from
`~/.agents/rules/csharp/worker-audit-grep-catalog.md`. The gate is
green **before** `git add && git commit`, not after.

Per-commit discipline:

1. Format gate (above).
2. Build (above).
3. Test (above; covers per-module unit projects).
4. Self-audit grep on changed files.
5. `git add <staged files>` + commit with subject from the plan.
6. `git push origin tessera/<short-branch>` when the workstream
   is ready for review / merge.

## 5. Commit hygiene (specific to this workflow)

- **No-op commit** (`git commit --allow-empty`) is fine for change-log-only
  commits, e.g. bumping a header doc that's mentioned in the plan.
  Don't fake-commit just to satisfy a count.
- **Commit too big** (> ~500 LoC, excluding generated code) —
  split. The plan was wrong; `git reset HEAD~1` and re-do in
  pieces. A diff that's reviewable in 10 minutes beats one that
  needs an hour.
- **Merge commit** — keep the feature's own commit history
  intact (`git merge --no-ff`); the merge commit carries the
  reference `Implements: #<issue-id>` so future `git log
  --grep` finds the whole workstream.
- **Worktree crash** — `git worktree prune` clears stale
  metadata after a hard kill mid-process. `git worktree list`
  is the source of truth for which worktrees exist; if a path is
  gone, the entry auto-prunes.

## Refs

- `process/commit-format.md` — `[.stbl](feat/<area>): <subject>` shape
- `process/build-verification.md` — local build gate commands
- `process/worker-audit.md` — self-audit grep on changed files
- `process/engineering-zone-access.md` — engineering-zone rules
  (Tessera.Host / Directory.Build.props / .editorconfig / rules/)
- `.agents/rules/_index.md` — Tessera-specific rule index
- `.github/ISSUE_DRAFT_*` examples:
  - `.github/ISSUE_DRAFT_phase-7-windows.md`
  - `.github/ISSUE_DRAFT_diagnostics-pattern.md`
