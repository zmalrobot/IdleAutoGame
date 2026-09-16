@echo off
rem ============================================================================
rem IdleAutoGame - Test Suite Runner (Windows CMD)
rem ============================================================================
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
for %%i in ("%~dp0..") do set "ROOT_DIR=%%~fi"
set "SOLUTION_FILE=%ROOT_DIR%\IdleAutoGame.slnx"

if "%~1"=="-h" goto :show_help
if "%~1"=="--help" goto :show_help
if "%~1"=="/?" goto :show_help

call "%SCRIPT_DIR%common.cmd" print_header "IdleAutoGame - Test Suite Execution (Windows)" "%ROOT_DIR%"

call "%SCRIPT_DIR%common.cmd" check_prerequisites "%ROOT_DIR%"
if !ERRORLEVEL! neq 0 exit /b 1

call "%SCRIPT_DIR%common.cmd" log_info "Executing unit tests in Release mode..."
dotnet test "%SOLUTION_FILE%" -c Release --verbosity normal %*
if !ERRORLEVEL! neq 0 (
    echo.
    call "%SCRIPT_DIR%common.cmd" log_error "Test execution failed."
    exit /b 1
)

echo.
call "%SCRIPT_DIR%common.cmd" log_success "All tests passed successfully!"
exit /b 0

:show_help
echo Usage: scripts\test.cmd [OPTIONS] [FILTER]
echo.
echo Runs the automated test suite for IdleAutoGame.
echo.
echo Options:
echo   -h, --help, /?    Show this help message and exit
echo.
echo Examples:
echo   scripts\test.cmd
echo   scripts\test.cmd --filter FullyQualifiedName~LocalLlama
exit /b 0

