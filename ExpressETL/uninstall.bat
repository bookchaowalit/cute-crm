@echo off
echo ========================================
echo  ExpressETL — Uninstaller
echo ========================================
echo.

set INSTALL_DIR=%ProgramFiles%\ExpressETL

if not exist "%INSTALL_DIR%\ExpressETL.exe" (
    echo ExpressETL is not installed.
    pause
    exit /b
)

echo Stopping service...
sc stop ExpressETL 2>nul
timeout /t 3 >nul

echo Uninstalling service...
sc delete ExpressETL 2>nul

echo Removing files...
rmdir /s /q "%INSTALL_DIR%"

echo Removing Start Menu shortcut...
set SHORTCUT_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\ExpressETL
rmdir /s /q "%SHORTCUT_DIR%" 2>nul

echo.
echo ========================================
echo  Uninstall Complete!
echo ========================================
echo.
pause
