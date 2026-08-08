---
name: tessera-ui
description: Tessera's console design system — tokens, density, the time gutter, type roles, the two-reds rule, component map and copy voice. Load before building or restyling any screen in web/, before adding a shadcn component, before touching index.css, or when a screen "looks cheap" and you need to know why.
---

# Tessera console — design system

The console is used by one person: an SRE at 3am with an alert open in another
window. Every screen has the same job — get from "something is wrong" to "this
span, this log line" without losing the thread. This is not browsing. It is
hunting, and the interface is an instrument, not a page.

Visual register is GitHub's **Primer**: quiet functional grey, hairline
structure, 6px radius, and **colour that only speaks when state changes**. Two
deliberate departures from Primer: our density is higher, and our accent is red.

---

## 1. Tokens

Two layers, in `web/apps/console/src/index.css`. Change colour in layer 1 only.

**Layer 1 — foundation.** Functional names, the name states the role:

| Token | Role |
|---|---|
| `--canvas-default` | the page ground |
| `--canvas-subtle` | the raised slab content sits on |
| `--canvas-inset` | recessed channels: time gutter, rail, filter strip |
| `--canvas-overlay` | popovers, dialogs |
| `--borderColor-muted` | hairline between rows |
| `--borderColor-default` | edge of a surface |
| `--borderColor-emphasis` | the one strong line: gutter edge, header underline |
| `--fgColor-default` / `-muted` / `-subtle` | ink, three levels |
| `--accentColor-fg` / `-emphasis` | **structure only** — see §2 |
| `--dangerColor-fg` / `--successColor-fg` / `--attentionColor-fg` | **data only** |

Neutral values are Primer's own, converted to OKLCH — do not eyeball
replacements, convert.

**Layer 2 — shadcn slots** (`--background`, `--card`, `--primary`, `--border` …)
derived from layer 1, so all vendored primitives keep working.

**Page and slab swap between themes.** Light: white slab on a grey page. Dark:
lighter slab on near-black. Deriving both from one token collapses them into a
single plane — that flat sheet is what makes a screen read as unfinished.

---

## 2. The two-reds rule — do not break this

GitHub affords a blue accent because its danger red never collides with it. Ours
is red for both the brand and errors, so they are separated by **role**:

- **accent** (deep) is only ever **structure**: rail bar, gutter stamp, focus
  ring, selected row, primary button.
- **danger** (bright) is only ever **data**: the word ERROR, failing bars, red
  timestamps.

Never an accent-coloured status, never a danger-coloured control. The moment one
crosses over, the screen has two indistinguishable reds and neither means
anything.

---

## 3. Type — three roles, and the role says what the string is

| Role | Face | Used for |
|---|---|---|
| data | JetBrains Mono, tabular | every machine value: durations, ids, timestamps, service and operation names, counts |
| meta (`.meta`) | same mono, uppercase, `0.09em` | column headers, labels, tags — the scaffolding |
| prose | Onest | sentences only: empty states, errors, help |

An APM screen is almost entirely machine values. Setting those in a machine face
and reserving the human face for sentences is information, not decoration.

---

## 4. Density and the time gutter

`--row: 28px`, `--gutter: 62px`, `--rail: 52px`.

**The time gutter is the app's signature.** Every listing gets a fixed first
column carrying position in time, separated by `--borderColor-emphasis`:

- trace list — how long ago
- waterfall — offset from trace start (`0`, `+6ms`, `+350ms`, `+1.2s`)
- log stream — the timestamp

A failing row states it **in red on that axis**; the selected span claims the
axis with the accent. Wherever you are, "when" is in the same place. A trace is
a timeline, so the axis is the thing being navigated — never move it, never
change its width per screen.

---

## 5. Components — screens contain no raw markup

**A screen may use `div` for layout (flex/grid) and nothing else.** No `table`,
`button`, `input`, `p`, `span`, `h2`, `code`, `a`, `section`. If the element you
want has no component, **add the component** — do not reach past the system.

Three layers, and you build downward only when the layer above has nothing:

1. **`shared/ui/console/`** — the console layer, what screens import.
   `Listing`/`Row`/`Cell`/`WhenCell`/`NumCell`/`TrackCell`, `Subject`,
   `ServiceMark`, `ServiceDots`, `InlineList`, `Tag`, `Blank`/`BlankText`/`Code`,
   `Notice`, `Strip`/`Seg`/`Chip`/`Meta`, `Panel`/`PanelRow`/`PanelValue`,
   `LoadingRows`.
2. **`shared/ui/apm/`** — domain primitives that carry semantics and do their own
   unit maths, so screens pass raw values: `Duration`, `TimeFormat`, `TraceId`,
   `LogLevel`, `SpanRow`, `Waterfall`, `LogEntry`.
3. **`shared/ui/primitives/`** — the vendored shadcn/Base UI set. Almost
   everything already exists here: `Table`, `ToggleGroup`, `Badge`, `Empty`,
   `Field`/`FieldRow`, `Input`, `Button`, `Alert`, `Skeleton`, `Separator`,
   `Select`, `Combobox`, `Popover`, `Checkbox`.

Every console component is a **thin skin over a primitive** — `Listing` is
shadcn `Table`, `Seg` is Base UI `ToggleGroup`, `Chip` is `Badge` + `Button`,
`Blank` is shadcn `Empty`, `Notice` is `Alert`. Structure, keyboard and ARIA come
from the primitive; this layer adds Tessera's density and tokens through the
unlayered CSS in `index.css`, which outranks Tailwind utilities without
`!important`. Check `primitives/` before writing anything new.

Build a component from scratch only when nothing generic covers it — the time
gutter, the latency track, the service swatch, a listing-shaped skeleton — and
say so in the file. `LoadingRows` is the example: shadcn's `Skeleton` is a filled
pulsing lozenge, which is the pattern §8 rejects, so it is not the base.

Adding a shadcn component: `bunx shadcn@latest add <name>` — `components.json`
puts it in `shared/ui/primitives` with the right utils alias. Restyle it with
tokens, never with hard-coded colour.

| Need | Use |
|---|---|
| text field, search | `Input` |
| action | `Button` |
| single choice from many | `Select`, or `Combobox` when it needs typing |
| yes/no | `Checkbox` |
| floating panel | `Popover` |
| exclusive small choice | `Seg` |
| active filter | `Chip` — reads `key=value`, clears in place |
| tabular data | `Listing` + `WhenCell` gutter |
| a caveat about the data below | `Notice` |
| loading | `LoadingRows` — shows the shape that is coming, gutter included |
| nothing to show | `Blank` — a direction, never a shrug |
| a settings surface | `Panel` + `PanelRow` |
| a duration, a timestamp, a level | `Duration`, `TimeFormat`, `LogLevel` |

---

## 6. Copy

Calm, precise, technical. No exclamation marks, no apologies, no emoji.

- **Errors** state what happened and what to do. "Trace a1b2… has no spans in
  this window" — not "Oops, something went wrong".
- **Buttons** are imperative and keep the same word through the flow: a button
  that says Refresh produces a toast that says Refreshed.
- **Empty states** are an instruction with a way out: "Nothing in this window.
  Widen the range, or clear the service filter."
- **Never promise what the API cannot do.** A field labelled "LogsQL query" when
  the endpoint only accepts a stream name is a lie the user finds at the worst
  moment.

---

## 7. Before calling a screen done

1. `bun run typecheck && bun run lint && bun run test && bun run build` — all four.
2. Look at it. Screenshot at 1500×950 dark and light. Not "it should look fine".
3. Density: is the row 28px, are values monospace, is the gutter present and
   the same width as elsewhere?
4. Colour: does anything coloured mean a state? Remove the rest.
5. Keyboard: can every interactive row be reached and activated with Enter?
   Is the focus ring visible?
6. Empty and loading states exist and say something useful.

---

## 8. Mistakes already made here — do not repeat them

- **Designing per screen instead of from the system.** Every "looks cheap"
  verdict in this project traced back to inventing a screen locally.
- **Fixing structure when the problem was material.** Density and typography
  were right twice while the app still looked unfinished, because it was a flat
  white sheet with no surfaces.
- **A linear scale on latency.** One 8.9s outlier squashed nine of ten rows into
  2–30px stubs. Latency is log-normal — scale it with `log1p`.
- **`Date.now()` inside a React Query key.** Recomputed each render, so every
  render is a cache miss: the skeleton never clears and requests loop. Quantize
  the window (`resolveTimeWindow`).
- **Filled pastel pills for status.** Across forty rows they were the loudest
  thing on screen and said the least. A word in its own colour.
- **Raw markup in a screen.** Every `<table>`, `<button>` and `<p>` written into
  a feature file was a component that should have existed. Half of them already
  did, in `primitives/`, unread. See §5 — this is the rule most often broken.
