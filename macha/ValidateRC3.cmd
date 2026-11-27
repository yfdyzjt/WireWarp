@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "SCRIPT_DIR=%~dp0"
set "NOPAUSE="
if /i "%~1"=="/nopause" set "NOPAUSE=1"
pushd "%SCRIPT_DIR%" || goto fail_no_pop
where python >nul 2>&1 || goto fail
where zig >nul 2>&1 || goto fail
where dotnet >nul 2>&1 || goto fail
rem All standalone test projects must be adjacent to macha, with the release frontend source.
for %%P in ("..\tests\FrontendParity.csproj" "..\device-tests\DeviceParity.csproj" "..\transport-tests\TransportParity.csproj" "..\backend-pipe-tests\BackendPipeSmoke.csproj" "..\WireWarp\GlobalUsings.cs") do (
    if not exist "%%~P" (
        echo ERROR: Missing required validation file: %%~P
        echo Extract the entire V4.2 archive into one parent folder, not just macha.
        echo No generic build or AOT reset has been performed by this run.
        goto fail_layout
    )
)
echo Close Terraria and all WireWarp backends before validation.
echo This builds generic macha.exe and clears generated AOT; rebuild AOT afterwards.
for %%T in (test_aot_generator test_aot_fusion test_aot_regions test_profile_summary test_packed_gates test_continuations test_compact_protocol test_release_setup test_rc2_builder test_quickstart) do (
    python tools/%%T.py || goto fail
)
call BuildV42.cmd generic /nopause || goto fail
zig build test --release=fast --summary all || goto fail
dotnet run --project "..\tests\FrontendParity.csproj" -c Release || goto fail
dotnet run --project "..\device-tests\DeviceParity.csproj" -c Release || goto fail
dotnet run --project "..\transport-tests\TransportParity.csproj" -c Release || goto fail
dotnet run --project "..\backend-pipe-tests\BackendPipeSmoke.csproj" -c Release -- "%CD%\zig-out\bin\macha.exe" || goto fail
echo SUCCESS: V4.2 generic validation complete. Build your AOT mode before gameplay.
popd
if not defined NOPAUSE pause
exit /b 0
:fail_layout
echo ERROR: Incomplete V4.2 validation layout.
popd
if not defined NOPAUSE pause
exit /b 1
:fail
echo ERROR: V4.2 validation stopped. Preserve console output; do not use stale generated source.
popd
:fail_no_pop
if not defined NOPAUSE pause
exit /b 1
