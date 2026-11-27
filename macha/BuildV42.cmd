@echo off
rem SPDX-License-Identifier: GPL-3.0-only
rem Modified 2026-11-27: explicit mode selection and safe unattended usage.
setlocal EnableExtensions DisableDelayedExpansion
set "SCRIPT_DIR=%~dp0"
set "MODE="
set "NOPAUSE="
set "HELP="
set "BADARGS="
:args
if "%~1"=="" goto start
if /i "%~1"=="/nopause" (
    set "NOPAUSE=1"
) else if /i "%~1"=="/?" (
    set "HELP=1"
) else if /i "%~1"=="--help" (
    set "HELP=1"
) else (
    if defined MODE set "BADARGS=1"
    set "MODE=%~1"
)
shift /1
goto args
:start
if defined BADARGS goto bad_usage
if defined HELP goto help
if defined MODE goto check_mode
if defined NOPAUSE goto bad_usage
 echo ============================================================
 echo Wire Warp V4.2 - Backend Builder
 echo ============================================================
 echo 1. V4.1    - Recommended AOT acceleration
 echo 2. V4.0    - Packed gate acceleration
 echo 3. V3.5    - Compiled wire acceleration
 echo 4. Generic - No graph specialization; clears generated AOT
 echo 5. Exit without building
 echo.
choice /c 12345 /n /m "Select backend [1-5]: "
set "SELECTED=%errorlevel%"
if "%SELECTED%"=="5" goto cancelled
if "%SELECTED%"=="0" goto cancelled
if "%SELECTED%"=="1" set "MODE=v4.1"
if "%SELECTED%"=="2" set "MODE=v4.0"
if "%SELECTED%"=="3" set "MODE=v3.5"
if "%SELECTED%"=="4" set "MODE=generic"
if not defined MODE goto bad_usage
:check_mode
for %%M in (generic v3.5 v4.0 v4.1) do if /i "%MODE%"=="%%M" set "MODE=%%M"
if "%MODE%"=="generic" goto start_build
if "%MODE%"=="v3.5" goto start_build
if "%MODE%"=="v4.0" goto start_build
if "%MODE%"=="v4.1" goto start_build
goto bad_usage
:start_build
pushd "%SCRIPT_DIR%" || goto fail_no_pop
where python >nul 2>&1 || goto fail
where zig >nul 2>&1 || goto fail
set "ZIG_VERSION="
for /f "delims=" %%V in ('zig version') do set "ZIG_VERSION=%%V"
if not "%ZIG_VERSION%"=="0.16.0" (
    echo ERROR: V4.2 targets Zig 0.16.0; found "%ZIG_VERSION%".
    goto fail
)
echo Requested backend: %MODE%
if "%MODE%"=="generic" goto generic
goto aot
:generic
echo WARNING: Explicit generic build clears generated AOT and replaces macha.exe.
python tools/aot_generate.py --clear || goto fail
goto build
:aot
call ResolveWorld.cmd || goto fail
python tools/check_world.py "%WWORLD%" || goto fail
python tools/generate_for_world.py "%WWORLD%.wwir" --mode "%MODE%" || goto fail
:build
zig build --release=fast || goto fail
echo SUCCESS: Built %MODE% in zig-out\bin\macha.exe. Check actual mode at sync.
if "%MODE%"=="generic" echo AOT is disabled. Use BuildV42.cmd v4.1 to specialize.
popd
if not defined NOPAUSE pause
exit /b 0
:cancelled
echo Cancelled. No build or AOT reset performed.
exit /b 0
:help
call :usage
if not defined NOPAUSE pause
exit /b 0
:bad_usage
echo ERROR: Specify exactly one valid mode for unattended builds.
call :usage
echo No build or AOT reset performed.
if not defined NOPAUSE pause
exit /b 2
:usage
echo Usage: BuildV42.cmd [generic ^| v3.5 ^| v4.0 ^| v4.1] [/nopause]
echo Without arguments, an interactive selection is displayed.
echo Recommended: BuildV42.cmd v4.1
exit /b 0
:fail
echo ERROR: Build stopped. Generated source may have changed; do not use a stale executable.
popd
:fail_no_pop
if not defined NOPAUSE pause
exit /b 1
