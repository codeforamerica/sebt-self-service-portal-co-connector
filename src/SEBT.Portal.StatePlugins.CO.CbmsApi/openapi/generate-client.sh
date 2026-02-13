#!/usr/bin/env bash
# Generates/updates the C# API client for the CBMS SEBT API using Kiota.
# Re-run this script whenever the OpenAPI spec (api-spec.yaml) changes.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
SPEC_FILE="$SCRIPT_DIR/api-spec.yaml"
OUTPUT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CSPROJ_FILE="$OUTPUT_DIR/SEBT.Portal.StatePlugins.CO.CbmsApi.csproj"

# Ensure .NET tools (kiota) are available
dotnet tool restore --tool-manifest "$REPO_ROOT/.config/dotnet-tools.json"

# Preserve the .csproj and openapi/ directory before --clean-output wipes everything
BACKUP_DIR="$(mktemp -d)"
cp "$CSPROJ_FILE" "$BACKUP_DIR/"
cp -r "$SCRIPT_DIR" "$BACKUP_DIR/openapi"

dotnet kiota generate \
  --language CSharp \
  --openapi "$SPEC_FILE" \
  --output "$OUTPUT_DIR" \
  --class-name CbmsSebtApiClient \
  --namespace-name SEBT.Portal.StatePlugins.CO.CbmsApi \
  --exclude-backward-compatible \
  --clean-output \
  --log-level Warning

# Restore the .csproj and openapi/ directory
cp "$BACKUP_DIR/SEBT.Portal.StatePlugins.CO.CbmsApi.csproj" "$CSPROJ_FILE"
cp -r "$BACKUP_DIR/openapi" "$OUTPUT_DIR/"
rm -rf "$BACKUP_DIR"

echo "Client generated in $OUTPUT_DIR"
