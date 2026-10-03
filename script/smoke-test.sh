#!/usr/bin/env bash
# End-to-end smoke test for Perpetuum Server in Docker
# Builds and starts the server stack, asserts on startup logs,
# shuts down gracefully, and asserts on shutdown logs and exit codes.
#
# Exit codes:
#   0  pass
#   2  build failed or container failed to start
#   4  timed out waiting for the server to come online
#   5  a forbidden pattern was found in the log
#   6  the server did not shut down gracefully or reported non-zero exit
#   7  unexpected error

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

TIMEOUT="${TIMEOUT:-180}"
SETTLE_SECONDS="${SETTLE_SECONDS:-15}"
SHUTDOWN_TIMEOUT="${SHUTDOWN_TIMEOUT:-120}"

COMPOSE_FILE="${REPO_ROOT}/compose.yml"
ENV_FILE="${REPO_ROOT}/.env.local"

# Helper to run compose commands
compose() {
    docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" "$@"
}

REQUIRED_ONLINE="State : \[Online\]"
REQUIRED_OFFLINE="State : \[Off\]"

FORBIDDEN_PATTERNS=(
    "Unhandled exception"
    "System\.InvalidOperationException"
    "System\.NullReferenceException"
    "The current TransactionScope is already complete"
    "nesting level exceeded"
)

write_section() {
    echo ""
    echo "== $1"
}

cd "${REPO_ROOT}"

# --- Phase 0: Environment -------------------------------------------------
write_section "Environment"
if [ ! -f "${ENV_FILE}" ]; then
    echo "Environment file ${ENV_FILE} not found." >&2
    exit 7
fi
echo "Compose File     : ${COMPOSE_FILE}"
echo "Env File         : ${ENV_FILE}"
echo "Online Timeout   : ${TIMEOUT}s"
echo "Settle Soak      : ${SETTLE_SECONDS}s"
echo "Shutdown Timeout : ${SHUTDOWN_TIMEOUT}s"

# --- Phase 1: Launch ------------------------------------------------------
write_section "Launch"
echo "Starting stack (db, migration, server)..."
compose stop server 2>/dev/null || true
compose rm -f server 2>/dev/null || true
if ! compose up -d --build --force-recreate server; then
    echo "Failed to start server stack." >&2
    exit 2
fi

# --- Phase 2: Wait for [Online] -------------------------------------------
write_section "Waiting for [Online]"
start_time=$(date +%s)
online=0

while true; do
    current_time=$(date +%s)
    elapsed=$((current_time - start_time))

    if [ "$elapsed" -ge "$TIMEOUT" ]; then
        break
    fi

    if compose logs server 2>&1 | grep -qE "$REQUIRED_ONLINE"; then
        online=1
        break
    fi

    current_container=$(compose ps -a -q server 2>/dev/null || true)
    if [ -n "$current_container" ]; then
        status=$(docker inspect -f '{{.State.Status}}' "$current_container" 2>/dev/null || echo "unknown")
        if [ "$status" = "exited" ] || [ "$status" = "dead" ]; then
            echo "Server container exited unexpectedly with status: $status" >&2
            break
        fi
    fi

    sleep 1
done

elapsed_online=$(( $(date +%s) - start_time ))

if [ "$online" -ne 1 ]; then
    echo "Server did not reach [Online] within ${TIMEOUT} seconds." >&2
    echo "=== Server Logs ==="
    compose logs server || true
    exit 4
fi

echo "Online after ${elapsed_online} seconds."

# --- Phase 3: Wait for startup to settle ----------------------------------
write_section "Waiting for startup to settle"
echo "Server is [Online]. Soaking for ${SETTLE_SECONDS}s to allow initial zone flock spawning to complete..."
sleep "$SETTLE_SECONDS"
echo "Startup settle completed after ${SETTLE_SECONDS}s. Proceeding to assertions and shutdown."

# --- Phase 4: Assert on the startup log -----------------------------------
write_section "Startup assertions"
startup_log=$(compose logs server 2>&1)
violations=()

for pattern in "${FORBIDDEN_PATTERNS[@]}"; do
    if echo "$startup_log" | grep -qE "$pattern"; then
        violations+=("$pattern")
    fi
done

spawn_count=$(echo "$startup_log" | grep -c "member spawned to zone" || true)
flock_count=$(echo "$startup_log" | grep -c "NPCs created" || true)

echo "Reported values (not asserted):"
printf "  %-18s: %s\n" "members spawned" "$spawn_count"
printf "  %-18s: %s\n" "flock batches" "$flock_count"
printf "  %-18s: %ss\n" "time to online" "$elapsed_online"
printf "  %-18s: %ss\n" "settle duration" "$SETTLE_SECONDS"

# --- Phase 5: Graceful shutdown -------------------------------------------
write_section "Shutdown"
shutdown_start_time=$(date +%s)

echo "Stopping server container (sending SIGTERM, timeout ${SHUTDOWN_TIMEOUT}s)..."
if ! compose stop -t "$SHUTDOWN_TIMEOUT" server; then
    echo "Failed to stop server container." >&2
    exit 6
fi

shutdown_elapsed=$(( $(date +%s) - shutdown_start_time ))
echo "Shutdown took ${shutdown_elapsed}s."

final_log=$(compose logs server 2>&1)
if ! echo "$final_log" | grep -qE "$REQUIRED_OFFLINE"; then
    echo "Server exited but never reported [Off]." >&2
    exit 6
fi

server_container=$(compose ps -a -q server 2>/dev/null || true)
if [ -n "$server_container" ]; then
    exit_code=$(docker inspect -f '{{.State.ExitCode}}' "$server_container" 2>/dev/null || echo "1")
    if [ "$exit_code" -ne 0 ]; then
        echo "Server container exit code was $exit_code, expected 0." >&2
        exit 6
    fi
fi

echo "Graceful shutdown confirmed."

# --- Phase 6: Verdict -----------------------------------------------------
write_section "Verdict"
if [ ${#violations[@]} -gt 0 ]; then
    echo "Forbidden patterns found in the log:" >&2
    for v in "${violations[@]}"; do
        echo "  $v" >&2
    done
    exit 5
fi

echo "SMOKE TEST PASSED"
exit 0
