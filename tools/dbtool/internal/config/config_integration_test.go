package config

import (
	"os"
	"testing"
)

func TestIntegration_LoadFromEnv(t *testing.T) {
	if os.Getenv("DBTOOL_INTEGRATION") != "1" {
		t.Skip("skipping integration test (set DBTOOL_INTEGRATION=1 to run)")
	}

	// Set env vars
	os.Setenv("DB_HOST", "localhost")
	os.Setenv("DB_PORT", "16998")
	os.Setenv("DB_USER", "sa")
	os.Setenv("DB_NAME", "perpetuumsa")
	os.Setenv("BACKUP_DIR", "/tmp/test-backups")
	defer func() {
		os.Unsetenv("DB_HOST")
		os.Unsetenv("DB_PORT")
		os.Unsetenv("DB_USER")
		os.Unsetenv("DB_NAME")
		os.Unsetenv("BACKUP_DIR")
	}()

	cfg := Load()

	if cfg.DBHost != "localhost" {
		t.Errorf("DBHost = %q, want %q", cfg.DBHost, "localhost")
	}
	if cfg.DBPort != "16998" {
		t.Errorf("DBPort = %q, want %q", cfg.DBPort, "16998")
	}
	if cfg.DBUser != "sa" {
		t.Errorf("DBUser = %q, want %q", cfg.DBUser, "sa")
	}
	if cfg.DBName != "perpetuumsa" {
		t.Errorf("DBName = %q, want %q", cfg.DBName, "perpetuumsa")
	}
	if cfg.BackupDir != "/tmp/test-backups" {
		t.Errorf("BackupDir = %q, want %q", cfg.BackupDir, "/tmp/test-backups")
	}

	// ConnString should contain the right values
	connStr := cfg.ConnString()
	if !contains(connStr, "server=localhost") {
		t.Errorf("ConnString missing server=localhost: %s", connStr)
	}
	if !contains(connStr, "port=16998") {
		t.Errorf("ConnString missing port=16998: %s", connStr)
	}
	if !contains(connStr, "database=perpetuumsa") {
		t.Errorf("ConnString missing database=perpetuumsa: %s", connStr)
	}
}

func TestIntegration_FindPatchesDir(t *testing.T) {
	if os.Getenv("DBTOOL_INTEGRATION") != "1" {
		t.Skip("skipping integration test (set DBTOOL_INTEGRATION=1 to run)")
	}

	cfg := Load()
	if cfg.PatchesDir == "" {
		t.Error("PatchesDir is empty")
	}
	// Verify the directory actually exists
	info, err := os.Stat(cfg.PatchesDir)
	if err != nil {
		t.Fatalf("PatchesDir %s does not exist: %v", cfg.PatchesDir, err)
	}
	if !info.IsDir() {
		t.Errorf("PatchesDir %s is not a directory", cfg.PatchesDir)
	}
	t.Logf("PatchesDir: %s", cfg.PatchesDir)
}

func contains(s, substr string) bool {
	return len(s) >= len(substr) && (s == substr || len(s) > 0 && containsImpl(s, substr))
}

func containsImpl(s, substr string) bool {
	for i := 0; i <= len(s)-len(substr); i++ {
		if s[i:i+len(substr)] == substr {
			return true
		}
	}
	return false
}
