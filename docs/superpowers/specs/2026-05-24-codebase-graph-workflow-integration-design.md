# Codebase Graph Workflow Integration Design

**Date:** 2026-05-24
**Status:** Approved
**Area:** Infrastructure / Tooling / AI

---

## Overview

Wire the existing `graphify-dotnet` codebase graph (IMPROVEMENT-021) into Claude's actual
workflow. The graph artifacts exist and are up to date, but Claude has no instruction path
that triggers their use. This spec closes that gap with three targeted changes:

1. `tools/query-graph.ps1` — a PowerShell query script for programmatic graph lookups
2. `.claude/knowledge/codebase-graph.md` — updated to reference `GRAPH_REPORT.md` and the script
3. `CLAUDE.md` — wired into the knowledge table and Required Workflow

---

## Graph Structure (Reference)

`docs/graph/graph.json` contains:

| Property | Value |
|---|---|
| Total nodes | 19,926 |
| Total edges | 33,689 |
| Communities (Louvain) | 2,616 |
| Edge: `contains` | 17,785 — within-file hierarchy (file → namespace → class → method) |
| Edge: `imports` | 66 — cross-file namespace dependencies |

Node shape:
```json
{
  "id": "standinglist",
  "label": "StandingList.cs",
  "type": "Entity",
  "community": 1211,
  "file_path": "Perpetuum.RequestHandlers/Standings/StandingList.cs"
}
```

Edge shape:
```json
{
  "source": "standinglist",
  "target": "standinglist_perpetuum_requesthandlers_standings",
  "relationship": "contains",
  "weight": 1
}
```

Node labels include: file names (`StandingList.cs`), method names (`HandleRequest()`),
class names, and namespace names. Search by label (case-insensitive, partial match).

---

## Component 1: `tools/query-graph.ps1`

### Purpose

Provide a single reusable script for Claude and developers to query the codebase graph
without parsing 21MB of JSON manually.

### Location

`tools/query-graph.ps1` (new file — `tools/` is a new directory created for this script)

### Interface

```powershell
.\tools\query-graph.ps1 <ClassName> [-Direction <in|out|both|community>]
```

| Parameter | Default | Meaning |
|---|---|---|
| `ClassName` | required | Case-insensitive partial match against node `label` |
| `Direction` | `both` | Which edges to traverse |

### Direction Semantics

| Direction | Query | Use case |
|---|---|---|
| `in` | Edges where `target == node.id` | Who depends on this class (blast radius) |
| `out` | Edges where `source == node.id` | What this class depends on |
| `both` | Both above, labeled separately | Full dependency picture |
| `community` | All nodes with same `community` value | Related types in same subsystem |

Output labels the relationship type (`contains` vs `imports`) so the caller can distinguish
within-file hierarchy from cross-file dependencies.

### Error Handling

- No match: print "No node found matching '<ClassName>'" and exit 0
- Multiple matches: list all matches with their `file_path` and prompt the user to be more specific
- `graph.json` not present: print a clear message directing user to build `Perpetuum.Server`

### Implementation Notes

- Load graph with `$g = Get-Content docs/graph/graph.json | ConvertFrom-Json`
- Match nodes: `$g.nodes | Where-Object { $_.label -like "*$ClassName*" }`
- Sort community output by label for readability
- Prefix each result line with the relationship type: `[contains]` or `[imports]`

---

## Component 2: `.claude/knowledge/codebase-graph.md` (updated)

### Changes

Replace the current file content with an updated version that:

1. **Adds GRAPH_REPORT.md as the primary starting point** — token-efficient, already summarizes
   god nodes, community clusters, and stats. Read this before reaching for `graph.json`.

2. **Updates god node guidance** — remove the hardcoded list (it becomes stale); instead point
   to `GRAPH_REPORT.md > God Nodes` as the authoritative, regenerated source.

3. **Adds concrete "How to use" section** with three explicit workflows:

   | Goal | Action |
   |---|---|
   | Architectural overview | Read `docs/graph/GRAPH_REPORT.md` |
   | Blast radius for a class | `.\tools\query-graph.ps1 <ClassName> -Direction in` |
   | Subsystem navigation | `.\tools\query-graph.ps1 <ClassName> -Direction community` |
   | Full dependency picture | `.\tools\query-graph.ps1 <ClassName> -Direction both` |

4. **Notes the edge sparsity** — `imports` edges (66 total) are the meaningful cross-file
   dependencies; `contains` edges (17,785) are the within-file hierarchy. Impact analysis
   via `imports` may be limited due to graphify AST-only mode.

---

## Component 3: `CLAUDE.md` changes

### Change A — Knowledge table

Add one row to the AI Contribution Rules table under "Where to Edit":

| Codebase graph & impact analysis | `.claude/knowledge/codebase-graph.md` |

### Change B — Required Workflow (new step 4)

Insert between step 3 ("Locate similar implementations") and step 4 ("Understand existing patterns"):

```
4. Check `docs/graph/GRAPH_REPORT.md` — if the target type appears in the God Nodes list,
   note high blast radius; run `tools/query-graph.ps1 <ClassName>` for full dependent
   enumeration
```

Existing steps 4–7 shift to 5–8.

---

## Files Changed

| Action | Path |
|---|---|
| Create | `tools/query-graph.ps1` |
| Modify | `.claude/knowledge/codebase-graph.md` |
| Modify | `CLAUDE.md` |

---

## Constraints

- No changes to `graph.json`, `GRAPH_REPORT.md`, or `Directory.Build.targets` — those are owned by IMPROVEMENT-021
- Script must work on PowerShell 5.1 (Windows PowerShell, not PS Core requirement)
- Script must soft-fail if `graph.json` is absent (e.g., freshly cloned without a build)
- No new NuGet or npm dependencies

---

## Validation

1. Run `.\tools\query-graph.ps1 Zone` — verify output shows node matches and their edges
2. Run `.\tools\query-graph.ps1 Zone -Direction community` — verify community members listed
3. Run `.\tools\query-graph.ps1 Zone -Direction in` — verify inbound edges listed with relationship type
4. Run `.\tools\query-graph.ps1 NonExistentClass` — verify graceful "no match" message
5. Rename `graph.json` temporarily, run script — verify it prints a clear missing-file message
6. Open `CLAUDE.md` and confirm knowledge table row and workflow step 4 are present
7. Open `.claude/knowledge/codebase-graph.md` and confirm GRAPH_REPORT.md reference and How to use table are present
