#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Publish Script (Linux)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

show_help() {
    echo "Usage: ./scripts/publish.sh [OPTIONS]"
    echo ""
    echo "Publishes IdleAutoGame Presentation binary to artifacts/publish/<RID>."
    echo ""
    echo "Options:"
    echo "  -r, --rid <RID>             Target Runtime Identifier (default: linux-x64)"
    echo "  --self-contained [true|false] Publish self-contained runtime (default: false)"
    echo "  -h, --help                  Show this help message and exit"
    echo ""
    echo "Examples:"
    echo "  ./scripts/publish.sh"
    echo "  ./scripts/publish.sh -r linux-x64 --self-contained false"
}

TARGET_RID="${DEFAULT_RID_LINUX}"
SELF_CONTAINED=false

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
        --self-contained)
            if [[ $# -gt 1 && ("$2" == "true" || "$2" == "false") ]]; then
                SELF_CONTAINED="$2"
                shift 2
            else
                SELF_CONTAINED=true
                shift
            fi
            ;;
        *)
            log_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

OUTPUT_DIR="${PUBLISH_DIR}/${TARGET_RID}"

print_header "IdleAutoGame - Publish (Linux)"
echo "Target RID:       ${TARGET_RID}"
echo "Self-Contained:   ${SELF_CONTAINED}"
echo "Output Directory: ${OUTPUT_DIR}"

check_prerequisites

log_info "Preparing output directory..."
rm -rf "${OUTPUT_DIR}"
mkdir -p "${OUTPUT_DIR}"

log_info "Restoring NuGet dependencies for ${TARGET_RID}..."
dotnet restore "${PRESENTATION_PROJECT}" -r "${TARGET_RID}"

log_info "Publishing application in Release configuration..."
if ! dotnet publish "${PRESENTATION_PROJECT}" \
    -c Release \
    -r "${TARGET_RID}" \
    --self-contained "${SELF_CONTAINED}" \
    -o "${OUTPUT_DIR}"; then
    echo ""
    log_error "Publish failed."
    exit 1
fi

# Copy models.json if available
if [ -f "${ROOT_DIR}/models.json" ]; then
    log_info "Copying models.json to output directory..."
    cp "${ROOT_DIR}/models.json" "${OUTPUT_DIR}/"
fi

# Copy Linux desktop launcher entry and icon if available
if [ -f "${ROOT_DIR}/scripts/IdleAutoGame.desktop" ]; then
    cp "${ROOT_DIR}/scripts/IdleAutoGame.desktop" "${OUTPUT_DIR}/"
fi
if [ -f "${ROOT_DIR}/scripts/idle-auto-game.png" ]; then
    cp "${ROOT_DIR}/scripts/idle-auto-game.png" "${OUTPUT_DIR}/"
fi

# Ensure executable bit on produced binary
TARGET_BIN="${OUTPUT_DIR}/IdleAutoGame.Presentation"
if [ -f "${TARGET_BIN}" ]; then
    chmod +x "${TARGET_BIN}"
    echo ""
    log_success "Publish completed successfully!"
    echo "Binary executable: ${TARGET_BIN}"
    echo "Run with: ./scripts/run-published.sh"
    exit 0
else
    echo ""
    log_error "Expected binary was not created: ${TARGET_BIN}"
    exit 1
fi

