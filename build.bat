@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "CONFIGURATION=%~1"
if "%CONFIGURATION%"=="" set "CONFIGURATION=Release"

set "PLATFORM=Any CPU"
set "SOLUTION=GabelstaplerKameraMonitor.sln"
set "EXE=bin\%CONFIGURATION%\GabelstaplerKameraMonitor.exe"
set "SIGN_CERT=certificates\gabelstapler-camera.pfx"
set "SIGN_TIMESTAMP=http://timestamp.digicert.com"

echo Building %SOLUTION% [%CONFIGURATION%]...

call :FindMSBuild
if not defined MSBUILD_EXE (
    echo MSBuild was not found.
    echo Install Visual Studio 2022 or Visual Studio Build Tools 2022 with .NET desktop build tools.
    exit /b 1
)

"%MSBUILD_EXE%" "%SOLUTION%" /m /t:Build /p:Configuration="%CONFIGURATION%" /p:Platform="%PLATFORM%"
if errorlevel 1 exit /b 1

if /I "%CONFIGURATION%"=="Release" (
    if exist "%SIGN_CERT%" (
        call :FindSignTool
        if defined SIGNTOOL_EXE (
            if "%SIGN_CERT_PASSWORD%"=="" (
                echo Signing certificate found, but SIGN_CERT_PASSWORD is not set.
                echo The EXE was built but not signed.
            ) else (
                echo Signing %EXE%...
                "%SIGNTOOL_EXE%" sign /f "%SIGN_CERT%" /p "%SIGN_CERT_PASSWORD%" /tr "%SIGN_TIMESTAMP%" /td sha256 /fd sha256 "%EXE%"
                if errorlevel 1 exit /b 1
            )
        ) else (
            echo signtool.exe was not found. The EXE was built but not signed.
        )
    ) else (
        echo No code-signing certificate found at "%SIGN_CERT%". The EXE was built unsigned.
    )
)

echo Done.
exit /b 0

:FindMSBuild
where msbuild >nul 2>nul
if not errorlevel 1 (
    for /f "delims=" %%I in ('where msbuild') do (
        set "MSBUILD_EXE=%%I"
        exit /b 0
    )
)

if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq tokens=*" %%I in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\Current\Bin\MSBuild.exe`) do (
        set "MSBUILD_EXE=%%I"
        exit /b 0
    )
)

if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_EXE=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    exit /b 0
)

if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_EXE=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    exit /b 0
)

if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_EXE=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    exit /b 0
)

if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_EXE=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    exit /b 0
)

exit /b 0

:FindSignTool
where signtool >nul 2>nul
if not errorlevel 1 (
    for /f "delims=" %%I in ('where signtool') do (
        set "SIGNTOOL_EXE=%%I"
        exit /b 0
    )
)

for /f "delims=" %%I in ('dir /b /s "%ProgramFiles(x86)%\Windows Kits\10\bin\*\x64\signtool.exe" 2^>nul') do (
    set "SIGNTOOL_EXE=%%I"
    exit /b 0
)

exit /b 0
