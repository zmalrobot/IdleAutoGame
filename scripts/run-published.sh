#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Run Published Application (Linux)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

show_help() {
    echo "Usage: ./scripts/run-published.sh [OPTIONS] [-- APP_ARGS...]"
    echo ""
    echo "Runs the previously published IdleAutoGame application binary."
    echo ""
    echo "Options:"
    echo "  -r, --rid <RID>   Target Runtime Identifier (default: linux-x64)"
    echo "  -h, --help        Show this help message and exit"
}

TARGET_RID="${DEFAULT_RID_LINUX}"
APP_ARGS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)
            show_help
            exit 0
            ;;
        -r|--rid)
            TARGET_RID="$2"
            shift 2
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

TARGET_BIN="${PUBLISH_DIR}/${TARGET_RID}/IdleAutoGame.Presentation"

if [ ! -f "${TARGET_BIN}" ]; then
    log_error "Published application not found."
    echo "Expected location: ${TARGET_BIN}"
    echo "Run ./scripts/publish.sh first."
    exit 1
fi

if [ ! -x "${TARGET_BIN}" ]; then
    log_info "Setting executable permissions on ${TARGET_BIN}..."
    chmod +x "${TARGET_BIN}"
fi

log_info "Launching published binary: ${TARGET_BIN}"
exec "${TARGET_BIN}" "${APP_ARGS[@]}"

