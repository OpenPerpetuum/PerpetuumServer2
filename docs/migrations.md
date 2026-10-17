# Database Migrations

Database migration tooling for the OpenPerpetuum game server. Built on `dbtool` (Go binary) with Makefile targets for common operations.

## Quick Reference

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           MIGRATION COMMANDS                                    │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  make db-status          Check current version and pending migrations           │
│  make db-migrate         Apply all pending migrations (interactive)             │
│  make db-migrate-dry     Preview pending migrations without applying            │
│  make db-partial         Reset DB and migrate only up to a specific patch       │
│  make db-backup          Create a backup with version metadata                  │
│  make db-restore         Restore from backup (interactive picker)               │
│  make db-list            List available backups with version info               │
│  make db-verify          Verify DB integrity (tables, rows, constraints)        │
│  make db-shell           Open sqlcmd shell connected to the DB                  │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## Use Cases

### 1. Normal Development Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     NORMAL DEVELOPMENT (full stack)                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   make up                                                               │
│       │                                                                 │
│       ├──► db container starts (SQL Server)                             │
│       ├──► migration runs automatically (first boot only)               │
│       │       ├── Restores base backup (perpetuumsa.bak)                │
│       │       ├── Applies all patches (Pre_Alpha_* → Live_*)            │
│       │       └── Records versions in dbo.schema_migrations             │
│       └──► server starts                                                │
│                                                                         │
│   After pulling new patches:                                            │
│                                                                         │
│   make db-migrate-dry     ← preview what will run                       │
│   make db-migrate         ← apply with confirmation                     │
│   make db-status          ← verify you're up to date                    │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2. Testing a Specific Patch Version

```
┌─────────────────────────────────────────────────────────────────────────┐
│              TEST INCREMENTAL MIGRATION (partial → catch up)            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   make db-partial TO=Live_25                                            │
│       │                                                                 │
│       ├──► Tears down DB (volume wipe)                                  │
│       ├──► Restores base backup                                         │
│       └──► Applies patches up to Live_25 only (TUI)                     │
│                                                                         │
│   ... test at Live_25 state ...                                         │
│                                                                         │
│   make db-migrate-dry     ← shows 11 pending (Live_26..Live_36)         │
│   make db-migrate         ← catches up to Live_36                       │
│   make db-status          ← UP TO DATE                                  │
│   make up                 ← start full stack                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3. Backup and Restore

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      BACKUP & RESTORE WORKFLOW                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   make db-backup                                                        │
│       │                                                                 │
│       ├──► Connects to SQL Server                                       │
│       ├──► Queries current version (dbo.schema_migrations)              │
│       ├──► BACKUP DATABASE ... WITH DESCRIPTION='version=Live_36'       │
│       └──► Writes to ./backups/perpetuumsa_YYYYMMDD_HHMMSS.bak          │
│                                                                         │
│   make db-list                                                          │
│       │                                                                 │
│       └──► Lists backups with version (read from backup metadata)       │
│                                                                         │
│   make db-restore               ← interactive picker                    │
│       │                                                                 │
│       ├──► Shows available backups with [version]                       │
│       ├──► You pick one                                                 │
│       ├──► Confirms (DROP warning)                                      │
│       └──► RESTORE DATABASE ... WITH REPLACE                            │
│                                                                         │
│   make db-restore FILE=./backups/perpetuumsa_20261017_120000.bak        │
│       └──► Direct restore (no picker, auto-confirm)                     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4. Before Deploying Changes

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SAFETY CHECKS BEFORE DEPLOY                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   make db-backup              ← snapshot current state                  │
│   make db-migrate-dry         ← verify no unexpected migrations         │
│   make db-verify              ← check table integrity                   │
│                                                                         │
│   ... deploy ...                                                        │
│                                                                         │
│   make db-status              ← confirm version                         │
│   make db-verify              ← post-deploy check                       │
│                                                                         │
│   (if something broke:)                                                 │
│   make db-restore             ← roll back to pre-deploy backup          │
│   make db-migrate             ← re-apply migrations                     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Backup Storage

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        BACKUP STORAGE LAYOUT                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   Host:  ./backups/                                                     │
│            ├── perpetuumsa_20261017_120000.bak    [Live_25]             │
│            ├── perpetuumsa_20261017_180000.bak    [Live_36]             │
│            └── perpetuumsa_20261018_090000.bak    [Live_36]             │
│                                                                         │
│   Container:  /backups/   (same files via bind mount)                   │
│                                                                         │
│   ┌────────────────────┐           ┌──────────────────────────┐         │
│   │       HOST         │           │      DB CONTAINER        │         │
│   │                    │           │                          │         │
│   │  ./backups/        │  bind     │  /backups/               │         │
│   │  (dbtool r/w)      │<--------->│  (SQL writes .bak)       │         │
│   │                    │           │                          │         │
│   └────────────────────┘           └──────────────────────────┘         │
│                                                                         │
│   • Backups are compressed by default (BACKUP ... WITH COMPRESSION)     │
│   • Version is embedded in the .bak file metadata (DESCRIPTION field)   │
│   • No sidecar files — version detected at runtime via RESTORE HEADER   │
│   • Directory is gitignored                                             │
│   • Permissions: 777 (shared between host user and container UID 10001) │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Version Tracking

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      HOW VERSIONS ARE TRACKED                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   dbo.schema_migrations table:                                          │
│                                                                         │
│   ┌──────────────┬──────────────────────┬───────────────────────┐       │
│   │ version      │ applied_at           │ source_file           │       │
│   ├──────────────┼──────────────────────┼───────────────────────┤       │
│   │ Pre_Alpha_0  │ 2026-10-17 12:00:01  │ Pre_Alpha_0           │       │
│   │ Pre_Alpha_1  │ 2026-10-17 12:00:02  │ Pre_Alpha_1           │       │
│   │ ...          │ ...                  │ ...                   │       │
│   │ Live_36      │ 2026-10-17 12:05:33  │ Live_36               │       │
│   └──────────────┴──────────────────────┴───────────────────────┘       │
│                                                                         │
│   • Written by: migration.sh (recordVersion) or dbtool migrate          │
│   • Read by:   dbtool status, dbtool migrate, dbtool backup             │
│   • Purpose:   Enables incremental migration (skip already-applied)     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Patch Directory Structure

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      PATCH LAYOUT (db/Patches/)                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   db/Patches/                                                           │
│   ├── Pre_Alpha_0/                                                      │
│   │   └── prealpha_patch_0.sql                                          │
│   ├── Pre_Alpha_1/                                                      │
│   │   └── prealpha_patch_1.sql                                          │
│   ├── ...                                                               │
│   ├── Pre_Alpha_7_FInal/                                                │
│   │   ├── FIX_robottemplaterelation_pinkarkhe.sql                       │
│   │   └── NPC_robottemplates_argano_GetsEcms__2018_04_12.sql            │
│   ├── Live_1/                                                           │
│   │   └── live_patch_1.sql                                              │
│   ├── ...                                                               │
│   └── Live_36/                                                          │
│       └── Raw_SQL/                                                      │
│           ├── 01_...sql                                                 │
│           ├── ...                                                       │
│           └── 77_...sql                                                 │
│                                                                         │
│   • 44 patches total (8 Pre_Alpha + 36 Live)                            │
│   • Order: Pre_Alpha_* → Live_* (sorted version-wise)                   │
│   • Each patch = one directory, may contain multiple .sql files         │
│   • SQL files use GO batch separators (like sqlcmd)                     │
│   • UTF-16LE files are auto-detected and transcoded                     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Building dbtool

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      BUILDING THE dbtool BINARY                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   make build-dbtool         ← builds via Docker (no Go needed on host)  │
│   make build-dbtool-local   ← builds via local Go (if installed)        │
│                                                                         │
│   The binary is auto-built when running any db-* target if missing.     │
│   Source: tools/dbtool/    Binary: tools/dbtool/dbtool (gitignored)     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Testing

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      TESTING                                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   make test-dbtool              ← unit tests (no DB needed)             │
│   make test-dbtool-integration  ← integration tests (needs live DB)     │
│                                                                         │
│   Integration tests are gated behind DBTOOL_INTEGRATION=1.              │
│   They test: compute plan, apply, record version, backup/restore,       │
│   batch splitting on real patch files, config loading.                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Environment Variables

All `db-*` Makefile targets auto-source `.env.local` and export:

| Variable | Default | Description |
|----------|---------|-------------|
| `DB_HOST` | `localhost` | SQL Server host (container uses `db`) |
| `DB_PORT` | `1433` | SQL Server port (host-mapped in `.env.local`) |
| `DB_USER` | `sa` | SQL Server user |
| `DB_PASSWORD` | *(from .env.local)* | SQL Server password |
| `DB_NAME` | `perpetuumsa` | Database name |
| `BACKUP_DIR` | `/backups` | Container path for backup files |
| `BACKUP_HOST_DIR` | `./backups` | Host path for backup files |
