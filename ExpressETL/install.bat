@echo off
echo ========================================
echo  ExpressETL — Quick Installer
echo ========================================
echo.

set INSTALL_DIR=%ProgramFiles%\ExpressETL

echo Installing to: %INSTALL_DIR%
echo.

if exist "%INSTALL_DIR%" (
    echo Existing installation found.
    echo Stopping service if running...
    sc stop ExpressETL 2>nul
    timeout /t 2 >nul
)

echo Creating install directory...
mkdir "%INSTALL_DIR%" 2>nul

echo Copying files...
copy /Y "ExpressETL.exe" "%INSTALL_DIR%\" >nul
echo.

echo Creating Start Menu shortcut...
set SHORTCUT_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\ExpressETL
mkdir "%SHORTCUT_DIR%" 2>nul

(
echo Set oWS = WScript.CreateObject("WScript.Shell"^)
echo sLinkFile = oWS.SpecialFolders("AppData"^) ^& "\Microsoft\Windows\Start Menu\Programs\ExpressETL\ExpressETL.lnk"
echo Set oLink = oWS.CreateShortcut(sLinkFile^)
echo oLink.TargetPath = "%INSTALL_DIR%\ExpressETL.exe"
echo oLink.WorkingDirectory = "%INSTALL_DIR%"
echo oLink.Save
) > "%TEMP%\create_shortcut.vbs"
cscript //nologo "%TEMP%\create_shortcut.vbs"
del "%TEMP%\create_shortcut.vbs"

echo.
echo ========================================
echo  Installation Complete!
echo ========================================
echo.
echo  Location: %INSTALL_DIR%\ExpressETL.exe
echo.
echo  To install as Windows Service:
echo    1. Open ExpressETL
echo    2. Go to Settings
echo    3. Click "Install as Windows Service"
echo.
echo  Or run from command line:
echo    "%INSTALL_DIR%\ExpressETL.exe" /install
echo.
echo.
pause
