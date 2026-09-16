@echo off
rem ============================================================================
rem IdleAutoGame - Run Published Application (Windows CMD)
rem ============================================================================
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
for %%i in ("%~dp0..") do set "ROOT_DIR=%%~fi"

set "TARGET_RID=win-x64"
set "APP_ARGS="

:parse_args
if "%~1"=="" goto :done_args
if "%~1"=="-h" goto :show_help
if "%~1"=="--help" goto :show_help
if "%~1"=="/?" goto :show_help
if "%~1"=="-r" (
    set "TARGET_RID=%~2"
    shift
    shift
    goto :parse_args
)
if "%~1"=="--rid" (
    set "TARGET_RID=%~2"
    shift
    shift
    goto :parse_args
)
if "%~1"=="--" (
    shift
    goto :collect_rest
)
set "APP_ARGS=!APP_ARGS! %1"
shift
goto :parse_args

:collect_rest
if "%~1"=="" goto :done_args
set "APP_ARGS=!APP_ARGS! %1"
shift
goto :collect_rest

:done_args

set "TARGET_EXE=%ROOT_DIR%\artifacts\publish\%TARGET_RID%\IdleAutoGame.Presentation.exe"

if not exist "%TARGET_EXE%" (
    call "%SCRIPT_DIR%common.cmd" log_error "Published application not found."
    echo Expected location: %TARGET_EXE%
    echo Run scripts\publish.cmd first.
    exit /b 1
)

call "%SCRIPT_DIR%common.cmd" log_info "Launching published binary: %TARGET_EXE%"
"%TARGET_EXE%" %APP_ARGS%
exit /b %ERRORLEVEL%

:show_help
echo Usage: scripts\run-published.cmd [OPTIONS] [-- APP_ARGS...]
echo.
echo Runs the previously published IdleAutoGame application binary.
echo.
echo Options:
echo   -r, --rid ^<RID^>   Target Runtime Identifier (default: win-x64)
echo   -h, --help, /?    Show this help message and exit
exit /b 0

