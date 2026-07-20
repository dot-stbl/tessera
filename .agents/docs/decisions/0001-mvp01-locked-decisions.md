# ADR-0001 — MVP-01 locked decisions

> Status: Accepted 2026-07-20
> Owner: bradw
> Source: planning session 2026-07-20

## Context

MVP-01 backend-only APM proxy for the Victoria stack. Phases 0–5
shipped (commit `301c93f` — MVP-01 + Phase 5 stack + `/compose/`
artefacts + `Tessera.LoadGen`). Phase 6 (CI + container publish) NOT
started. Several open decisions blocked Phase 6 and Windows support.

This ADR locks 12 architectural decisions that previously lived only
in `.agents/STATE.md` prose. The ADR pattern is new — this file
establishes `.agents/docs/decisions/` as the canonical home for
future ADRs.

---

## Decision 1 — Auth: multi-provider framework, gradual rollout

**Decision:** Multi-provider auth via `IAuthProvider` abstraction.
Rollout **постепенно**:
- 5a — Guest + AdminBearer (status quo, refactor to IAuthProvider)
- 5b — LDAP (System.DirectoryServices.Protocols)
- 5c — Keycloak (OIDC JWT bearer)

**Rationale:** Self-hosted enterprise APM commonly integrates with
LDAP (Active Directory, OpenLDAP) and/or OIDC (Keycloak). Tessera
implements the framework in MVP-01 to avoid breaking refactor in
MVP-02. Guest tier remains for anonymous read access.

**Alternatives considered:**
- Single admin bearer forever — too narrow for enterprise.
- All providers implemented in MVP-02 — risks shipping auth shape
  without feedback loop.

**Consequences:**
- `Tessera.Shared.Authentication` expands: `Core/IAuthProvider`,
  `Providers/{Guest,Admin,Ldap,Keycloak}/`
- TOML gains `[auth]` + `[auth.providers.<name>]` sections
- `[Authorize(Roles = "admin,editor")]` becomes fully usable at 5a
- Admin scheme (status quo) stays backward-compatible — existing
  MVP-01 deploys work unchanged

**Refs:** `.agents/docs/security/auth-model.md`, Decision 2 + 3.

---

## Decision 2 — LDAP library: System.DirectoryServices.Protocols

**Decision:** Microsoft BCL package (part of .NET 10). No third-party.

**Rationale:** Best Microsoft Learn docs, most StackOverflow answers,
default path in all Microsoft tutorials. Active Directory is the
primary enterprise target. Native Linux via `libldap.so.2` —
`apk add libldap` in Dockerfile runtime stage.

**Alternatives considered:**
- `Novell.Directory.Ldap.NETStandard` — pure-managed, no native deps.
  Decided against: docs less rich, AD-specific scenarios have less
  coverage.

**Consequences:**
- Dockerfile runtime: `RUN apk add --no-cache libldap`
- Tests: AD/OpenLDAP via Testcontainers OpenLDAP
- One additional native dep on Alpine Linux images

**Refs:** Decision 1, `compose/Dockerfile`.

---

## Decision 3 — Keycloak: OIDC JWT bearer

**Decision:** `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.9`
(already pre-staged in `Directory.Packages.props`). Authority-based
discovery via `/well-known/openid-configuration`.

**Rationale:** Standard OIDC discovery. Keycloak handles federation
with LDAP/AD/SAML/etc. server-side. Tessera needs only the JWT-bearer
trust path.

**Trade-off:** If Keycloak federates with LDAP, Tessera doesn't need
direct LDAP (Decision 2 covers both scenarios — choose at deploy
time via `[auth.providers.<name>]/enabled`).

**Refs:** Decision 1 + 2.

---

## Decision 4 — Config format: TOML, nested sections

**Decision:** TOML only. `[victoria.traces]/url` nested, not flat
`[victoria]/traces_url`. `VictoriaOptions` refactors to nested
subclasses (`VictoriaTracesOptions` / `LogsOptions` / `MetricsOptions`).

**Rationale:** Matches existing compose config + docs + natural for
future nested sections (`[auth.providers.ldap]`,
`[storage.sqlite]`). Aligns with Victoria/Grafana convention.

**Alternatives considered:**
- Flat keys — fewer files touched but breaks compose config + docs.

**Consequences:**
- `VictoriaOptions.Traces/Logs/Metrics` — nested option classes
- Bind via `GetSection("victoria")` + fluent child binds
- Breaking change: flat keys removed, new `MetricsUrl` appears

**Refs:** `.agents/docs/architecture/config-format.md`, compose
config drift fix in `compose/config/tessera.conf/tessera.toml`.

---

## Decision 5 — SecretReference wired via PostConfigure

**Decision:** `SecretReference.Resolve` invoked in `PostConfigure` for
all secret-typed fields, after `Bind`. Classes:
- `VictoriaOptions` → `Traces.Token`, `Logs.Token`, `Metrics.Token`,
  `Auth.AuthToken`
- `AdminBearerOptions` → `AdminToken`
- `LdapAuthOptions` → `BindPassword`
- `KeycloakAuthOptions` → `ClientSecret`

**Rationale:** Documented feature (`config-format.md` §Secrets) only
worked for `env:VAR`. `file:/path` was dead code. `PostConfigure` is
the standard ASP.NET pattern for value-resolution after binding.

**Consequences:**
- All secret fields run through `SecretReference.Resolve` after bind
- `file:/path` references start working
- Env vars + file-mounted secrets share one resolution path

**Refs:** `SecretReference.Resolve` at
`src/shared/Tessera.Shared.Kernel/Configuration/Paths/SecretReference.cs`.

---

## Decision 6 — Storage: SQLite + EF Core + Repository + Spec in MVP-01

**Decision:** New `Tessera.Shared.Storage`:
- `Microsoft.EntityFrameworkCore.Sqlite 10.0.0`
- `Microsoft.EntityFrameworkCore.Design 10.0.0`
- Generic `Repository<T>` + `Specification<T, TResult>` bases
- `TesseraDbContext` with `UseSnakeCaseNamingConvention()`
- Entities: `UserEntity` + `DashboardEntity` (only this in MVP-01)
- Initial migration v0.1

**Rationale:** User-scoped auth requires User persistence (LDAP/Keycloak
mapping). Dashboard metadata is the foundation for MVP-02 dashboard
editor. EF Core + Repository + Spec is the canonical pattern from
global rules (`ef-core.md`, `repository-spec.md`). SQLite = zero
infrastructure, single-binary deploy.

**Alternatives considered:**
- Defer storage to MVP-02 — but auth needs User mapping now.
- Pure JSON files (no DB) — works for Users but Dashboards need
  queryable indexes for Phase 7+ search.

**Consequences:**
- 6 commits in Phase 5 (primitives, sqlite setup, User entity,
  Dashboard entity, migration, wiring)
- Data dir from `IFileSystemLayout` (Decision 7) — `tessera.db` under
  `${data_dir}/tessera.db`
- `dotnet ef migrations add/remove` integration
- **APM data still lives in Victoria** (Decision 8)

**Refs:** global `ef-core.md`, `repository-spec.md`,
`ef-migrations.md`.

---

## Decision 7 — FS layout: IFileSystemLayout abstraction

**Decision:** New abstraction
`Tessera.Shared.Kernel.Configuration.Layout.IFileSystemLayout`:
- `ResolveConfigDirectory()` → where `tessera.toml` lives
- `ResolveDataDirectory()` → where `tessera.db` + dashboards live
- Two implementations:
  - `LinuxFileSystemLayout` → `/etc/tessera/` (config, RO mount OK),
    `/var/lib/tessera/` (data, RW), XDG fallback for user dev
  - `WindowsFileSystemLayout` → `%ProgramData%\tessera\config\`,
    `%LocalAppData%\tessera\data\`
- Auto-detect via `OperatingSystem.IsLinux()` / `IsWindows()`

**Rationale:** Windows support (Decision 10) requires different
paths. Current `TesseraConfigPaths` / `TesseraDataPaths` are
Linux-only with confusing `~/etc/tessera` data fallback (mixes
system `/etc` with user dir).

**Alternatives considered:**
- Linux-only paths, document "use TESSERA_DATA_ROOT env on Windows".
  Rejected — confuses dev experience.
- Hardcode Windows paths in current classes — no abstraction layer.

**Consequences:**
- `TesseraConfigPaths`/`TesseraDataPaths` become thin wrappers
- `~/etc/tessera` data fallback **removed** (mixing concern)
- Override via `TESSERA_CONFIG_PATH` / `TESSERA_DATA_PATH` env vars
  works on both OS

**Refs:** `src/shared/Tessera.Shared.Kernel/Configuration/Paths/*.cs`.

---

## Decision 8 — APM data sources: external only

**Decision:** Tessera **does not store APM data**. Sources (Victoria
now, Tempo/Jaeger/Loki/Mimir later) are the single source of truth
for traces/logs/metrics. Tessera SQLite (Decision 6) is **only for
Tessera's own metadata** (users, dashboards metadata, future audit).

**Rationale:** Original MVP-01 positioning — "Thin proxy, no
persistence, no business logic, no ingest" (`HANDOFF.md`). User
confirmation: "без postgres, без зависимостей внешних, кроме
приложений которые предоставляют нам данные для APM".

**Consequences:**
- SQLite ≠ storage for APM telemetry
- Victoria stack remains source of truth
- Tessera SQLite = Tessera metadata only
- Future providers (Tempo, Loki) plug in via existing
  `ITraceProvider` / `ILogProvider` interfaces — no Tessera-side
  schema work

**Refs:** `.agents/docs/victoria-stack.md`,
`.agents/HANDOFF.md` (provider abstraction layer).

---

## Decision 9 — Frontend: bundled prod, dev server dev

**Decision:** Production: `bun run build` copies
`web/apps/console/dist/*` into `src/host/Tessera.Host/wwwroot/`.
ASP.NET `UseDefaultFiles` + `UseStaticFiles` middleware mount.
Dev: `bun --filter '@tessera/console' dev` on `:1991` with proxy to
backend `:1990`.

**Rationale:** Single binary deploy for prod (vite output becomes
part of the host DLL). Vite dev server for dev convenience. Both
work on Windows + Linux.

**Consequences:**
- MSBuild target in `Directory.Build.props` copies `dist/` → `wwwroot/`
  at publish time
- Single Docker image, no separate frontend container
- Dev workflow unchanged (proxy `:1990`)
- SPA history fallback via `MapFallback` for non-API paths

**Refs:** `web/`, `compose/Dockerfile`.

---

## Decision 10 — Windows support: full (L1 + L2 + L3 + L4)

**Decision:**
- L1: Path handling via `IFileSystemLayout` (Decision 7)
- L2: Config paths work (`%ProgramData%\tessera`,
  `$XDG_CONFIG_HOME/tessera`, cwd-relative)
- L3: Dev workflow (vite dev on Windows, dotnet test on Windows,
  `bun install` on Windows)
- L4: Docker on Windows / WSL2 — full support

**NOT in MVP-01:** Win32 service installer, MSIX package, native
Windows Credential Manager integration.

**Rationale:** User is a Windows dev. Production deploy (Linux) is
not affected. Dev experience significantly improves. Global rules
(`~/.agents/rules/`) already document the technology (BCL native
libs via `apk add`).

**Consequences:**
- `install.md` gains Windows section
- `troubleshoot.md` gains Windows recipes
- Tests run on both OS

**Refs:** Decision 7, `.agents/docs/operations/install.md`.

---

## Decision 11 — Pre-existing drift cleanup as Phase 0

**Decision:** Before any feature work — Phase 0 cleanup:
- 0a — format drift cleanup + VSTHRD111 disable (~150 violations,
  26 files)
- 0b — stale project names in 2 rules (`Tessera.Shared.Telemetry`
  → `Tessera.Shared.Authentication` etc.)
- 0c — STATE.md / HANDOFF.md reconcile with disk reality
  (Grafana provisioning claims are stale)

**Rationale:** `dotnet build tessera.slnx -c Debug` exited non-zero
from pre-existing format drift. Any feature commit would land with
the drift violation, violating `build-verification.md`. Cleanup must
be first.

**0a executed as:** mechanical fix + VSTHRD111 disable, **commit
`2cb9533`** landed 2026-07-20. Build gate green, 107/107 tests
passing.

**0b + 0c:** follow-up commits in MVP-01 Phase 6.

**Consequences:**
- One cleanup commit landed before any feature work
- Build gate green for the first time in this session

**Refs:** global `build-verification.md`, `worker-audit.md`.

---

## Decision 12 — Compose stack shape (no change)

**Decision:** Victoria metrics + logs + traces as **3 separate
containers** + otel-collector (see `compose/docker-compose.yml`).
**Not** the deprecated `victoria-stack` single-binary.

**Status:** Already implemented in Phase 5. No change. STATE.md
previously claimed Grafana provisioning was added — that is stale
(false positive on the Phase 5 description). STATE.md reconcile
follows in Phase 0c.

**Refs:** `compose/docker-compose.yml`, `compose/otel-collector/config.yaml`.

---

## Consequences summary

### What changes (MVP-01 Phase 6+)

- **AGENTS.md** in repo root — force-loads global rules
- **3 new global rules** in `~/.agents/rules/csharp/`:
  - `multi-provider-auth.md` (Tier 1 gap fill)
  - `filesystem-paths.md` (Tier 2 gap fill)
  - `cross-platform.md` (Tier 2 gap fill)
- `Tessera.Shared.Storage` (~30 files, SQLite + EF Core + Repo + Spec)
- `Tessera.Shared.Authentication` expansion (Guest + Admin → IAuthProvider,
  + LDAP, + Keycloak providers)
- `Tessera.Shared.Kernel.Configuration.Layout/` (~6 files, abstraction)
- VictoriaOptions nested refactor
- ServerOptions binding to Kestrel
- SecretReference PostConfigure wiring
- Frontend bundle integration into Tessera.Host wwwroot
- Windows install docs

### What stays the same

- Provider abstraction (4 interfaces — `ITraceProvider`,
  `ILogProvider`, `IDiscoveryProvider`, `IHealthProvider`)
- Module structure (4 modules — `Traces/Logs/Discovery/Health`)
- Layer isolation rule (modules ≠ providers ≠ host)
- TOML config (format unchanged)
- RFC 9457 ProblemDetails error shape
- Wire format (UTC unix ms)
- Port pool (1990–2120)

### What is deferred to MVP-02

- `IMetricsProvider` interface + `VictoriaMetricsProvider` impl
- Dashboard editor (foundation in MVP-01 only)
- Per-user preferences table
- Audit log
- Token rotation via `IOptionsMonitor` reload (TOML reloads on
  change)
- Win32 service installer
- RED metrics queries / service map / flame graph
- Grafana provisioning files (STATE.md claim was stale)

### Risks

| Risk | Mitigation |
|------|------------|
| **Scope expansion** (SQLite + EF Core in MVP-01) | 6 phased commits, stop after any phase if needed |
| **Format drift cleanup touches shared files** | Only mechanical fix (`dotnet format` + targeted Edit), no logic changes |
| **Multi-provider auth scope** | 4a (foundation + guest + admin) is self-contained; 4b LDAP + 4c Keycloak optional |
| **Windows native dep** (`libldap` via `apk add`) | Standard ASP.NET Alpine pattern, +1MB image size, no functional impact |
| **VSTHRD111 global disable** | Approved by owner 2026-07-20, documented inline; CA2007 already disabled (consistency) |

---

## Related documents

- `.agents/STATE.md` — current progress log (updated separately)
- `.agents/HANDOFF.md` — strategic context (updated separately)
- `.agents/docs/architecture/config-format.md` — TOML schema
- `.agents/docs/security/auth-model.md` — auth tiers + admin bearer
- `.agents/docs/operations/install.md` — install (Linux section; Windows added Phase 7)
- `.agents/docs/operations/storage.md` — storage layout (revised for MVP-01 SQLite scope)
- `.agents/docs/victoria-stack.md` — VT/VL/VM HTTP API reference
- `~/.agents/rules/csharp/ef-core.md`, `repository-spec.md`,
  `ef-migrations.md` — storage layer rules
- `~/.agents/rules/csharp/configuration-toml-env.md` — config + secret rules
- `~/.agents/rules/process/build-verification.md`, `worker-audit.md` — gates