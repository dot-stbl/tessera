# Audit-debug rule

> Triggered when: the user explicitly reports a missed rule violation,
> or when a `Task(general, "audit ...")` run is challenged with
> "what else did you miss".

When triggered, the agent MUST produce a debug trace **before**
proposing any fix commit. The trace is the deliverable; the diff is
a downstream consequence of it.

## Five-step debug trace

1. **Cite the rule** by file path and section.

   Example: `code-shape.md §1 — Get-or-return-existing — merge
   обязателен в is { } x`.

2. **Cite the violation** with `path:line` and the actual snippet
   in the source. Never paraphrase.

   Example:
   ```
   src/shared/Tessera.Shared.Kernel/Configuration/Source/TomlConfigurationProvider.cs:36-37
   var model = Tomlyn.TomlSerializer.Deserialize<TomlTable>(tomlText);
   if (model is not null)
   ```

3. **Debug why the previous scan missed it.** Pick one of:
   - "grep pattern in `worker-audit.md §2` doesn't cover this rule
     family" (catalog gap)
   - "rule was outside the sensu-stricto sealed/class-layout/naming
     bucket, so the audit prompt skipped" (mental-category gap)
   - "I wrote the code myself and didn't apply the rule" (own-
     behaviour gap)

   State it explicitly so the user can spot whether the rule needs
   to be promoted to a default scan.

4. **Show the concrete fix.** The code-shape.md §1 form
   `if (X is { } x) { use x; }`, the error-mapping.md §5 form
   `try { ... } catch (HttpRequestException ex) { throw new ProviderException("provider.network_error", ..., ex); }`,
   whichever the rule requires. The fix is not optional.

5. **Grep for the same violation across the project**, before
   claiming "fixed in one place". Wire the result into either the
   same commit (if small) or a follow-up commit (if > 3 hits).

## After the trace

The agent may proceed with the fix(es) proposed in the trace.
The user reviews the trace + the diff, not just the diff. Skipping
the trace and going straight to a fix is the failure mode this
rule exists to prevent.

## Why this file exists

Audit agents scan prose rules poorly and scan executable regex
patterns well. The companion file
`.agents/rules/csharp/worker-audit-grep-catalog.md` ships a per-rule
grep catalog so the audit self-gate (worker-audit.md §2) actually
finds what the rule prose describes. Without the grep, every
audit is at the mercy of whether the model happened to think of
the rule family in its first pass.

## When the user complains

> "почему не нашёл?"

The right response is the five-step trace, not a fix. The fix is
implied by step 4. If the rule needs a grep that doesn't exist,
step 5 exposes the gap and step 0 (commit A on the audit-sweep
branch) adds the missing grep to the catalog.

## Worked example

User flagged: `var model = Tomlyn.TomlSerializer.Deserialize<TomlTable>(tomlText); if (model is not null)`.

Response (paraphrased from the actual debug tree on this branch):

| Step | Output |
|---|---|
| 1. Rule | `code-shape.md §1` — `var + if is not null + use` → merge в `is { } x` |
| 2. Cite | `TomlConfigurationProvider.cs:36-37` |
| 3. Debug | "self-gate `worker-audit.md §2` greps are scoped to `code-shape §9 private methods` + `naming-and-types` + `class-layout-and-tooling`. Pattern-matching rules are in a different family and weren't in the regex catalog." |
| 4. Fix | `if (Tomlyn.TomlSerializer.Deserialize<TomlTable>(tomlText) is { } model) { TomlTableFlattener.FlattenTable(model, prefix: string.Empty, data); }` (wrapped in try/catch for the second violation, error-mapping.md §5) |
| 5. Grep | `rg "var\s+\w+\s*=[^;]+;\s*\r?\n\s*if\s*\(\s*\1\s+is\s+(null|not\s+null)\s*\)"` across `src/` → 1 hit (TomlConfigurationProvider.cs); across `tests/` → 0 hits. Catalog added in commit A. |