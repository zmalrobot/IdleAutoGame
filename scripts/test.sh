#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Test Suite Runner (Linux)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

show_help() {
    echo "Usage: ./scripts/test.sh [OPTIONS] [FILTER]"
    echo ""
    echo "Runs the automated test suite for IdleAutoGame."
    echo ""
    echo "Options:"
    echo "  -h, --help    Show this help message and exit"
    echo ""
    echo "Examples:"
    echo "  ./scripts/test.sh"
    echo "  ./scripts/test.sh --filter FullyQualifiedName~LocalLlama"
}

PASSTHROUGH_ARGS=()
for arg in "$@"; do
    case "${arg}" in
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            PASSTHROUGH_ARGS+=("${arg}")
            ;;
    esac
done

print_header "IdleAutoGame - Test Suite Execution"

check_prerequisites

log_info "Executing unit tests in Release mode..."
if dotnet test "${SOLUTION_FILE}" -c Release --verbosity normal "${PASSTHROUGH_ARGS[@]}"; then
    echo ""
    log_success "All tests passed successfully!"
    exit 0
else
    echo ""
    log_error "Test execution failed."
    exit 1
fi

