#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
OUTPUT_DIR="${ROOT_DIR}/dist/linux-x64"

echo "=========================================="
echo "  IdleAutoGame - Linux x64 Publish Script "
echo "=========================================="
echo "Project root: ${ROOT_DIR}"
echo "Output path:  ${OUTPUT_DIR}"

mkdir -p "${OUTPUT_DIR}"

dotnet publish "${ROOT_DIR}/src/IdleAutoGame.Presentation/IdleAutoGame.Presentation.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "${OUTPUT_DIR}"

# Copy models.json if present
if [ -f "${ROOT_DIR}/models.json" ]; then
    cp "${ROOT_DIR}/models.json" "${OUTPUT_DIR}/"
fi

# Ensure executable bit
chmod +x "${OUTPUT_DIR}/IdleAutoGame.Presentation"

echo "------------------------------------------"
echo "Build succeeded!"
echo "Binary executable: ${OUTPUT_DIR}/IdleAutoGame.Presentation"
echo "Run via: ${OUTPUT_DIR}/IdleAutoGame.Presentation"
echo "=========================================="

