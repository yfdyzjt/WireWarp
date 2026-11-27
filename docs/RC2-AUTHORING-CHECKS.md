# RC2 authoring checks (historical, 2026-11-27)

- 49 Python unit tests passed, one Windows-only test skipped (48 previous tests + layout check; Windows test groups six no-build cases).
- 5,000 randomized compact IPC streams, 10,000 malformed-length checks and seven buffer cases passed.
- All 75 IO device files and seven backend simulation core files are byte-for-byte identical to RC1. All explicit C# compile includes resolve. Only C# benchmark version labels changed; backend connection diagnostic text changed, not protocol handling.
- Three SVG files regenerated from verified RC1 derived CSVs and rendered. License text copied verbatim from system GPL-3 license document. Source-only archive excludes private graph exports, logs, cached profiles and binaries.
- No Zig, .NET or Windows CMD compiler/runtime is available here; native RC2, interactive menu, Windows no-build path and game acceptance remain pending. RC1 native and live evidence is not claimed as RC2 testing. Subsequent RC2 Windows acceptance is recorded in VALIDATION.md.
