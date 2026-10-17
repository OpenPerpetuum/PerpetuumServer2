package cmd

import (
	"context"
	"fmt"

	"github.com/openperpetuum/dbtool/internal/migrations"
	"github.com/spf13/cobra"
)

var verifyFull bool

var verifyCmd = &cobra.Command{
	Use:   "verify",
	Short: "Run integrity checks on the database",
	RunE: func(cmd *cobra.Command, args []string) error {
		ctx := context.Background()

		if err := connectDB(); err != nil {
			return err
		}
		defer rootDB.Close()

		fmt.Printf("Verifying '%s'...\n\n", cfg.DBName)
		errors := 0

		// 1. Migrations table check
		applied, err := migrations.GetAppliedVersions(ctx, rootDB)
		if err != nil {
			fmt.Printf("  \x1b[31m[FAIL]\x1b[0m schema_migrations: %v\n", err)
			errors++
		} else {
			fmt.Printf("  \x1b[32m[OK]\x1b[0m   schema_migrations: %d entries\n", len(applied))
			if len(applied) > 0 {
				fmt.Printf("         latest: %s\n", applied[len(applied)-1])
			}
		}

		// 2. Basic table existence checks
		checks := []struct {
			label string
			query string
		}{
			{"accounts", "SELECT COUNT(*) FROM dbo.accounts"},
			{"robots", "SELECT COUNT(*) FROM dbo.robots"},
			{"structures", "SELECT COUNT(*) FROM dbo.structures"},
		}

		for _, check := range checks {
			val, err := rootDB.QueryOne(ctx, check.query)
			if err != nil {
				fmt.Printf("  \x1b[33m[WARN]\x1b[0m %s: %v\n", check.label, err)
			} else {
				fmt.Printf("  \x1b[32m[OK]\x1b[0m   %s: %s rows\n", check.label, val)
			}
		}

		// 3. Full integrity check (optional)
		if verifyFull {
			fmt.Println("\n  Running DBCC CHECKDB (this may take a while)...")
			if _, err := rootDB.ExecSQL(ctx, "DBCC CHECKDB WITH NO_INFOMSGS"); err != nil {
				fmt.Printf("  \x1b[31m[FAIL]\x1b[0m DBCC CHECKDB: %v\n", err)
				errors++
			} else {
				fmt.Printf("  \x1b[32m[OK]\x1b[0m   DBCC CHECKDB passed\n")
			}
		}

		fmt.Println()
		if errors == 0 {
			fmt.Println("\x1b[32m✓\x1b[0m All checks passed")
		} else {
			fmt.Printf("\x1b[31m✗ %d check(s) failed\x1b[0m\n", errors)
		}
		return nil
	},
}

func init() {
	verifyCmd.Flags().BoolVar(&verifyFull, "full", false, "Include DBCC CHECKDB (slower)")
	rootCmd.AddCommand(verifyCmd)
}
