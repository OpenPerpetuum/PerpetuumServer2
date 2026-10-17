package backup

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"

	"github.com/openperpetuum/dbtool/internal/db"
)

// Backup creates a compressed backup of the database.
// containerPath is the path SQL Server writes to (e.g. /backups in the container).
// hostPath is the corresponding path on the host (e.g. ./backups) for file verification.
// description is embedded in the backup metadata (e.g. "version=Live_25").
// Returns the host-side path to the created .bak file.
func Backup(ctx context.Context, d *db.DB, dbName, containerPath, hostPath, description string, compressed bool) (string, error) {
	if !d.DatabaseExists(ctx, dbName) {
		return "", fmt.Errorf("database '%s' does not exist", dbName)
	}

	ts := time.Now().Format("20060102_150405")
	bakName := fmt.Sprintf("%s_%s.bak", dbName, ts)
	containerBakPath := filepath.Join(containerPath, bakName)
	hostBakPath := filepath.Join(hostPath, bakName)

	options := "WITH FORMAT, INIT"
	if compressed {
		options += ", COMPRESSION"
	}
	if description != "" {
		options += fmt.Sprintf(", DESCRIPTION = '%s'", description)
	}

	sql := fmt.Sprintf("BACKUP DATABASE [%s] TO DISK = '%s' %s", dbName, containerBakPath, options)
	fmt.Printf("  Backing up '%s'...\n", dbName)

	if _, err := d.ExecSQL(ctx, sql); err != nil {
		return "", fmt.Errorf("backup: %w", err)
	}

	// Verify file exists on the host side (via bind mount)
	size := fileSize(hostBakPath)
	if size == 0 {
		return "", fmt.Errorf("backup file not found at %s (is the container running with the backup volume mounted?)", hostBakPath)
	}

	fmt.Printf("  Backup complete: %s (%.1f MB)\n", hostBakPath, float64(size)/1024/1024)
	return hostBakPath, nil
}

// Restore restores a database from a .bak file.
// The backupPath must be accessible from the SQL Server container.
// Callers are responsible for verifying the file exists on the host side.
func Restore(ctx context.Context, d *db.DB, backupPath, targetDB string, overwrite bool) error {
	if overwrite {
		if d.DatabaseExists(ctx, targetDB) {
			fmt.Printf("  Dropping existing database '%s'...\n", targetDB)
			_, _ = d.ExecSQL(ctx, fmt.Sprintf("ALTER DATABASE [%s] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", targetDB))
			_, _ = d.ExecSQL(ctx, fmt.Sprintf("DROP DATABASE [%s]", targetDB))
		}
		if err := d.EnsureDatabase(ctx, targetDB); err != nil {
			return fmt.Errorf("create database: %w", err)
		}
	} else {
		if !d.DatabaseExists(ctx, targetDB) {
			if err := d.EnsureDatabase(ctx, targetDB); err != nil {
				return fmt.Errorf("create database: %w", err)
			}
		} else {
			_, _ = d.ExecSQL(ctx, fmt.Sprintf("ALTER DATABASE [%s] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", targetDB))
		}
	}

	fmt.Printf("  Restoring '%s' -> [%s]...\n", filepath.Base(backupPath), targetDB)

	restoreSQL := fmt.Sprintf(
		"RESTORE DATABASE [%s] FROM DISK = '%s' WITH REPLACE, STATS = 10",
		targetDB, backupPath,
	)

	// Restore can take a while - use extended timeout
	restoreCtx, cancel := context.WithTimeout(ctx, 30*time.Minute)
	defer cancel()

	if _, err := d.ExecSQL(restoreCtx, restoreSQL); err != nil {
		return fmt.Errorf("restore: %w", err)
	}

	_, _ = d.ExecSQL(ctx, fmt.Sprintf("ALTER DATABASE [%s] SET MULTI_USER", targetDB))
	fmt.Printf("  Restore complete.\n")
	return nil
}

// ListBackups returns .bak files in the host backup directory with metadata
// read from the backup files at runtime via RESTORE HEADERONLY.
func ListBackups(ctx context.Context, d *db.DB, containerDir, hostDir string) ([]BackupInfo, error) {
	infos, err := ListBackupFiles(hostDir)
	if err != nil {
		return nil, err
	}

	// Enrich with version metadata from backup files
	for i := range infos {
		containerPath := filepath.Join(containerDir, infos[i].Name)
		if meta, err := ReadBackupMetadata(ctx, d, containerPath); err == nil && meta != "" {
			infos[i].Version = meta
		}
	}

	return infos, nil
}

// ListBackupFiles returns .bak files in the directory (no DB connection needed).
func ListBackupFiles(hostDir string) ([]BackupInfo, error) {
	entries, err := os.ReadDir(hostDir)
	if err != nil {
		if os.IsNotExist(err) {
			return nil, nil
		}
		return nil, err
	}

	var infos []BackupInfo
	for _, e := range entries {
		if e.IsDir() || filepath.Ext(e.Name()) != ".bak" {
			continue
		}
		hostPath := filepath.Join(hostDir, e.Name())
		fi, err := os.Stat(hostPath)
		if err != nil {
			continue
		}

		infos = append(infos, BackupInfo{
			Path:    hostPath,
			Name:    e.Name(),
			Size:    fi.Size(),
			ModTime: fi.ModTime(),
		})
	}

	sort.Slice(infos, func(i, j int) bool {
		return infos[i].ModTime.After(infos[j].ModTime)
	})
	return infos, nil
}

// ReadBackupMetadata reads the DESCRIPTION field from a backup file using
// RESTORE HEADERONLY. Returns the version string (e.g. "Live_25") or "" if
// no version is embedded.
func ReadBackupMetadata(ctx context.Context, d *db.DB, containerPath string) (string, error) {
	// RESTORE HEADERONLY returns a wide result set; we only need the description column.
	// The description column is at position 13 (0-indexed) in SQL Server 2016+.
	// We use a simpler approach: RESTORE HEADERONLY and look for "version=" prefix.
	rows, err := d.QuerySQL(ctx, fmt.Sprintf("RESTORE HEADERONLY FROM DISK = '%s'", containerPath))
	if err != nil {
		return "", err
	}
	if len(rows) == 0 || len(rows[0]) == 0 {
		return "", nil
	}

	// The description is in the 13th column (index 13) for SQL Server 2016+
	// but varies by version. Search all columns for "version=" prefix.
	row := rows[0]
	for _, col := range row {
		if strings.HasPrefix(col, "version=") {
			return strings.TrimPrefix(col, "version="), nil
		}
	}
	return "", nil
}

// BackupInfo holds metadata about a backup file.
type BackupInfo struct {
	Path    string
	Name    string
	Size    int64
	ModTime time.Time
	Version string // e.g. "Live_25" (read from backup metadata at runtime)
}

func fileSize(path string) int64 {
	fi, err := os.Stat(path)
	if err != nil {
		return 0
	}
	return fi.Size()
}
