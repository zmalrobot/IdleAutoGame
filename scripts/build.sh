#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Clean Build Script (Linux)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

show_help() {
    echo "Usage: ./scripts/build.sh [OPTIONS]"
    echo ""
    echo "Performs a clean build of IdleAutoGame in Release configuration."
    echo ""
    echo "Process:"
    echo "  1. Check system prerequisites (.NET 10 SDK, project files)"
    echo "  2. Clean transient build outputs (bin, obj)"
    echo "  3. Restore NuGet dependencies (dotnet restore)"
    echo "  4. Compile solution in Release mode (dotnet build)"
    echo ""
    echo "Options:"
    echo "  -h, --help        Show this help message and exit"
    echo "  --no-clean        Skip cleaning bin/obj directories prior to building"
}

DO_CLEAN=true

for arg in "$@"; do
    case "${arg}" in
        -h|--help)
            show_help
            exit 0
            ;;
        --no-clean)
            DO_CLEAN=false
            ;;
        *)
            ;;
    esac
done

print_header "IdleAutoGame - Clean Release Build (Linux)"

# 1. Check prerequisites
check_prerequisites

# 2. Clean if requested
if [ "${DO_CLEAN}" = true ]; then
    log_info "Cleaning previous build outputs..."
    find "${ROOT_DIR}/src" "${ROOT_DIR}/tests" -depth -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true
fi

# 3. NuGet Restore
log_info "Restoring NuGet packages..."
if ! dotnet restore "${SOLUTION_FILE}"; then
    echo ""
    echo -e "${COLOR_RED}================================${COLOR_RESET}"
    echo -e "${COLOR_RED} BUILD FAILED: NuGet Restore Error${COLOR_RESET}"
    echo -e "${COLOR_RED}================================${COLOR_RESET}"
    exit 1
fi

# 4. Build Release
log_info "Building solution in Release configuration..."
if ! dotnet build "${SOLUTION_FILE}" -c Release --no-restore; then
    echo ""
    echo -e "${COLOR_RED}================================${COLOR_RESET}"
    echo -e "${COLOR_RED} BUILD FAILED${COLOR_RESET}"
    echo -e "${COLOR_RED}================================${COLOR_RESET}"
    exit 1
fi

# 5. Native Fallback llama.cpp Runtime
if [ -x "${SCRIPT_DIR}/build-native-fallback.sh" ]; then
    log_info "Ensuring native fallback llama.cpp runtime is deployed..."
    "${SCRIPT_DIR}/build-native-fallback.sh" "${ROOT_DIR}/src/IdleAutoGame.Presentation/bin/Release/net10.0/runtimes/linux-x64-fallback/native" || log_warn "Native fallback build skipped or failed; continuing."
fi

echo ""
echo -e "${COLOR_GREEN}================================${COLOR_RESET}"
echo -e "${COLOR_GREEN} BUILD SUCCESS${COLOR_RESET}"
echo -e "${COLOR_GREEN}================================${COLOR_RESET}"
exit 0

