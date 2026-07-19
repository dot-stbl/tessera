<div align="center">

# tessera <span class="by-stbl"><span class="by">by</span><span class="dot"></span>stbl</span>

**APM UI for the Victoria stack — traces, logs, metrics, dashboards.**

[Docs](.agents/docs/architecture.md) · [Install](.agents/docs/operations/install.md) · [Issues] · [License]

</div>

---

## What it does

Tessera is a self-hosted web application that sits in front of the
[Victoria stack](https://victoriametrics.com/) (Traces + Logs + Metrics) and
gives it a Kibana-APM-like interface. Trace explorer with waterfall view,
structured log search, service inventory, and Grafana-style custom dashboards.

Built for teams that already run Victoria and want a UI that doesn't
require Grafana's data-source plumbing to navigate traces.

## Install

```sh
docker run -d \
  --name tessera \
  --restart unless-stopped \
  -p 1990:1990 \
  -v /var/lib/tessera:/var/lib/tessera:rw \
  -v /etc/tessera:/etc/tessera:ro \
  -e TESSERA_ADMIN_TOKEN="$(openssl rand -hex 32)" \
  -e TESSERA_VICTORIA_TOKEN="your-victoria-bearer-token" \
  ghcr.io/dot-stbl/tessera:latest
```

See [`.agents/docs/operations/install.md`](.agents/docs/operations/install.md)
for Docker Compose, systemd, and k8s manifests.

## Quickstart

```sh
# 1. Generate admin token
export TESSERA_ADMIN_TOKEN=$(openssl rand -hex 32)

# 2. Run with Victoria (assumes vt/vl/vm on localhost)
docker compose up -d

# 3. Open http://localhost:1990 — guest mode for read,
#    admin endpoints need Authorization: Bearer $TESSERA_ADMIN_TOKEN
```

See [`.agents/docs/architecture.md`](.agents/docs/architecture.md) for the
full architecture overview.

## Documentation

- [`.agents/docs/`](.agents/docs/) — architecture, modules, operations
- [`.agents/docs/victoria-stack.md`](.agents/docs/victoria-stack.md) — VT / VL / VM API reference
- [`.agents/docs/architecture/multi-tenancy.md`](.agents/docs/architecture/multi-tenancy.md) — single-tenant config
- [`.agents/docs/architecture/log-format.md`](.agents/docs/architecture/log-format.md) — dot.case convention
- [`.agents/docs/dashboard-schema.md`](.agents/docs/dashboard-schema.md) — custom JSON schema
- [`.agents/rules/`](.agents/rules/) — coding and process rules

## Status

| | |
|--|--|
| Version | 0.0.0 (pre-release) |
| .NET | 10 |
| UI | React 19 + Vite + shadcn/ui + Tailwind 4 |
| License | TBD (likely MIT) |
| Maintained by | [.stbl](https://github.com/dot-stbl) |

## Stack

- **Backend** — ASP.NET Core 10 controllers, Refit + Polly + OpenTelemetry
- **Frontend** — React 19 + TanStack Router + TanStack Query + shadcn/ui
- **Data** — VictoriaMetrics + VictoriaLogs + VictoriaTraces over HTTP
- **Storage** — SQLite + JSON files (no PostgreSQL, no Kafka)
- **Auth** — guest (anonymous) + admin (bearer token); OIDC + LDAP stretch
- **Config** — TOML with env-var overrides; secrets in env only

## Brand

Tessera follows the [`.stbl` brand guidelines](https://github.com/dot-stbl/brand).

Brand assets are vendored via git submodule from
[dot-stbl/.github](https://github.com/dot-stbl/.github) at `assets/stbl/`:

- `assets/stbl/assets/by-stbl.css` — `.by-stbl` utility class (used in this README)
- `assets/stbl/assets/lockup-template.svg` — template for `ProductName by .stbl` lockups
- `assets/stbl/assets/og-plexor.svg` — reference OG card

**Tessera-specific assets** (not submodule):

- `assets/lockup-tessera.svg` — generated from `lockup-template.svg`
- `assets/og-tessera.svg` — generated from `og-plexor.svg` template
- `assets/favicon.svg` — Tessera mark, 32×32, theme-aware (black tiles in light mode, white in dark mode)
- `assets/mark-tessera.svg` — canonical 80×80 mark, white background (fixed colors)
- `assets/mark-tessera-transparent.svg` — same mark, no background (for dark/colored surfaces, theme-aware)

To update brand assets: `git submodule update --remote assets/stbl`. To
modify the `.stbl` brand kit itself, open a PR at
[dot-stbl/brand](https://github.com/dot-stbl/brand) or
[dot-stbl/.github](https://github.com/dot-stbl/.github).

## Related

- [.stbl brand kit](https://github.com/dot-stbl/brand) — design rules, templates
- [dot-stbl/.github](https://github.com/dot-stbl/.github) — org profile + brand assets
- [Plexor](https://github.com/dot-stbl/plexor) — self-hosted cloud platform (sister project, primary reference)
- [.stbl org](https://github.com/dot-stbl) — other products

---

<sub>Tessera is built by <a href="https://github.com/dot-stbl">.stbl</a>.</sub>