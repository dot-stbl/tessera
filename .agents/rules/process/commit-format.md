---
description: commit format is [.stbl](<feat/...>): <subject> — project tag, required feature path, imperative subject
priority: high
always: true
---

# Commit format

Every commit in this repository follows exactly one shape:

```
[.stbl](<feat/...>): <subject>
```

Three required parts, no exceptions:

| Part | Description |
|---|---|
| `[.stbl]` | Literal project tag. Identifies this repo's commits in a multi-repo workspace; the `.stbl` prefix means "stbl.solutions project". |
| `(<feat/...>)` | Required feature path in parentheses. Always starts with `feat/` followed by a hierarchical, lowercase kebab-case area (see §Feature path). |
| `<subject>` | Imperative summary, no period, ≤72 chars, lowercase. |

**The feature path is always present** — there is no `[.stbl]: <subject>` form.
If a change spans multiple features, pick the dominant one and put the rest in
the body. A change without a clear dominant feature belongs to `feat/meta`
(rules, build, CI, scripts, repo-level config — see §Top-level areas).

## Feature path

A feature path is **lowercase kebab-case**, slashes for nesting. The first segment
is **always** `feat/`; segments after are the area name. Two to five segments is
the sweet spot.

```
[.stbl](feat/<area>): <subject>
[.stbl](feat/<area>/<sub-area>): <subject>
```

### Top-level areas for Tessera

Driven by the project layout — `src/host/*`, `src/shared/*`, `src/modules/*`,
plus tooling/CI/docs:

| Area | When |
|------|------|
| `feat/host` | Changes to `src/host/Tessera.Host/` (composition root, endpoints registration, options) |
| `feat/build-tools` | Changes to `src/host/Tessera.Build.Tools/` (MSBuild SDK, targets, gates) |
| `feat/shared-kernel` | `src/shared/Tessera.Shared.Kernel/` (Result, Error, Id, TimeRange, Pagination) |
| `feat/shared-http` | `src/shared/Tessera.Shared.Http/` (Refit base, resilience, OTel HTTP) |
| `feat/shared-telemetry` | `src/shared/Tessera.Shared.Telemetry/` (OTel setup, logging helpers) |
| `feat/shared-openapi` | `src/shared/Tessera.Shared.OpenApi/` (Scalar, OpenAPI doc) |
| `feat/shared-validation` | `src/shared/Tessera.Shared.Validation/` (FluentValidation helpers) |
| `feat/traces` | `src/modules/Tessera.Modules.Traces/` (Trace, Span models + VT client) |
| `feat/logs` | `src/modules/Tessera.Modules.Logs/` (LogEntry model + VL client) |
| `feat/discovery` | `src/modules/Tessera.Modules.Discovery/` (Service inventory aggregator) |
| `feat/health` | `src/modules/Tessera.Modules.Health/` (Health endpoint + per-Victoria checks) |
| `feat/fe` | Changes inside `web/` (Tessera console monorepo) |
| `feat/tests` | Changes inside `tests/` (unit + integration + architecture) |
| `feat/meta` | Build, CI, deps, scripts, repo-level config, **rules themselves** |
| `feat/docs` | Documentation-only commits (`.agents/docs/`, ADRs, README updates) |

**All commits start with `feat/`** — `feat/` is the universal first segment.
The second segment picks the area. Sub-segments localize further.

### Sub-paths

Append a sub-path to localize the change inside an area:

```
[.stbl](feat/traces): add list traces endpoint
[.stbl](feat/traces/handlers): wire get trace handler
[.stbl](feat/traces/options): add victoria traces options
[.stbl](feat/shared-http/resilience): add retry policy for vtselect
[.stbl](feat/host/composition): wire all module extension methods
[.stbl](feat/tests/architecture): assert modules don't cross-reference
[.stbl](feat/meta/rules): rewrite commit-format rule
[.stbl](feat/meta/format): cleanup pre-existing format drift
[.stbl](feat/docs): add ADR-0001 for project structure
[.stbl](feat/meta/deps): bump dotnet to 10.0.110
[.stbl](feat/fe/traces): wire traces screen to /api/traces
```

### Rules for picking a feature path

1. **The first segment is always `feat/`.** No `[.stbl](traces): ...` —
   the second segment picks the area.
2. **The second segment must be a top-level area** from the table above. No
   inventing new top-level areas without discussion in chat.
3. **One feature path per commit.** If a commit touches `feat/traces` and
   `feat/tests`, prefer the one that drives the change (usually
   `feat/traces`) and mention the rest in the body.
4. **Don't duplicate path segments.** `[.stbl](feat/traces/traces/api)`
   is wrong — use `[.stbl](feat/traces/api)`.
5. **Don't use `feat/meta` for code changes.** `feat/meta` is for tooling,
   rules, CI, build, scripts. Code changes use a feature area even if they
   touch a build script — e.g. `feat/build-tools` for csproj edits in
   `src/host/Tessera.Build.Tools/`.

### When no clear area fits

A pure docs change with no implementation impact → `feat/docs`. A
repo-config / rules / build / CI change → `feat/meta`. Anything that
primarily changes a runtime area → that area. If two areas tie, pick the
one that's downstream of the other and mention the upstream in the body.

## Subject

- **Imperative** — "add", "fix", "bump", "wire" — not "added", "fixed", "bumped".
- **No period** at the end.
- **≤72 characters** total in the subject line.
- **Lowercase** for the subject.
- **No "wip", "tmp", "draft" markers** — if it's not ready, don't commit it.
- **File names in subject = OK** (e.g. `fix Dockerfile`, `wire OpenApiBuildTimeExtensions`).
  **Type names in subject = bad** — names go in code, commit messages describe intent.

## Body (optional)

Through a blank line after the subject. Wrap at ~72 chars. Explains **why**, not
**what** — the diff already shows what. The body is where context, trade-offs,
and risk live.

```
[.stbl](feat/traces/handlers): wire get trace handler with span tree reconstruction

Раньше GetTraceHandler возвращал flat list spans. UI нужен nested
parent/child tree для waterfall display. Server-side reconstruction
на backend дешевле чем на frontend — avoids shipping raw spans.

Verified: 7-span test trace renders as proper tree in unit test.
```

## Footer (optional)

Through a blank line after the body. For breaking changes and ticket refs:

```
[.stbl](feat/traces/api): change /traces response shape

BREAKING CHANGE: /traces now returns { items, cursor } instead of array.
Migration: clients must read .items and follow .cursor for pagination.

Refs: TES-42
```

## Good

```
[.stbl](feat/traces): add list traces endpoint
[.stbl](feat/traces/handlers): wire create-trace handler
[.stbl](feat/logs/options): add vlselect base url option
[.stbl](feat/shared-http/resilience): add retry policy for vt/vl/vm
[.stbl](feat/host/composition): wire all module extension methods
[.stbl](feat/tests/architecture): assert modules don't cross-reference
[.stbl](feat/meta/rules): rewrite commit-format rule
[.stbl](feat/meta/format): cleanup pre-existing format drift
[.stbl](feat/docs): add ADR-0001 for project structure
[.stbl](feat/meta/deps): bump dotnet to 10.0.110
[.stbl](feat/fe/traces): wire traces screen to /api/traces
```

## Bad

```
feat(traces): add workload endpoint             ← no [.stbl] tag, no feat/ prefix
chore(format): apply fixes                       ← Conventional Commits 1.0.0, deprecated
[plexor](traces): add workload endpoint          ← wrong [plexor] tag
[.stbl] add workload endpoint                    ← feature path missing entirely
[.stbl](traces) add workload endpoint            ← missing feat/ prefix
[.stbl](feat/traces) add workload endpoint       ← missing `: ` separator
[.stbl](feat/traces/traces): add workload        ← duplicate path segment
[.stbl](feat/traces/api): Added endpoint.        ← past tense, period at end
[.stbl](feat/traces/api): wip                   ← WIP marker
[.stbl](feat/TRACES): add workload               ← uppercase feature path
[.stbl](feat/traces/api): this is a long subject that exceeds the seventy-two character limit and rambles on
[.stbl](feat/traces/api): add GetTraceHandler    ← TYPE names in subject
```

## Validation regex

For tooling (commit-msg hook, CI lint, etc.):

```regex
^\[.stbl\]\(feat/([a-z][a-z0-9-]*(/[a-z][a-z0-9-]*)*)\): .{1,72}$
```

Group 1 = feature path (without the leading `feat/`), rest = subject. The body
and footer are not checked by the regex.

## Overrides

This rule overrides global harness git rules. Per the soly rule hierarchy
(`.agents/rules/` beats global rules), `[.stbl](<feat/...>)` wins in this
repository.

## Other places this format appears

If you copy a snippet from these rules or a previous commit, double-check
the format. Common places wrong tag sneak in:

- `.agents/rules/coding/analyzers.md` §"Что делать при новых warnings" — example commit
- `.agents/rules/process/build-verification.md` §"Good / Bad" — example commit

If you find a wrong tag reference in any `.agents/rules/*.md` file that's
NOT in `commit-format.md` (this file), update it. Format drift applies to
docs too.
