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
set "EXPECT="
if /i "%MODE%"=="v4.0" set "EXPECT=--expect-v40"
if /i "%MODE%"=="v4.1" set "EXPECT=--expect-v41"
if /i "%MODE%"=="v3.5" set "EXPECT=--expect-v35"
if /i "%MODE%"=="v3.4" set "ROUNDS=6"

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
call :require_file "tools\bench_summary.py"
call :require_file "tools\profile_summary.py"
if defined PREFLIGHT_FAILED goto :failed
python tools/check_world.py "%WWORLD%" || goto :failed
echo [OK] All preflight checks passed.
echo(

set "STEP=Create log directory"
set "STAMP="
for /f "delims=" %%T in ('python -c "import datetime; print(datetime.datetime.now().isoformat().replace(':','-').replace('.','-'))"') do set "STAMP=%%T"
if not defined STAMP goto :failed
set "LOGDIR=logs\rgb-%MODE%-%STAMP%"
mkdir "%LOGDIR%" || goto :failed
set "COMBINED=%LOGDIR%\RGB-Full.log"

echo ============================================================
echo Wire Warp AOT %MODE% - Full RGB Benchmarks
echo ============================================================
echo World: "%WWORLD%"
echo Logs:  "%LOGDIR%"
echo(
>"%COMBINED%" echo Wire Warp AOT %MODE% - Full RGB Benchmarks
>>"%COMBINED%" echo World: "%WWORLD%"

set "STEP=Generate RGB specialization"
echo [1/6] Generate RGB specialization
python tools\generate_for_world.py "%WWORLD%.wwir" --mode "%MODE%" --trace 8 8 9 9 2 2 3 3 8 9 2 3 >"%LOGDIR%\specialization.log" 2>&1
set "RC=%ERRORLEVEL%"
type "%LOGDIR%\specialization.log"
type "%LOGDIR%\specialization.log" >>"%COMBINED%"
if not "%RC%"=="0" goto :failed

call :bench 2 100 8 port-8 || goto :failed
call :bench 3 100 9 port-9 || goto :failed
call :bench 4 100 2 port-2 || goto :failed
call :bench 5 100 3 port-3 || goto :failed
call :bench 6 1000 "" mixed || goto :failed
if /i "%MODE%"=="v3.4" goto :profiles
if /i "%MODE%"=="v4.0" goto :profiles
if /i "%MODE%"=="v4.1" goto :profiles
if /i not "%MODE%"=="v3.5" goto :done
:profiles
call :profile 100 8 profile-port-8 || goto :failed
call :profile 1000 "" profile-mixed || goto :failed

:done
echo(
echo ============================================================
echo SUCCESS: All requested RGB benchmarks and profiling completed.
echo Combined log: "%COMBINED%"
echo AOT %MODE% specialization remains enabled.
echo ============================================================
>>"%COMBINED%" echo SUCCESS: All requested RGB benchmarks and profiling completed.
popd
echo(
echo Copy the console output before continuing.
if not defined NOPAUSE pause
exit /b 0

:bench
set "STEP=Benchmark %~4"
set "LOG=%LOGDIR%\%~4.log"
echo(
echo [%~1/6] %~4 - %~2 events, %ROUNDS% rounds
>>"%COMBINED%" echo(
>>"%COMBINED%" echo ==== %~4 - %~2 events, %ROUNDS% rounds ====
echo Running benchmark; output will be displayed when this workload finishes.
if "%~3"=="" (
    zig build aot-bench --release=fast -- "%WWORLD%.wwld" %~2 %ROUNDS% >"%LOG%" 2>&1
) else (
    zig build aot-bench --release=fast -- "%WWORLD%.wwld" %~2 %ROUNDS% %~3 >"%LOG%" 2>&1
)
set "RC=%ERRORLEVEL%"
type "%LOG%"
if not "%RC%"=="0" (
    type "%LOG%" >>"%COMBINED%"
    exit /b 1
)
set "STEP=Summarize %~4"
python tools/bench_summary.py "%LOG%" %EXPECT% >"%LOGDIR%\%~4-summary.log" 2>&1
set "RC=%ERRORLEVEL%"
type "%LOGDIR%\%~4-summary.log"
type "%LOGDIR%\%~4-summary.log" >>"%LOG%"
type "%LOG%" >>"%COMBINED%"
if not "%RC%"=="0" exit /b 1
exit /b 0

:profile
set "STEP=Stage profile %~3"
set "LOG=%LOGDIR%\%~3.log"
echo(
echo [Profiling] %~3 - %~1 events; stage comparison
>>"%COMBINED%" echo(
>>"%COMBINED%" echo ==== %~3 ====
if "%~2"=="" (
    zig build aot-bench --release=fast -- "%WWORLD%.wwld" %~1 1 --profile >"%LOG%" 2>&1
) else (
    zig build aot-bench --release=fast -- "%WWORLD%.wwld" %~1 1 %~2 --profile >"%LOG%" 2>&1
)
set "RC=%ERRORLEVEL%"
type "%LOG%"
type "%LOG%" >>"%COMBINED%"
if not "%RC%"=="0" exit /b 1
python tools/profile_summary.py "%LOG%" %EXPECT% >"%LOGDIR%\%~3-summary.log" 2>&1
set "RC=%ERRORLEVEL%"
type "%LOGDIR%\%~3-summary.log"
type "%LOGDIR%\%~3-summary.log" >>"%COMBINED%"
if not "%RC%"=="0" exit /b 1
exit /b 0

:failed
echo(
echo ============================================================
echo ERROR: %STEP% failed. RGB benchmark suite stopped.
echo Check the required files, command output and any saved logs.
if defined COMBINED echo Combined log: "%COMBINED%"
if defined COMBINED >>"%COMBINED%" echo ERROR: %STEP% failed.
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
