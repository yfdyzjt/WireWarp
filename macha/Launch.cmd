@echo off
rem SPDX-License-Identifier: GPL-3.0-only
setlocal EnableExtensions DisableDelayedExpansion
set "NOPAUSE="
set "RC=1"
if /i "%~1"=="/nopause" set "NOPAUSE=1"
if not "%~2"=="" goto usage
if not "%~1"=="" if not defined NOPAUSE goto usage
pushd "%~dp0" || goto finish
if not exist "zig-out\bin\macha.exe" goto missing
 echo Connecting to Terraria. The world must be waiting for WireWarp.
 echo If backend execution is generic or the graph hashes differ, exit and run Setup.cmd again.
 echo If a previous build failed, rerun Setup instead of using the old executable.
"zig-out\bin\macha.exe" serve
set "RC=%errorlevel%"
popd
goto finish
:missing
echo ERROR: No compiled backend. Open a local world and run Setup.cmd first.
popd
goto finish
:usage
echo Usage: Launch.cmd [/nopause]
set "RC=2"
:finish
if not defined NOPAUSE pause
exit /b %RC%
