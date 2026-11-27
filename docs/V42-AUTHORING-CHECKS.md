# V4.2 promotion checks

- RC3 distribution manifest verified before edits. All 24 Zig files (15 production source files, build file and eight test files) and 75 frontend IO C# files are byte-identical to RC3; Runtime.cs, Transport.cs, Setup.cmd, Launch.cmd and quickstart.py are unchanged. Only benchmark labels and mod version changed in frontend production files. Builder/validator messages were updated and Validate.cmd delegates to the existing validation suite.
- Python regression run: 55 passed, one Windows-only test skipped; 5,000 randomized IPC streams, 10,000 malformed-length cases and seven buffer cases passed. RC3 Windows evidence is recorded in VALIDATION.md; no new Windows native or tModLoader run is claimed.
- Seven SVG files regenerated from the same verified RC2/earlier CSVs, parsed and rendered. No new performance claims from RC3 smoke tests.
- Generic placeholder verified; no local binaries, raw world files, profile caches, or private logs. No upstream checkout modified. Review remaining rights and integration gates before publication.
