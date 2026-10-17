package cmd

import (
	"fmt"
	"os"

	"github.com/openperpetuum/dbtool/internal/config"
	"github.com/openperpetuum/dbtool/internal/db"
	"github.com/spf13/cobra"
)

var cfg *config.Config
var rootDB *db.DB

var rootCmd = &cobra.Command{
	Use:   "dbtool",
	Short: "Database migration, backup, and restore tool for OpenPerpetuum",
	Long: `dbtool is a CLI tool for managing the OpenPerpetuum game database.

It handles version-tracked migrations, database backups/restores,
and integrity verification.

Environment variables:
  DB_HOST        SQL Server hostname (default: "db")
  DB_PORT        SQL Server port (default: "1433")
  DB_USER        Login user (default: "sa")
  DB_PASSWORD    Login password
  DB_NAME        Database name (default: "perpetuumsa")
  PATCHES_DIR    Path to Patches directory
  BACKUP_DIR     Backup file directory (default: "/data")`,
	SilenceUsage:  true,
	SilenceErrors: true,
	PersistentPreRunE: func(cmd *cobra.Command, args []string) error {
		cfg = config.Load()
		return nil
	},
}

// Execute runs the root command.
func Execute() {
	if err := rootCmd.Execute(); err != nil {
		fmt.Fprintf(os.Stderr, "\n\x1b[31mERROR:\x1b[0m %v\n", err)
		os.Exit(1)
	}
}

// connectDB opens a database connection and stores it in rootDB.
func connectDB() error {
	var err error
	rootDB, err = db.Open(cfg.ConnString())
	if err != nil {
		return err
	}
	return nil
}

// connectDBNoDB opens a server-level connection (no specific database).
func connectDBNoDB() error {
	var err error
	rootDB, err = db.OpenNoDB(cfg.ConnStringNoDB())
	if err != nil {
		return err
	}
	return nil
}

func init() {
	rootCmd.CompletionOptions.DisableDefaultCmd = true
}
