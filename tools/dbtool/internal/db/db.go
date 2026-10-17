package db

import (
	"context"
	"database/sql"
	"fmt"
	"strings"
	"time"

	_ "github.com/microsoft/go-mssqldb"
)

// DB wraps a sql.DB connection.
type DB struct {
	*sql.DB
	connStr string
}

// Open creates a new DB connection.
func Open(connStr string) (*DB, error) {
	db, err := sql.Open("sqlserver", connStr)
	if err != nil {
		return nil, fmt.Errorf("open connection: %w", err)
	}

	// Test the connection with a timeout
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	if err := db.PingContext(ctx); err != nil {
		db.Close()
		return nil, fmt.Errorf("ping database: %w", err)
	}

	return &DB{DB: db, connStr: connStr}, nil
}

// OpenNoDB opens a connection without specifying a database (server-level).
func OpenNoDB(connStr string) (*DB, error) {
	db, err := sql.Open("sqlserver", connStr)
	if err != nil {
		return nil, fmt.Errorf("open connection: %w", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	if err := db.PingContext(ctx); err != nil {
		db.Close()
		return nil, fmt.Errorf("ping server: %w", err)
	}

	return &DB{DB: db, connStr: connStr}, nil
}

// ExecSQL executes a SQL statement and returns rows affected.
func (d *DB) ExecSQL(ctx context.Context, query string) (int64, error) {
	result, err := d.ExecContext(ctx, query)
	if err != nil {
		return 0, fmt.Errorf("exec: %w", err)
	}
	rows, _ := result.RowsAffected()
	return rows, nil
}

// QuerySQL executes a query and returns all rows as a slice of string slices.
func (d *DB) QuerySQL(ctx context.Context, query string) ([][]string, error) {
	rows, err := d.QueryContext(ctx, query)
	if err != nil {
		return nil, fmt.Errorf("query: %w", err)
	}
	defer rows.Close()

	var results [][]string
	columns, _ := rows.Columns()
	_ = columns // column names available if needed

	for rows.Next() {
		values := make([]interface{}, len(columns))
		ptrs := make([]interface{}, len(columns))
		for i := range values {
			ptrs[i] = &values[i]
		}
		if err := rows.Scan(ptrs...); err != nil {
			return nil, fmt.Errorf("scan row: %w", err)
		}
		row := make([]string, len(values))
		for i, v := range values {
			if v == nil {
				row[i] = ""
			} else {
				row[i] = fmt.Sprintf("%v", v)
			}
		}
		results = append(results, row)
	}

	return results, rows.Err()
}

// QueryOne executes a query expected to return a single value.
func (d *DB) QueryOne(ctx context.Context, query string) (string, error) {
	var result string
	err := d.QueryRowContext(ctx, query).Scan(&result)
	if err == sql.ErrNoRows {
		return "", nil
	}
	return result, err
}

// DatabaseExists checks if a database exists on the server.
func (d *DB) DatabaseExists(ctx context.Context, name string) bool {
	var count int
	err := d.QueryRowContext(ctx, "SELECT COUNT(*) FROM sys.databases WHERE name = '" + name + "'").Scan(&count)
	return err == nil && count > 0
}

// EnsureDatabase creates the database if it doesn't exist.
func (d *DB) EnsureDatabase(ctx context.Context, name string) error {
	if d.DatabaseExists(ctx, name) {
		return nil
	}
	_, err := d.ExecSQL(ctx, fmt.Sprintf("CREATE DATABASE [%s]", name))
	return err
}

// ExecBatches executes multiple SQL batches on a single pinned connection.
// This is required when transactions span across GO batch separators.
func (d *DB) ExecBatches(ctx context.Context, batches []string) error {
	conn, err := d.Conn(ctx)
	if err != nil {
		return fmt.Errorf("get connection: %w", err)
	}
	defer conn.Close()

	for i, batch := range batches {
		batch = strings.TrimSpace(batch)
		if batch == "" {
			continue
		}
		if _, err := conn.ExecContext(ctx, batch); err != nil {
			return fmt.Errorf("batch %d/%d: %w", i+1, len(batches), err)
		}
	}
	return nil
}

// ExecBatchesLenient executes batches on a single connection but does NOT
// stop on first error. It returns the first error encountered (for reporting)
// but continues executing remaining batches. This matches sqlcmd behavior
// without the -b flag.
func (d *DB) ExecBatchesLenient(ctx context.Context, batches []string) error {
	conn, err := d.Conn(ctx)
	if err != nil {
		return fmt.Errorf("get connection: %w", err)
	}
	defer conn.Close()

	var firstErr error
	for i, batch := range batches {
		batch = strings.TrimSpace(batch)
		if batch == "" {
			continue
		}
		if _, err := conn.ExecContext(ctx, batch); err != nil {
			if firstErr == nil {
				firstErr = fmt.Errorf("batch %d/%d: %w", i+1, len(batches), err)
			}
		}
	}
	return firstErr
}
