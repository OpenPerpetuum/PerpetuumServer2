help:
	@echo "Commands to compile, deploy, log services used to run an OpenPerpetuum server (used for local development)"

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

# Stop and delete the containers, also delete the openperpetuum-data volume
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

phonyx: help up start stop down log-asset log-db log-server
