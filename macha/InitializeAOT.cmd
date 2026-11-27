@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "SCRIPT_DIR=%~dp0"
set "MODE=v4.1"
set "NOPAUSE="
:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="/nopause" (
    set "NOPAUSE=1"
) else (
    set "MODE=%~1"
)
shift /1
goto :parse_args
:args_done
pushd "%SCRIPT_DIR%" || (
    echo ERROR: Unable to open script directory: "%SCRIPT_DIR%"
    echo(
    echo Copy the output before continuing.
    if not defined NOPAUSE pause
    exit /b 1
)

set "STEP=Resolve world export"
call ResolveWorld.cmd || goto :failed
set "ROUNDS=7"
if /i "%MODE%"=="v3.4" set "ROUNDS=6"

echo ============================================================
echo Wire Warp AOT %MODE% - Initialization and Validation
echo ============================================================
echo World: "%WWORLD%"
echo(

set "STEP=Preflight checks"
set "PREFLIGHT_FAILED="
echo Checking required programs and files in "%CD%"...
call :require_command python
call :require_command zig
call :require_file "build.zig"
call :require_file "tools\aot_generate.py"
call :require_file "%WWORLD%.wwir"
call :require_file "%WWORLD%.wwio"
call :require_file "tools\generate_for_world.py"
call :require_file "tools\calibrate_mask_profile.py"
call :require_file "tools\test_aot_generator.py"
call :require_file "tools\test_aot_fusion.py"
call :require_file "tools\test_aot_regions.py"
call :require_file "tools\test_profile_summary.py"
call :require_file "tools\test_compact_protocol.py"
call :require_file "tools\test_packed_gates.py"
call :require_file "tools\test_continuations.py"
if defined PREFLIGHT_FAILED goto :failed
python tools/check_world.py "%WWORLD%" || goto :failed
echo [OK] All preflight checks passed.
echo(

echo [1/3] Standard validation
set "STEP=Generator tests"
python tools/test_aot_generator.py || goto :failed
set "STEP=Fusion tests"
python tools/test_aot_fusion.py || goto :failed
set "STEP=Region tests"
python tools/test_aot_regions.py || goto :failed
set "STEP=Stage-summary tests"
python tools/test_profile_summary.py || goto :failed
set "STEP=Compact IPC contract model tests"
python tools/test_compact_protocol.py || goto :failed
set "STEP=Packed gate contract model tests"
python tools/test_packed_gates.py || goto :failed
set "STEP=Continuation model tests"
python tools/test_continuations.py || goto :failed
set "STEP=Clear generated specialization"
python tools/aot_generate.py --clear || goto :failed
set "STEP=Native Zig tests"
zig build test --release=fast --summary all || goto :failed

echo(
echo [2/3] Generate RGB specialization
set "STEP=Generate RGB specialization"
python tools\generate_for_world.py "%WWORLD%.wwir" --mode "%MODE%" --trace 8 8 9 9 2 2 3 3 8 9 2 3 || goto :failed

echo(
echo [3/3] Initial native benchmark - port 8, 10 events, %ROUNDS% rounds
set "STEP=Initial native benchmark"
zig build aot-bench --release=fast -- "%WWORLD%.wwld" 10 %ROUNDS% 8 || goto :failed

if /i "%MODE%"=="v3.4" goto :smoke
if /i "%MODE%"=="v4.0" goto :smoke
if /i "%MODE%"=="v4.1" goto :smoke
if /i not "%MODE%"=="v3.5" goto :done
:smoke
set "STEP=Initial stage-profile smoke test"
zig build aot-bench --release=fast -- "%WWORLD%.wwld" 10 1 8 --profile || goto :failed
:done
echo(
echo ============================================================
echo SUCCESS: All commands completed without errors.
echo Review the parity and timing results above.
echo AOT %MODE% specialization remains enabled.
echo Generated mode is %MODE%; verify the actual backend mode when the world syncs.
echo ============================================================
popd
echo(
echo Copy the console output before continuing.
if not defined NOPAUSE pause
exit /b 0

:failed
echo(
echo ============================================================
echo ERROR: %STEP% failed. Initialization stopped.
echo Check the command output and required paths above.
if /i "%STEP%"=="Preflight checks" (
    echo No tests or generation were started. Existing specialization unchanged.
) else (
    echo Generated specialization may be cleared or partially updated.
)
echo ============================================================
popd
echo(
echo Copy the console output before continuing.
if not defined NOPAUSE pause
exit /b 1

:require_file
if exist "%~1" (
    echo [OK] File: "%~1"
    exit /b 0
)
echo [MISSING] File: "%~1"
set "PREFLIGHT_FAILED=1"
exit /b 0

:require_command
where.exe "%~1" >nul 2>&1
if not errorlevel 1 (
    echo [OK] Command: %~1
    exit /b 0
)
echo [MISSING] Command: %~1 - check PATH.
set "PREFLIGHT_FAILED=1"
exit /b 0
