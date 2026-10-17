package config

import (
	"os"
	"testing"
)

func TestLoadDefaults(t *testing.T) {
	// Clear relevant env vars
	os.Unsetenv("DB_HOST")
	os.Unsetenv("DB_PORT")
	os.Unsetenv("DB_USER")
	os.Unsetenv("DB_PASSWORD")
	os.Unsetenv("DB_NAME")
	os.Unsetenv("PATCHES_DIR")
	os.Unsetenv("BACKUP_DIR")

	cfg := Load()

	if cfg.DBHost != "db" {
		t.Errorf("DBHost = %q, want %q", cfg.DBHost, "db")
	}
	if cfg.DBPort != "1433" {
		t.Errorf("DBPort = %q, want %q", cfg.DBPort, "1433")
	}
	if cfg.DBUser != "sa" {
		t.Errorf("DBUser = %q, want %q", cfg.DBUser, "sa")
	}
	if cfg.DBName != "perpetuumsa" {
		t.Errorf("DBName = %q, want %q", cfg.DBName, "perpetuumsa")
	}
	if cfg.BackupDir != "/backups" {
		t.Errorf("BackupDir = %q, want %q", cfg.BackupDir, "/backups")
	}
	if cfg.BackupHostDir != "./backups" {
		t.Errorf("BackupHostDir = %q, want %q", cfg.BackupHostDir, "./backups")
	}
	if !cfg.TrustCert {
		t.Error("TrustCert = false, want true")
	}
}

func TestLoadFromEnv(t *testing.T) {
	os.Setenv("DB_HOST", "localhost")
	os.Setenv("DB_PORT", "14330")
	os.Setenv("DB_USER", "admin")
	os.Setenv("DB_PASSWORD", "secret")
	os.Setenv("DB_NAME", "gamedb")
	os.Setenv("BACKUP_DIR", "/backups")
	defer func() {
		os.Unsetenv("DB_HOST")
		os.Unsetenv("DB_PORT")
		os.Unsetenv("DB_USER")
		os.Unsetenv("DB_PASSWORD")
		os.Unsetenv("DB_NAME")
		os.Unsetenv("BACKUP_DIR")
	}()

	cfg := Load()

	if cfg.DBHost != "localhost" {
		t.Errorf("DBHost = %q, want %q", cfg.DBHost, "localhost")
	}
	if cfg.DBPort != "14330" {
		t.Errorf("DBPort = %q, want %q", cfg.DBPort, "14330")
	}
	if cfg.DBUser != "admin" {
		t.Errorf("DBUser = %q, want %q", cfg.DBUser, "admin")
	}
	if cfg.DBPassword != "secret" {
		t.Errorf("DBPassword = %q, want %q", cfg.DBPassword, "secret")
	}
	if cfg.DBName != "gamedb" {
		t.Errorf("DBName = %q, want %q", cfg.DBName, "gamedb")
	}
	if cfg.BackupDir != "/backups" {
		t.Errorf("BackupDir = %q, want %q", cfg.BackupDir, "/backups")
	}
}

func TestConnString(t *testing.T) {
	cfg := &Config{
		DBHost:     "myserver",
		DBPort:     "1433",
		DBUser:     "sa",
		DBPassword: "pass123",
		DBName:     "testdb",
		TrustCert:  true,
	}

	cs := cfg.ConnString()

	expected := "server=myserver;port=1433;user id=sa;password=pass123;encrypt=true;trustservercertificate=true;database=testdb"
	if cs != expected {
		t.Errorf("ConnString() = %q\nwant:      %q", cs, expected)
	}
}

func TestConnStringNoDB(t *testing.T) {
	cfg := &Config{
		DBHost:     "myserver",
		DBPort:     "1433",
		DBUser:     "sa",
		DBPassword: "pass123",
		TrustCert:  true,
	}

	cs := cfg.ConnStringNoDB()

	expected := "server=myserver;port=1433;user id=sa;password=pass123;encrypt=true;trustservercertificate=true"
	if cs != expected {
		t.Errorf("ConnStringNoDB() = %q\nwant:       %q", cs, expected)
	}
}

func TestConnStringNoTrust(t *testing.T) {
	cfg := &Config{
		DBHost:     "myserver",
		DBPort:     "1433",
		DBUser:     "sa",
		DBPassword: "pass123",
		DBName:     "testdb",
		TrustCert:  false,
	}

	cs := cfg.ConnString()

	if !containsStr(cs, "trustservercertificate=false") {
		t.Errorf("ConnString() should contain trustservercertificate=false, got: %q", cs)
	}
}

func containsStr(s, substr string) bool {
	return len(s) >= len(substr) && (s == substr || len(s) > 0 && indexOf(s, substr) >= 0)
}

func indexOf(s, substr string) int {
	for i := 0; i <= len(s)-len(substr); i++ {
		if s[i:i+len(substr)] == substr {
			return i
		}
	}
	return -1
}

func TestFindPatchesDirFromEnv(t *testing.T) {
	os.Setenv("PATCHES_DIR", "/custom/patches")
	defer os.Unsetenv("PATCHES_DIR")

	dir := findPatchesDir()
	if dir != "/custom/patches" {
		t.Errorf("findPatchesDir() = %q, want %q", dir, "/custom/patches")
	}
}

func TestIsDir(t *testing.T) {
	if isDir("/nonexistent/path/xyz") {
		t.Error("isDir() = true for nonexistent path")
	}
	if !isDir("/tmp") {
		t.Error("isDir() = false for /tmp")
	}
}
