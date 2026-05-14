@echo off
echo ========================================
echo  AccountingETL — Uninstaller
echo ========================================
echo.

set INSTALL_DIR=%ProgramFiles%\AccountingETL

if not exist "%INSTALL_DIR%\AccountingETL.exe" (
    echo AccountingETL is not installed.
    pause
    exit /b
)

echo Stopping service...
sc stop AccountingETL 2>nul
timeout /t 3 >nul

echo Uninstalling service...
sc delete AccountingETL 2>nul

echo Removing files...
rmdir /s /q "%INSTALL_DIR%"

echo Removing Start Menu shortcut...
set SHORTCUT_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\AccountingETL
rmdir /s /q "%SHORTCUT_DIR%" 2>nul

echo.
echo ========================================
echo  Uninstall Complete!
echo ========================================
echo.
pause
