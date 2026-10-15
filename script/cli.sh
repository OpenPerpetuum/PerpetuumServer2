#!/usr/bin/env bash
# Runs the Perpetuum Server CLI client inside the .NET SDK container
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

DOCKER_FLAGS="-i"
if [ -t 0 ] && [ -t 1 ]; then
    DOCKER_FLAGS="-it"
fi

CLI_DLL="/repo/src/Perpetuum.CliClient/bin/x64/Release/net8.0/Perpetuum.CliClient.dll"

# Build if DLL does not exist
if [ ! -f "${REPO_ROOT}/src/Perpetuum.CliClient/bin/x64/Release/net8.0/Perpetuum.CliClient.dll" ]; then
    echo "Building Perpetuum CLI client..."
    docker run --rm -v "${REPO_ROOT}:/repo" -w /repo mcr.microsoft.com/dotnet/sdk:8.0 \
        dotnet build src/Perpetuum.CliClient/Perpetuum.CliClient.csproj -c Release -p:Platform=x64
fi

docker run ${DOCKER_FLAGS} --rm \
    --network host \
    -v "${REPO_ROOT}:/repo" \
    -w /repo \
    mcr.microsoft.com/dotnet/sdk:8.0 \
    dotnet "${CLI_DLL}" "$@"
