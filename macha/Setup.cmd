@echo off
rem SPDX-License-Identifier: GPL-3.0-only
setlocal EnableExtensions DisableDelayedExpansion
set "NOPAUSE="
set "RC=1"
if /i "%~1"=="/nopause" set "NOPAUSE=1"
if not "%~2"=="" goto usage
if not "%~1"=="" if not defined NOPAUSE goto usage
pushd "%~dp0" || goto finish
where python >nul 2>&1 || goto missing
 echo Open a LOCAL backup world in Terraria and wait for the backend connection.
 echo Setup builds V4.1 for the selected export, then connects. No benchmarks are run.
if defined NOPAUSE (
    python tools\quickstart.py --unattended
) else (
    python tools\quickstart.py
)
set "RC=%errorlevel%"
popd
goto finish
:missing
echo ERROR: Python 3.12 is required on PATH for setup. Install it and reopen CMD.
popd
goto finish
:usage
echo Usage: Setup.cmd [/nopause]
echo Unattended setup requires WWORLD or exactly one discovered export pair.
set "RC=2"
:finish
if not defined NOPAUSE pause
exit /b %RC%
