@echo off
setlocal EnableExtensions DisableDelayedExpansion
title BoardStatsPlugin - Compilation uniquement

echo.
echo ============================================================
echo   BoardStatsPlugin - creation de la DLL
echo ============================================================
echo.

set "PROJECT_DIR=%~dp0"
set "INPUT_PATH=%~1"
set "MSBUILD="
set "OUTPUT_DIR=%PROJECT_DIR%dist"
set "RESULT_FILE=%TEMP%\BoardStatsPlugin_HDT_%RANDOM%_%RANDOM%.txt"
set "ERROR_FILE=%TEMP%\BoardStatsPlugin_HDT_ERROR_%RANDOM%_%RANDOM%.txt"

if not defined INPUT_PATH (
    echo Indique au choix :
    echo.
    echo - le dossier d'installation de Hearthstone Deck Tracker ;
    echo - ou un fichier HearthstoneDeckTracker.exe.
    echo.
    set /p "INPUT_PATH=Chemin : "
)

set "INPUT_PATH=%INPUT_PATH:"=%"

if not defined INPUT_PATH (
    echo.
    echo ERREUR : aucun chemin n'a ete indique.
    echo.
    pause
    exit /b 1
)

echo.
echo Recherche du veritable assembly Hearthstone Deck Tracker...
echo.

powershell.exe ^
    -NoLogo ^
    -NoProfile ^
    -ExecutionPolicy Bypass ^
    -File "%PROJECT_DIR%find_hdt_assembly.ps1" ^
    -InputPath "%INPUT_PATH%" ^
    1>"%RESULT_FILE%" 2>"%ERROR_FILE%"

if errorlevel 1 (
    echo ERREUR pendant la recherche de HearthstoneDeckTracker.exe :
    echo.
    type "%ERROR_FILE%"
    echo.
    del "%RESULT_FILE%" >nul 2>nul
    del "%ERROR_FILE%" >nul 2>nul
    pause
    exit /b 1
)

set /p "HDT_ASSEMBLY="<"%RESULT_FILE%"

del "%RESULT_FILE%" >nul 2>nul
del "%ERROR_FILE%" >nul 2>nul

if not defined HDT_ASSEMBLY (
    echo.
    echo ERREUR : aucun assembly .NET HDT n'a ete retourne.
    echo.
    pause
    exit /b 1
)

for %%F in ("%HDT_ASSEMBLY%") do set "HDT_ASSEMBLY_DIR=%%~dpF"

set "HEARTHDB_ASSEMBLY=%HDT_ASSEMBLY_DIR%HearthDb.dll"

if not exist "%HEARTHDB_ASSEMBLY%" (
    echo.
    echo ERREUR : HearthDb.dll est introuvable a cote de :
    echo %HDT_ASSEMBLY%
    echo.
    echo Fichier recherche :
    echo %HEARTHDB_ASSEMBLY%
    echo.
    pause
    exit /b 1
)

echo Assembly HDT selectionne :
echo %HDT_ASSEMBLY%
echo.
echo Assembly HearthDb selectionne :
echo %HEARTHDB_ASSEMBLY%
echo.

copy /Y "%HDT_ASSEMBLY%" "%PROJECT_DIR%lib\HearthstoneDeckTracker.exe" >nul

if errorlevel 1 (
    echo.
    echo ERREUR : impossible de copier HearthstoneDeckTracker.exe.
    echo.
    pause
    exit /b 1
)

copy /Y "%HEARTHDB_ASSEMBLY%" "%PROJECT_DIR%lib\HearthDb.dll" >nul

if errorlevel 1 (
    echo.
    echo ERREUR : impossible de copier HearthDb.dll.
    echo.
    pause
    exit /b 1
)

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if exist "%VSWHERE%" (
    for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
        if not defined MSBUILD set "MSBUILD=%%I"
    )
)

if not defined MSBUILD (
    where msbuild >nul 2>nul
    if not errorlevel 1 set "MSBUILD=msbuild"
)

if not defined MSBUILD (
    echo.
    echo ERREUR : MSBuild n'a pas ete trouve.
    echo.
    pause
    exit /b 1
)

echo Compilation en cours...
echo.

"%MSBUILD%" "%PROJECT_DIR%BoardStatsPlugin.csproj" ^
    /t:Restore,Build ^
    /p:Configuration=Release ^
    /p:Platform=x64 ^
    /m

if errorlevel 1 (
    echo.
    echo ============================================================
    echo   ERREUR DE COMPILATION
    echo ============================================================
    echo.
    pause
    exit /b 1
)

set "BUILT_DLL=%PROJECT_DIR%bin\Release\BoardStatsPlugin.dll"

if not exist "%BUILT_DLL%" (
    echo.
    echo ERREUR : BoardStatsPlugin.dll n'a pas ete generee.
    echo.
    pause
    exit /b 1
)

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

copy /Y "%BUILT_DLL%" "%OUTPUT_DIR%\BoardStatsPlugin.dll" >nul

if errorlevel 1 (
    echo.
    echo ERREUR : impossible de copier la DLL dans dist.
    echo.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   COMPILATION TERMINEE
echo ============================================================
echo.
echo DLL creee :
echo %OUTPUT_DIR%\BoardStatsPlugin.dll
echo.
echo Aucune installation n'a ete effectuee.
echo.
pause
