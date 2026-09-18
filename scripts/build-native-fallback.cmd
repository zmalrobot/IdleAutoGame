@echo off
rem ==============================================================================
rem IdleAutoGame - Build Native llama.cpp Fallback Runtime (Windows CMD)
rem ==============================================================================

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
call "%SCRIPT_DIR%common.cmd"

set "DEST_DIR=%~1"
if "%DEST_DIR%"=="" set "DEST_DIR=%ROOT_DIR%\src\IdleAutoGame.Presentation\bin\Release\net10.0\runtimes\win-x64-fallback\native"

echo ======================================================
echo   IdleAutoGame - Native llama.cpp Fallback Build (Windows)
echo ======================================================

rem 1. Locate llama.cpp source directory
set "LLAMA_SOURCE="
if defined LLAMA_CPP_SOURCE_DIR if exist "%LLAMA_CPP_SOURCE_DIR%" set "LLAMA_SOURCE=%LLAMA_CPP_SOURCE_DIR%"
if not defined LLAMA_SOURCE if exist "%ROOT_DIR%\native\llama.cpp" set "LLAMA_SOURCE=%ROOT_DIR%\native\llama.cpp"

if not defined LLAMA_SOURCE (
    echo [INFO] Cloning llama.cpp into native\llama.cpp...
    if not exist "%ROOT_DIR%\native" mkdir "%ROOT_DIR%\native"
    git clone --depth 1 https://github.com/ggerganov/llama.cpp.git "%ROOT_DIR%\native\llama.cpp"
    set "LLAMA_SOURCE=%ROOT_DIR%\native\llama.cpp"
)

where cmake >nul 2>nul
if errorlevel 1 (
    echo [WARNING] CMake not found in PATH. Skipping native fallback build on Windows.
    exit /b 0
)

set "BUILD_DIR=%ROOT_DIR%\native\build-fallback-win"
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

echo [INFO] Configuring CMake for legacy CPU compatibility with Vulkan GPU acceleration...
cmake -B "%BUILD_DIR%" -S "%LLAMA_SOURCE%" -DBUILD_SHARED_LIBS=ON -DGGML_AVX=ON -DGGML_AVX2=OFF -DGGML_FMA=OFF -DGGML_AVX512=OFF -DGGML_VULKAN=ON -DCMAKE_BUILD_TYPE=Release
if errorlevel 1 (
    echo [WARNING] CMake configuration failed.
    exit /b 0
)

echo [INFO] Building native fallback shared libraries (llama, mtmd, ggml-vulkan)...
cmake --build "%BUILD_DIR%" --config Release --target llama mtmd ggml-vulkan -j %NUMBER_OF_PROCESSORS%
if errorlevel 1 (
    echo [WARNING] Native build failed.
    exit /b 0
)

if not exist "%DEST_DIR%" mkdir "%DEST_DIR%"
copy /Y "%BUILD_DIR%\bin\Release\*.dll" "%DEST_DIR%\" >nul 2>nul
echo [SUCCESS] Native fallback runtime deployed to %DEST_DIR%

endlocal
exit /b 0

