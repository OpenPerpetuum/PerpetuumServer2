#!/usr/bin/env sh

# Check if the file /data/done exist
# If the file exist, skip migration
# OR override check with FORCE_MIGRATION
# Using ${val+x} to ensure is not defined or empty
ls /data/done
if [ $? -eq 0 ] && [ "$FORCE_MIGRATION" != "true" ]; then
    echo Skipping migration, exiting
    exit 0
fi 

echo "$FORCE_MIGRATION"

set -eux

# Script to run database migration
# - seed the /data using the original PerpetuumServer/data content
# - Set initial state using backup
# - Run migration for each patch

cp -v /work/perpetuum.ini /data/

# Copy PerpetuumServer/data to the shared /data
cp -rv /base-data/layers /data/

runSqlCmd () {
    set +x
    /opt/mssql-tools/bin/sqlcmd -S db -d perpetuumsa -C -U sa -P "${DB_PASSWORD}" -I -i $1
    set -x
}

# applyPatch run SQL file and optionally copy content of a directory to
#  PerpetuumServer/data.
#
# Arguments:
# - 1: Name of the directory of the patch to apply (Ex: Live_99)
# - 2: SQL File name to execute (Ex: some_patch.sql)
# - 3: (optional) Name of the directory containing the "data" folder (Ex: Server)
applyPatch () {
    runSqlCmd "/migration/Patches/$1/$2"

    if [ $# -eq 3 ]; then
        cp -rv "/migration/Patches/$1/$3/data/" /
    fi
}

# preparePatch concatenate all raw sql files into a single patch file.
# save the patch as "$1.sql" in the folder of the patch.
#
# Arguments:
# - 1: Name of the directory of the patch to prepare (Ex: Live_99)
preparePatch () {
    cat /migration/Patches/$1/Raw_SQL/* > /migration/Patches/$1/$1.sql
}

# Create perperuumsa database if it does not exist
echo "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'perpetuumsa') CREATE DATABASE perpetuumsa" > /work/create-database.sql
/opt/mssql-tools/bin/sqlcmd -S db -C -U sa -P "${DB_PASSWORD}" -I -i "/work/create-database.sql"
rm /work/create-database.sql

# echo "CREATE LOGIN sa WITH PASSWORD = '${DB_PASSWORD}'" > /work/create-user.sql
# runSqlCmd

# Restore DB original state
runSqlCmd "/work/restore_DB_to_original_state.sql"

# Apply patches
applyPatch Pre_Alpha_0 prealpha_patch_0.sql
applyPatch Pre_Alpha_1 prealpha_patch_1.sql Server
applyPatch Pre_Alpha_2 prealpha_patch_2.sql Server
applyPatch Pre_Alpha_3 prealpha_patch_3.sql
applyPatch Pre_Alpha_4 prealpha_patch_4.sql Server
applyPatch Pre_Alpha_5 prealpha_patch_5.sql
applyPatch Pre_Alpha_6 prealpha_patch_6.sql
applyPatch Pre_Alpha_7_FInal FIX_robottemplaterelation_pinkarkhe.sql
applyPatch Pre_Alpha_7_FInal NPC_robottemplates_argano_GetsEcms__2018_04_12.sql
applyPatch Live_1 live_patch_1.sql
applyPatch Live_2 live_patch_2.sql
applyPatch Live_3 live_patch_3.sql
applyPatch Live_4 live_patch_4.sql
applyPatch Live_5 live_patch_5.sql
applyPatch Live_6 live_patch_6.sql
applyPatch Live_7 live_patch_7.sql
applyPatch Live_8 live_patch_8.sql
applyPatch Live_9 live_patch_9.sql
applyPatch Live_10 live_patch_10.sql Server
applyPatch Live_11 live_patch_11.sql Server
applyPatch Live_12 live_patch_12.sql
applyPatch Live_13 live_patch_13.sql Server
applyPatch Live_14 live_patch_14.sql
applyPatch Live_15 live_patch_15.sql Server
applyPatch Live_16 live_patch_16.sql Server
applyPatch Live_17 live_patch_17.sql Server
applyPatch Live_18 live_patch_18.sql Server
applyPatch Live_19 live_patch_19.sql Server
applyPatch Live_20 live_patch_20.sql Server
applyPatch Live_21 live_patch_21.sql Server
applyPatch Live_22 live_patch_22.sql Server
applyPatch Live_23 live_patch_23.sql
applyPatch Live_24 live_patch_24.sql Server
applyPatch Live_25 live_patch_25.sql Server
applyPatch Live_26 live_patch_26.sql Server
applyPatch Live_27 live_patch_27.sql Server
applyPatch Live_28 live_patch_28.sql Server
applyPatch Live_29 live_patch_29.sql
applyPatch Live_30 live_patch_30.sql Server
applyPatch Live_31 live_patch_31.sql Server
applyPatch Live_32 live_patch_32.sql Server
applyPatch Live_33 live_patch_33.sql Server
preparePatch Live_34
applyPatch Live_34 Live_34.sql Server
preparePatch Live_35
applyPatch Live_35 Live_35.sql Server


# Add test account (user: test, pass: test)
runSqlCmd "/migration/Tools/TOOL_test_account.sql"

echo Patching complete

touch /data/done