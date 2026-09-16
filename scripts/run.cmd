@echo off
rem ============================================================================
rem IdleAutoGame - Build & Run Script (Windows CMD)
rem ============================================================================
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
for %%i in ("%~dp0..") do set "ROOT_DIR=%%~fi"
set "SOLUTION_FILE=%ROOT_DIR%\IdleAutoGame.slnx"
set "PRESENTATION_PROJECT=%ROOT_DIR%\src\IdleAutoGame.Presentation\IdleAutoGame.Presentation.csproj"

if "%~1"=="-h" goto :show_help
if "%~1"=="--help" goto :show_help
if "%~1"=="/?" goto :show_help

set "DO_CLEAN=true"
set "APP_ARGS="

:parse_args
if "%~1"=="" goto :done_args
if "%~1"=="--no-clean" (
    set "DO_CLEAN=false"
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

call "%SCRIPT_DIR%common.cmd" print_header "IdleAutoGame - Build & Run (Windows)" "%ROOT_DIR%"

call "%SCRIPT_DIR%common.cmd" check_prerequisites "%ROOT_DIR%"
if !ERRORLEVEL! neq 0 exit /b 1

if "!DO_CLEAN!"=="true" (
    call "%SCRIPT_DIR%common.cmd" log_info "Cleaning previous build outputs..."
    for /d /r "%ROOT_DIR%\src" %%d in (bin obj) do (
        if exist "%%d" rd /s /q "%%d" 2>nul
    )
    for /d /r "%ROOT_DIR%\tests" %%d in (bin obj) do (
        if exist "%%d" rd /s /q "%%d" 2>nul
    )
)

call "%SCRIPT_DIR%common.cmd" log_info "Restoring NuGet dependencies..."
dotnet restore "%SOLUTION_FILE%"
if !ERRORLEVEL! neq 0 exit /b 1

call "%SCRIPT_DIR%common.cmd" log_info "Compiling in Release mode..."
dotnet build "%SOLUTION_FILE%" -c Release --no-restore
if !ERRORLEVEL! neq 0 exit /b 1

call "%SCRIPT_DIR%common.cmd" log_info "Starting IdleAutoGame Presentation..."
dotnet run --project "%PRESENTATION_PROJECT%" -c Release --no-build %APP_ARGS%
exit /b %ERRORLEVEL%

:show_help
echo Usage: scripts\run.cmd [OPTIONS] [-- APP_ARGS...]
echo.
echo Compiles IdleAutoGame in Release mode and runs the application.
echo.
echo Options:
echo   -h, --help, /?    Show this help message and exit
echo   --no-clean        Skip clean step before building
exit /b 0

