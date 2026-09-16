#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Build & Run Script (Linux)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

show_help() {
    echo "Usage: ./scripts/run.sh [OPTIONS] [-- APP_ARGS...]"
    echo ""
    echo "Compiles IdleAutoGame in Release mode and runs the application."
    echo ""
    echo "Options:"
    echo "  -h, --help        Show this help message and exit"
    echo "  --no-clean        Skip clean step before building"
}

DO_CLEAN=true
APP_ARGS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)
            show_help
            exit 0
            ;;
        --no-clean)
            DO_CLEAN=false
            shift
            ;;
        --)
            shift
            APP_ARGS=("$@")
            break
            ;;
        *)
            APP_ARGS+=("$1")
            shift
            ;;
    esac
done

print_header "IdleAutoGame - Build & Run (Linux)"

check_prerequisites

# Build first
if [ "${DO_CLEAN}" = true ]; then
    log_info "Cleaning previous build outputs..."
    find "${ROOT_DIR}/src" "${ROOT_DIR}/tests" -depth -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true
fi

log_info "Restoring NuGet dependencies..."
dotnet restore "${SOLUTION_FILE}"

log_info "Compiling in Release mode..."
dotnet build "${SOLUTION_FILE}" -c Release --no-restore

log_info "Starting IdleAutoGame Presentation..."
exec dotnet run --project "${PRESENTATION_PROJECT}" -c Release --no-build "${APP_ARGS[@]}"

