package migrations

import (
	"os"
	"path/filepath"
	"testing"
)

// findRepoPatchesDir walks up from the test file to find db/Patches.
func findRepoPatchesDir(t *testing.T) string {
	t.Helper()
	wd, _ := os.Getwd()
	dir := wd
	for i := 0; i < 10; i++ {
		candidate := filepath.Join(dir, "db", "Patches")
		if isDir(candidate) {
			return candidate
		}
		parent := filepath.Dir(dir)
		if parent == dir {
			break
		}
		dir = parent
	}
	t.Skip("could not find db/Patches directory from test location")
	return ""
}

func TestDiscoverPatchesFromRepo(t *testing.T) {
	patchesDir := findRepoPatchesDir(t)

	patches, err := Discover(patchesDir)
	if err != nil {
		t.Fatalf("Discover() error: %v", err)
	}

	// We expect 44 patches (8 Pre_Alpha + 36 Live)
	if len(patches) != 44 {
		t.Errorf("len(patches) = %d, want 44", len(patches))
	}

	// First should be Pre_Alpha_0
	if patches[0].Version != "Pre_Alpha_0" {
		t.Errorf("patches[0].Version = %q, want %q", patches[0].Version, "Pre_Alpha_0")
	}

	// Last should be Live_36
	last := patches[len(patches)-1]
	if last.Version != "Live_36" {
		t.Errorf("patches[last].Version = %q, want %q", last.Version, "Live_36")
	}

	// All patches should have at least one SQL file
	for _, p := range patches {
		if len(p.SQLFiles) == 0 {
			t.Errorf("patch %s has no SQL files", p.Version)
		}
	}
}

func TestDiscoverPatchesOrdering(t *testing.T) {
	patchesDir := findRepoPatchesDir(t)

	patches, err := Discover(patchesDir)
	if err != nil {
		t.Fatalf("Discover() error: %v", err)
	}

	// Verify Pre_Alpha patches come before Live patches
	sawLive := false
	for _, p := range patches {
		if len(p.Version) > 4 && p.Version[:5] == "Live_" {
			sawLive = true
		} else if sawLive {
			t.Errorf("patch %s appears after a Live patch but is Pre_Alpha", p.Version)
		}
	}

	// Verify numeric ordering within Live patches
	var liveNums []int
	for _, p := range patches {
		if len(p.Version) > 4 && p.Version[:5] == "Live_" {
			n := versionNum(p.Version)
			if n > 0 {
				if len(liveNums) > 0 && n <= liveNums[len(liveNums)-1] {
					t.Errorf("Live patches not in ascending order: %d after %d", n, liveNums[len(liveNums)-1])
				}
				liveNums = append(liveNums, n)
			}
		}
	}
}

func TestDiscoverNonexistentDir(t *testing.T) {
	_, err := Discover("/nonexistent/patches/dir")
	if err == nil {
		t.Error("Discover() should error for nonexistent directory")
	}
}

func TestVersionNum(t *testing.T) {
	tests := []struct {
		input string
		want  int
	}{
		{"Pre_Alpha_0", 0},
		{"Pre_Alpha_7_FInal", 7},
		{"Live_1", 1},
		{"Live_10", 10},
		{"Live_36", 36},
		{"Live_123", 123},
	}

	for _, tc := range tests {
		got := versionNum(tc.input)
		if got != tc.want {
			t.Errorf("versionNum(%q) = %d, want %d", tc.input, got, tc.want)
		}
	}
}

func TestNaturalLess(t *testing.T) {
	tests := []struct {
		a, b string
		want bool
	}{
		{"001_foo.sql", "002_bar.sql", true},
		{"002_bar.sql", "001_foo.sql", false},
		{"010_ten.sql", "002_two.sql", false},  // 10 > 2
		{"002_two.sql", "010_ten.sql", true},   // 2 < 10
		{"1_first.sql", "2_second.sql", true},
		{"abc.sql", "abd.sql", true},
		{"z.sql", "a.sql", false},
	}

	for _, tc := range tests {
		got := naturalLess(tc.a, tc.b)
		if got != tc.want {
			t.Errorf("naturalLess(%q, %q) = %v, want %v", tc.a, tc.b, got, tc.want)
		}
	}
}

func TestBuildPatchConsolidated(t *testing.T) {
	// Create a temp dir with a consolidated patch file
	dir := t.TempDir()
	patchDir := filepath.Join(dir, "Live_99")
	os.MkdirAll(patchDir, 0755)
	os.WriteFile(filepath.Join(patchDir, "live_patch_99.sql"), []byte("SELECT 1"), 0644)

	patch := buildPatch("Live_99", patchDir)

	if len(patch.SQLFiles) != 1 {
		t.Fatalf("len(SQLFiles) = %d, want 1", len(patch.SQLFiles))
	}
	if filepath.Base(patch.SQLFiles[0]) != "live_patch_99.sql" {
		t.Errorf("SQLFiles[0] = %q, want live_patch_99.sql", patch.SQLFiles[0])
	}
}

func TestBuildPatchRawSQL(t *testing.T) {
	// Create a temp dir with Raw_SQL containing multiple files
	dir := t.TempDir()
	patchDir := filepath.Join(dir, "Live_100")
	rawDir := filepath.Join(patchDir, "Raw_SQL")
	os.MkdirAll(rawDir, 0755)

	os.WriteFile(filepath.Join(rawDir, "001_first.sql"), []byte("SELECT 1"), 0644)
	os.WriteFile(filepath.Join(rawDir, "002_second.sql"), []byte("SELECT 2"), 0644)
	os.WriteFile(filepath.Join(rawDir, "010_tenth.sql"), []byte("SELECT 10"), 0644)
	os.WriteFile(filepath.Join(rawDir, "readme.txt"), []byte("ignore me"), 0644)

	patch := buildPatch("Live_100", patchDir)

	if len(patch.SQLFiles) != 3 {
		t.Fatalf("len(SQLFiles) = %d, want 3 (only .sql files)", len(patch.SQLFiles))
	}
	// Verify natural ordering
	if filepath.Base(patch.SQLFiles[0]) != "001_first.sql" {
		t.Errorf("first = %q, want 001_first.sql", patch.SQLFiles[0])
	}
	if filepath.Base(patch.SQLFiles[1]) != "002_second.sql" {
		t.Errorf("second = %q, want 002_second.sql", patch.SQLFiles[1])
	}
	if filepath.Base(patch.SQLFiles[2]) != "010_tenth.sql" {
		t.Errorf("third = %q, want 010_tenth.sql", patch.SQLFiles[2])
	}
}

func TestBuildPatchTopLevel(t *testing.T) {
	// Create a temp dir with top-level SQL files (no Raw_SQL, no consolidated)
	dir := t.TempDir()
	patchDir := filepath.Join(dir, "Pre_Alpha_99")
	os.MkdirAll(patchDir, 0755)

	os.WriteFile(filepath.Join(patchDir, "001_init.sql"), []byte("SELECT 1"), 0644)
	os.WriteFile(filepath.Join(patchDir, "002_data.sql"), []byte("SELECT 2"), 0644)

	patch := buildPatch("Pre_Alpha_99", patchDir)

	if len(patch.SQLFiles) != 2 {
		t.Fatalf("len(SQLFiles) = %d, want 2", len(patch.SQLFiles))
	}
}

func TestFindConsolidated(t *testing.T) {
	dir := t.TempDir()

	// No consolidated file
	if result := findConsolidated(dir); result != "" {
		t.Errorf("findConsolidated() = %q, want empty", result)
	}

	// Add a prealpha_patch file
	os.WriteFile(filepath.Join(dir, "prealpha_patch_1.sql"), []byte("SELECT 1"), 0644)
	result := findConsolidated(dir)
	if filepath.Base(result) != "prealpha_patch_1.sql" {
		t.Errorf("findConsolidated() = %q, want prealpha_patch_1.sql", result)
	}
}
