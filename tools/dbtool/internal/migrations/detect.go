package migrations

import (
	"context"
	"fmt"

	"github.com/openperpetuum/dbtool/internal/db"
)

// Sentinel checks for version detection on legacy databases without schema_migrations.
// Each entry maps a patch version to a SQL query that returns a row if that
// patch (or later) has been applied. We walk backwards from the latest patch
// to find the newest one whose sentinel is present.
//
// Add entries here as you identify distinctive objects each patch creates.
var sentinels = map[string]string{
	// Format: "Live_NN": "SELECT 1 FROM sys.tables WHERE name = '...' AND schema_id = SCHEMA_ID('dbo')"
	//
	// Examples (fill in as you identify sentinel objects):
	// "Live_36": "SELECT 1 FROM sys.tables WHERE name = 'season_announcements' AND schema_id = SCHEMA_ID('dbo')",
	// "Live_35": "SELECT 1 FROM sys.tables WHERE name = 'insurance_policies' AND schema_id = SCHEMA_ID('dbo')",
	// "Live_34": "SELECT 1 FROM sys.views WHERE name = 'vw_equipment_sets' AND schema_id = SCHEMA_ID('dbo')",
}

// DetectVersion attempts to detect the DB version using sentinel checks.
// Returns the version string of the latest detected patch, or "" if detection fails.
//
// Strategy: walk backwards from the latest patch. For each patch with a sentinel,
// check if the sentinel object exists. The first one that passes (walking backwards)
// is the latest applied patch.
func DetectVersion(ctx context.Context, d *db.DB, patches []Patch) string {
	if len(sentinels) == 0 {
		return "" // No sentinels defined yet
	}

	// Walk backwards from latest
	for i := len(patches) - 1; i >= 0; i-- {
		patch := patches[i]
		check, ok := sentinels[patch.Version]
		if !ok {
			continue // No sentinel for this version, skip
		}

		rows, err := d.QuerySQL(ctx, check)
		if err != nil {
			continue // Error checking, assume not present
		}

		if len(rows) == 0 {
			// Sentinel not found - DB is older than this patch
			// Return the previous patch version
			if i > 0 {
				return patches[i-1].Version
			}
			return "" // Older than all patches with sentinels
		}
	}

	// All sentinels passed (or none had sentinels)
	// Return the latest patch that has a passing sentinel
	for i := len(patches) - 1; i >= 0; i-- {
		check, ok := sentinels[patches[i].Version]
		if !ok {
			continue
		}
		rows, err := d.QuerySQL(ctx, check)
		if err == nil && len(rows) > 0 {
			return patches[i].Version
		}
	}

	return ""
}

// SuggestSentinels prints info about patches missing sentinel definitions.
// Useful for developers adding new sentinel checks.
func SuggestSentinels(patches []Patch) {
	missing := 0
	for _, p := range patches {
		if _, ok := sentinels[p.Version]; !ok {
			missing++
		}
	}
	if missing > 0 {
		fmt.Printf("  Note: %d patch(es) have no sentinel defined for legacy detection.\n", missing)
		fmt.Printf("  Add entries to the 'sentinels' map in detect.go for improved fallback.\n")
	}
}
