# graphify-dotnet Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire `graphify-dotnet` as a local dotnet tool with a pre-build MSBuild target that commits a structural JSON + HTML knowledge graph of the codebase to `docs/graph/`.

**Architecture:** `.config/dotnet-tools.json` registers the tool; `Directory.Build.targets` at solution root fires a `BeforeBuild` MSBuild target conditioned on `Perpetuum.Server` only. The target runs `graphify-dotnet` with `ContinueOnError="true"` (soft-fail). Two output artifacts (`graph.json`, `graph.html`) are committed. A new `.claude/knowledge/codebase-graph.md` tells Claude how to interpret the graph.

**Tech Stack:** .NET 10 SDK (tool runtime only — project TFMs stay at net8.0), graphify-dotnet 0.7.0, MSBuild, PowerShell

**Spec:** `docs/superpowers/specs/2026-05-23-improvement-021-graphify-integration-design.md`

---

## File Map

| Action | Path | Purpose |
|---|---|---|
| Create | `.config/dotnet-tools.json` | Registers `graphify-dotnet@0.7.0` as a local tool |
| Create | `Directory.Build.targets` | MSBuild pre-build target at solution root |
| Create | `docs/graph/graph.json` | Committed machine-readable structural graph |
| Create | `docs/graph/graph.html` | Committed interactive HTML visualization |
| Create | `.claude/knowledge/codebase-graph.md` | Claude orientation file |
| Modify | `docs/codebase/ARCHITECTURE.md` | Add graph artifact pointer section |
| Modify | `docs/backlog/improvements.md` | Update IMPROVEMENT-021 to DONE, correct URL |

---

### Task 1: Register graphify-dotnet as a local dotnet tool

**Files:**
- Create: `.config/dotnet-tools.json`

- [ ] **Step 1: Create the `.config/` directory and `dotnet-tools.json`**

From the solution root (`E:\MyStuff\Projects\PerpetuumServer2`):

```powershell
New-Item -ItemType Directory -Force ".config"
```

Create `.config/dotnet-tools.json` with this content:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "graphify-dotnet": {
      "version": "0.7.0",
      "commands": [
        "graphify-dotnet"
      ]
    }
  }
}
```

- [ ] **Step 2: Install the tool**

```
dotnet tool restore
```

Expected: output contains a line like `Restored graphify-dotnet (version 0.7.0)`.

If it fails with "SDK version not found" or similar, verify .NET 10 is installed:
```
dotnet --list-sdks
```
There must be a `10.x.x` entry. Install .NET 10 SDK from https://dot.net if absent.

- [ ] **Step 3: Confirm the registered command name**

```
dotnet tool list
```

Expected output (approximate):
```
Package Id          Version      Commands
----------------------------------------------
graphify-dotnet     0.7.0        graphify-dotnet
```

Note the value in the `Commands` column exactly. If it reads something other than `graphify-dotnet` (e.g., just `graphify`), record that value — you will substitute it in Task 2.

- [ ] **Step 4: Check available CLI flags**

```
dotnet tool run graphify-dotnet --help
```

Look for flags equivalent to:
- Input source directory (likely `--input` or `-i` or first positional arg)
- Output directory (likely `--output` or `-o`)
- Format selection (likely `--format` or `-f`, with values like `json`, `html`)

If the flag names differ from `--input`, `--output`, `--format`, note the correct names for Task 2.

- [ ] **Step 5: Commit**

```
git add .config/dotnet-tools.json
git commit -m "chore: register graphify-dotnet 0.7.0 local tool"
```

---

### Task 2: Create Directory.Build.targets pre-build target

**Files:**
- Create: `Directory.Build.targets` (solution root — same folder as `PerpetuumServer2.sln`)
- Create: `docs/graph/.gitkeep`

- [ ] **Step 1: Create `Directory.Build.targets`**

Create this file at the solution root. If the CLI command name or flags from Task 1 differed from the defaults, update the `Command` attribute accordingly.

```xml
<Project>
  <!--
    Regenerates the codebase knowledge graph before building Perpetuum.Server.
    ContinueOnError="true": build is never blocked if the tool is unavailable
    (e.g. .NET 10 SDK not installed or dotnet tool restore not run).
    Conditioned on Perpetuum.Server to run once per solution build, not once
    per project.
  -->
  <Target Name="GenerateCodeGraph" BeforeTargets="Build"
          Condition="'$(MSBuildProjectName)' == 'Perpetuum.Server'">
    <Exec Command="dotnet tool run graphify-dotnet --input &quot;$(SolutionDir)src&quot; --output &quot;$(SolutionDir)docs\graph&quot; --format json,html"
          ContinueOnError="true"
          WorkingDirectory="$(SolutionDir)" />
  </Target>
</Project>
```

> **Substitution note:** Replace `graphify-dotnet` in `dotnet tool run graphify-dotnet` with the Commands column value from Task 1 Step 3 if different. Replace `--input`, `--output`, `--format json,html` with the correct flags from Task 1 Step 4 if different.

- [ ] **Step 2: Create `docs/graph/` directory with a placeholder**

```powershell
New-Item -ItemType Directory -Force "docs\graph"
New-Item -ItemType File -Force "docs\graph\.gitkeep"
```

- [ ] **Step 3: Build Perpetuum.Server and verify the target fires**

```
dotnet build src/Perpetuum.Server/Perpetuum.Server.csproj -c Release -p:Platform=x64
```

Scan the build output for `GenerateCodeGraph`. You should see it listed in the target execution sequence. The tool should run and produce output.

- [ ] **Step 4: Verify graph files were created**

```powershell
Get-ChildItem "docs\graph"
```

Expected: `graph.json` and `graph.html` are present alongside `.gitkeep`.

If only `.gitkeep` is present, the target ran but the tool produced no output. Diagnose by running the tool directly:
```
dotnet tool run graphify-dotnet --input src --output docs\graph --format json,html
```
Check for errors and adjust the `--input` path or format flags as needed, then update `Directory.Build.targets` to match.

- [ ] **Step 5: Commit**

```
git add Directory.Build.targets docs/graph/.gitkeep
git commit -m "chore: add graphify pre-build target and graph output directory"
```

---

### Task 3: Verify graph quality and commit artifacts

**Files:**
- Verify: `docs/graph/graph.json`
- Verify: `docs/graph/graph.html`

- [ ] **Step 1: Inspect `graph.json` for meaningful content**

Open `docs/graph/graph.json`. It should contain arrays of nodes and edges. Look for known class names from the codebase — for example `Zone`, `Player`, `Robot`, `EntityDefault`. These should appear as node labels or names.

Expected structure (exact shape depends on graphify-dotnet version):
```json
{
  "nodes": [ { "id": "...", "label": "Zone", ... }, ... ],
  "edges": [ { "source": "...", "target": "...", ... }, ... ]
}
```

If the file is empty or contains only `{}`, the tool ran without errors but found no C# source. Check that the `--input` path in `Directory.Build.targets` resolves to the `src/` directory containing `.cs` files.

- [ ] **Step 2: Open `graph.html` in a browser**

Open `docs/graph/graph.html` directly in a browser (no server needed). Confirm:
- The page loads without JavaScript console errors
- Nodes are visible and the graph is interactive (pan, zoom, click)
- Known class names (`Zone`, `Player`, `Robot`) are findable via any search or filter UI

- [ ] **Step 3: Remove `.gitkeep` now that real files exist**

```powershell
Remove-Item "docs\graph\.gitkeep"
```

- [ ] **Step 4: Commit the graph artifacts**

```
git add docs/graph/
git commit -m "chore: add initial graphify codebase graph artifacts"
```

---

### Task 4: Create Claude knowledge orientation file

**Files:**
- Create: `.claude/knowledge/codebase-graph.md`

- [ ] **Step 1: Create the `.claude/knowledge/` directory**

```powershell
New-Item -ItemType Directory -Force ".claude\knowledge"
```

- [ ] **Step 2: Create `.claude/knowledge/codebase-graph.md`**

```markdown
# Codebase Dependency Graph

Generated by graphify-dotnet. Regenerates automatically before each build of Perpetuum.Server
(requires .NET 10 SDK and `dotnet tool restore` from the solution root).

- Machine-readable graph: `docs/graph/graph.json`
- Interactive visualization: `docs/graph/graph.html` (open in browser)

## Structure

Nodes represent C# classes, interfaces, and namespaces.
Edges represent inheritance, composition, and namespace imports.
Communities (Louvain clustering) group related classes into clusters.

## How to use this

- **Impact analysis:** When a type is modified, query `graph.json` for edges pointing to it
  to find all dependents before assessing blast radius.
- **Subsystem navigation:** Look up a class in the graph to find its community cluster
  and discover related types in the same subsystem.
- **Dependency verification:** Check the graph to confirm no unintended cross-subsystem
  dependency is introduced by a change.
```

- [ ] **Step 3: Commit**

```
git add .claude/knowledge/codebase-graph.md
git commit -m "docs: add Claude orientation file for codebase graph"
```

---

### Task 5: Update ARCHITECTURE.md and backlog

**Files:**
- Modify: `docs/codebase/ARCHITECTURE.md`
- Modify: `docs/backlog/improvements.md`

- [ ] **Step 1: Add graph artifact section to ARCHITECTURE.md**

Open `docs/codebase/ARCHITECTURE.md`. After the opening comment (`<!-- refreshed: ... -->`), insert a new section before the `# Architecture` heading:

```markdown
## Graph Artifact

A committed structural graph of this codebase is available at `docs/graph/`:
- `graph.json` — machine-readable nodes/edges for programmatic use
- `graph.html` — interactive browser visualization

See `.claude/knowledge/codebase-graph.md` for how Claude uses these files.
Regenerated automatically before each `Perpetuum.Server` build (requires `dotnet tool restore`).

```

- [ ] **Step 2: Update IMPROVEMENT-021 in the backlog**

Open `docs/backlog/improvements.md`. Find the `## IMPROVEMENT-021` section and replace it entirely with:

```markdown
## IMPROVEMENT-021 - Graphify Codebase Graph Integration

Status: DONE
Priority: HIGH
Area: Infrastructure / Tooling / AI
Spec: docs/superpowers/specs/2026-05-23-improvement-021-graphify-integration-design.md

### Description
Integrated `graphify-dotnet` (https://github.com/elbruno/graphify-dotnet) as a local dotnet
tool with an MSBuild pre-build target. Regenerates a structural JSON + HTML knowledge graph
of the codebase before every `Perpetuum.Server` build. Artifacts committed to `docs/graph/`.
Claude reads `graph.json` for impact analysis and navigation.

### Implementation
- `.config/dotnet-tools.json` registers `graphify-dotnet@0.7.0`
- `Directory.Build.targets` (solution root) fires `GenerateCodeGraph` before `Perpetuum.Server` builds; `ContinueOnError="true"` soft-fails on machines without .NET 10 SDK
- `docs/graph/graph.json` and `docs/graph/graph.html` committed and kept current
- `.claude/knowledge/codebase-graph.md` added for Claude orientation

### Notes
Phase 2 (.NET 8 → .NET 10 project TFM migration) is deferred as an independent workstream.
The graphify tool requires .NET 10 SDK but does not require project TFMs to change.
Run `dotnet tool restore` once after cloning to enable graph regeneration.

---
```

- [ ] **Step 3: Commit**

```
git add docs/codebase/ARCHITECTURE.md docs/backlog/improvements.md
git commit -m "docs: update ARCHITECTURE.md and backlog for graphify integration (IMPROVEMENT-021)"
```

---

## Manual Validation Checklist

After all tasks are complete:

1. Fresh clone (or `dotnet tool restore` on existing clone) → `dotnet tool restore` succeeds
2. `dotnet build src/Perpetuum.Server/Perpetuum.Server.csproj -c Release -p:Platform=x64` → builds successfully, `GenerateCodeGraph` target appears in output
3. `docs/graph/graph.json` exists and contains known class names
4. `docs/graph/graph.html` opens in browser and renders nodes interactively
5. Remove .NET 10 SDK or skip `dotnet tool restore`, rebuild → build succeeds (soft-fail confirmed)
6. `git log --oneline -5` shows all 5 commits from this plan
