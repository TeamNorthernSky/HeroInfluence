@echo off
setlocal EnableExtensions

REM UTF-8 console output
chcp 65001 >nul

REM packageTool\build_sync_packages.bat -> Unity project root
set "TOOL_DIR=%~dp0"
set "PROJECT_ROOT=%TOOL_DIR%.."
set "SPEC_FILE=%PROJECT_ROOT%\sync_packages.spec"
set "BUILD_DIR=%TOOL_DIR%build"
set "DIST_DIR=%TOOL_DIR%dist"
set "OUTPUT_EXE=%DIST_DIR%\sync_packages.exe"

echo [Build] sync_packages.exe
echo [Build] Project root : %PROJECT_ROOT%
echo [Build] Spec file    : %SPEC_FILE%
echo [Build] Output       : %OUTPUT_EXE%
echo.

if not exist "%SPEC_FILE%" (
    echo [ERROR] Spec file not found:
    echo   %SPEC_FILE%
    echo.
    pause
    exit /b 1
)

if not exist "%TOOL_DIR%sync_packages.py" (
    echo [ERROR] sync_packages.py not found in packageTool.
    pause
    exit /b 1
)

if not exist "%TOOL_DIR%required-packages.json" (
    echo [ERROR] required-packages.json not found in packageTool.
    pause
    exit /b 1
)

set "PYINSTALLER_CMD="
where pyinstaller >nul 2>nul
if not errorlevel 1 (
    set "PYINSTALLER_CMD=pyinstaller"
) else (
    where python >nul 2>nul
    if not errorlevel 1 (
        set "PYINSTALLER_CMD=python -m PyInstaller"
    )
)

if not defined PYINSTALLER_CMD (
    echo [ERROR] PyInstaller not found.
    echo Install: python -m pip install pyinstaller
    echo.
    pause
    exit /b 1
)

echo [Build] Using: %PYINSTALLER_CMD%
echo [Build] Work path  : %BUILD_DIR%
echo [Build] Dist path  : %DIST_DIR%
echo.

if exist "%BUILD_DIR%" (
    echo [Build] Cleaning: %BUILD_DIR%
    rmdir /s /q "%BUILD_DIR%"
)

if exist "%DIST_DIR%" (
    echo [Build] Cleaning: %DIST_DIR%
    rmdir /s /q "%DIST_DIR%"
)

pushd "%PROJECT_ROOT%"
if errorlevel 1 (
    echo [ERROR] Cannot enter project root: %PROJECT_ROOT%
    pause
    exit /b 1
)

echo [Build] Running PyInstaller...
%PYINSTALLER_CMD% --distpath "%DIST_DIR%" --workpath "%BUILD_DIR%" "%SPEC_FILE%"
set "BUILD_RESULT=%ERRORLEVEL%"
popd

if not "%BUILD_RESULT%"=="0" (
    echo.
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

if not exist "%OUTPUT_EXE%" (
    echo.
    echo [ERROR] Build finished but exe not found:
    echo   %OUTPUT_EXE%
    pause
    exit /b 1
)

echo.
echo [Build] Build completed successfully.
echo [Build] Output: %OUTPUT_EXE%
echo.
echo Run from project root:
echo   packageTool\dist\sync_packages.exe --dry-run
echo.
pause
exit /b 0
