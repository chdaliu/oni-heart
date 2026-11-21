#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

# Use the .NET SDK from PATH by default. Set DOTNET to another executable to override.
DOTNET="${DOTNET:-dotnet}"

"$DOTNET" build -c Release "$@"

echo
echo "已部署到：$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local/ONI-Heart"
