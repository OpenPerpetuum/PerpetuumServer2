package backup

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/openperpetuum/dbtool/internal/config"
	"github.com/openperpetuum/dbtool/internal/db"
)

func integrationSetup(t *testing.T) (*db.DB, *config.Config) {
	t.Helper()
	if os.Getenv("DBTOOL_INTEGRATION") != "1" {
		t.Skip("skipping integration test (set DBTOOL_INTEGRATION=1 to run)")
	}

	cfg := config.Load()

	// Use a temp backup dir for integration tests
	dir := t.TempDir()
	cfg.BackupDir = dir
	cfg.BackupHostDir = dir

	d, err := db.OpenNoDB(cfg.ConnStringNoDB())
	if err != nil {
		t.Fatalf("connect: %v", err)
	}
	t.Cleanup(func() { d.Close() })
	return d, cfg
}

func TestIntegration_BackupAndList(t *testing.T) {
	d, cfg := integrationSetup(t)
	ctx := context.Background()

	// Create backup with version description
	bakPath, err := Backup(ctx, d, cfg.DBName, cfg.BackupDir, cfg.BackupHostDir, "version=Live_36", true)
	if err != nil {
		t.Fatalf("Backup: %v", err)
	}
	defer os.Remove(bakPath)

	// Verify file exists and has content
	info, err := os.Stat(bakPath)
	if err != nil {
		t.Fatalf("backup file not found: %v", err)
	}
	if info.Size() == 0 {
		t.Error("backup file is empty")
	}
	t.Logf("Backup created: %s (%.1f MB)", bakPath, float64(info.Size())/1024/1024)

	// List backups with metadata
	infos, err := ListBackups(ctx, d, cfg.BackupDir, cfg.BackupHostDir)
	if err != nil {
		t.Fatalf("ListBackups: %v", err)
	}
	if len(infos) == 0 {
		t.Error("ListBackups returned 0 entries, expected at least 1")
	}
	if len(infos) > 0 && infos[0].Version != "Live_36" {
		t.Errorf("expected version 'Live_36', got %q", infos[0].Version)
	}
}

func TestIntegration_BackupNoCompress(t *testing.T) {
	d, cfg := integrationSetup(t)
	ctx := context.Background()

	bakPath, err := Backup(ctx, d, cfg.DBName, cfg.BackupDir, cfg.BackupHostDir, "version=Live_36", false)
	if err != nil {
		t.Fatalf("Backup (no compress): %v", err)
	}
	defer os.Remove(bakPath)

	info, _ := os.Stat(bakPath)
	t.Logf("Uncompressed backup: %s (%.1f MB)", bakPath, float64(info.Size())/1024/1024)
}

func TestIntegration_Restore(t *testing.T) {
	d, cfg := integrationSetup(t)
	ctx := context.Background()

	// Create a backup first
	bakPath, err := Backup(ctx, d, cfg.DBName, cfg.BackupDir, cfg.BackupHostDir, "version=Live_36", true)
	if err != nil {
		t.Fatalf("Backup for restore test: %v", err)
	}
	defer os.Remove(bakPath)

	// Restore to a temp database name to avoid overwriting
	tempDB := fmt.Sprintf("test_restore_%d", time.Now().Unix())

	err = Restore(ctx, d, bakPath, tempDB, true)
	if err != nil {
		t.Fatalf("Restore: %v", err)
	}
	t.Logf("Restored to temp DB: %s", tempDB)

	// Verify the temp DB exists
	if !d.DatabaseExists(ctx, tempDB) {
		t.Error("restored database does not exist")
	}

	// Cleanup: drop the temp database
	d.ExecSQL(ctx, fmt.Sprintf("DROP DATABASE [%s]", tempDB))
}

func TestIntegration_ListBackupsEmpty(t *testing.T) {
	d, _ := integrationSetup(t)
	ctx := context.Background()

	// Use an empty temp dir
	emptyDir := t.TempDir()
	infos, err := ListBackups(ctx, d, emptyDir, emptyDir)
	if err != nil {
		t.Fatalf("ListBackups on empty dir: %v", err)
	}
	if len(infos) != 0 {
		t.Errorf("expected 0 backups in empty dir, got %d", len(infos))
	}
}

func TestIntegration_ListBackups_SortsByTime(t *testing.T) {
	d, _ := integrationSetup(t)
	ctx := context.Background()

	dir := t.TempDir()

	// Create fake backup files with different timestamps
	files := []string{
		fmt.Sprintf("%s_perpetuumsa_20260101_000000.bak", dir),
		fmt.Sprintf("%s_perpetuumsa_20260103_000000.bak", dir),
		fmt.Sprintf("%s_perpetuumsa_20260102_000000.bak", dir),
	}
	for _, f := range files {
		os.WriteFile(f, []byte("fake"), 0644)
	}
	// Non-bak file should be ignored
	os.WriteFile(filepath.Join(dir, "notes.txt"), []byte("hi"), 0644)

	infos, err := ListBackups(ctx, d, dir, dir)
	if err != nil {
		t.Fatalf("ListBackups: %v", err)
	}
	if len(infos) != 3 {
		t.Fatalf("expected 3 .bak files, got %d", len(infos))
	}

	// Verify sorted newest first
	for i := 1; i < len(infos); i++ {
		if infos[i].ModTime.After(infos[i-1].ModTime) {
			t.Errorf("not sorted by time desc: %s after %s", infos[i].Name, infos[i-1].Name)
		}
	}
}

func TestIntegration_ReadBackupMetadata(t *testing.T) {
	d, cfg := integrationSetup(t)
	ctx := context.Background()

	// Create backup with known version
	bakPath, err := Backup(ctx, d, cfg.DBName, cfg.BackupDir, cfg.BackupHostDir, "version=Live_25", true)
	if err != nil {
		t.Fatalf("Backup: %v", err)
	}
	defer os.Remove(bakPath)

	// Read metadata back
	bakName := filepath.Base(bakPath)
	containerPath := filepath.Join(cfg.BackupDir, bakName)
	version, err := ReadBackupMetadata(ctx, d, containerPath)
	if err != nil {
		t.Fatalf("ReadBackupMetadata: %v", err)
	}
	if version != "Live_25" {
		t.Errorf("expected version 'Live_25', got %q", version)
	}
}
