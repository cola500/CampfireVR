# Tooling — known issues

Compatibility issues we've hit with the MCP servers wired into this project. Each entry is a snapshot in time; check the **Status** line before assuming it's still current.

---

## hzdb (Meta Quest Horizon Debug Bridge) breaks Claude Code with `tools.NN.custom.input_schema`

**Status:** Open. hzdb removed from `.mcp.json` on 2026-05-15. Workflow stable on `mcp-unity` only. Re-enable blocked on either an hzdb schema fix upstream or a per-server `skipSchemaValidation` switch.

### Symptoms

After adding `hzdb` to `.mcp.json` and restarting Claude Code, the very first prompt fails with a 400 from the Anthropic API and the session becomes unusable — every subsequent message fails with the same error until the offending MCP server is removed and Claude Code is restarted.

### Exact error

```
API Error: 400 {"type":"error","error":{"type":"invalid_request_error",
"message":"tools.18.custom.input_schema: input_schema does not support
oneOf, allOf, or anyOf at the top level"}}
```

The `tools.NN` index varies per session because tool ordering depends on which MCP servers are loaded. `tools.18` was the index we saw locally; the same root cause produces `tools.9`, `tools.21`, `tools.26` etc. in other reports.

### Environment

| | |
|---|---|
| Date observed | 2026-05-15 |
| Claude Code | 2.1.142 |
| Platform | macOS (Darwin 24.6.0, arm64) |
| hzdb package | `@meta-quest/hzdb` 1.2.0 (published 2026-05-13) |
| Other MCPs loaded | `mcp-unity` (`com.gamelovers.mcp-unity@d176a9d737cc`) |
| Install path | `.mcp.json` (project scope), via `npx -y @meta-quest/hzdb mcp install project` |

### Suspected root cause

hzdb registers ~40 tools and at least one of them ships a JSON schema with `oneOf`, `anyOf`, or `allOf` at the **root** of `input_schema`. The Anthropic API rejects that — top-level combinators are unsupported, only nested combinators are allowed. Claude Code forwards the rejection as the 400 above.

This is not specific to hzdb. The same error has been reported across many MCP servers (Perplexity, time-mcp, pandoc, Pencil, etc.) since Claude Code v2.0.21 tightened MCP schema validation. See `docs/hzdb-investigation.md` for the upstream issue trail.

### Temporary workaround (in effect now)

Removed the `hzdb` entry from `.mcp.json`. The current file contains only `mcp-unity`. A copy of the broken config — with `hzdb` still present — is preserved at `.mcp.json.bak` for reference and is gitignored alongside `.mcp.json`.

If/when we want to retry hzdb without waiting on an upstream fix, the documented escape hatch is to add `"skipSchemaValidation": true` to the `hzdb` entry. We have **not** tried this yet; doing so suppresses the validation but the offending tool will still fail at call-time, so it's only useful if we want partial functionality from the other ~39 hzdb tools.

### Rollback steps (already applied)

1. `cp .mcp.json .mcp.json.bak` (preserve the broken state for diagnosis).
2. Edit `.mcp.json` and remove the `hzdb` block from `mcpServers`. Resulting file:
   ```json
   {
     "mcpServers": {
       "mcp-unity": {
         "command": "node",
         "args": ["…/UnityProject/Library/PackageCache/com.gamelovers.mcp-unity@<HASH>/Server~/build/index.js"]
       }
     }
   }
   ```
3. Quit Claude Code (`/exit`) and re-open the project so the new server list is read.
4. Run `/mcp` to confirm only `mcp-unity` is connected.

### What to try before re-enabling

- Check if a newer `@meta-quest/hzdb` release fixes the schema (npm registry, `meta-quest/agentic-tools` release notes).
- Check whether Anthropic relaxes the top-level-combinator restriction in a future Claude Code release.
- If we just want hzdb's `capture` group, scope it down: install hzdb behind `skipSchemaValidation`, and only call tools we've confirmed work — accept that ~1 hzdb tool will break the session if invoked.

### Related issues

See `docs/hzdb-investigation.md` for full upstream issue links.
