#!/usr/bin/env bash
# Runs the Fallout build (build/_build.csproj, SPEC §19): ./build.sh [targets] [--parameters].
set -eo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

command -v dotnet >/dev/null || { echo "dotnet not found: install the SDK pinned by global.json" >&2; exit 1; }
echo "Microsoft (R) .NET SDK version $(dotnet --version)"

dotnet tool restore
exec dotnet fallout "$@"
