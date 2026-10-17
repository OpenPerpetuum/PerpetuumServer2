package migrations

import (
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strconv"
	"strings"
)

// Patch represents a single migration patch.
type Patch struct {
	Version  string        // e.g. "Live_36", "Pre_Alpha_0"
	Path     string        // Directory containing the patch
	SQLFiles []string      // SQL files to apply, in order
}

// Discover finds all patches in chronological order.
func Discover(patchesDir string) ([]Patch, error) {
	info, err := os.Stat(patchesDir)
	if err != nil || !info.IsDir() {
		return nil, fmt.Errorf("patches directory not found: %s", patchesDir)
	}

	entries, err := os.ReadDir(patchesDir)
	if err != nil {
		return nil, fmt.Errorf("read patches dir: %w", err)
	}

	var preAlpha, live []Patch

	for _, entry := range entries {
		if !entry.IsDir() {
			continue
		}
		name := entry.Name()
		patchPath := filepath.Join(patchesDir, name)

		var patch Patch
		if strings.HasPrefix(name, "Pre_Alpha_") {
			patch = buildPatch(name, patchPath)
			preAlpha = append(preAlpha, patch)
		} else if strings.HasPrefix(name, "Live_") {
			patch = buildPatch(name, patchPath)
			live = append(live, patch)
		}
	}

	sort.Slice(preAlpha, func(i, j int) bool {
		return versionNum(preAlpha[i].Version) < versionNum(preAlpha[j].Version)
	})
	sort.Slice(live, func(i, j int) bool {
		return versionNum(live[i].Version) < versionNum(live[j].Version)
	})

	return append(preAlpha, live...), nil
}

func buildPatch(version, patchPath string) Patch {
	var sqlFiles []string

	// 1. Check for consolidated patch file
	consolidated := findConsolidated(patchPath)
	if consolidated != "" {
		sqlFiles = append(sqlFiles, consolidated)
	} else if isDir(filepath.Join(patchPath, "Raw_SQL")) {
		// 2. Individual SQL files in Raw_SQL
		sqlFiles = listSQLFiles(filepath.Join(patchPath, "Raw_SQL"))
	} else {
		// 3. Top-level SQL files in patch dir
		sqlFiles = listSQLFiles(patchPath)
	}

	return Patch{
		Version:  version,
		Path:     patchPath,
		SQLFiles: sqlFiles,
	}
}

func findConsolidated(patchDir string) string {
	patterns := []string{"live_patch_*.sql", "prealpha_patch_*.sql"}
	for _, pattern := range patterns {
		matches, _ := filepath.Glob(filepath.Join(patchDir, pattern))
		if len(matches) > 0 {
			return matches[0]
		}
	}
	return ""
}

func listSQLFiles(dir string) []string {
	entries, err := os.ReadDir(dir)
	if err != nil {
		return nil
	}

	var files []string
	for _, e := range entries {
		if e.IsDir() || !strings.HasSuffix(e.Name(), ".sql") {
			continue
		}
		files = append(files, filepath.Join(dir, e.Name()))
	}

	sort.Slice(files, func(i, j int) bool {
		return naturalLess(filepath.Base(files[i]), filepath.Base(files[j]))
	})
	return files
}

var numRegex = regexp.MustCompile(`(\d+)`)

func versionNum(version string) int {
	matches := numRegex.FindStringSubmatch(version)
	if len(matches) < 2 {
		return 0
	}
	n, _ := strconv.Atoi(matches[1])
	return n
}

// naturalRegex matches segments of digits or non-digits for natural sorting.
var naturalRegex = regexp.MustCompile(`(\d+|\D+)`)

// naturalLess provides natural sort ordering for filenames.
// e.g. "002_foo.sql" < "010_bar.sql" (numeric comparison of 2 < 10)
func naturalLess(a, b string) bool {
	aParts := naturalRegex.FindAllString(a, -1)
	bParts := naturalRegex.FindAllString(b, -1)

	for i := 0; i < len(aParts) && i < len(bParts); i++ {
		if aParts[i] == bParts[i] {
			continue
		}
		// Try numeric comparison
		aNum, aErr := strconv.Atoi(aParts[i])
		bNum, bErr := strconv.Atoi(bParts[i])
		if aErr == nil && bErr == nil {
			return aNum < bNum
		}
		return strings.ToLower(aParts[i]) < strings.ToLower(bParts[i])
	}
	return len(aParts) < len(bParts)
}

func isDir(path string) bool {
	info, err := os.Stat(path)
	return err == nil && info.IsDir()
}
