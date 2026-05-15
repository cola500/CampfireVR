# hzdb × Claude Code — investigation

Companion to `docs/tooling-known-issues.md`. Captures the upstream context so we don't have to re-discover it next time.

## Question

Is the `tools.NN.custom.input_schema` failure we hit when adding `@meta-quest/hzdb` to `.mcp.json` a known compatibility issue, or something specific to our setup?

## Answer

**Known issue. Not specific to hzdb. Not specific to us.** The 400 from the Anthropic API is the same one many MCP servers have triggered since Claude Code v2.0.21 (Oct 2025) tightened tool-schema validation. Anthropic has closed every related bug as **"not planned"**, treating the validation as the desired behaviour and pushing the fix onto MCP authors. hzdb just happens to be the latest large MCP server to ship with at least one offending tool.

## What the error actually means

The Anthropic API rejects any tool whose `input_schema` uses `oneOf`, `anyOf`, or `allOf` at the **root** (top level). Nested combinators inside a property's schema are fine; combinators that *replace* the root object schema are not.

A schema like this is rejected:

```json
{
  "oneOf": [
    { "type": "object", "properties": { "deviceId": { "type": "string" } } },
    { "type": "object", "properties": { "deviceSerial": { "type": "string" } } }
  ]
}
```

A semantically equivalent rewrite that would be accepted:

```json
{
  "type": "object",
  "properties": {
    "deviceId":     { "type": "string" },
    "deviceSerial": { "type": "string" }
  }
}
```

(Plus, optionally, a `description` that says exactly one of `deviceId` / `deviceSerial` is required — the API doesn't enforce the constraint, but the model can read the description.)

We don't know which specific hzdb tool ships the bad schema. Empirically, hzdb registers ~40 tools and *one* of them was enough to break the whole session — the API rejects the entire `tools` array, so even the tools with valid schemas can't be called.

## Why mcp-unity was ruled out as the culprit

Before adding hzdb, Claude Code worked reliably with mcp-unity (~30 tools) for weeks across many slices in this repo. The only change between "working" and "broken" was the addition of the `hzdb` block in `.mcp.json`. Removing that single block — without touching anything in mcp-unity — restored normal operation. That isolates the regression cleanly to hzdb.

A more careful test would be to launch Claude Code with **only** hzdb in `.mcp.json` (no mcp-unity) and confirm the same 400. We didn't run it because the rollback already restored a working state and the upstream issue trail (below) makes the diagnosis confident enough.

## Upstream issue trail

Anthropic's `claude-code` repo has at least eight open/closed issues for the same root cause. All of the most direct ones are **closed as not planned** or **closed as duplicate**, which is the signal that Anthropic does not intend to relax the API restriction.

| # | Title | Status | Notes |
|---|---|---|---|
| [#4886](https://github.com/anthropics/claude-code/issues/4886) | input_schema does not support oneOf, allOf, or anyOf at the top level | Closed (duplicate) | The canonical writeup. v1.0.65, macOS. |
| [#10606](https://github.com/anthropics/claude-code/issues/10606) | Strict MCP schema validation in v2.0.21+ breaks working MCPs with no opt-out or migration path | Closed (not planned) | Documents v2.0.21 as the regression point. Requested env var opt-out; declined. |
| [#27337](https://github.com/anthropics/claude-code/issues/27337) | Anthropic API Error: Tool input_schema doesn't support oneOf/allOf/anyOf at top level | Closed (duplicate) | |
| [#30212](https://github.com/anthropics/claude-code/issues/30212) | tools.26 schema error in v2.1.63 | Closed (not planned) | Confirms the issue still reproduces in 2.1.x — same family as our 2.1.142. |
| [#40075](https://github.com/anthropics/claude-code/issues/40075) | Tool input_schema does not support oneOf/allOf/anyOf at top level | Closed (duplicate) | v2.1.86, Windows. |
| [#3940](https://github.com/anthropics/claude-code/issues/3940) | Invalid Tool Input Schema: Unsupported JSON Schema Validation Construct | Open | |
| [#1690](https://github.com/anthropics/claude-code/issues/1690) | MCP Server Configuration Fails: Invalid JSON Schema for Tool Input | Open | |
| [#34771](https://github.com/anthropics/claude-code/issues/34771) | MCP tool with invalid JSON Schema property key breaks entire session irrecoverably | Open | Adjacent failure mode — bad property *names* — same blast radius (whole session dies). |

Cross-checked the `meta-quest/agentic-tools` repo (the upstream for hzdb): on 2026-05-15 there are exactly two open issues (#3 and #4), neither of them about Claude Code or schema validation. **No one has reported the Claude Code incompatibility upstream yet.** That's a gap we could close — see "Suggested next steps".

## Workarounds

| Option | What it does | Cost |
|---|---|---|
| **Remove hzdb from `.mcp.json`** (what we did) | Eliminates the bad tool from the request entirely. | Loses all hzdb capability, including `capture`. |
| **`"skipSchemaValidation": true` on the hzdb entry** | Tells Claude Code to forward the schema unchecked. The API still validates it, so the offending tool will fail when *called* — but the session no longer dies on startup. | Partial functionality. We'd need to discover by trial-and-error which hzdb tool is broken and avoid it. |
| **Downgrade Claude Code to 2.0.20** | Pre-validation behaviour. | Loses every Claude Code improvement of the last ~7 months, including everything we use day-to-day. Hard no. |
| **Wait for hzdb to fix the schema upstream** | Real fix. | Indeterminate timing. We'd want to file the issue first (`meta-quest/agentic-tools`). |
| **Wait for Anthropic to relax the restriction** | Real fix. | Anthropic has explicitly declined this multiple times. Don't hold your breath. |

## Will a newer Claude Code version help?

Almost certainly **no**. The validation was *added* in 2.0.21 and Anthropic has closed every request to relax or opt out of it. The hzdb side is more promising — `@meta-quest/hzdb` shipped 1.2.0 two days before our test (2026-05-13) and the package is on a roughly weekly release cadence. A schema-only patch from Meta is plausible if we file the issue.

## Suggested next steps

1. **File an issue against `meta-quest/agentic-tools`** describing the failure, the exact error, and the upstream Anthropic constraint. Link to issue #4886 / #10606. This is the highest-leverage move — Meta is best positioned to fix the schema, and right now they don't appear to know it's broken.
2. **Skip experimental retries until that issue is acknowledged.** A `skipSchemaValidation` retry costs little but adds noise; deferring keeps our workflow stable.
3. **Re-evaluate when hzdb releases a new minor version.** If the release notes mention schema fixes, drop `hzdb` back into `.mcp.json` and try again.

## Today's status

`.mcp.json` contains only `mcp-unity`. Claude Code is stable. `.mcp.json.bak` preserves the broken config for reference and remains gitignored.
