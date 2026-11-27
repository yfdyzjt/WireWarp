# Changelog

## V4.2 (mod 0.1.11; 2026-11-27)
- Promote the accepted RC3 startup workflow. No new execution algorithm, protocol or device changes. Update version metadata and benchmark label, add stable `Validate.cmd` entry point, retain prior validation script names.
- RC3 Windows validation: 56 Python tests passed, 39 native Zig passes and 5 skips, C# startup/reopen defaults and all standalone parity harnesses passed, 18 IPC transport cases and seven actual named-pipe cases passed.
- Setup and Launch succeeded on the historical RGB graph. A second circuit with a Unicode filename, 18,404 inputs and 37,193 outputs was automatically specialized and connected in V4.1. Reopening with a changed export correctly warned and fell back to generic; both sessions closed cleanly.
- Keep RC2 measured performance charts and RC1 historical peak; no new performance measurement is claimed for RC3 or V4.2. General-purpose setup selected a different mask subset from the historical RGB trace.
- Retain GPL-3.0-only notices and upstream credit. Publication still requires maintainer integration and outstanding rights/compatibility review.

## V4.2-RC3 (mod 0.1.10; candidate)
- Fast frontend is now the startup default; trace, capture and profiling reset off on world startup. Native campfire default and all execution modes retained. No engine, device or IPC changes.
- Add Setup.cmd and Launch.cmd: select complete stable/dev world exports or a custom prefix, build V4.1 and connect; reuse executable for unchanged graph. No benchmark in setup. Unattended operation requires explicit WWORLD or a unique pair.
- Add setup orchestration and startup/reopen regression tests, retaining validation and benchmark scripts.
- Update SVGs and anonymized CSVs with RC2 native and live measurements, including full workload medians and stage profile. Retain RC1 peak and historical comparisons.
- Windows acceptance subsequently completed; see V4.2 above. Rights review remains separate.

## V4.2-RC2 (mod 0.1.9; 2026-11-27)
- Freeze simulation and device code. Only benchmark version labels change in frontend C#; server connection failure message adds startup instructions.
- Replace silent generic default with interactive mode menu. Explicit noninteractive modes; missing or duplicate modes fail without mutating specialization.
- Integrate validated RC1 layout hotfix; `ValidateRC2.cmd` with legacy alias and isolated Windows no-build tests.
- Include full GPL-3.0 license and project notice (GPL-3.0-only selected, upstream approval reported, provenance and third-party review still pending). Retain original author metadata.
- Add contribution, security, issue/PR templates, RC1 verified derived CSVs and regenerated vector graphics. Preserve historical V4.1 data.
- RC1 native and gameplay evidence: 39 Zig passes/5 skips, 48 Python tests, C# and 7 real pipe checks passed; mixed backend 6.953x vs generic, 52.31/29.83 draw Hz at 60/120 nominal workload. Not newly measured RC2 performance.
- RC2 native acceptance subsequently passed: 50 Python tests, 39 Zig passes/5 skips, C# harnesses and 7 native pipe cases, correct interactive selection, matching live AOT graph hashes, clean exit code 0. Latest RC2 mixed 6.584x; 53.02/30.70 draw Hz at 60/120 workloads.

## V4.2-RC1 (mod 0.1.8; candidate)
- Freeze V4.1 simulation core and device implementation. Keep V4.0/V3.5 and generic paths.
- Add generic-first `BuildV42.cmd`, world-pair header check, graph-neutral calibration for new worlds and atomic installation of generated AOT source. Keep explicit RGB-specific trace in RGB scripts; preserve existing hash-bound profiles.
- Distinguish Windows `ERROR_BROKEN_PIPE` read EOF from true I/O failures; report actual Win32 errors, truncated messages, bad headers/tags, and request-ID gaps. Wire format unchanged. Subsequently passed RC1 Windows pipe tests and live generic closure.
- Snapshot backend mode and frontend graph hash at benchmark start; cancel if backend mode changes; report RC version.
- Add Windows validation entry point, four Python setup regression tests, seven actual backend named-pipe smoke cases, clean package, benchmark CSVs and SVGs.
- Preserve reference frontend startup and native campfires. No major new simulation optimizations.

## V4.1 (validated baseline)
Wave-local ordered compact wire continuations (1–3 indices, fallback for unrepresentable groups), retaining existing wave boundaries. Native/live tested. Mixed RGB median 0.608979 ms/event, 6.691× vs generic. Small fanout regression; retain V4.0.

## V4.0 and earlier
Packed immutable gate descriptors; v3.9 device specialization; v3.8 compact16 output payload; v3.7 compiled frontend and buffer reuse; v3.6 indexed frontend; v3.5 compiled short wires; v3.2 selective trigger masks; v3.1 shared lazy parity; v3 partial regions; v2 whole-root fusion. Historical results not all comparable or confirmed AOT.
