package cmd

import (
	"bufio"
	"context"
	"fmt"
	"os"
	"strings"

	"github.com/openperpetuum/dbtool/internal/migrations"
	"github.com/spf13/cobra"
)

var (
	migrateDryRun bool
	migrateYes    bool
	migrateTo     string
	migrateStrict bool
)

var migrateCmd = &cobra.Command{
	Use:   "migrate",
	Short: "Apply pending migrations (catch up to latest)",
	Long: `Computes which migrations are pending and applies them in order.
Each applied migration is recorded in dbo.schema_migrations.

Use --to to stop at a specific version (e.g., --to Live_25) to test
incremental migration later.`,
	RunE: func(cmd *cobra.Command, args []string) error {
		ctx := context.Background()

		if err := connectDB(); err != nil {
			return err
		}
		defer rootDB.Close()

		plan, err := migrations.ComputePlan(ctx, rootDB, cfg.PatchesDir)
		if err != nil {
			return err
		}

		// Filter pending to stop at target version
		if migrateTo != "" {
			plan.Pending = filterPendingToVersion(plan.Pending, migrateTo)
			if len(plan.Pending) == 0 {
				fmt.Printf("\x1b[32m✓\x1b[0m Database is already at or past %s. Nothing to do.\n", migrateTo)
				return nil
			}
		}

		if plan.IsUpToDate() {
			fmt.Println("\x1b[32m✓\x1b[0m Database is already up to date. Nothing to do.")
			return nil
		}

		target := "latest"
		if migrateTo != "" {
			target = migrateTo
		}
		fmt.Printf("Found \x1b[1m%d\x1b[0m pending migration(s) [target: %s]:\n\n", len(plan.Pending), target)
		for _, p := range plan.Pending {
			fmt.Printf("  • %s (%d file(s))\n", p.Version, len(p.SQLFiles))
		}

		if migrateDryRun {
			fmt.Println("\n\x1b[33m[DRY RUN]\x1b[0m No changes made.")
			return nil
		}

		if !migrateYes {
			fmt.Print("\nProceed with migration? [y/N] ")
			if !confirmInput() {
				fmt.Println("Aborted.")
				return nil
			}
		}

		fmt.Println()
		applied, err := migrations.ApplyPending(ctx, rootDB, plan, migrations.ApplyOptions{
			Strict: migrateStrict,
		})
		if err != nil {
			return err
		}

		newVersion, _ := migrations.GetLatestVersion(ctx, rootDB)
		fmt.Printf("\n\x1b[32m✓\x1b[0m Migration complete. Applied %d patch(es).\n", len(applied))
		if newVersion != "" {
			fmt.Printf("  Current version: %s\n", newVersion)
		}
		return nil
	},
}

// filterPendingToVersion returns only patches up to and including the target version.
func filterPendingToVersion(pending []migrations.Patch, target string) []migrations.Patch {
	var result []migrations.Patch
	for _, p := range pending {
		result = append(result, p)
		if p.Version == target {
			break
		}
	}
	return result
}

func init() {
	migrateCmd.Flags().BoolVar(&migrateDryRun, "dry-run", false, "Show what would be done without applying")
	migrateCmd.Flags().BoolVarP(&migrateYes, "yes", "y", false, "Skip confirmation prompt")
	migrateCmd.Flags().StringVar(&migrateTo, "to", "", "Stop at this version (e.g., Live_25)")
	migrateCmd.Flags().BoolVar(&migrateStrict, "strict", false, "Fail on any batch error (default: lenient, matches sqlcmd)")
	rootCmd.AddCommand(migrateCmd)
}

func confirmInput() bool {
	reader := bufio.NewReader(os.Stdin)
	line, _ := reader.ReadString('\n')
	return strings.TrimSpace(strings.ToLower(line)) == "y" || strings.TrimSpace(strings.ToLower(line)) == "yes"
}
