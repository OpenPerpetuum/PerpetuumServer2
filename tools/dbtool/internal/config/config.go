package config

import (
	"fmt"
	"os"
	"path/filepath"
)

// Config holds database and tool configuration.
type Config struct {
	DBHost     string
	DBPort     string
	DBUser     string
	DBPassword string
	DBName     string
	TrustCert  bool

	PatchesDir   string
	BackupDir    string // Path SQL Server uses (container path, e.g. /backups)
	BackupHostDir string // Path on the host for file ops (e.g. ./backups)
}

// Load builds config from environment variables with sensible defaults.
func Load() *Config {
	return &Config{
		DBHost:     getEnv("DB_HOST", "db"),
		DBPort:     getEnv("DB_PORT", "1433"),
		DBUser:     getEnv("DB_USER", "sa"),
		DBPassword: getEnv("DB_PASSWORD", ""),
		DBName:     getEnv("DB_NAME", "perpetuumsa"),
		TrustCert:  true,
		PatchesDir:    findPatchesDir(),
		BackupDir:     getEnv("BACKUP_DIR", "/backups"),
		BackupHostDir: getEnv("BACKUP_HOST_DIR", "./backups"),
	}
}

// ConnString returns the go-mssqldb connection string.
func (c *Config) ConnString() string {
	return c.baseConnStr() + fmt.Sprintf(";database=%s", c.DBName)
}

// ConnStringNoDB returns connection string without database (for server-level ops).
func (c *Config) ConnStringNoDB() string {
	return c.baseConnStr()
}

func (c *Config) baseConnStr() string {
	encrypt := "true"
	trust := "true"
	if !c.TrustCert {
		trust = "false"
	}
	return fmt.Sprintf(
		"server=%s;port=%s;user id=%s;password=%s;encrypt=%s;trustservercertificate=%s",
		c.DBHost, c.DBPort, c.DBUser, c.DBPassword, encrypt, trust,
	)
}



func getEnv(key, defaultVal string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return defaultVal
}

// findPatchesDir locates the Patches directory.
// Search order: $PATCHES_DIR, /migration/Patches (container), db/Patches (repo).
func findPatchesDir() string {
	if env := os.Getenv("PATCHES_DIR"); env != "" {
		return env
	}

	container := "/migration/Patches"
	if isDir(container) {
		return container
	}

	// Walk up from this binary/source to find repo root
	wd, _ := os.Getwd()
	dir := wd
	for i := 0; i < 10; i++ {
		candidate := filepath.Join(dir, "db", "Patches")
		if isDir(candidate) {
			return candidate
		}
		parent := filepath.Dir(dir)
		if parent == dir {
			break
		}
		dir = parent
	}

	return container
}

func isDir(path string) bool {
	info, err := os.Stat(path)
	return err == nil && info.IsDir()
}
