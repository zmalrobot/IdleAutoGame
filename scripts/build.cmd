@echo off
rem ============================================================================
rem IdleAutoGame - Clean Build Script (Windows CMD)
rem ============================================================================
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
for %%i in ("%~dp0..") do set "ROOT_DIR=%%~fi"
set "SOLUTION_FILE=%ROOT_DIR%\IdleAutoGame.slnx"

if "%~1"=="-h" goto :show_help
if "%~1"=="--help" goto :show_help
if "%~1"=="/?" goto :show_help

set "DO_CLEAN=true"
if "%~1"=="--no-clean" set "DO_CLEAN=false"

call "%SCRIPT_DIR%common.cmd" print_header "IdleAutoGame - Clean Release Build (Windows)" "%ROOT_DIR%"

rem 1. Check prerequisites
call "%SCRIPT_DIR%common.cmd" check_prerequisites "%ROOT_DIR%"
if !ERRORLEVEL! neq 0 (
    echo.
    echo ================================
    echo  BUILD FAILED: Missing Prerequisites
    echo ================================
    exit /b 1
)

rem 2. Clean previous build outputs if requested
if "!DO_CLEAN!"=="true" (
    call "%SCRIPT_DIR%common.cmd" log_info "Cleaning previous build outputs..."
    for /d /r "%ROOT_DIR%\src" %%d in (bin obj) do (
        if exist "%%d" rd /s /q "%%d" 2>nul
    )
    for /d /r "%ROOT_DIR%\tests" %%d in (bin obj) do (
        if exist "%%d" rd /s /q "%%d" 2>nul
    )
)

rem 3. NuGet Restore
call "%SCRIPT_DIR%common.cmd" log_info "Restoring NuGet packages..."
dotnet restore "%SOLUTION_FILE%"
if !ERRORLEVEL! neq 0 (
    echo.
    echo ================================
    echo  BUILD FAILED: NuGet Restore Error
    echo ================================
    exit /b 1
)

rem 4. Build in Release configuration
call "%SCRIPT_DIR%common.cmd" log_info "Building solution in Release configuration..."
dotnet build "%SOLUTION_FILE%" -c Release --no-restore
if !ERRORLEVEL! neq 0 (
    echo.
    echo ================================
    echo  BUILD FAILED
    echo ================================
    exit /b 1
)

rem 5. Native Fallback Runtime
if exist "%SCRIPT_DIR%build-native-fallback.cmd" (
    call "%SCRIPT_DIR%common.cmd" log_info "Checking native fallback runtime..."
    call "%SCRIPT_DIR%build-native-fallback.cmd" "%ROOT_DIR%\src\IdleAutoGame.Presentation\bin\Release\net10.0\runtimes\win-x64-fallback\native"
)

echo.
echo ================================
echo  BUILD SUCCESS
echo ================================
exit /b 0

:show_help
echo Usage: scripts\build.cmd [OPTIONS]
echo.
echo Performs a clean build of IdleAutoGame in Release configuration.
echo.
echo Process:
echo   1. Check system prerequisites (.NET 10 SDK, project files)
echo   2. Clean transient build outputs (bin, obj)
echo   3. Restore NuGet dependencies (dotnet restore)
echo   4. Compile solution in Release mode (dotnet build)
echo.
echo Options:
echo   -h, --help, /?    Show this help message and exit
echo   --no-clean        Skip cleaning bin/obj directories prior to building
exit /b 0

