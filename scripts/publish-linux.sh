#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Linux x64 Publish Script (Compatibility Wrapper)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Invoke the unified publish.sh
"${SCRIPT_DIR}/publish.sh" -r linux-x64 --self-contained false "$@"

# Ensure dist/linux-x64 backwards compatibility
mkdir -p "${ROOT_DIR}/dist"
rm -rf "${ROOT_DIR}/dist/linux-x64"
cp -r "${ROOT_DIR}/artifacts/publish/linux-x64" "${ROOT_DIR}/dist/linux-x64"

echo "Backwards compatibility directory created at: ${ROOT_DIR}/dist/linux-x64"
