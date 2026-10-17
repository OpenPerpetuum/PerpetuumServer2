package cmd

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"github.com/openperpetuum/dbtool/internal/backup"
	"github.com/openperpetuum/dbtool/internal/db"
	"github.com/openperpetuum/dbtool/internal/migrations"
	"github.com/spf13/cobra"
)

var (
	backupOutput     string
	backupNoCompress bool
)

var backupCmd = &cobra.Command{
	Use:   "backup",
	Short: "Create a database backup",
	RunE: func(cmd *cobra.Command, args []string) error {
		ctx := context.Background()

		if err := connectDBNoDB(); err != nil {
			return err
		}
		defer rootDB.Close()

		// Query current version to embed in backup metadata
		version := "unknown"
		if dbConn, err := db.Open(cfg.ConnString()); err == nil {
			defer dbConn.Close()
			if v, err := migrations.GetLatestVersion(ctx, dbConn); err == nil && v != "" {
				version = v
			}
		}

		compressed := !backupNoCompress
		description := fmt.Sprintf("version=%s", version)
		bakPath, err := backup.Backup(ctx, rootDB, cfg.DBName, cfg.BackupDir, cfg.BackupHostDir, description, compressed)
		if err != nil {
			return err
		}

		if backupOutput != "" {
			// Rename to specified output path
			outputPath := backupOutput
			if !filepath.IsAbs(outputPath) {
				wd, _ := os.Getwd()
				outputPath = filepath.Join(wd, outputPath)
			}
			if err := os.Rename(bakPath, outputPath); err != nil {
				// Fallback: copy
				if err := copyFile(bakPath, outputPath); err != nil {
					return fmt.Errorf("move backup to %s: %w", outputPath, err)
				}
			}
			bakPath = outputPath
		}

		fmt.Printf("\n\x1b[32m✓\x1b[0m Backup saved to: %s [%s]\n", bakPath, version)
		return nil
	},
}

func init() {
	backupCmd.Flags().StringVarP(&backupOutput, "output", "o", "", "Output file path")
	backupCmd.Flags().BoolVar(&backupNoCompress, "no-compress", false, "Disable compression")
	rootCmd.AddCommand(backupCmd)
}

// restoreCmd
var (
	restoreOverwrite bool
	restoreYes       bool
)

var restoreCmd = &cobra.Command{
	Use:   "restore [FILE]",
	Short: "Restore a database from a .bak file (interactive picker if no FILE given)",
	Args:  cobra.MaximumNArgs(1),
	RunE: func(cmd *cobra.Command, args []string) error {
		ctx := context.Background()

		var backupFile string
		if len(args) == 1 {
			backupFile = args[0]
			// If just a filename (no dir component), look in BackupHostDir
			if !strings.Contains(backupFile, "/") && !fileExists(backupFile) {
				candidate := filepath.Join(cfg.BackupHostDir, backupFile)
				if fileExists(candidate) {
					backupFile = candidate
				}
			}
		} else {
			// Interactive picker (needs DB for metadata)
			if err := connectDBNoDB(); err != nil {
				return err
			}
			defer rootDB.Close()

			infos, err := backup.ListBackups(ctx, rootDB, cfg.BackupDir, cfg.BackupHostDir)
			if err != nil {
				return err
			}
			if len(infos) == 0 {
				fmt.Printf("\nNo backups found in %s\n", cfg.BackupHostDir)
				fmt.Println("Create one with: make db-backup")
				return nil
			}

			fmt.Println()
			fmt.Printf("Available backups in %s:\n", cfg.BackupHostDir)
			for i, info := range infos {
				sizeMB := float64(info.Size) / 1024 / 1024
				version := info.Version
				if version == "" {
					version = "?"
				}
				fmt.Printf("  %2d) %-45s %5.1f MB  [%s]\n", i+1, info.Name, sizeMB, version)
			}
			fmt.Println()
			fmt.Printf("Select backup to restore [1-%d]: ", len(infos))
			var choice int
			fmt.Scanln(&choice)
			if choice < 1 || choice > len(infos) {
				fmt.Println("Invalid selection.")
				return nil
			}
			backupFile = infos[choice-1].Path
		}

		if !restoreYes {
			fmt.Printf("About to restore '%s' -> '%s'\n", filepath.Base(backupFile), cfg.DBName)
			if restoreOverwrite {
				fmt.Println("\x1b[33mWARNING: This will DROP the existing database!\x1b[0m")
			}
			fmt.Print("Proceed? [y/N] ")
			if !confirmInput() {
				fmt.Println("Aborted.")
				return nil
			}
		}

		// Ensure we have a DB connection for the restore
		if rootDB == nil {
			if err := connectDBNoDB(); err != nil {
				return err
			}
			defer rootDB.Close()
		}

		// Convert host path to container path for SQL Server access
		containerPath := toContainerPath(backupFile)

		if err := backup.Restore(ctx, rootDB, containerPath, cfg.DBName, restoreOverwrite); err != nil {
			return err
		}

		// Check for pending migrations after restore
		if err := connectDB(); err == nil {
			plan, err := migrations.ComputePlan(ctx, rootDB, cfg.PatchesDir)
			rootDB.Close()
			if err == nil && !plan.IsUpToDate() {
				fmt.Printf("\n\x1b[33mNOTE:\x1b[0m Database is at an older version. %d migration(s) pending.\n", len(plan.Pending))
				fmt.Println("  Run 'dbtool migrate' to catch up.")
			}
		}

		return nil
	},
}

func init() {
	restoreCmd.Flags().BoolVar(&restoreOverwrite, "overwrite", false, "Drop and recreate target database")
	restoreCmd.Flags().BoolVarP(&restoreYes, "yes", "y", false, "Skip confirmation prompt")
	rootCmd.AddCommand(restoreCmd)
}

// listBackupsCmd
var listBackupsCmd = &cobra.Command{
	Use:   "list-backups",
	Short: "List available backup files",
	RunE: func(cmd *cobra.Command, args []string) error {
		ctx := context.Background()

		if err := connectDBNoDB(); err != nil {
			return err
		}
		defer rootDB.Close()

		infos, err := backup.ListBackups(ctx, rootDB, cfg.BackupDir, cfg.BackupHostDir)
		if err != nil {
			return err
		}

		if len(infos) == 0 {
			fmt.Printf("No backups found in %s\n", cfg.BackupHostDir)
			return nil
		}

		fmt.Printf("Backups in %s:\n\n", cfg.BackupHostDir)
		fmt.Printf("  \x1b[1m%-48s %8s  %-12s  %s\x1b[0m\n", "FILE", "SIZE", "VERSION", "MODIFIED")
		for _, info := range infos {
			sizeMB := float64(info.Size) / 1024 / 1024
			version := info.Version
			if version == "" {
				version = "?"
			}
			fmt.Printf("  %-48s %6.1f MB  %-12s  %s\n", info.Name, sizeMB, version, info.ModTime.Format("2006-01-02 15:04"))
		}
		return nil
	},
}

func init() {
	rootCmd.AddCommand(listBackupsCmd)
}

// toContainerPath converts a host backup path to the container-equivalent path.
// e.g. ./backups/foo.bak -> /backups/foo.bak
func toContainerPath(hostPath string) string {
	abs, err := filepath.Abs(hostPath)
	if err != nil {
		return hostPath
	}
	hostDir, err := filepath.Abs(cfg.BackupHostDir)
	if err != nil {
		return hostPath
	}
	if strings.HasPrefix(abs, hostDir) {
		rel := strings.TrimPrefix(abs, hostDir)
		return cfg.BackupDir + rel
	}
	return hostPath
}

func fileExists(path string) bool {
	info, err := os.Stat(path)
	return err == nil && !info.IsDir()
}

func copyFile(src, dst string) error {
	data, err := os.ReadFile(src)
	if err != nil {
		return err
	}
	return os.WriteFile(dst, data, 0644)
}
