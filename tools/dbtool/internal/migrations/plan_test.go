package migrations

import (
	"strings"
	"testing"
)

func TestPlanIsUpToDate(t *testing.T) {
	plan := &Plan{
		CurrentVersion: "Live_36",
		Pending:        nil,
		TotalAvailable: 44,
	}

	if !plan.IsUpToDate() {
		t.Error("IsUpToDate() = false, want true (no pending)")
	}
}

func TestPlanNotUpToDate(t *testing.T) {
	plan := &Plan{
		CurrentVersion: "Live_30",
		Pending: []Patch{
			{Version: "Live_31", SQLFiles: []string{"a.sql"}},
			{Version: "Live_32", SQLFiles: []string{"b.sql"}},
		},
		TotalAvailable: 44,
	}

	if plan.IsUpToDate() {
		t.Error("IsUpToDate() = true, want false (has pending)")
	}
}

func TestPlanSummaryUpToDate(t *testing.T) {
	plan := &Plan{
		CurrentVersion: "Live_36",
		TotalAvailable: 44,
	}

	summary := plan.Summary()

	if !contains(summary, "UP TO DATE") {
		t.Errorf("Summary() should contain 'UP TO DATE', got:\n%s", summary)
	}
	if !contains(summary, "Live_36") {
		t.Errorf("Summary() should contain 'Live_36', got:\n%s", summary)
	}
	if !contains(summary, "44") {
		t.Errorf("Summary() should contain total '44', got:\n%s", summary)
	}
}

func TestPlanSummaryPending(t *testing.T) {
	plan := &Plan{
		CurrentVersion: "Live_34",
		Pending: []Patch{
			{Version: "Live_35", SQLFiles: []string{"a.sql", "b.sql"}},
			{Version: "Live_36", SQLFiles: []string{"c.sql"}},
		},
		TotalAvailable: 44,
	}

	summary := plan.Summary()

	if !contains(summary, "Pending migrations: 2") {
		t.Errorf("Summary() should show 2 pending, got:\n%s", summary)
	}
	if !contains(summary, "Live_35") {
		t.Errorf("Summary() should list Live_35, got:\n%s", summary)
	}
	if !contains(summary, "Live_36") {
		t.Errorf("Summary() should list Live_36, got:\n%s", summary)
	}
	if contains(summary, "UP TO DATE") {
		t.Error("Summary() should NOT contain 'UP TO DATE' when pending")
	}
}

func TestPlanSummaryNoVersion(t *testing.T) {
	plan := &Plan{
		CurrentVersion: "",
		Pending:        []Patch{{Version: "Pre_Alpha_0"}},
		TotalAvailable: 44,
	}

	summary := plan.Summary()

	if !contains(summary, "none") {
		t.Errorf("Summary() should show '(none...)' for empty version, got:\n%s", summary)
	}
}

func TestSplitBatchesBasic(t *testing.T) {
	sql := "SELECT 1\nGO\nSELECT 2\nGO\nSELECT 3"
	batches := splitBatches(sql)

	if len(batches) != 3 {
		t.Fatalf("expected 3 batches, got %d", len(batches))
	}
	if !strings.Contains(batches[0], "SELECT 1") {
		t.Error("batch 1 should contain 'SELECT 1'")
	}
	if !strings.Contains(batches[1], "SELECT 2") {
		t.Error("batch 2 should contain 'SELECT 2'")
	}
	if !strings.Contains(batches[2], "SELECT 3") {
		t.Error("batch 3 should contain 'SELECT 3'")
	}
}

func TestSplitBatchesGONumbers(t *testing.T) {
	sql := "SELECT 1\nGO 2\nSELECT 2"
	batches := splitBatches(sql)

	if len(batches) != 2 {
		t.Fatalf("expected 2 batches, got %d", len(batches))
	}
}

func TestSplitBatchesGOInBlockComment(t *testing.T) {
	sql := "SELECT 1\n/*\nGO\n*/\nSELECT 2\nGO\nSELECT 3"
	batches := splitBatches(sql)

	// GO inside /* */ should NOT split; only the real GO at the end should
	if len(batches) != 2 {
		t.Fatalf("expected 2 batches, got %d:\n%v", len(batches), batches)
	}
	if !strings.Contains(batches[0], "SELECT 1") || !strings.Contains(batches[0], "SELECT 2") {
		t.Error("batch 1 should contain SELECT 1 and SELECT 2 (GO in comment ignored)")
	}
	if !strings.Contains(batches[1], "SELECT 3") {
		t.Error("batch 2 should contain SELECT 3")
	}
}

func TestSplitBatchesMultiLineBlockComment(t *testing.T) {
	sql := "/*\nDECLARE @x INT\nGO\nSET @x = 1\nGO\n*/\nSELECT 1\nGO\nSELECT 2"
	batches := splitBatches(sql)

	if len(batches) != 2 {
		t.Fatalf("expected 2 batches, got %d", len(batches))
	}
	// First batch should have the full comment + SELECT 1
	if !strings.Contains(batches[0], "DECLARE @x INT") {
		t.Error("batch 1 should contain the comment content")
	}
	if !strings.Contains(batches[0], "SELECT 1") {
		t.Error("batch 1 should contain SELECT 1")
	}
}

func TestSplitBatchesCaseInsensitive(t *testing.T) {
	sql := "SELECT 1\ngo\nSELECT 2\nGo\nSELECT 3"
	batches := splitBatches(sql)

	if len(batches) != 3 {
		t.Fatalf("expected 3 batches, got %d", len(batches))
	}
}

func TestSplitBatchesNoGO(t *testing.T) {
	sql := "SELECT 1\nSELECT 2\nSELECT 3"
	batches := splitBatches(sql)

	if len(batches) != 1 {
		t.Fatalf("expected 1 batch, got %d", len(batches))
	}
}

func TestIsBatchSeparator(t *testing.T) {
	tests := []struct {
		input string
		want  bool
	}{
		{"GO", true},
		{"go", true},
		{"Go", true},
		{"GO 1", true},
		{"GO 3", true},
		{"GO 0", true},
		{"GO abc", false},
		{"GO 1 2", false},
		{"GO -- comment", true},
		{"GO--comment", true},
		{"SELECT GO", false},
		{"", false},
		{"   GO   ", true},
		{"GO\t2", true},
	}
	for _, tt := range tests {
		got := isBatchSeparator(tt.input)
		if got != tt.want {
			t.Errorf("isBatchSeparator(%q) = %v, want %v", tt.input, got, tt.want)
		}
	}
}

func contains(s, substr string) bool {
	return strings.Contains(s, substr)
}
