# Phase 7 (L1–L4) — Windows install + dev workflow docs

> **Status:** Draft. Self-hosted runner for this workspace is Linux-only at
> the moment of writing — Phase 7 is **deferred** until a Windows runner
> is provisioned or operator-side manual verification covers the
> surface. Tracking this here so we don't lose the work scope.

## Scope

Deferred Phase 7 deliverable per `.agents/STATE.md`:

- `compose/scripts/install-windows.ps1` — Tessera install as Windows Service
- `compose/scripts/install-unix.sh` — Linux companion (parity with Windows)
- `compose/scripts/run-windows.ps1` + `run-unix.sh` — runtime wrapper
- `.github/workflows/build.yml` — CI matrix (Linux + Windows)
- `/etc/tessera.conf` (Linux) and `%ProgramData%\tessera\tessera.conf`
  (Windows) — operator-facing examples
- Testing on Windows: `install + Invoke-WebRequest /api/v1/health` against
  the published `Tessera.Host.exe`

## Acceptance criteria

1. `install-windows.ps1` registers Tessera as a Windows Service via
   `sc.exe`, copies the published binary to `%ProgramFiles%\tessera`,
   drops `tessera.conf` to `%ProgramData%\tessera`, and primes
   `%LOCALAPPDATA%\tessera` (data dir under per-user profile). Idempotent
   — run twice, second run no-ops.
2. `install-unix.sh` does the FHS layout (binary in `/usr/local/bin`,
   config in `/etc/tessera/tessera.conf`, data in `/var/lib/tessera`).
   Also idempotent.
3. `Invoke-WebRequest http://127.0.0.1:1990/api/v1/health` returns 200
   after either installer runs on its target OS.
4. CI job `windows-installer` (matrix only when a `[self-hosted, windows, x64]`
   runner is online) executes installer + health-probe in < 2 min.

## Why deferred

The `windows-latest` GitHub-hosted runner is not available on this
repo (no paid plan / quota). Self-hosted runners are Linux-only at
the moment. The Phase 7 surface is operator-actionable: a developer
on Windows can run `install-windows.ps1 -BinaryPath .\publish` on
their workstation, verify `/api/v1/health`, and report results —
that path is fully covered by the cross-platform abstractions in
`Tessera.Shared.Kernel.Configuration.Layout` (Phase 3c).

## Trade-offs

- No CI-driven regression on Windows; format gate + dotnet-build-on-Linux
  catches most regressions that would manifest cross-platform (path
  separators, line endings, casing).
- Manual verify cadence: every Tessera release should be smoked on
  one Windows box before publishing the next image.

## Resumption checklist

- [ ] Provision a self-hosted Windows runner (or move to GH-paid for
      `windows-latest`)
- [ ] Add `compose/scripts/install-windows.ps1` + `install-unix.sh`
- [ ] Add `.github/workflows/build.yml` with matrix `["self-hosted
      linux x64", "self-hosted windows x64"]` and three jobs:
      `build-and-test`, `compose-smoke`, `windows-installer`
- [ ] Phase 7 closeout: docs/operations/install.md updated with both
      OSes side-by-side, .agents/HANDOFF.md notes Windows runner
      prerequisite

Refs: ADR-0001, .agents/STATE.md §Phase 5 (deferred)
      .agents/HANDOFF.md §"Engineering zone"
      .agents/rules/csharp/cross-platform.md §1
