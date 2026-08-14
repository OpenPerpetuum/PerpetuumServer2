#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET 8 SDK is required: https://dotnet.microsoft.com/download/dotnet/8.0" >&2
  exit 1
fi

if [[ $# -gt 2 ]]; then
  echo "Usage: scripts/publish-admin-tool.sh [runtime] [output-directory]" >&2
  echo "Example: scripts/publish-admin-tool.sh linux-x64" >&2
  exit 2
fi

if [[ $# -ge 1 ]]; then
  runtime="$1"
else
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) runtime="linux-x64" ;;
    Linux-aarch64|Linux-arm64) runtime="linux-arm64" ;;
    Darwin-x86_64) runtime="osx-x64" ;;
    Darwin-arm64) runtime="osx-arm64" ;;
    *)
      echo "Could not infer a .NET runtime; pass one explicitly (for example linux-x64)." >&2
      exit 2
      ;;
  esac
fi

output="${2:-$project_root/artifacts/admin-tool-$runtime}"

dotnet publish \
  "$project_root/src/Perpetuum.AdminTool.Avalonia/Perpetuum.AdminTool.Avalonia.csproj" \
  --configuration Release \
  --runtime "$runtime" \
  --self-contained true \
  --output "$output"

echo "Published Perpetuum AdminTool to $output"
