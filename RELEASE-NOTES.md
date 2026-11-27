# WireWarp V4.2

Graph-specialized ahead-of-time wiring acceleration for Terraria via a tModLoader frontend and Zig backend. Built on the original WireWarp project by yfdyzjt.

## Highlights
- V4.1 AOT engine with compiled wire execution, packed gate metadata and ordered wave-local continuations; V4.0, V3.5 and generic alternatives retained.
- Fast frontend by default: optimized dispatch, reusable transport buffers, asynchronous IPC, compact16 lossless encoding where eligible, and specialized device execution.
- First-time setup via `macha/Setup.cmd`; subsequent launches via `macha/Launch.cmd`. Automatic world selection and graph-bound calibration; no benchmarks needed for normal use.
- SHA-256 mismatch warning and generic fallback, explicit backend selection, clean disconnection, optional historical campfire compatibility, native campfires by default.
- GPL-3.0-only, original attribution, regression tools, seven SVG diagrams and benchmark data.

## Measured performance
On the tested historical RGB workload, RC2 measured 6.584× backend speedup (1,000 mixed events, seven rounds, ratio of medians). Earlier RC1 peak: 6.953×. RC2 measured 53.02 and 30.70 draw-callback Hz for nominal 60 and 120 Hz workloads, respectively, compared with 37.43 and 20.15 in separate RC1 generic runs. These are workload-specific historical measurements, not universal speedups, GPU FPS, or newly measured V4.2 results. See docs/BENCHMARKS.md.

## Installation
Install compatible tModLoader, Python 3.12 and Zig 0.16.0 (on PATH). Extract the full source package, copy `WireWarp/` into active ModSources and Build + Reload. Open a local backup world; when it waits, run `macha/Setup.cmd` and select its export. On subsequent sessions run `macha/Launch.cmd` when the world waits. Rerun Setup if the export digest changes. No chat commands are necessary for ordinary use. See README.md.

## Validation and limitations
RC3 passed 56 Python tests, 39 Zig tests (5 skipped), standalone C# tests, 18 transport cases and seven native pipe cases. Setup/Launch, graph match and fallback were exercised on two distinct worlds, including a Unicode filename. Final V4.2 promotion changes only version/report labels, documentation and validation alias; the final integration smoke test remains. Compatibility outside tested Windows/tModLoader configurations, multiplayer and universal vanilla equivalence are not established. Use backup worlds. Confirm remaining publication rights and upstream integration as described in docs/RELEASE-CHECKLIST.md before posting these release notes.
