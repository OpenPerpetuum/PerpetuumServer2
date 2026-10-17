package backup

import (
	"os"
	"path/filepath"
	"testing"
	"time"
)

func TestListBackupFilesEmptyDir(t *testing.T) {
	dir := t.TempDir()
	infos, err := ListBackupFiles(dir)
	if err != nil {
		t.Fatalf("ListBackupFiles() error: %v", err)
	}
	if len(infos) != 0 {
		t.Errorf("len(infos) = %d, want 0", len(infos))
	}
}

func TestListBackupFilesNonexistentDir(t *testing.T) {
	infos, err := ListBackupFiles("/nonexistent/backup/dir")
	if err != nil {
		t.Fatalf("ListBackupFiles() error for nonexistent dir: %v", err)
	}
	if infos != nil {
		t.Errorf("infos = %v, want nil", infos)
	}
}

func TestListBackupFilesFindsFiles(t *testing.T) {
	dir := t.TempDir()

	// Create fake backup files with distinct mtimes
	oldPath := filepath.Join(dir, "perpetuumsa_20260101_120000.bak")
	newPath := filepath.Join(dir, "perpetuumsa_20260102_120000.bak")
	os.WriteFile(oldPath, []byte("fake"), 0644)
	os.WriteFile(newPath, []byte("fake2"), 0644)
	os.WriteFile(filepath.Join(dir, "notabackup.txt"), []byte("ignore"), 0644)
	os.Mkdir(filepath.Join(dir, "subdir.bak"), 0755) // directory with .bak ext should be skipped

	// Set distinct mtimes
	oldTime := time.Now().Add(-24 * time.Hour)
	newTime := time.Now()
	os.Chtimes(oldPath, oldTime, oldTime)
	os.Chtimes(newPath, newTime, newTime)

	infos, err := ListBackupFiles(dir)
	if err != nil {
		t.Fatalf("ListBackupFiles() error: %v", err)
	}

	if len(infos) != 2 {
		t.Fatalf("len(infos) = %d, want 2 (only .bak files, not dirs)", len(infos))
	}

	// Should be sorted newest first
	if infos[0].Name != "perpetuumsa_20260102_120000.bak" {
		t.Errorf("infos[0].Name = %q, want the newer one", infos[0].Name)
	}
}

func TestListBackupFilesSortOrder(t *testing.T) {
	dir := t.TempDir()

	// Create files with different mtimes
	path1 := filepath.Join(dir, "old.bak")
	path2 := filepath.Join(dir, "new.bak")
	os.WriteFile(path1, []byte("old"), 0644)
	os.WriteFile(path2, []byte("new"), 0644)

	// Set different modification times
	oldTime := time.Now().Add(-24 * time.Hour)
	newTime := time.Now()
	os.Chtimes(path1, oldTime, oldTime)
	os.Chtimes(path2, newTime, newTime)

	infos, _ := ListBackupFiles(dir)
	if len(infos) != 2 {
		t.Fatalf("len(infos) = %d, want 2", len(infos))
	}

	if infos[0].Name != "new.bak" {
		t.Errorf("infos[0].Name = %q, want new.bak (newest first)", infos[0].Name)
	}
	if infos[1].Name != "old.bak" {
		t.Errorf("infos[1].Name = %q, want old.bak", infos[1].Name)
	}
}

func TestBackupInfoFields(t *testing.T) {
	dir := t.TempDir()
	content := []byte("test backup content")
	path := filepath.Join(dir, "test.bak")
	os.WriteFile(path, content, 0644)

	infos, _ := ListBackupFiles(dir)
	if len(infos) != 1 {
		t.Fatalf("len(infos) = %d, want 1", len(infos))
	}

	info := infos[0]
	if info.Name != "test.bak" {
		t.Errorf("Name = %q, want test.bak", info.Name)
	}
	if info.Size != int64(len(content)) {
		t.Errorf("Size = %d, want %d", info.Size, len(content))
	}
	if info.Path != path {
		t.Errorf("Path = %q, want %q", info.Path, path)
	}
}

func TestFileSize(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "test.txt")
	os.WriteFile(path, []byte("12345"), 0644)

	size := fileSize(path)
	if size != 5 {
		t.Errorf("fileSize() = %d, want 5", size)
	}
}

func TestFileSizeNonexistent(t *testing.T) {
	size := fileSize("/nonexistent/file.txt")
	if size != 0 {
		t.Errorf("fileSize() for nonexistent = %d, want 0", size)
	}
}
