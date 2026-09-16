#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Common Linux Bash Utilities
# ==============================================================================

set -euo pipefail

# Determine script and repository root directories
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Global Paths & Constants
SOLUTION_FILE="${ROOT_DIR}/IdleAutoGame.slnx"
PRESENTATION_PROJECT="${ROOT_DIR}/src/IdleAutoGame.Presentation/IdleAutoGame.Presentation.csproj"
TEST_PROJECT="${ROOT_DIR}/tests/IdleAutoGame.Tests.Unit/IdleAutoGame.Tests.Unit.csproj"
DEFAULT_RID_LINUX="linux-x64"
DEFAULT_RID_WINDOWS="win-x64"
ARTIFACTS_DIR="${ROOT_DIR}/artifacts"
PUBLISH_DIR="${ARTIFACTS_DIR}/publish"
REQUIRED_DOTNET_MAJOR="10"

# Colors for terminal output
if [ -t 1 ]; then
    COLOR_RESET="\033[0m"
    COLOR_GREEN="\033[1;32m"
    COLOR_RED="\033[1;31m"
    COLOR_YELLOW="\033[1;33m"
    COLOR_CYAN="\033[1;36m"
    COLOR_BOLD="\033[1m"
else
    COLOR_RESET=""
    COLOR_GREEN=""
    COLOR_RED=""
    COLOR_YELLOW=""
    COLOR_CYAN=""
    COLOR_BOLD=""
fi

log_info() {
    echo -e "${COLOR_CYAN}[INFO]${COLOR_RESET} $1"
}

log_success() {
    echo -e "${COLOR_GREEN}[SUCCESS]${COLOR_RESET} $1"
}

log_warn() {
    echo -e "${COLOR_YELLOW}[WARN]${COLOR_RESET} $1"
}

log_error() {
    echo -e "${COLOR_RED}[ERROR]${COLOR_RESET} $1" >&2
}

print_header() {
    local title="$1"
    echo -e "${COLOR_BOLD}======================================================${COLOR_RESET}"
    echo -e "  ${COLOR_CYAN}${title}${COLOR_RESET}"
    echo -e "${COLOR_BOLD}======================================================${COLOR_RESET}"
    echo "Repository Root: ${ROOT_DIR}"
}

# Verify system prerequisites: dotnet CLI, SDK major version 10, files
check_prerequisites() {
    log_info "Checking prerequisites..."

    # 1. Check dotnet in PATH
    if ! command -v dotnet >/dev/null 2>&1; then
        log_error ".NET CLI (dotnet) is not found in PATH."
        echo "Please install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
        exit 1
    fi

    # 2. Check installed SDKs
    local sdks
    sdks="$(dotnet --list-sdks 2>/dev/null || true)"
    if [ -z "${sdks}" ]; then
        log_error "No .NET SDKs found installed on this machine."
        echo "Please install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
        exit 1
    fi

    # 3. Check for .NET 10 SDK specifically
    if ! echo "${sdks}" | grep -E "^10\." >/dev/null 2>&1; then
        log_error "Required .NET SDK (v${REQUIRED_DOTNET_MAJOR}.x) was not found."
        echo "Installed SDKs:"
        echo "${sdks}"
        echo "Please install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
        exit 1
    fi

    # 4. Check solution file existence
    if [ ! -f "${SOLUTION_FILE}" ]; then
        log_error "Solution file not found: ${SOLUTION_FILE}"
        exit 1
    fi

    # 5. Check presentation project existence
    if [ ! -f "${PRESENTATION_PROJECT}" ]; then
        log_error "Presentation project not found: ${PRESENTATION_PROJECT}"
        exit 1
    fi

    log_success "Prerequisites check passed (Using .NET SDK $(dotnet --version))."
}

