# Validation — V4.2

V4.1 native and live validation is historical evidence. V4.1 had 39 native Zig passes, five skipped Unix-specific tests on Windows, 44 Python unit tests and exact reference-simulator parity across the synthetic and RGB traces. Both V4.0 and V4.1 live connections reported matching compiled/loaded graph digests and intended modes. The eight comparison runs applied 804,688,744 activations with zero missing mappings; count equality does not prove vanilla equivalence.

## Historical RC1 authoring checks
48 Python unit tests passed (14 generator + 10 fusion + 6 regions + 7 stage summary + 4 packed gates + 3 continuations + 4 release setup). 5,000 randomized compact protocol streams, 10,000 malformed lengths and seven buffer cases passed. Source audit: simulation core and all IO device files unchanged. Native Zig, Windows CMD, .NET and tModLoader not available in the authoring environment; native RC1 tests were not executed. Subsequent user-provided Windows logs confirmed RC1 native builds, all C# harnesses (host stubs), seven actual named-pipe cases, RGB parity, and accelerated gameplay. The generic session exited with a clean frontend-closed message. Both RC1 accelerated gameplay exports had matching mode, zero missing mappings, 255,008,884 applied activations total. RC2 later confirmed accelerated closure with exit code 0.

## RC2 scope
Simulation core and IO device source are unchanged. RC2 updates setup, packaging, version labels, diagnostics and documentation. The new builder test checks the sibling layout and invokes actual Windows CMD in a temporary tree to test six safe no-build cases. Windows-specific tests must run on Windows; Linux skips them.

## RC2 native acceptance (completed)
User Windows logs show 50/50 Python tests, 39 Zig passes (5 skipped), C# frontend/device tests, 18 transport responses, seven actual backend pipe cases, full RGB parity, interactive V4.1 build and live matching SHA-256 digests. Accelerated exit code 0 confirmed. See RC2 derived data in BENCHMARKS.md.

## RC3 startup scope
Fast frontend default and trace reset, plus convenience wrappers. Core, IPC and device handlers unchanged. New Python setup tests exercise discovery, explicit/custom paths, cancellation, ambiguous unattended setup, failure/no-launch and exit-code propagation with mocked processes. C# harness now checks startup/reopen defaults. Windows smoke tests subsequently passed as detailed below.

## Windows developer acceptance
From `macha` with Terraria closed:

```bat
call Validate.cmd
```

Requires .NET 8 SDK (not only runtime). Compiles generic backend and runs standalone frontend dispatch, device, named-pipe mock frontend and actual backend-pipe smoke projects. The actual backend test uses a unique pipe and `--empty`; it tests clean close, startup/shutdown message exchange, truncated header/body, bad magic, invalid request tag and request ID gap. It does not exercise world simulation. A clean pipe closure must return exit code 0; malformed data must return nonzero. Unexpected Win32 codes remain errors, not silently swallowed. `Validate.cmd` clears the generated specialization; preserve your prior binaries and rebuild AOT before live use. Report failures with full console output.

Historical RGB-specific native suite (actual dev graph; explicit prefix):

```bat
set "WWORLD=%USERPROFILE%\Documents\My Games\Terraria\tModLoader-dev\Worlds\world"
call InitializeAOT.cmd /nopause && call BenchmarkV42.cmd /nopause && call BenchmarkRGB.cmd
```

Synthetic suite clears the generated source. The final RGB suite regenerates it. Preserve the locally calibrated mask profile to reproduce V4.1 comparisons. For an arbitrary graph use the generic tests and `zig build aot-bench --release=fast -- "<prefix>.wwld" 1000 7` after building a specialization; the benchmark needs valid inputs and `.wwir/.wwio` files, not a real `.wwld` file.

## Game acceptance
Build + Reload active mod source. Test generic/reference first on a backed-up test world, then V4.0 and V4.1 with same graph, settings and same frontend mode. Verify `/ww status` before each test. Use `/ww frontend fast`, `/ww campfire legacy` only for historical RGB, `/ww trace off`, `/ww capture off`; benchmark 30 seconds at 60 and 120 nominal workload labels. Repeat in reversed order. Exit world normally; log clean closure, backend exit code and any faults. Deliberately use a mismatched specialization on a separate test copy to confirm explicit generic fallback and warning; do not edit hashes. Keep a record of mode, loaded digest, frontend mode, actual draw/update/input rates, allocations and received/applied/missing.

Additional release tests: standalone deterministic and probabilistic gates, overlapping transmutation footprints, normal and legacy campfires, multi-input gates, output order and timing against vanilla (seed/control RNG where feasible; otherwise separate statistical testing), unsupported/malformed tiles, multiplayer/server notifications and framing, repeated activation and oversized IDs, reset/sync/reopen, deliberate malformed IPC, regression fanout. Do not treat a pass on reference-simulator parity as universal vanilla proof. Modern RGB circuit not yet benchmarked. The inherited auto-start waits for the backend and the input hook intercepts HitSwitch; review behavior outside supported wiring before claiming plug-and-play use.

## Repeatable startup smoke test
On Windows, run Validate.cmd with Terraria closed, then Build + Reload the new frontend. Open a local backup world and leave it waiting; run Setup.cmd, choose the correct export and confirm V4.1 active. Do not issue frontend or trace commands; `/ww status` should already show frontend=fast, deviceFast=True, backend=v4.1 and campfire=native. For the historical RGB circuit only, enable legacy campfire mode before its first activation. Test normal playback, exit, then reopen the same world and use Launch.cmd. Confirm fast mode persists as the startup default, and clean backend exit code 0. Check multiple exported worlds, custom path, cancel, and failure paths with no stale launch. Test a different graph and verify generic fallback and warning. Repeat when the engine, tooling or environment changes.

## RC3 Windows acceptance (reported 2026-11-27)

- Full native regression script succeeded: 56 Python unit tests (including Windows builder and six quick-start tests), 39 Zig passes / 5 skips, startup and reopen defaults, 1,000 decoder checks, six frontend modes, 2,000 actual-handler sequences on stub storage (80,000 activations per path), 18 transport responses and seven real backend named-pipe cases. Host stubs are not Terraria itself.
- On the historical RGB world (17 inputs, 1,316 outputs), Setup discovered and built the correct export, automatically calibrated 283 of 3,335 candidate mask wires, matched loaded and compiled digests, and entered V4.1. Launch reused that specialization without a build. A clean exit returned 0. A failed connection before the world was opened was correctly reported.
- A second circuit with a Unicode filename (18,404 inputs, 37,193 outputs, 225,152 lamps, 92,657 gates, 92,727 wires) was discovered, calibrated (15 of 928 candidates), generated 1,024 fused ports and 5,052 plan entries, compiled, matched digests and entered V4.1. Its next session exported a different digest; Launch warned and used generic. Both sessions closed cleanly. The backend console displayed the Unicode filename with apparent encoding artifacts, but file access succeeded.
- User reports correct operation. The startup/reopen harness confirms fast defaults; the RC3 live status screenshot was not available for independent inspection. No new gameplay timing exports were supplied for RC3 or the second world.
- V4.2 promotion changes labels, version metadata, documentation and a validation alias only. The final archive has not itself been compiled in Windows; RC3 native results apply to its unchanged execution and setup code, not to a newly executed final build. Broader compatibility, multiplayer, and full vanilla parity remain unestablished.
