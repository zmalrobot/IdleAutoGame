@echo off
rem ============================================================================
rem IdleAutoGame - Common Windows CMD Utilities
rem ============================================================================

if "%~1"=="" goto :eof
goto %~1

:print_header
echo ======================================================
echo   %~2
echo ======================================================
echo Repository Root: %~3
goto :eof

:log_info
echo [INFO] %~2
goto :eof

:log_success
echo [SUCCESS] %~2
goto :eof

:log_warn
echo [WARN] %~2
goto :eof

:log_error
echo [ERROR] %~2
goto :eof

:check_prerequisites
set "REQ_ROOT=%~2"
set "REQ_SOLUTION=%REQ_ROOT%\IdleAutoGame.slnx"
set "REQ_PROJECT=%REQ_ROOT%\src\IdleAutoGame.Presentation\IdleAutoGame.Presentation.csproj"

echo [INFO] Checking prerequisites...

rem 1. Check dotnet CLI in PATH
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET CLI was not found in PATH.
    echo Please install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
    exit /b 1
)

rem 2. Check for .NET 10 SDK
set "FOUND_10_SDK="
for /f "tokens=*" %%a in ('dotnet --list-sdks 2^>nul') do (
    echo %%a | findstr /R "^10\." >nul 2>&1
    if !ERRORLEVEL! equ 0 set "FOUND_10_SDK=%%a"
)

if "%FOUND_10_SDK%"=="" (
    echo [ERROR] Required .NET SDK v10.x was not found.
    echo Installed SDKs:
    dotnet --list-sdks 2>nul
    echo Please install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
    exit /b 1
)

rem 3. Check Solution File
if not exist "%REQ_SOLUTION%" (
    echo [ERROR] Solution file not found: %REQ_SOLUTION%
    exit /b 1
)

rem 4. Check Presentation Project
if not exist "%REQ_PROJECT%" (
    echo [ERROR] Presentation project not found: %REQ_PROJECT%
    exit /b 1
)

echo [SUCCESS] Prerequisites check passed (Found .NET 10 SDK: %FOUND_10_SDK%).
exit /b 0
