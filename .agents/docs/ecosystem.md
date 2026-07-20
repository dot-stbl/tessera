# `.stbl` ecosystem

> **Tessera is a product of the `.stbl` studio** (GitHub org `github.com/dot-stbl`).
> This doc captures the conventions that flow from that membership so agents
> don't have to re-derive them every time.

## What `.stbl` is

A small open-source studio (per `github.com/dot-stbl/.github`):

> "A hidden file in Unix. `ls` won't show it. `ls -la` will.
> We sit at the bottom layer of every product we ship."

Mostly .NET, open source, infrastructure-grade. Products ship under
`.stbl` and inherit the brand.

## Products (visible + local)

| Product | Type | Repo | Status |
|---------|------|------|--------|
| Plexor | App (self-hosted cloud) | `github.com/dot-stbl/plexor` | Published |
| Anlytra | App (crypto analytics) | `github.com/dot-stbl/anlytra` | Published |
| re.password | App (password manager) | `github.com/dot-stbl/re.password` | Published |
| notifliwy | Lib (.NET distributed notifications) | `github.com/dot-stbl/notifliwy` | Published |
| cyclex | Lib (.NET task scheduler) | `github.com/dot-stbl/cyclex` | Published |
| synaptix.packages | Lib (.NET shared utilities) | `github.com/dot-stbl/synaptix.packages` | Published |
| **Tessera** | **App (APM UI for Victoria stack)** | **`dot-stbl/tessera` (local)** | **Pre-release (this repo)** |
| Kubix | App (TBD) | `github.com/dot-stbl/kubix` | Local |
| infrastructure.upva | Infra (TBD) | local | Local |

## Brand conventions

Per `.stbl/brand` repo:

### Visual

- **Pure B&W** — monochrome palette, status semantics only (ok/err/warn/idle)
- **Monospace** — JetBrains Mono for numerics, Onest for sans
- **No bright brand colors** — green reserved for `ok`/success
- **No marketing imagery** — utility aesthetic, not consumer

### Lockup pattern

Every product: `ProductName by .stbl`

HTML markup (used in README):

```html
<h1 class="by-stbl">
  ProductName <span class="by">by</span>
  <span class="dot"></span>stbl
</h1>
```

CSS at `assets/by-stbl.css` (mirrored from `dot-stbl/.github/assets/`).

### Assets

- `assets/logo.svg`, `assets/logo-light.svg` — `.stbl` umbrella mark (org-only)
- `assets/wordmark.svg`, `assets/wordmark-dark.svg` — `.stbl` text + mark
- `assets/lockup-<product>.svg` — `ProductName by .stbl` lockup (per product)
- `assets/og-<product>.svg` — OG card (1200×630, used for social previews)
- `assets/favicon-<size>.png` + `favicon.ico` — raster exports

Tessera has: `lockup-tessera.svg`, `og-tessera.svg`, `favicon.svg`.

### Products use their own mark

The `.stbl` logo is reserved for org-level use. Each product has its
own mark. **Tessera mark** = 4 tiles in a scatter (referencing
*tessera* = "tile" in Latin).

## Commit format

Already in `.agents/rules/process/commit-format.md`:

```
[.stbl](<feat/...>): <subject>
```

This IS the `.stbl` convention — same as plexor and other products.

## Asset sync

`.stbl` brand assets (logo, wordmark, by-stbl.css) are owned by
`github.com/dot-stbl/.github` repo. Product repos can either:

- **Git submodule** — `git submodule add git@github.com:dot-stbl/.github.git assets/stbl`
- **Copy on every release** — simpler, error-prone

Tessera uses **git submodule** at `assets/stbl/`:

```
$ cat .gitmodules
[submodule "assets/stbl"]
    path = assets/stbl
    url = <github.com/dot-stbl/.github or local path>
```

To update brand assets:

```sh
git submodule update --remote assets/stbl
```

The submodule structure (from `.github` repo):

```
assets/stbl/
├── LICENSE
├── profile/
│   └── README.md
└── assets/
    ├── by-stbl.css
    ├── logo.svg, logo-light.svg
    ├── wordmark.svg, wordmark-dark.svg
    ├── lockup-template.svg, lockup-plexor.svg
    ├── og-default.svg, og-plexor.svg
    ├── badge.svg
    └── favicon.ico, favicon-{16,32,64,128,256}.png
```

**Tessera-specific assets** (not submodule, kept in main repo):

```
assets/
├── lockup-tessera.svg   generated from assets/stbl/assets/lockup-template.svg
├── og-tessera.svg       generated from assets/stbl/assets/og-plexor.svg
└── favicon.svg          Tessera mark (4-tile mosaic)
```

When publishing to GitHub, change the submodule URL to the public remote:

```sh
# In assets/stbl/ (or via .gitmodules edit)
git remote set-url origin git@github.com:dot-stbl/.github.git
```

## When to publish

Per `.stbl/CONTRIBUTING.md`:

1. Open an issue at `dot-stbl/.github` first
2. Wait for approval (infrastructure-grade bar)
3. Then transfer repo ownership to `dot-stbl` org

**Tessera is currently local-only** under `C:/Users/bradw/source/stbl/tessera`.
Not yet transferred to `github.com/dot-stbl/tessera`.

## DCO

When publishing, enable DCO sign-off on branch protection:

```
Signed-off-by: Name <email>
```

GitHub UI handles this automatically when you enable DCO in
Settings → Branches → Branch protection rules.

## Local-only secrets

`.stbl/.settings/` folder (sibling of `stbl-kit/`) contains the PAT for
`dot-stbl` org. **Never commit anything in this folder.** This is a
project-level convention, mirrored from the rules:

> Tokens need to live somewhere the agent can read.
> Storing them inside any repo = leaked on push.

## What tessera specifically inherits from `.stbl`

| `.stbl` convention | Tessera implementation |
|---|---|
| `.stbl` org brand | `assets/lockup-tessera.svg`, `assets/og-tessera.svg` |
| `by-stbl` CSS class | `assets/stbl/assets/by-stbl.css` (submodule) |
| `ProductName by .stbl` lockup in README | `README.md` (already in this repo) |
| `[.stbl](<feat/...>): <subject>` commits | enforced by `process/commit-format.md` |
| JetBrains Mono for numerics | `web/index.css` (font import) |
| Plexor as reference | `web/` scaffolded from plexor template, adapted for tessera |
| No playwright / browser automation | `process/agent-runtime-safety.md` |
| "Powered by stbl" pattern | `og-tessera.svg` tagline: "APM UI for the Victoria stack" |
| Asset sync via submodule | `assets/stbl/` tracks `dot-stbl/.github` |

## Related docs

- `architecture.md` — tessera architecture
- `../rules/process/commit-format.md` — commit format (`.stbl` convention)
- `web/README.md` — frontend stack details
- `README.md` — public-facing README (`.stbl` lockup)
- Plexor source: `C:\Users\bradw\source\stbl\plexor\` (reference implementation)