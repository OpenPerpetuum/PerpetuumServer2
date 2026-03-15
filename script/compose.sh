#!/usr/bin/env sh
set -eux

# Default values
FILE=compose.yml
ENV=.env.local

docker compose -f $FILE --env-file $ENV "$@"
