# RC3 authoring checks (historical, 2026-11-27)

This records pre-acceptance checks; subsequent Windows acceptance is documented in VALIDATION.md.

- Baseline: corrected RC2 source archive, subsequently accepted in the user's Windows logs.
- 55 Python unit tests passed, one Windows-only CMD test skipped (50 prior test cases including the skip, plus six setup tests). Tests ran on Python 3.13 in the authoring environment; Python 3.12 is the previously validated Windows requirement.
- 5,000 randomized IPC streams, 10,000 malformed-length checks, seven buffer growth/shrink checks passed.
- New quick-start tests use simulated process outcomes and temporary file pairs, not actual Windows builds. The C# startup/reopen assertion is included but has not been run natively.
- 567 RC2 raw timing records checked for seven rounds per mode, equal event/output/check counts per workload, and summarized into 81 medians. Ten aggregate stage rows and two gameplay exports derived without personal paths or graph fingerprints. Seven genuine SVGs regenerated, parsed, rendered and visually inspected.
- All backend Zig source files (including generic AotGenerated placeholder) and all 75 frontend IO source files match the corrected RC2 archive byte-for-byte. Only runtime startup default/trace reset and benchmark version label change in production C#. No new execution algorithm or protocol change.
- Native Zig, .NET, Windows CMD and tModLoader unavailable here. New setup/launch wrappers, startup/reopen harness, live default fast/native-campfire behavior and mismatch fallback need Windows acceptance. Prior RC2 results do not constitute RC3 tests.
- Publication still requires upstream permission scope, third-party rights, supported versions and final integration review.
