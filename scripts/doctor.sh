#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Environment Doctor & Diagnostics (Linux)
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

show_help() {
    echo "Usage: ./scripts/doctor.sh [OPTIONS]"
    echo ""
    echo "Diagnoses system environment, .NET SDK, project files, and NuGet reachability."
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

print_header "IdleAutoGame - Environment Diagnostics (Doctor)"

ERRORS=0

# 1. Check Operating System & Architecture
OS_NAME="$(uname -s)"
ARCH_NAME="$(uname -m)"
echo -e "Operating System: ${COLOR_BOLD}${OS_NAME} (${ARCH_NAME})${COLOR_RESET}"

if [ "${OS_NAME}" != "Linux" ]; then
    log_warn "Current OS is '${OS_NAME}', expected Linux."
fi

# 2. Check .NET CLI
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_PATH="$(command -v dotnet)"
    DOTNET_VER="$(dotnet --version)"
    echo -e "dotnet CLI:       ${COLOR_GREEN}OK${COLOR_RESET} (${DOTNET_PATH})"
    echo -e "Active SDK:       ${COLOR_GREEN}OK${COLOR_RESET} (${DOTNET_VER})"
else
    echo -e "dotnet CLI:       ${COLOR_RED}FAILED${COLOR_RESET} (dotnet not found in PATH)"
    ERRORS=$((ERRORS + 1))
fi

# 3. Check .NET 10 SDK presence
SDKS="$(dotnet --list-sdks 2>/dev/null || true)"
if echo "${SDKS}" | grep -E "^10\." >/dev/null 2>&1; then
    SDK_10="$(echo "${SDKS}" | grep -E "^10\." | head -n 1)"
    echo -e ".NET 10 SDK:      ${COLOR_GREEN}OK${COLOR_RESET} (${SDK_10})"
else
    echo -e ".NET 10 SDK:      ${COLOR_RED}FAILED${COLOR_RESET} (Requires .NET 10 SDK)"
    ERRORS=$((ERRORS + 1))
fi

# 4. Check global.json
if [ -f "${ROOT_DIR}/global.json" ]; then
    echo -e "global.json:      ${COLOR_GREEN}OK${COLOR_RESET}"
else
    echo -e "global.json:      ${COLOR_YELLOW}MISSING${COLOR_RESET}"
fi

# 5. Check Solution File
if [ -f "${SOLUTION_FILE}" ]; then
    echo -e "Solution File:    ${COLOR_GREEN}OK${COLOR_RESET} ($(basename "${SOLUTION_FILE}"))"
else
    echo -e "Solution File:    ${COLOR_RED}FAILED${COLOR_RESET} (Missing: ${SOLUTION_FILE})"
    ERRORS=$((ERRORS + 1))
fi

# 6. Check Project Files
MISSING_PROJECTS=0
PROJECT_LIST=(
    "src/IdleAutoGame.Core/IdleAutoGame.Core.csproj"
    "src/IdleAutoGame.Application/IdleAutoGame.Application.csproj"
    "src/IdleAutoGame.Infrastructure.Adb/IdleAutoGame.Infrastructure.Adb.csproj"
    "src/IdleAutoGame.Infrastructure.Llm/IdleAutoGame.Infrastructure.Llm.csproj"
    "src/IdleAutoGame.Infrastructure.Persistence/IdleAutoGame.Infrastructure.Persistence.csproj"
    "src/IdleAutoGame.Games.TapTitans2/IdleAutoGame.Games.TapTitans2.csproj"
    "src/IdleAutoGame.Presentation/IdleAutoGame.Presentation.csproj"
    "tests/IdleAutoGame.Tests.Unit/IdleAutoGame.Tests.Unit.csproj"
)

for proj in "${PROJECT_LIST[@]}"; do
    if [ ! -f "${ROOT_DIR}/${proj}" ]; then
        log_error "Missing project file: ${proj}"
        MISSING_PROJECTS=$((MISSING_PROJECTS + 1))
    fi
done

if [ "${MISSING_PROJECTS}" -eq 0 ]; then
    echo -e "Projects (8/8):   ${COLOR_GREEN}OK${COLOR_RESET}"
else
    echo -e "Projects (8/8):   ${COLOR_RED}FAILED${COLOR_RESET} (${MISSING_PROJECTS} missing)"
    ERRORS=$((ERRORS + 1))
fi

# 7. Check NuGet Feed Configuration
if dotnet nuget list source >/dev/null 2>&1; then
    echo -e "NuGet Sources:    ${COLOR_GREEN}OK${COLOR_RESET}"
else
    echo -e "NuGet Sources:    ${COLOR_RED}FAILED${COLOR_RESET} (Cannot query NuGet sources)"
    ERRORS=$((ERRORS + 1))
fi

# 8. Check Write Access to Artifacts
TEST_DIR="${ARTIFACTS_DIR}/.doctor_test"
mkdir -p "${TEST_DIR}" 2>/dev/null || true
if [ -d "${TEST_DIR}" ] && touch "${TEST_DIR}/write_test" 2>/dev/null; then
    rm -rf "${TEST_DIR}"
    echo -e "Disk Write:       ${COLOR_GREEN}OK${COLOR_RESET}"
else
    echo -e "Disk Write:       ${COLOR_RED}FAILED${COLOR_RESET} (Cannot write to ${ARTIFACTS_DIR})"
    ERRORS=$((ERRORS + 1))
fi

echo "------------------------------------------------------"
if [ "${ERRORS}" -eq 0 ]; then
    echo -e "${COLOR_GREEN}${COLOR_BOLD}Environment is ready.${COLOR_RESET}"
    exit 0
else
    echo -e "${COLOR_RED}${COLOR_BOLD}Doctor found ${ERRORS} issue(s). Please fix the issues before building.${COLOR_RESET}"
    exit 1
fi

