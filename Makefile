help:
	@echo "Commands to compile, deploy, log services used to run an OpenPerpetuum server, and run the test tiers (used for local development)"

# Create and start the containers
up:
	./script/compose.sh up -d --build --remove-orphans --wait

# Start the containers
start:
	./script/compose.sh start

# Stop the containers
stop:
	./script/compose.sh stop

# Stop and delete the containers
down:
	./script/compose.sh down

# Stop and delete the containers, also delete the volumes (openperpetuum-data, openperpetuum-db)
delete:
	./script/compose.sh down -v

# Stop, delete and start the containers
restart: down up

log-asset:
	./script/compose.sh logs asset -f

log-db:
	./script/compose.sh logs db -f

log-server:
	./script/compose.sh logs server -f

# Run the unit test tier (2) in the test container, no database required
test-unit:
	./script/compose.sh --profile test run --build --rm test dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --no-build

# Run the integration test tier (3) in the test container, against the live database,
# bringing up db + migration first (migration is idempotent and exits when already done)
test-integration:
	./script/compose.sh up -d db --wait
	./script/compose.sh up migration
	./script/compose.sh --profile test run --build --rm test dotnet test src/Perpetuum.Tests.Integration/Perpetuum.Tests.Integration.csproj -c Release -p:Platform=x64 --no-build

PHONY: help up start stop down log-asset log-db log-server test-unit test-integration
