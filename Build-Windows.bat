@echo off
setlocal EnableDelayedExpansion
set "MODVERSION=1.1.0"
cd /d "%~dp0"
echo ============================================
echo   Pscht - All in One QoL %MODVERSION% - Build
echo ============================================
echo.

where dotnet >nul 2>&1 || goto :nodotnet

rem ---------- Valheim-Ordner ----------
set "VALHEIM=%~1"
if "!VALHEIM!"=="" if exist "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll" set "VALHEIM=C:\Program Files (x86)\Steam\steamapps\common\Valheim"
if "!VALHEIM!"=="" set /p "VALHEIM=Valheim Installationsordner eingeben: "
if not exist "!VALHEIM!\valheim_Data\Managed\assembly_valheim.dll" goto :novalheim
echo Valheim:  !VALHEIM!

rem ---------- Mod-Manager-Profil (BepInEx + Jotunn) ----------
set "PROFILE=%~2"
rem Nur das Testprofil im Thunderstore Mod Manager
if "!PROFILE!"=="" set "PROFILE=%APPDATA%\Thunderstore Mod Manager\DataFolder\Valheim\profiles\TestMods"
if "!PROFILE!"=="" set /p "PROFILE=Profilordner des Mod Managers eingeben (der Ordner, in dem BepInEx liegt): "
set "BEPINEX=!PROFILE!\BepInEx\core\BepInEx.dll"
if not exist "!BEPINEX!" goto :nobepinex
echo Profil:   !PROFILE!

set "JOTUNN=!PROFILE!\BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll"
if not exist "!JOTUNN!" set /p "JOTUNN=Vollstaendigen Pfad zur Jotunn.dll eingeben: "
if not exist "!JOTUNN!" goto :nojotunn
echo Jotunn:   !JOTUNN!
echo.

rem ---------- Bauen ----------
dotnet build Pscht_QoL.csproj -c Release -p:ValheimDir="!VALHEIM!" -p:BepInExDll="!BEPINEX!" -p:JotunnDll="!JOTUNN!"
if errorlevel 1 goto :buildfail
echo.
echo DLL erstellt: bin\Release\netstandard2.1\Pscht_QoL.dll
echo.

rem ---------- Mod-Paket ----------
echo Erstelle Mod-Paket...
set "DIST=%~dp0dist"
set "PKG=!DIST!\pkg"
if exist "!DIST!" rmdir /s /q "!DIST!"
mkdir "!PKG!\plugins"
copy /y "bin\Release\netstandard2.1\Pscht_QoL.dll" "!PKG!\plugins\" >nul
copy /y manifest.json "!PKG!\" >nul
copy /y icon.png "!PKG!\" >nul
copy /y README.md "!PKG!\" >nul
copy /y CHANGELOG.md "!PKG!\" >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make_zip.ps1" -Source "!PKG!" -Target "!DIST!\Pscht_AllInOne_QoL_%MODVERSION%.zip"
if errorlevel 1 goto :pkgfail

rem Server-Paket: nur die DLL zum Hochladen nach BepInEx/plugins/Pscht_QoL/
mkdir "!DIST!\Server\BepInEx\plugins\Pscht_QoL"
copy /y "bin\Release\netstandard2.1\Pscht_QoL.dll" "!DIST!\Server\BepInEx\plugins\Pscht_QoL\" >nul

rem Direkt ins Testprofil installieren
set "INST=!PROFILE!\BepInEx\plugins\Pscht-AllInOne_QoL"
if exist "!INST!" rmdir /s /q "!INST!"
xcopy /e /i /q /y "!PKG!" "!INST!" >nul
if errorlevel 1 goto :pkgfail

echo.
echo ============================================
echo FERTIG
echo   Installiert im Testprofil: !INST!
echo   Mod-Paket:  dist\Pscht_AllInOne_QoL_%MODVERSION%.zip
echo               im Thunderstore Mod Manager: Settings - Import local mod
echo   Server:     dist\Server\BepInEx\plugins\Pscht_QoL\Pscht_QoL.dll
echo               auf den Server in denselben Ordner hochladen
echo ============================================
pause
exit /b 0

:nodotnet
echo FEHLER: .NET SDK fehlt. Download: https://dotnet.microsoft.com/download
pause & exit /b 1
:novalheim
echo FEHLER: Valheim-Ordner nicht gefunden.
echo Erwartet wird ein Ordner mit valheim_Data\Managed\assembly_valheim.dll
pause & exit /b 1
:nobepinex
echo FEHLER: BepInEx.dll nicht gefunden.
echo Erwartet: Profilordner\BepInEx\core\BepInEx.dll
pause & exit /b 1
:nojotunn
echo FEHLER: Jotunn.dll nicht gefunden.
echo Jotunn liegt im Profil unter BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll
pause & exit /b 1
:buildfail
echo.
echo BUILD FEHLGESCHLAGEN - Fehlermeldungen oben beachten.
echo Tipp: Fehlerliste kopieren und an Claude schicken.
pause & exit /b 1
:pkgfail
echo PAKET FEHLGESCHLAGEN
pause & exit /b 1
