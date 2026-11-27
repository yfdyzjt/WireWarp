@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "SCRIPT_DIR=%~dp0"
set "NOPAUSE="
if /i "%~1"=="/nopause" set "NOPAUSE=1"
pushd "%SCRIPT_DIR%" || (
    echo ERROR: Unable to open script directory: "%SCRIPT_DIR%"
    echo(
    echo Copy the output before continuing.
    if not defined NOPAUSE pause
    exit /b 1
)
set "ZIG_VERSION="
for /f "delims=" %%V in ('zig version') do set "ZIG_VERSION=%%V"
if not "%ZIG_VERSION%"=="0.16.0" (
    echo Expected Zig 0.16.0, found "%ZIG_VERSION%".
    goto fail
)
if not exist logs mkdir logs
set "STAMP="
for /f "delims=" %%T in ('python -c "import datetime; print(datetime.datetime.now().isoformat().replace(':','-').replace('.','-'))"') do set "STAMP=%%T"
if not defined STAMP goto fail
set "LOGDIR=logs\synthetic-v4.1-%STAMP%"
mkdir "%LOGDIR%" || goto fail
python tools/test_aot_generator.py
if errorlevel 1 goto fail
python tools/test_aot_fusion.py
if errorlevel 1 goto fail
python tools/test_aot_regions.py
if errorlevel 1 goto fail
python tools/test_profile_summary.py
if errorlevel 1 goto fail
python tools/test_packed_gates.py
if errorlevel 1 goto fail
python tools/test_continuations.py
if errorlevel 1 goto fail
python tools/test_compact_protocol.py
if errorlevel 1 goto fail
python tools/aot_generate.py --clear
if errorlevel 1 goto fail
zig build test --release=fast --summary all
if errorlevel 1 goto fail
for %%F in (chain fanout hybrid mixed) do (
    call :bench %%F
    if errorlevel 1 goto fail
)
python tools/aot_generate.py --clear
if errorlevel 1 goto fail
echo All benchmarks completed. Logs are in %LOGDIR%.
echo Generated source restored to generic stub. Regenerate before another AOT build.
popd
echo(
echo Copy the console output before continuing.
if not defined NOPAUSE pause
exit /b 0
:bench
echo ==== %1 ==== 
python tools/aot_fixtures.py fixtures/%1.wwld --gates 128 --variant %1
if errorlevel 1 exit /b 1
python tools/aot_generate.py fixtures/%1.wwir --mode v4.1
if errorlevel 1 exit /b 1
zig build aot-bench --release=fast -- fixtures/%1.wwld 100000 7 > "%LOGDIR%\%1.log" 2>&1
set "BENCH_EXIT=%ERRORLEVEL%"
type "%LOGDIR%\%1.log"
if not "%BENCH_EXIT%"=="0" exit /b 1
python tools/bench_summary.py "%LOGDIR%\%1.log" --expect-v41 > "%LOGDIR%\%1-summary.log" 2>&1
set "SUMMARY_EXIT=%ERRORLEVEL%"
type "%LOGDIR%\%1-summary.log"
if not "%SUMMARY_EXIT%"=="0" exit /b 1
exit /b 0
:fail
echo Benchmark suite stopped after an error. Preserve console output and logs.
popd
echo(
echo Copy the console output before continuing.
if not defined NOPAUSE pause
exit /b 1
