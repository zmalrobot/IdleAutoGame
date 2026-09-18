#!/usr/bin/env bash
# ==============================================================================
# IdleAutoGame - Build Native llama.cpp Fallback Runtime (Linux)
# ==============================================================================
# Compiles a legacy/compatible build of llama.cpp shared libraries without
# AVX2/FMA/BMI2 instructions, specifically supporting Intel Ivy Bridge
# (e.g. Xeon E5 v2), Sandy Bridge, and x86-64-v2 microarchitectures.
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/common.sh
source "${SCRIPT_DIR}/common.sh"

DEST_DIR="${1:-${ROOT_DIR}/src/IdleAutoGame.Presentation/bin/Release/net10.0/runtimes/linux-x64-fallback/native}"

print_header "IdleAutoGame - Native llama.cpp Fallback Build"

# 1. Ensure llama.cpp source directory is at the exact ABI commit required by LLamaSharp 0.27.0
# According to official LLamaSharp v0.27.0 documentation:
# Tag: b8816, Commit: 3f7c29d318e317b63f54c558bc69803963d7d88c
TARGET_LLAMA_COMMIT="3f7c29d318e317b63f54c558bc69803963d7d88c"
LLAMA_SOURCE="${ROOT_DIR}/native/llama.cpp"

if [ ! -d "${LLAMA_SOURCE}/.git" ]; then
    mkdir -p "${ROOT_DIR}/native"
    if [ -d "/home/simone/ai/llama.cpp" ]; then
        log_info "Cloning llama.cpp using /home/simone/ai/llama.cpp as reference into native/llama.cpp..."
        git clone --reference /home/simone/ai/llama.cpp /home/simone/ai/llama.cpp "${LLAMA_SOURCE}"
    else
        log_info "Cloning llama.cpp from GitHub into native/llama.cpp..."
        git clone https://github.com/ggerganov/llama.cpp.git "${LLAMA_SOURCE}"
    fi
fi

CURRENT_COMMIT=$(git -C "${LLAMA_SOURCE}" rev-parse HEAD 2>/dev/null || echo "")
if [ "${CURRENT_COMMIT}" != "${TARGET_LLAMA_COMMIT}" ]; then
    log_info "Checking out required ABI commit ${TARGET_LLAMA_COMMIT} (LLamaSharp 0.27.0 compatible)..."
    git -C "${LLAMA_SOURCE}" checkout -f "${TARGET_LLAMA_COMMIT}"
fi

log_info "Using llama.cpp sources at: ${LLAMA_SOURCE} (commit: ${TARGET_LLAMA_COMMIT})"

# 2. Check build tools
if ! command -v cmake &>/dev/null; then
    log_error "cmake is required but not installed."
    exit 1
fi

BUILD_GENERATOR="Unix Makefiles"
BUILD_CMD="make -j$(nproc)"
if command -v ninja &>/dev/null; then
    BUILD_GENERATOR="Ninja"
    BUILD_CMD="ninja -j$(nproc)"
fi

log_info "Using CMake generator: ${BUILD_GENERATOR}"

# 3. Configure CMake in dedicated build folder
BUILD_DIR="${ROOT_DIR}/native/build-fallback"
COMMIT_FILE="${BUILD_DIR}/.llama_commit"
if [ ! -f "${COMMIT_FILE}" ] || [ "$(cat "${COMMIT_FILE}" 2>/dev/null)" != "${TARGET_LLAMA_COMMIT}" ]; then
    log_info "Clean build required for ABI commit ${TARGET_LLAMA_COMMIT}; resetting ${BUILD_DIR}..."
    rm -rf "${BUILD_DIR}"
fi
mkdir -p "${BUILD_DIR}"

log_info "Configuring CMake for x86-64-v2 (AVX 1.0, no FMA, no AVX2, no BMI2) with Vulkan GPU acceleration..."
cmake -B "${BUILD_DIR}" -S "${LLAMA_SOURCE}" \
    -G "${BUILD_GENERATOR}" \
    -DCMAKE_BUILD_TYPE=Release \
    -DBUILD_SHARED_LIBS=ON \
    -DGGML_AVX=ON \
    -DGGML_AVX2=OFF \
    -DGGML_FMA=OFF \
    -DGGML_AVX512=OFF \
    -DGGML_VULKAN=ON \
    -DGGML_CCACHE=OFF \
    -DCMAKE_C_FLAGS="-march=x86-64-v2 -mtune=generic" \
    -DCMAKE_CXX_FLAGS="-march=x86-64-v2 -mtune=generic"
echo "${TARGET_LLAMA_COMMIT}" > "${COMMIT_FILE}"

# 4. Compile shared libraries
log_info "Compiling native shared libraries (llama, mtmd, ggml-vulkan)..."
if [ "${BUILD_GENERATOR}" = "Ninja" ]; then
    ninja -C "${BUILD_DIR}" llama mtmd ggml-vulkan -j "$(nproc)"
else
    make -C "${BUILD_DIR}" llama mtmd ggml-vulkan -j "$(nproc)"
fi

# 5. Install libraries to target destination and staging area
log_info "Installing fallback native libraries to: ${DEST_DIR}"
mkdir -p "${DEST_DIR}"
cp -d "${BUILD_DIR}"/bin/lib*.so* "${DEST_DIR}/"

STAGING_DIR="${ROOT_DIR}/native/runtimes/linux-x64-fallback/native"
log_info "Staging fallback native libraries to: ${STAGING_DIR}"
mkdir -p "${STAGING_DIR}"
cp -d "${BUILD_DIR}"/bin/lib*.so* "${STAGING_DIR}/"

# Verify that libllama.so exists in destination
if [ ! -f "${DEST_DIR}/libllama.so" ]; then
    log_error "Failed to produce ${DEST_DIR}/libllama.so"
    exit 1
fi

log_success "Native fallback runtime built and deployed to: ${DEST_DIR}"

