@echo off
rem ============================================================================
rem IdleAutoGame - Clean Script (Windows CMD)
rem ============================================================================
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
for %%i in ("%~dp0..") do set "ROOT_DIR=%%~fi"

if "%~1"=="-h" goto :show_help
if "%~1"=="--help" goto :show_help
if "%~1"=="/?" goto :show_help

call "%SCRIPT_DIR%common.cmd" print_header "IdleAutoGame - Clean Build Artifacts (Windows)" "%ROOT_DIR%"
call "%SCRIPT_DIR%common.cmd" log_info "Removing build and temporary output directories..."

rem Clean bin and obj folders in src and tests
for /d /r "%ROOT_DIR%\src" %%d in (bin obj) do (
    if exist "%%d" rd /s /q "%%d" 2>nul
)
for /d /r "%ROOT_DIR%\tests" %%d in (bin obj) do (
    if exist "%%d" rd /s /q "%%d" 2>nul
)

if exist "%ROOT_DIR%\artifacts" rd /s /q "%ROOT_DIR%\artifacts" 2>nul
if exist "%ROOT_DIR%\dist" rd /s /q "%ROOT_DIR%\dist" 2>nul

call "%SCRIPT_DIR%common.cmd" log_success "Clean completed successfully."
exit /b 0

:show_help
echo Usage: scripts\clean.cmd [OPTIONS]
echo.
echo Cleans all build outputs (bin, obj), artifacts, and distribution directories.
echo.
echo Options:
echo   -h, --help, /?    Show this help message and exit
exit /b 0

