#!/usr/bin/env sh

# Check if /data/done exists (skips on simple container restarts)
if [ -f /data/done ] && [ "$FORCE_MIGRATION" != "true" ]; then
    echo "Skipping migration, /data/done already exists."
    exit 0
fi 

set -eux

CACHE_BAK="/base-data/database/perpetuumsa_migrated.bak"
CACHE_HASH_FILE="/base-data/database/perpetuumsa_migrated.hash"

# 1. Sync server data and layer files to shared /data
echo "==> Syncing base server data and layers..."
cp -r /perpetuum-service-data/* /data/
cp -v /work/perpetuum.ini /data/
cp -r /base-data/layers /data/
[ -d /custom-layers ] && cp -r /custom-layers/* /data/layers/

# Copy all patch-specific data/layers
for d in /migration/Patches/*/Server/data; do
    [ -d "$d" ] && cp -r "$d"/* /data/
done

runSqlCmd () {
    set +x
    sqlcmd -S db -d perpetuumsa -C -U sa -P "${DB_PASSWORD}" -I -i "$1"
    set -x
}

# 2. Compute SHA-256 hash of all migration sources
compute_migration_hash() {
    (
        find /migration -type f \( -name "*.sql" -o -name "*.bin" \) -exec sha256sum {} + | sort
        [ -f /base-data/database/perpetuumsa.bak ] && sha256sum /base-data/database/perpetuumsa.bak
        [ -f /work/restore_DB_to_original_state.sql ] && sha256sum /work/restore_DB_to_original_state.sql
        [ -f /work/migration.sh ] && sha256sum /work/migration.sh
    ) | sha256sum | awk '{print $1}'
}

CURRENT_HASH=$(compute_migration_hash)
CACHED_HASH=""
[ -f "$CACHE_HASH_FILE" ] && CACHED_HASH=$(cat "$CACHE_HASH_FILE")

USE_CACHE=false
if [ "$FORCE_MIGRATION" != "true" ] && [ -f "$CACHE_BAK" ] && [ "$CURRENT_HASH" = "$CACHED_HASH" ]; then
    USE_CACHE=true
fi

# 3. Restore from cache OR run automated full migration
if [ "$USE_CACHE" = "true" ]; then
    echo "==> Migration cache HIT (hash matches). Restoring snapshot..."
    set +x
    sqlcmd -S db -C -U sa -P "${DB_PASSWORD}" -b -I -i "/work/restore_migrated_DB.sql"
    set -x
    echo "==> Restored migrated DB snapshot in seconds."
else
    echo "==> Migration cache MISS or FORCED. Running full migration from scratch..."
    
    # Create perpetuumsa database if it does not exist
    echo "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'perpetuumsa') CREATE DATABASE perpetuumsa" > /work/create-database.sql
    sqlcmd -S db -C -U sa -P "${DB_PASSWORD}" -I -i "/work/create-database.sql"
    rm /work/create-database.sql

    # Restore base vanilla state
    set +x
    sqlcmd -S db -C -U sa -P "${DB_PASSWORD}" -b -I -i "/work/restore_DB_to_original_state.sql"
    set -x

    # Discover and apply all patches dynamically in chronological version order
    PATCH_DIRS=$(ls -1d /migration/Patches/Pre_Alpha_* 2>/dev/null | sort -V; ls -1d /migration/Patches/Live_* 2>/dev/null | sort -V)

    for patch_dir in $PATCH_DIRS; do
        [ -d "$patch_dir" ] || continue
        patch_name=$(basename "$patch_dir")
        echo "==> Applying patch: $patch_name"

        # Check for consolidated patch file
        consolidated=$(find "$patch_dir" -maxdepth 1 -type f \( -name "live_patch_*.sql" -o -name "prealpha_patch_*.sql" \) | head -n 1)

        if [ -n "$consolidated" ]; then
            runSqlCmd "$consolidated"
        elif [ -d "$patch_dir/Raw_SQL" ]; then
            # Run all SQL scripts in Raw_SQL in numerical order
            find "$patch_dir/Raw_SQL" -maxdepth 1 -type f -name "*.sql" | sort -V | while read -r sql_file; do
                runSqlCmd "$sql_file"
            done
        else
            # Run any top-level SQL scripts in the patch folder in order
            find "$patch_dir" -maxdepth 1 -type f -name "*.sql" | sort -V | while read -r sql_file; do
                runSqlCmd "$sql_file"
            done
        fi
    done

    # Add test account (user: test, pass: test)
    runSqlCmd "/migration/Tools/TOOL_test_account.sql"

    echo "==> Creating compressed migration database snapshot..."
    set +x
    sqlcmd -S db -C -U sa -P "${DB_PASSWORD}" -b -I -Q "BACKUP DATABASE perpetuumsa TO DISK = '/data/perpetuumsa_migrated.bak' WITH FORMAT, INIT, COMPRESSION"
    set -x

    # Save hash and set permissions
    echo "$CURRENT_HASH" > "$CACHE_HASH_FILE"
    chmod 666 "$CACHE_BAK" "$CACHE_HASH_FILE" 2>/dev/null || true
    echo "==> Migration snapshot cache saved."
fi

touch /data/done
echo "Patching complete."
