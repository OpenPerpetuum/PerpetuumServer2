help: ## Show this help message
	@grep -E '^[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "%-20s %s\n", $$1, $$2}'

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

.PHONY: help up start stop down delete restart reset clean-cache log-asset log-db log-server test-unit test-integration


