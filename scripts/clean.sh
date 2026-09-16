#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Clean Script (Linux)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

show_help() {
    echo "Usage: ./scripts/clean.sh [OPTIONS]"
    echo ""
    echo "Cleans all build outputs (bin, obj), artifacts, and distribution directories."
    echo ""
    echo "Options:"
    echo "  -h, --help    Show this help message and exit"
}

for arg in "$@"; do
    case "${arg}" in
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            ;;
    esac
done

print_header "IdleAutoGame - Clean Build Artifacts"

log_info "Removing build and temporary output directories..."

# Delete all bin and obj folders in src and tests
find "${ROOT_DIR}/src" "${ROOT_DIR}/tests" -depth -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true

# Remove artifacts and dist
if [ -d "${ARTIFACTS_DIR}" ]; then
    rm -rf "${ARTIFACTS_DIR}"
fi

if [ -d "${ROOT_DIR}/dist" ]; then
    rm -rf "${ROOT_DIR}/dist"
fi

log_success "Clean completed successfully."

