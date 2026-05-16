# Draft GitHub issue — meta-quest/agentic-tools

**Target repo:** https://github.com/meta-quest/agentic-tools
**Status:** Draft. Not posted. Review and edit before submitting.

---

## Title

`hzdb breaks Claude Code sessions: tool input_schema rejected for top-level oneOf/anyOf/allOf`

## Body

### Summary

Adding `@meta-quest/hzdb` as an MCP server to Claude Code (`.mcp.json`) causes Claude Code to fail on the very first prompt with an Anthropic API 400. The Anthropic API rejects the entire `tools` array when one tool's `input_schema` is invalid, so this single offending tool makes Claude Code unusable for the session — including any other (well-formed) MCP servers that happen to be configured alongside hzdb. Removing the `hzdb` entry from `.mcp.json` and restarting Claude Code restores normal operation.

I think one of hzdb's ~40 registered tools is shipping a JSON schema with `oneOf`, `anyOf`, or `allOf` at the **root** of `input_schema`. The Anthropic API requires those combinators to be nested rather than at the top level — a constraint Claude Code began enforcing in v2.0.21.

### Reproduction

1. Have a working Claude Code project with at least one other MCP server already configured (in my case, `mcp-unity`).
2. Add hzdb to project-scope `.mcp.json`:
   ```bash
   npx -y @meta-quest/hzdb mcp install project
   ```
   The resulting `.mcp.json` entry:
   ```json
   "hzdb": {
     "command": "npx",
     "args": ["-y", "@meta-quest/hzdb", "mcp", "server"]
   }
   ```
3. Restart Claude Code so the new server is loaded.
4. Send any prompt.

**Expected:** prompt is processed; hzdb tools are available alongside the other MCP server's tools.
**Actual:** the request fails with the 400 below, and every subsequent prompt fails the same way until hzdb is removed from `.mcp.json` and Claude Code is restarted.

### Exact error

```
API Error: 400 {"type":"error","error":{"type":"invalid_request_error",
"message":"tools.18.custom.input_schema: input_schema does not support
oneOf, allOf, or anyOf at the top level"}}
```

The `tools.18` index reflects local ordering (mcp-unity tools first, then hzdb's). Other reporters of the same root cause have seen `tools.9`, `tools.21`, `tools.26`, etc. — same error class, different index.

### Environment

| | |
|---|---|
| `@meta-quest/hzdb` | 1.2.0 (npm, published 2026-05-13) |
| Claude Code | 2.1.142 |
| OS | macOS (Darwin 24.6.0, arm64) |
| Other MCP servers loaded | `mcp-unity` (~30 tools, no schema issues) |
| Install scope | project (`.mcp.json`) |

### Suspected root cause

The Anthropic API rejects any tool whose `input_schema` uses `oneOf`, `anyOf`, or `allOf` at the **root** of the schema object. Nested combinators inside a property are accepted; combinators that *replace* the root object schema are not. A patch would likely be schema-only — flatten the root combinator into a single `type: "object"` schema with all variant properties listed, optionally describing the "exactly one of" constraint in the `description`.

I don't know which specific hzdb tool is the offender (Claude Code reports the index in the assembled array, not the tool name), but isolating it should be straightforward by enumerating the tools registered by `npx -y @meta-quest/hzdb mcp server` and checking each `inputSchema`.

### Why this matters / blast radius

The Anthropic API validates the `tools` array as a whole, so a single invalid tool means **every** tool in the request is rejected. In practice this means:

- Users can't use hzdb at all.
- Users can't use any *other* MCP server they had configured alongside hzdb — Claude Code becomes unusable until hzdb is removed.
- There's no per-tool graceful degradation; the failure is all-or-nothing.

For a project that already depends on another MCP server (mcp-unity, in my case), this means hzdb effectively can't be tried without breaking the existing workflow.

### Workarounds (from the user side)

| Workaround | Trade-off |
|---|---|
| Remove `hzdb` from `.mcp.json` | Loses all hzdb capability. What I did. |
| Add `"skipSchemaValidation": true` to the hzdb entry | Suppresses Claude Code's pre-flight check, but the Anthropic API will still reject the schema at call-time — only useful if the offending tool is never invoked. |
| Downgrade Claude Code to v2.0.20 | Predates the strict validation, but loses ~7 months of Claude Code improvements. |

The proper fix is server-side in hzdb: rewrite the offending tool's `input_schema` to avoid root-level combinators.

### Related upstream issues

This pattern is well-documented in `anthropics/claude-code`. Anthropic has closed every request to relax the validation as **not planned**, signalling that the API behaviour is intended and the fix path is on the MCP server side.

- anthropics/claude-code#4886 — original report of the top-level oneOf/anyOf/allOf rejection
- anthropics/claude-code#10606 — documents v2.0.21 as the version that introduced the strict validation
- anthropics/claude-code#27337, #30212, #40075 — same root cause, different MCP servers, different Claude Code versions

I checked this repo's open issues before filing — none currently mention Claude Code or input_schema validation, so this looks like the first report from the hzdb side.

Happy to test a patch release against my setup if helpful.
