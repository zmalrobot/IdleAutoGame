@echo off
rem ============================================================================
rem IdleAutoGame - Publish Script (Windows CMD)
rem ============================================================================
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
for %%i in ("%~dp0..") do set "ROOT_DIR=%%~fi"
set "PRESENTATION_PROJECT=%ROOT_DIR%\src\IdleAutoGame.Presentation\IdleAutoGame.Presentation.csproj"

set "TARGET_RID=win-x64"
set "SELF_CONTAINED=false"

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
if "%~1"=="--self-contained" (
    if "%~2"=="true" (
        set "SELF_CONTAINED=true"
        shift
        shift
        goto :parse_args
    )
    if "%~2"=="false" (
        set "SELF_CONTAINED=false"
        shift
        shift
        goto :parse_args
    )
    set "SELF_CONTAINED=true"
    shift
    goto :parse_args
)
call "%SCRIPT_DIR%common.cmd" log_error "Unknown option: %~1"
goto :show_help

:done_args

set "OUTPUT_DIR=%ROOT_DIR%\artifacts\publish\%TARGET_RID%"

call "%SCRIPT_DIR%common.cmd" print_header "IdleAutoGame - Publish (Windows)" "%ROOT_DIR%"
echo Target RID:       %TARGET_RID%
echo Self-Contained:   %SELF_CONTAINED%
echo Output Directory: %OUTPUT_DIR%

call "%SCRIPT_DIR%common.cmd" check_prerequisites "%ROOT_DIR%"
if !ERRORLEVEL! neq 0 exit /b 1

call "%SCRIPT_DIR%common.cmd" log_info "Preparing output directory..."
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%" 2>nul

call "%SCRIPT_DIR%common.cmd" log_info "Restoring NuGet dependencies for %TARGET_RID%..."
dotnet restore "%PRESENTATION_PROJECT%" -r "%TARGET_RID%"
if !ERRORLEVEL! neq 0 exit /b 1

call "%SCRIPT_DIR%common.cmd" log_info "Publishing application in Release configuration..."
dotnet publish "%PRESENTATION_PROJECT%" -c Release -r "%TARGET_RID%" --self-contained %SELF_CONTAINED% -o "%OUTPUT_DIR%"
if !ERRORLEVEL! neq 0 (
    echo.
    call "%SCRIPT_DIR%common.cmd" log_error "Publish failed."
    exit /b 1
)

rem Copy models.json if present
if exist "%ROOT_DIR%\models.json" (
    call "%SCRIPT_DIR%common.cmd" log_info "Copying models.json to output directory..."
    copy /y "%ROOT_DIR%\models.json" "%OUTPUT_DIR%\" >nul
)

set "TARGET_EXE=%OUTPUT_DIR%\IdleAutoGame.Presentation.exe"
if exist "%TARGET_EXE%" (
    echo.
    call "%SCRIPT_DIR%common.cmd" log_success "Publish completed successfully!"
    echo Binary executable: %TARGET_EXE%
    echo Run with: scripts\run-published.cmd
    exit /b 0
) else (
    echo.
    call "%SCRIPT_DIR%common.cmd" log_error "Expected binary was not created: %TARGET_EXE%"
    exit /b 1
)

:show_help
echo Usage: scripts\publish.cmd [OPTIONS]
echo.
echo Publishes IdleAutoGame Presentation binary to artifacts\publish\^<RID^>.
echo.
echo Options:
echo   -r, --rid ^<RID^>             Target Runtime Identifier (default: win-x64)
echo   --self-contained [true^|false] Publish self-contained runtime (default: false)
echo   -h, --help, /?              Show this help message and exit
echo.
echo Examples:
echo   scripts\publish.cmd
echo   scripts\publish.cmd -r win-x64 --self-contained false
exit /b 0

