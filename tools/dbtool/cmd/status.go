package cmd

import (
	"context"
	"fmt"

	"github.com/openperpetuum/dbtool/internal/migrations"
	"github.com/spf13/cobra"
)

var statusVerbose bool

var statusCmd = &cobra.Command{
	Use:   "status",
	Short: "Show current DB version and pending migrations",
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

		fmt.Println(plan.Summary())
		fmt.Printf("\n  Patches directory: %s\n", cfg.PatchesDir)

		if statusVerbose {
			patches, err := migrations.Discover(cfg.PatchesDir)
			if err != nil {
				return err
			}

			applied, _ := migrations.GetAppliedVersions(ctx, rootDB)
			appliedSet := make(map[string]bool)
			for _, v := range applied {
				appliedSet[v] = true
			}

			fmt.Printf("\n  \x1b[1m%-25s %-10s %s\x1b[0m\n", "VERSION", "STATUS", "SQL FILES")
			fmt.Println("  " + repeatChar("-", 55))
			for _, p := range patches {
				status := "\x1b[32mapplied\x1b[0m"
				if !appliedSet[p.Version] {
					status = "\x1b[33mpending\x1b[0m"
				}
				fmt.Printf("  %-25s %-15s %d\n", p.Version, status, len(p.SQLFiles))
			}
		}

		return nil
	},
}

func init() {
	statusCmd.Flags().BoolVarP(&statusVerbose, "verbose", "v", false, "Show all patches with status")
	rootCmd.AddCommand(statusCmd)
}

func repeatChar(c string, n int) string {
	result := ""
	for i := 0; i < n; i++ {
		result += c
	}
	return result
}
