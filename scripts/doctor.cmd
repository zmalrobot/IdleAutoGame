@echo off
rem ============================================================================
rem IdleAutoGame - Environment Doctor & Diagnostics (Windows CMD)
rem ============================================================================
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
for %%i in ("%~dp0..") do set "ROOT_DIR=%%~fi"

if "%~1"=="-h" goto :show_help
if "%~1"=="--help" goto :show_help
if "%~1"=="/?" goto :show_help

call "%SCRIPT_DIR%common.cmd" print_header "IdleAutoGame - Environment Diagnostics (Windows)" "%ROOT_DIR%"

set /a ERRORS=0

echo Operating System: %OS% (%PROCESSOR_ARCHITECTURE%)

rem 1. Check dotnet CLI in PATH
where dotnet >nul 2>&1
if errorlevel 1 (
    echo dotnet CLI:       FAILED - dotnet not found in PATH
    set /a ERRORS+=1
) else (
    for /f "tokens=*" %%p in ('where dotnet 2^>nul') do set "DOTNET_PATH=%%p"
    for /f "tokens=*" %%v in ('dotnet --version 2^>nul') do set "DOTNET_VER=%%v"
    echo dotnet CLI:       OK [!DOTNET_PATH!]
    echo Active SDK:       OK [!DOTNET_VER!]
)

rem 2. Check for .NET 10 SDK
set "FOUND_10_SDK="
for /f "tokens=*" %%a in ('dotnet --list-sdks 2^>nul') do (
    echo %%a | findstr /R "^10\." >nul 2>&1
    if !ERRORLEVEL! equ 0 set "FOUND_10_SDK=%%a"
)

if not "!FOUND_10_SDK!"=="" (
    echo .NET 10 SDK:      OK [!FOUND_10_SDK!]
) else (
    echo .NET 10 SDK:      FAILED - Requires .NET 10 SDK
    set /a ERRORS+=1
)

rem 3. Check global.json
if exist "%ROOT_DIR%\global.json" (
    echo global.json:      OK
) else (
    echo global.json:      MISSING
)

rem 4. Check Solution File
if exist "%ROOT_DIR%\IdleAutoGame.slnx" (
    echo Solution File:    OK - IdleAutoGame.slnx
) else (
    echo Solution File:    FAILED - Missing IdleAutoGame.slnx
    set /a ERRORS+=1
)

rem 5. Check Project Files (8 total)
set /a MISSING_PROJS=0
if not exist "%ROOT_DIR%\src\IdleAutoGame.Core\IdleAutoGame.Core.csproj" set /a MISSING_PROJS+=1
if not exist "%ROOT_DIR%\src\IdleAutoGame.Application\IdleAutoGame.Application.csproj" set /a MISSING_PROJS+=1
if not exist "%ROOT_DIR%\src\IdleAutoGame.Infrastructure.Adb\IdleAutoGame.Infrastructure.Adb.csproj" set /a MISSING_PROJS+=1
if not exist "%ROOT_DIR%\src\IdleAutoGame.Infrastructure.Llm\IdleAutoGame.Infrastructure.Llm.csproj" set /a MISSING_PROJS+=1
if not exist "%ROOT_DIR%\src\IdleAutoGame.Infrastructure.Persistence\IdleAutoGame.Infrastructure.Persistence.csproj" set /a MISSING_PROJS+=1
if not exist "%ROOT_DIR%\src\IdleAutoGame.Games.TapTitans2\IdleAutoGame.Games.TapTitans2.csproj" set /a MISSING_PROJS+=1
if not exist "%ROOT_DIR%\src\IdleAutoGame.Presentation\IdleAutoGame.Presentation.csproj" set /a MISSING_PROJS+=1
if not exist "%ROOT_DIR%\tests\IdleAutoGame.Tests.Unit\IdleAutoGame.Tests.Unit.csproj" set /a MISSING_PROJS+=1

if %MISSING_PROJS% equ 0 (
    echo Projects [8/8]:   OK
) else (
    echo Projects [8/8]:   FAILED - !MISSING_PROJS! missing
    set /a ERRORS+=1
)

rem 6. Check NuGet Feed Configuration
dotnet nuget list source >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo NuGet Sources:    OK
) else (
    echo NuGet Sources:    FAILED - Cannot query NuGet sources
    set /a ERRORS+=1
)

rem 7. Check Disk Write Access
if not exist "%ROOT_DIR%\artifacts" mkdir "%ROOT_DIR%\artifacts" 2>nul
echo test > "%ROOT_DIR%\artifacts\.doctor_test" 2>nul
if exist "%ROOT_DIR%\artifacts\.doctor_test" (
    del "%ROOT_DIR%\artifacts\.doctor_test" 2>nul
    echo Disk Write:       OK
) else (
    echo Disk Write:       FAILED - Cannot write to artifacts directory
    set /a ERRORS+=1
)

echo ------------------------------------------------------
if %ERRORS% equ 0 (
    echo Environment is ready.
    exit /b 0
) else (
    echo Doctor found !ERRORS! issues. Please fix the issues before building.
    exit /b 1
)

:show_help
echo Usage: scripts\doctor.cmd [OPTIONS]
echo.
echo Diagnoses system environment, .NET SDK, project files, and NuGet reachability.
echo.
echo Options:
echo   -h, --help, /?    Show this help message and exit
exit /b 0
