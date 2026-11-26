@echo off
setlocal

echo ============================================
echo   Building RyanDPI (Release - net8.0-windows)
echo ============================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found. Please install .NET 8.0 SDK or newer.
    pause
    exit /b 1
)

pushd "%~dp0"

if exist "RyanDPI\bin" rmdir /s /q "RyanDPI\bin"
if exist "RyanDPI\obj" rmdir /s /q "RyanDPI\obj"

dotnet build "RyanDPI\RyanDPI.csproj" -c Release
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    popd
    pause
    exit /b 1
)

set "OUT_DIR=RyanDPI\bin\Release\net8.0-windows"
if not exist "%OUT_DIR%\RyanDPI.exe" (
    echo [ERROR] RyanDPI.exe not found in %OUT_DIR%
    popd
    pause
    exit /b 1
)

if exist "%OUT_DIR%\RyanDPI.pdb" del /q "%OUT_DIR%\RyanDPI.pdb"

set "DIST_DIR=..\dist\RyanDPI"
if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
mkdir "%DIST_DIR%"
xcopy "%OUT_DIR%\*" "%DIST_DIR%\" /E /I /Y /Q >nul

echo.
echo [OK] Build complete!
echo      Portable folder ready at: dist\RyanDPI\RyanDPI.exe
echo.

echo [INFO] Packaging portable ZIP archive...
powershell -NoProfile -Command "if (Test-Path '..\dist\RyanDPI-Portable-Windows-3.0.0.zip') { Remove-Item '..\dist\RyanDPI-Portable-Windows-3.0.0.zip' -Force }; Compress-Archive -Path '..\dist\RyanDPI\*' -DestinationPath '..\dist\RyanDPI-Portable-Windows-3.0.0.zip' -CompressionLevel Optimal"
if not errorlevel 1 (
    echo [OK] Portable ZIP created at: dist\RyanDPI-Portable-Windows-3.0.0.zip
)

set "ISCC_EXE="
for /f "delims=" %%I in ('where iscc 2^>nul') do if not defined ISCC_EXE set "ISCC_EXE=%%I"
if not defined ISCC_EXE if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC_EXE if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC_EXE if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if defined ISCC_EXE (
    echo.
    echo [INFO] Compiling Windows Installer using "%ISCC_EXE%"...
    "%ISCC_EXE%" "RyanDPI-Setup.iss"
    if errorlevel 1 (
        echo [ERROR] Installer compilation failed!
        popd
        exit /b 1
    )
    echo [OK] Installer ready at: dist\RyanDPI-Setup-Windows-3.0.0.exe
) else (
    echo [INFO] Inno Setup 6 not found. To build the Windows Setup .exe, install Inno Setup 6 and run:
    echo        iscc "src\RyanDPI-Setup.iss"
)

popd
echo.
echo Done!
endlocal
