package migrations

import (
	"context"
	"os"
	"testing"

	"github.com/openperpetuum/dbtool/internal/config"
	"github.com/openperpetuum/dbtool/internal/db"
)

// integrationSetup returns a *db.DB connected to the live test database.
// Skips the test if DBTOOL_INTEGRATION is not set to "1".
func integrationSetup(t *testing.T) (*db.DB, *config.Config) {
	t.Helper()
	if os.Getenv("DBTOOL_INTEGRATION") != "1" {
		t.Skip("skipping integration test (set DBTOOL_INTEGRATION=1 to run)")
	}

	cfg := config.Load()
	d, err := db.Open(cfg.ConnString())
	if err != nil {
		t.Fatalf("connect: %v", err)
	}
	t.Cleanup(func() { d.Close() })
	return d, cfg
}

func TestIntegration_ComputePlan(t *testing.T) {
	d, cfg := integrationSetup(t)
	ctx := context.Background()

	plan, err := ComputePlan(ctx, d, cfg.PatchesDir)
	if err != nil {
		t.Fatalf("ComputePlan: %v", err)
	}

	if plan.TotalAvailable == 0 {
		t.Fatal("expected TotalAvailable > 0")
	}
	t.Logf("TotalAvailable=%d, CurrentVersion=%s, Pending=%d",
		plan.TotalAvailable, plan.CurrentVersion, len(plan.Pending))

	// If up to date, pending should be 0
	if plan.IsUpToDate() && len(plan.Pending) != 0 {
		t.Error("IsUpToDate()=true but Pending is not empty")
	}
}

func TestIntegration_GetAppliedVersions(t *testing.T) {
	d, _ := integrationSetup(t)
	ctx := context.Background()

	versions, err := GetAppliedVersions(ctx, d)
	if err != nil {
		t.Fatalf("GetAppliedVersions: %v", err)
	}

	if len(versions) == 0 {
		t.Skip("no versions recorded (fresh DB, run migrations first)")
	}

	// Versions should be sorted
	for i := 1; i < len(versions); i++ {
		if versions[i] < versions[i-1] {
			t.Errorf("versions not sorted: %s before %s", versions[i], versions[i-1])
		}
	}
	t.Logf("Found %d applied versions, latest: %s", len(versions), versions[len(versions)-1])
}

func TestIntegration_GetLatestVersion(t *testing.T) {
	d, _ := integrationSetup(t)
	ctx := context.Background()

	latest, err := GetLatestVersion(ctx, d)
	if err != nil {
		t.Fatalf("GetLatestVersion: %v", err)
	}

	if latest == "" {
		t.Skip("no version recorded yet")
	}
	t.Logf("Latest version: %s", latest)
}

func TestIntegration_RecordVersion_Idempotent(t *testing.T) {
	d, _ := integrationSetup(t)
	ctx := context.Background()

	// Use a test version that won't conflict with real patches
	testVersion := "__test_integration__"

	// Clean up from previous runs
	d.ExecSQL(ctx, "DELETE FROM dbo.schema_migrations WHERE version = '" + testVersion + "'")

	// First insert
	if err := RecordVersion(ctx, d, testVersion, "test"); err != nil {
		t.Fatalf("first RecordVersion: %v", err)
	}

	// Second insert should be a no-op (IF NOT EXISTS)
	if err := RecordVersion(ctx, d, testVersion, "test"); err != nil {
		t.Fatalf("second RecordVersion (idempotent): %v", err)
	}

	// Verify only one entry
	count, err := d.QueryOne(ctx, "SELECT COUNT(*) FROM dbo.schema_migrations WHERE version = '" + testVersion + "'")
	if err != nil {
		t.Fatalf("count query: %v", err)
	}
	if count != "1" {
		t.Errorf("expected 1 entry, got %s", count)
	}

	// Cleanup
	d.ExecSQL(ctx, "DELETE FROM dbo.schema_migrations WHERE version = '" + testVersion + "'")
}

func TestIntegration_SplitBatches_RealFiles(t *testing.T) {
	_, cfg := integrationSetup(t)
	_ = cfg

	// Test splitBatches on real patch files to ensure no panics
	patches, err := Discover(cfg.PatchesDir)
	if err != nil {
		t.Fatalf("Discover: %v", err)
	}

	tested := 0
	for _, patch := range patches {
		for _, sqlFile := range patch.SQLFiles {
			content, err := os.ReadFile(sqlFile)
			if err != nil {
				t.Logf("skip %s: %v", sqlFile, err)
				continue
			}
			sqlText := decodeSQLFile(content)
			batches := splitBatches(sqlText)
			if len(batches) == 0 {
				t.Errorf("%s: splitBatches returned 0 batches", sqlFile)
			}
			tested++
		}
	}
	if tested == 0 {
		t.Skip("no SQL files found to test")
	}
	t.Logf("Successfully split batches in %d files", tested)
}

func TestIntegration_EnsureMigrationsTable(t *testing.T) {
	d, _ := integrationSetup(t)
	ctx := context.Background()

	// Should be a no-op if table exists
	if err := EnsureMigrationsTable(ctx, d); err != nil {
		t.Fatalf("EnsureMigrationsTable: %v", err)
	}

	// Verify table structure
	rows, err := d.QuerySQL(ctx, `
		SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
		WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'schema_migrations' 
		ORDER BY ORDINAL_POSITION`)
	if err != nil {
		t.Fatalf("query columns: %v", err)
	}

	cols := make([]string, 0, len(rows))
	for _, row := range rows {
		cols = append(cols, row[0])
	}

	for _, expected := range []string{"version", "applied_at", "source_file"} {
		found := false
		for _, c := range cols {
			if c == expected {
				found = true
				break
			}
		}
		if !found {
			t.Errorf("schema_migrations missing column: %s (has: %v)", expected, cols)
		}
	}
}
