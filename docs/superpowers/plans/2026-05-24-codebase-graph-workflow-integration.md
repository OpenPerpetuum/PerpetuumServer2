# Codebase Graph Workflow Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the existing `graphify-dotnet` graph artifacts into Claude's actual workflow via a query script, an updated knowledge file, and two edits to CLAUDE.md.

**Architecture:** Three independent changes with no shared state: a new PowerShell script at `tools/query-graph.ps1`, an in-place replacement of `.claude/knowledge/codebase-graph.md`, and two targeted edits to `CLAUDE.md` (knowledge table row + Required Workflow step). Each task can be committed independently.

**Tech Stack:** PowerShell 5.1 (Windows PowerShell), `graph.json` / `GRAPH_REPORT.md` already present at `docs/graph/`

**Spec:** `docs/superpowers/specs/2026-05-24-codebase-graph-workflow-integration-design.md`

---

## File Map

| Action | Path | Purpose |
|---|---|---|
| Create dir | `tools/` | Container for project utility scripts |
| Create | `tools/query-graph.ps1` | Graph query script (all four directions) |
| Modify | `.claude/knowledge/codebase-graph.md` | Add GRAPH_REPORT reference, usage table, script callout, edge-sparsity note |
| Modify | `CLAUDE.md` | Add knowledge table row + Required Workflow step 4 |

---

### Task 1: Create `tools/query-graph.ps1`

**Files:**
- Create: `tools/query-graph.ps1`

- [ ] **Step 1: Create the `tools/` directory**

From the solution root (`E:\MyStuff\Projects\PerpetuumServer2`):

```powershell
New-Item -ItemType Directory -Force tools
```

- [ ] **Step 2: Create `tools/query-graph.ps1`**

Create the file with the following content exactly:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$ClassName,

    [Parameter(Mandatory=$false)]
    [ValidateSet('in', 'out', 'both', 'community')]
    [string]$Direction = 'both'
)

$graphPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\docs\graph\graph.json'))

if (-not (Test-Path $graphPath)) {
    Write-Host "graph.json not found at: $graphPath" -ForegroundColor Yellow
    Write-Host "Build Perpetuum.Server once to generate it:"
    Write-Host "  dotnet build src/Perpetuum.Server/Perpetuum.Server.csproj -c Release -p:Platform=x64"
    exit 0
}

Write-Host "Loading graph..." -ForegroundColor DarkGray
$g = Get-Content $graphPath -Raw | ConvertFrom-Json

$matchedNodes = @($g.nodes | Where-Object { $_.label -like "*$ClassName*" })

if ($matchedNodes.Count -eq 0) {
    Write-Host "No node found matching '$ClassName'"
    exit 0
}

if ($matchedNodes.Count -gt 1) {
    Write-Host "$($matchedNodes.Count) nodes match '$ClassName'. Use a more specific name:" -ForegroundColor Yellow
    $matchedNodes | ForEach-Object { Write-Host "  $($_.label)  ($($_.file_path))" }
    exit 0
}

$node = $matchedNodes[0]
Write-Host ""
Write-Host "Node: $($node.label)  [community $($node.community)]" -ForegroundColor Cyan
Write-Host "File: $($node.file_path)"
Write-Host ""

if ($Direction -eq 'in' -or $Direction -eq 'both') {
    $inEdges = @($g.edges | Where-Object { $_.target -eq $node.id })
    Write-Host "=== Inbound ($($inEdges.Count)): who depends on this ===" -ForegroundColor Green
    if ($inEdges.Count -eq 0) {
        Write-Host "  (none)"
    } else {
        foreach ($edge in $inEdges) {
            $srcNode = $g.nodes | Where-Object { $_.id -eq $edge.source } | Select-Object -First 1
            $srcLabel = if ($srcNode) { $srcNode.label } else { $edge.source }
            Write-Host "  [$($edge.relationship)]  $srcLabel"
        }
    }
    Write-Host ""
}

if ($Direction -eq 'out' -or $Direction -eq 'both') {
    $outEdges = @($g.edges | Where-Object { $_.source -eq $node.id })
    Write-Host "=== Outbound ($($outEdges.Count)): what this depends on ===" -ForegroundColor Green
    if ($outEdges.Count -eq 0) {
        Write-Host "  (none)"
    } else {
        foreach ($edge in $outEdges) {
            $tgtNode = $g.nodes | Where-Object { $_.id -eq $edge.target } | Select-Object -First 1
            $tgtLabel = if ($tgtNode) { $tgtNode.label } else { $edge.target }
            Write-Host "  [$($edge.relationship)]  $tgtLabel"
        }
    }
    Write-Host ""
}

if ($Direction -eq 'community') {
    $communityNodes = @($g.nodes | Where-Object { $_.community -eq $node.community -and $_.id -ne $node.id } | Sort-Object label)
    Write-Host "=== Community $($node.community) ($($communityNodes.Count + 1) members) ===" -ForegroundColor Green
    Write-Host "  [self]  $($node.label)"
    foreach ($member in $communityNodes) {
        Write-Host "  [$($member.type)]  $($member.label)  ($($member.file_path))"
    }
    Write-Host ""
}
```

- [ ] **Step 3: Smoke-test — basic lookup**

From the solution root:

```powershell
.\tools\query-graph.ps1 StandingList
```

Expected output (approximate):
```
Loading graph...

Node: StandingList.cs  [community 1211]
File: Perpetuum.RequestHandlers/Standings/StandingList.cs

=== Inbound (N): who depends on this ===
  ...
=== Outbound (N): what this depends on ===
  ...
```

The exact edge counts will vary. What matters: no errors, node is found, both sections print.

- [ ] **Step 4: Smoke-test — community direction**

```powershell
.\tools\query-graph.ps1 StandingList -Direction community
```

Expected: lists other members of community 1211 with `[Entity]` or `[File]` type prefix.

- [ ] **Step 5: Smoke-test — no-match case**

```powershell
.\tools\query-graph.ps1 ClassThatDoesNotExist999
```

Expected output:
```
Loading graph...
No node found matching 'ClassThatDoesNotExist999'
```

- [ ] **Step 6: Smoke-test — multiple-match case**

```powershell
.\tools\query-graph.ps1 Zone
```

Expected: either lists a single matched node, or prints "N nodes match 'Zone'. Use a more specific name:" followed by a list of matches. Both are valid — what matters is no crash.

- [ ] **Step 7: Smoke-test — missing graph.json**

Temporarily rename the graph file, run the script, then restore it:

```powershell
Rename-Item docs\graph\graph.json docs\graph\graph.json.bak
.\tools\query-graph.ps1 StandingList
Rename-Item docs\graph\graph.json.bak docs\graph\graph.json
```

Expected output during rename:
```
graph.json not found at: ...\docs\graph\graph.json
Build Perpetuum.Server once to generate it:
  dotnet build src/Perpetuum.Server/Perpetuum.Server.csproj -c Release -p:Platform=x64
```

- [ ] **Step 8: Commit**

```powershell
git add tools/query-graph.ps1
git commit -m "feat: add graph query script tools/query-graph.ps1"
```

---

### Task 2: Update `.claude/knowledge/codebase-graph.md`

**Files:**
- Modify: `.claude/knowledge/codebase-graph.md`

- [ ] **Step 1: Replace the file content**

Replace the entire content of `.claude/knowledge/codebase-graph.md` with:

```markdown
# Codebase Dependency Graph

Generated by graphify-dotnet from the `src/` directory. Regenerates automatically before
each build of `Perpetuum.Server` (requires .NET 10 SDK and `dotnet tool restore` from the
solution root).

## Artifacts

- **`docs/graph/GRAPH_REPORT.md`** — Markdown summary: god nodes, community clusters, stats.
  **Read this first.** Token-efficient starting point for any architectural question.
- **`docs/graph/graph.json`** — Full machine-readable graph (19,926 nodes, 33,689 edges).
  Use via `tools/query-graph.ps1` — do not read the raw file.
- **GitHub Wiki** — latest report published by CI:
  `https://github.com/OpenPerpetuum/PerpetuumServer2/wiki/Codebase-Graph`

## Graph Structure

Nodes represent C# classes, methods, and namespaces (type: `Entity` or `File`).
Edges have two relationship types:
- **`contains`** (17,785 edges) — within-file hierarchy: file → namespace → class → method
- **`imports`** (66 edges) — cross-file namespace dependencies (the meaningful ones for impact analysis)

Communities (Louvain clustering) group related symbols into 2,616 clusters.

## How to Use

| Goal | Action |
|---|---|
| Architectural overview or god-node check | Read `docs/graph/GRAPH_REPORT.md` |
| Blast radius before modifying a class | `.\tools\query-graph.ps1 <ClassName> -Direction in` |
| What a class depends on | `.\tools\query-graph.ps1 <ClassName> -Direction out` |
| Full dependency picture | `.\tools\query-graph.ps1 <ClassName>` (default: both) |
| Find related types in the same subsystem | `.\tools\query-graph.ps1 <ClassName> -Direction community` |

## God-Node Awareness

The top 10 most-connected symbols are listed in `docs/graph/GRAPH_REPORT.md` under "God Nodes".
These are the highest-risk symbols to change — check the report before modifying any of them.
The list regenerates on each build; do not rely on hardcoded names from prior sessions.

> **Note on import sparsity:** Only 66 `imports` edges exist across the entire codebase (AST-only
> analysis). Inbound results for most classes will be sparse. Absence of inbound edges does not
> mean a class is unused — it means graphify did not detect a namespace import.
```

- [ ] **Step 2: Verify the file reads correctly**

Open `.claude/knowledge/codebase-graph.md` and confirm:
- "Read this first" appears next to GRAPH_REPORT.md
- The How to Use table contains all five rows
- The god-node section points to GRAPH_REPORT.md (no hardcoded class names)
- The import-sparsity note is present at the bottom

- [ ] **Step 3: Commit**

```powershell
git add .claude/knowledge/codebase-graph.md
git commit -m "docs: update codebase-graph.md with GRAPH_REPORT, query script, and edge-sparsity note"
```

---

### Task 3: Update `CLAUDE.md`

**Files:**
- Modify: `CLAUDE.md`

Two edits, each committed together at the end.

- [ ] **Step 1: Add row to the knowledge table in AI Contribution Rules**

Find this block in `CLAUDE.md` (around line 327):

```markdown
| Purpose | File |
|---|---|
| Main AI instructions | `CLAUDE.md` |
| Architecture deep-dive | `.claude/knowledge/architecture.md` |
| Specialist agents | `.claude/agents/<name>.md` |
```

Replace it with:

```markdown
| Purpose | File |
|---|---|
| Main AI instructions | `CLAUDE.md` |
| Architecture deep-dive | `.claude/knowledge/architecture.md` |
| Codebase graph & impact analysis | `.claude/knowledge/codebase-graph.md` |
| Specialist agents | `.claude/agents/<name>.md` |
```

- [ ] **Step 2: Add step 4 to the Required Workflow**

Find this block in `CLAUDE.md` (around line 154):

```markdown
For any non-trivial task:

1. Identify affected subsystems
2. Identify relevant documentation
3. Locate similar implementations
4. Understand existing patterns
5. Evaluate runtime implications
6. Produce a short implementation plan
7. Then implement
```

Replace it with:

```markdown
For any non-trivial task:

1. Identify affected subsystems
2. Identify relevant documentation
3. Locate similar implementations
4. Check `docs/graph/GRAPH_REPORT.md` — if the target type is in the God Nodes list, note high blast radius; run `.\tools\query-graph.ps1 <ClassName>` for full dependent enumeration
5. Understand existing patterns
6. Evaluate runtime implications
7. Produce a short implementation plan
8. Then implement
```

- [ ] **Step 3: Verify both edits are present**

Open `CLAUDE.md` and confirm:
- The AI Contribution Rules table has the `codebase-graph.md` row
- Required Workflow has 8 steps with the graph check as step 4

- [ ] **Step 4: Commit**

```powershell
git add CLAUDE.md
git commit -m "docs: wire codebase graph into CLAUDE.md workflow and knowledge table"
```

---

## Manual Validation Checklist

After all tasks are complete:

1. `.\tools\query-graph.ps1 StandingList` — prints node + inbound + outbound sections, no errors
2. `.\tools\query-graph.ps1 StandingList -Direction community` — prints community members
3. `.\tools\query-graph.ps1 NoSuchClass999` — prints "No node found matching..." gracefully
4. `.\tools\query-graph.ps1 Zone` — either finds a single match or lists multiple matches without crashing
5. `CLAUDE.md` Required Workflow has 8 steps with graph check at position 4
6. `CLAUDE.md` AI Contribution Rules table includes `codebase-graph.md` row
7. `.claude/knowledge/codebase-graph.md` references GRAPH_REPORT.md as primary starting point, includes How to Use table, no hardcoded god-node names
8. `git log --oneline -3` shows the three commits from this plan
