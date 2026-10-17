package migrations

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"

	"github.com/openperpetuum/dbtool/internal/db"
)

const migrationsTableDDL = `
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'schema_migrations' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.schema_migrations (
        version VARCHAR(50) NOT NULL PRIMARY KEY,
        applied_at DATETIME NOT NULL DEFAULT GETDATE(),
        source_file VARCHAR(500) NULL
    );
END
`

// EnsureMigrationsTable creates the schema_migrations table if it doesn't exist.
func EnsureMigrationsTable(ctx context.Context, d *db.DB) error {
	_, err := d.ExecSQL(ctx, migrationsTableDDL)
	return err
}

// GetAppliedVersions returns all applied version strings ordered by application time.
func GetAppliedVersions(ctx context.Context, d *db.DB) ([]string, error) {
	if err := EnsureMigrationsTable(ctx, d); err != nil {
		return nil, err
	}
	rows, err := d.QuerySQL(ctx, "SELECT version FROM dbo.schema_migrations ORDER BY applied_at, version")
	if err != nil {
		return nil, err
	}
	versions := make([]string, 0, len(rows))
	for _, row := range rows {
		versions = append(versions, row[0])
	}
	return versions, nil
}

// GetLatestVersion returns the most recently applied version, or empty string.
func GetLatestVersion(ctx context.Context, d *db.DB) (string, error) {
	versions, err := GetAppliedVersions(ctx, d)
	if err != nil {
		return "", err
	}
	if len(versions) == 0 {
		return "", nil
	}
	return versions[len(versions)-1], nil
}

// RecordVersion inserts a version into the schema_migrations table.
func RecordVersion(ctx context.Context, d *db.DB, version, sourceFile string) error {
	if err := EnsureMigrationsTable(ctx, d); err != nil {
		return err
	}
	sourceExpr := "NULL"
	if sourceFile != "" {
		sourceExpr = fmt.Sprintf("'%s'", sourceFile)
	}
	query := fmt.Sprintf(
		"IF NOT EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE version = '%s') INSERT INTO dbo.schema_migrations (version, source_file) VALUES ('%s', %s)",
		version, version, sourceExpr,
	)
	_, err := d.ExecSQL(ctx, query)
	return err
}

// Plan represents the result of comparing applied vs available migrations.
type Plan struct {
	CurrentVersion string
	Pending        []Patch
	TotalAvailable int
}

// IsUpToDate returns true if no pending migrations.
func (p *Plan) IsUpToDate() bool {
	return len(p.Pending) == 0
}

// Summary returns a human-readable plan summary.
func (p *Plan) Summary() string {
	cur := p.CurrentVersion
	if cur == "" {
		cur = "(none - no version tracking yet)"
	}

	lines := []string{
		fmt.Sprintf("Current version: %s", cur),
		fmt.Sprintf("Total patches available: %d", p.TotalAvailable),
	}

	if p.IsUpToDate() {
		lines = append(lines, "Status: UP TO DATE")
	} else {
		lines = append(lines, fmt.Sprintf("Pending migrations: %d", len(p.Pending)))
		for _, patch := range p.Pending {
			lines = append(lines, fmt.Sprintf("  - %s (%d SQL file(s))", patch.Version, len(patch.SQLFiles)))
		}
	}
	return "\n  " + joinLines(lines)
}

// ComputePlan compares applied versions against available patches.
func ComputePlan(ctx context.Context, d *db.DB, patchesDir string) (*Plan, error) {
	patches, err := Discover(patchesDir)
	if err != nil {
		return nil, err
	}

	appliedSet, err := getAppliedSet(ctx, d)
	if err != nil {
		return nil, err
	}

	// If no versions recorded, try heuristic detection
	if len(appliedSet) == 0 {
		detected := DetectVersion(ctx, d, patches)
		if detected != "" {
			for _, p := range patches {
				if versionNum(p.Version) <= versionNum(detected) {
					appliedSet[p.Version] = true
				}
			}
		}
	}

	var pending []Patch
	for _, p := range patches {
		if !appliedSet[p.Version] {
			pending = append(pending, p)
		}
	}

	current, _ := GetLatestVersion(ctx, d)

	return &Plan{
		CurrentVersion: current,
		Pending:        pending,
		TotalAvailable: len(patches),
	}, nil
}

func getAppliedSet(ctx context.Context, d *db.DB) (map[string]bool, error) {
	versions, err := GetAppliedVersions(ctx, d)
	if err != nil {
		return nil, err
	}
	set := make(map[string]bool, len(versions))
	for _, v := range versions {
		set[v] = true
	}
	return set, nil
}

// ApplyOptions configures migration application behavior.
type ApplyOptions struct {
	DryRun  bool
	Strict  bool // If true, any batch error is fatal. If false (default), errors are logged and execution continues.
}

// ApplyPending applies all pending migrations and records them.
func ApplyPending(ctx context.Context, d *db.DB, plan *Plan, opts ApplyOptions) ([]string, error) {
	var applied []string
	var totalErrors int

	for _, patch := range plan.Pending {
		if opts.DryRun {
			fmt.Printf("  [DRY RUN] Would apply: %s (%d files)\n", patch.Version, len(patch.SQLFiles))
			applied = append(applied, patch.Version)
			continue
		}

		fmt.Printf("  Applying: %s\n", patch.Version)

		if len(patch.SQLFiles) == 0 {
			fmt.Printf("    (no SQL files, recording only)\n")
			if err := RecordVersion(ctx, d, patch.Version, ""); err != nil {
				return applied, fmt.Errorf("record %s: %w", patch.Version, err)
			}
			applied = append(applied, patch.Version)
			continue
		}

		for _, sqlFile := range patch.SQLFiles {
			fmt.Printf("    -> %s\n", filepath.Base(sqlFile))
			err := executeSQLFile(ctx, d, sqlFile, opts.Strict)
			if err != nil {
				if opts.Strict {
					return applied, fmt.Errorf("apply %s (%s): %w", patch.Version, filepath.Base(sqlFile), err)
				}
				// Lenient mode: log error and continue
				totalErrors++
				fmt.Printf("    \x1b[33m⚠\x1b[0m %s: %v\n", filepath.Base(sqlFile), err)
			}
		}

		if err := RecordVersion(ctx, d, patch.Version, filepath.Base(patch.Path)); err != nil {
			return applied, fmt.Errorf("record %s: %w", patch.Version, err)
		}
		applied = append(applied, patch.Version)
		fmt.Printf("    \x1b[32m\x1b[1m✓\x1b[0m %s complete\n", patch.Version)
	}

	if totalErrors > 0 {
		fmt.Printf("\n\x1b[33mWarning: %d batch(es) had errors (lenient mode). Use --strict to fail on errors.\x1b[0m\n", totalErrors)
	}

	return applied, nil
}

func executeSQLFile(ctx context.Context, d *db.DB, path string, strict bool) error {
	content, err := os.ReadFile(path)
	if err != nil {
		return fmt.Errorf("read %s: %w", path, err)
	}

	sqlText := decodeSQLFile(content)
	batches := splitBatches(sqlText)

	// Execute with a longer timeout for large scripts
	fileCtx, cancel := context.WithTimeout(ctx, 10*time.Minute)
	defer cancel()

	if strict {
		return d.ExecBatches(fileCtx, batches)
	}
	return d.ExecBatchesLenient(fileCtx, batches)
}

// decodeSQLFile handles UTF-16 BOM-prefixed files and strips UTF-8 BOM.
// Some migration files are saved as UTF-16LE (common with Windows SQL Server tools).
func decodeSQLFile(content []byte) string {
	// UTF-16LE BOM
	if len(content) >= 2 && content[0] == 0xFF && content[1] == 0xFE {
		return utf16LEToUTF8(content[2:])
	}
	// UTF-16BE BOM
	if len(content) >= 2 && content[0] == 0xFE && content[1] == 0xFF {
		return utf16BEToUTF8(content[2:])
	}
	// UTF-8 BOM
	if len(content) >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF {
		return string(content[3:])
	}
	return string(content)
}

func utf16LEToUTF8(data []byte) string {
	var sb strings.Builder
	for i := 0; i+1 < len(data); i += 2 {
		r := rune(uint16(data[i]) | uint16(data[i+1])<<8)
		sb.WriteRune(r)
	}
	return sb.String()
}

func utf16BEToUTF8(data []byte) string {
	var sb strings.Builder
	for i := 0; i+1 < len(data); i += 2 {
		r := rune(uint16(data[i])<<8 | uint16(data[i+1]))
		sb.WriteRune(r)
	}
	return sb.String()
}

// splitBatches splits SQL text into batches on GO separators.
// GO is a sqlcmd client-side command, not T-SQL. It must appear on its
// own line (optionally with leading/trailing whitespace and a repeat count).
// GO lines inside /* ... */ block comments are ignored.
func splitBatches(sql string) []string {
	var batches []string
	var current strings.Builder
	inBlockComment := false

	for _, line := range strings.Split(sql, "\n") {
		trimmed := strings.TrimSpace(line)

		// Track block comment state
		if inBlockComment {
			current.WriteString(line)
			current.WriteString("\n")
			if strings.Contains(trimmed, "*/") {
				inBlockComment = false
			}
			continue
		}
		if strings.HasPrefix(trimmed, "/*") {
			inBlockComment = !strings.Contains(trimmed, "*/")
			current.WriteString(line)
			current.WriteString("\n")
			continue
		}

		if isBatchSeparator(trimmed) {
			batches = append(batches, current.String())
			current.Reset()
		} else {
			current.WriteString(line)
			current.WriteString("\n")
		}
	}
	batches = append(batches, current.String())
	return batches
}

// isBatchSeparator returns true if the line is a GO batch separator.
// Matches: GO, GO 1, GO 3, go, Go, etc. (case insensitive)
func isBatchSeparator(line string) bool {
	line = strings.TrimSpace(line)
	if line == "" {
		return false
	}
	upper := strings.ToUpper(line)

	// Strip trailing -- comments (GO -- some comment is still a batch separator)
	if idx := strings.Index(upper, "--"); idx != -1 {
		upper = strings.TrimSpace(upper[:idx])
	}

	if upper == "GO" {
		return true
	}
	if strings.HasPrefix(upper, "GO ") || strings.HasPrefix(upper, "GO\t") {
		// GO <number>
		parts := strings.Fields(upper)
		if len(parts) == 2 {
			if _, err := strconv.Atoi(parts[1]); err == nil {
				return true
			}
		}
	}
	return false
}

func joinLines(lines []string) string {
	result := ""
	for i, l := range lines {
		if i > 0 {
			result += "\n"
		}
		result += l
	}
	return result
}
