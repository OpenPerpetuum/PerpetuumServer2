help: ## Show this help message
	@grep -E '^[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "%-20s %s\n", $$1, $$2}'

status: ## Show current stack status
	./script/compose.sh ps
	@echo
	@./script/compose.sh exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$${DB_PASSWORD:-$(shell grep '^DB_PASSWORD=' .env.local 2>/dev/null | cut -d= -f2 | tr -d '"')}" -C -Q "SELECT 'DB OK' AS status, CONVERT(VARCHAR, GETDATE(), 120) AS server_time" -W -s ' | ' -h -1 2>/dev/null || echo '  DB: unreachable'

up: ## Create and start the containers
	./script/compose.sh up -d --build --remove-orphans --wait

start: ## Start the containers
	./script/compose.sh start

stop: ## Stop the containers
	./script/compose.sh stop

down: ## Stop and delete the containers
	./script/compose.sh down

delete: ## Stop and delete the containers, also delete the volumes (openperpetuum-data, openperpetuum-db)
	./script/compose.sh down -v

restart: down up ## Stop, delete and start the containers

reset: ## Stop, delete and start the containers with forced migration
	FORCE_MIGRATION=true $(MAKE) restart

clean-cache: ## Delete migration snapshot cache to force full re-migration on next start
	rm -f ./perpetuum-data/database/perpetuumsa_migrated.bak ./perpetuum-data/database/perpetuumsa_migrated.hash 2>/dev/null || true

log-asset: ## Follow asset logs
	./script/compose.sh logs asset -f

log-db: ## Follow db logs
	./script/compose.sh logs db -f

log-server: ## Follow server logs
	./script/compose.sh logs server -f

test-unit: ## Run the unit test tier (2) in the test container, no database required
	./script/compose.sh --profile test run --no-deps --build --rm test dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --no-build

test-integration: ## Run the integration test tier (3) in the test container, against the live database, bringing up db + migration first (migration is idempotent and exits when already done)
	./script/compose.sh up -d db --wait
	./script/compose.sh up migration
	./script/compose.sh --profile test run --build --rm test dotnet test src/Perpetuum.Tests.Integration/Perpetuum.Tests.Integration.csproj -c Release -p:Platform=x64 --no-build

test-smoke: ## Run the end-to-end smoke test (tier 1) against the docker server stack
	./script/smoke-test.sh

check-assets: ## Verify presence and integrity of assets listed in manifest
	python3 ./script/check_assets.py --check-sizes

# --- dbtool (database migration/backup tool) ---

DBTOOL_BIN := tools/dbtool/dbtool
DBTOOL_DIR := tools/dbtool
BACKUP_HOST_DIR := ./backups
BACKUP_DIR := /backups  # Container path (mounted at ./backups on host)
# Source .env.local and export vars (prepended to db-* recipes)
# Defaults for host-side execution (container uses DB_HOST=db, DB_PORT=1433)
ENV := if [ -f .env.local ]; then set -a; . ./.env.local; set +a; fi; export DB_HOST=$${DB_HOST:-localhost} DB_PORT=$${DB_PORT:-1433} DB_USER=$${DB_USER:-sa} DB_NAME=$${DB_NAME:-perpetuumsa} BACKUP_DIR=$(BACKUP_DIR) BACKUP_HOST_DIR=$(BACKUP_HOST_DIR);

# File-based target: only rebuilds if binary is missing or source changed
$(DBTOOL_BIN): docker/Dockerfile.dbtool tools/dbtool/go.mod
	docker build -q -f docker/Dockerfile.dbtool -t perpetuumserver2-dbtool:build .
	-docker rm dbtool-build 2>/dev/null
	docker create --name dbtool-build perpetuumserver2-dbtool:build
	docker cp dbtool-build:/usr/local/bin/dbtool $(DBTOOL_BIN)
	docker rm dbtool-build

build-dbtool: $(DBTOOL_BIN) ## Build the dbtool binary (auto-skips if up to date)

build-dbtool-local: ## Force local rebuild with Go (no Docker)
	rm -f $(DBTOOL_BIN)
	cd tools/dbtool && go build -ldflags="-s -w" -o $(DBTOOL_BIN) .

test-dbtool: ## Run dbtool unit tests (requires Go on host)
	cd tools/dbtool && go test ./... -v -count=1

test-dbtool-integration: ## Run dbtool integration tests against live DB (requires Go + running DB)
	cd $(DBTOOL_DIR) && $(ENV) DBTOOL_INTEGRATION=1 go test ./... -v -count=1 -run TestIntegration

db-status: $(DBTOOL_BIN) ## Show current DB version and pending migrations
	$(ENV) $(DBTOOL_BIN) status -v

db-migrate: $(DBTOOL_BIN) ## Apply pending migrations (usage: make db-migrate [TO=Live_25] [DRY_RUN=1] [YES=1])
	$(ENV) $(DBTOOL_BIN) migrate $(if $(TO),--to $(TO),) $(if $(DRY_RUN),--dry-run,) $(if $(YES),-y,)

db-migrate-dry: ## Preview migrations without applying (shorthand for db-migrate DRY_RUN=1)
	$(MAKE) db-migrate DRY_RUN=1 $(if $(TO),TO=$(TO),)

db-backup: $(DBTOOL_BIN) ## Create a database backup
	$(ENV) $(DBTOOL_BIN) backup

db-restore: $(DBTOOL_BIN) ## Restore a database from backup (interactive picker or FILE=path.bak)
	$(ENV) $(DBTOOL_BIN) restore $(if $(FILE),$(FILE),) $(if $(OVERWRITE),--overwrite,) $(if $(FILE),-y,)

db-list: $(DBTOOL_BIN) ## List available database backups
	$(ENV) $(DBTOOL_BIN) list-backups

db-verify: $(DBTOOL_BIN) ## Run database integrity checks
	$(ENV) $(DBTOOL_BIN) verify $(if $(FULL),--full,)

db-shell: ## Open a sqlcmd shell connected to the database
	$(ENV) sqlcmd -S $${DB_HOST}:$${DB_PORT} -U $${DB_USER} -P $${DB_PASSWORD} -C -d $${DB_NAME}

db-partial: $(DBTOOL_BIN) ## Reset DB and migrate only up to a specific patch (usage: make db-partial TO=Live_25)
	@test -n "$(TO)" || (echo "Usage: make db-partial TO=Live_25" && exit 1)
	@set -a; . ./.env.local 2>/dev/null; set +a; \
	 echo ""; printf "\033[1mReset DB and migrate to $(TO) only\033[0m\n"; echo ""; \
	 echo "  Tearing down..."; \
	 ./script/compose.sh down -v 2>/dev/null; \
	 echo "  Starting database..."; \
	 ./script/compose.sh up -d db --wait 2>/dev/null; \
	 echo "  Restoring base backup..."; \
	 ./script/compose.sh exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P "$${DB_PASSWORD}" -b -Q "IF DB_ID('perpetuumsa') IS NOT NULL ALTER DATABASE perpetuumsa SET SINGLE_USER WITH ROLLBACK IMMEDIATE" 2>/dev/null; \
	 ./script/compose.sh exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P "$${DB_PASSWORD}" -b -Q "IF DB_ID('perpetuumsa') IS NOT NULL DROP DATABASE perpetuumsa" 2>/dev/null; \
	 ./script/compose.sh exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P "$${DB_PASSWORD}" -b -Q "CREATE DATABASE perpetuumsa" 2>/dev/null; \
	 ./script/compose.sh exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P "$${DB_PASSWORD}" -b -Q "RESTORE DATABASE perpetuumsa FROM DISK = '/data/perpetuumsa.bak' WITH MOVE 'perpetuumsa' TO '/var/opt/mssql/data/psa.mdf', MOVE 'perpetuumsa_log' TO '/var/opt/mssql/data/psa_log.ldf', REPLACE" > /dev/null 2>&1 || { echo "  \033[31mERROR: Base backup restore failed\033[0m"; exit 1; }; \
	 echo "  Base restored. Applying patches..."; \
	 echo ""; \
	 export DB_HOST=$${DB_HOST:-localhost} DB_PORT=$${DB_PORT:-1433} DB_USER=$${DB_USER:-sa} DB_NAME=$${DB_NAME:-perpetuumsa} BACKUP_DIR=$(BACKUP_DIR) BACKUP_HOST_DIR=$(BACKUP_HOST_DIR); \
	 $(DBTOOL_BIN) migrate --to $(TO) -y; \
	 echo ""; \
	 printf "\033[32m✓\033[0m DB migrated to $(TO).\n"; \
	 echo "  Run 'make db-migrate' to catch up to latest."; \
	 echo "  Then 'make up' to start the full stack."

.PHONY: help status up start stop down delete restart reset clean-cache log-asset log-db log-server test-smoke test-unit test-integration check-assets extract-models build-dbtool test-dbtool test-dbtool-integration db-status db-migrate db-backup db-restore db-list db-verify db-shell db-partial db-migrate-dry


