# V4.2 release gates

## Technical acceptance (RC3 baseline)
- [x] V4.1 recommended, V4.0/V3.5/generic and all frontend modes retained.
- [x] RC1/RC2 native regression and measured RGB backend/gameplay evidence; RC2 interactive builder and clean accelerated exit code 0.
- [x] RC3 Windows regression: 56 Python passes, 39 Zig passes / 5 skips, C# harnesses, 18 IPC responses, seven native pipe cases, startup/reopen fast defaults.
- [x] Setup and Launch on historical RGB world, graph match, automatic calibration, reuse and clean exit.
- [x] Second graph with Unicode filename: successful discovery, calibration, specialization, live AOT, warning and generic fallback on changed digest, clean disconnect.
- [x] Source-only V4.2 promotion; preserve tested simulation, device, transport and setup code; refresh metadata and release notes.
- [x] Seven SVG charts and anonymized benchmark data (RC2 latest measurements, RC1 peak separately labeled).
- [x] GPL-3.0-only license text and upstream attribution included; original developer approval reported by maintainer.

## Maintainer checks before public push / stable tag
- [ ] Review the exact permission record and original revision; confirm rights to original frontend, backend, icons, any additional contributions, adapted game logic and dependencies. See THIRD-PARTY-NOTICES.md. GPL does not grant rights to third-party material. Preserve applicable notices.
- [ ] Reconcile this consolidated root layout with the actual upstream checkout, retaining original history and reference/shared implementations where appropriate. Inspect all additions/deletions and any existing build configurations. See GITHUB-INTEGRATION.md.
- [ ] Build + Reload and perform a short Setup/Launch smoke test from the final integrated tree (version 0.1.11); use a backup and confirm `/ww status`, native campfires and correct playback. Final package differs from tested RC3 only in labels, docs and validation alias; no fresh native build is claimed.
- [ ] State the tested tModLoader/Terraria versions and platform accurately; do not claim untested multiplayer, all devices or universal vanilla equivalence. Expand compatibility tests as appropriate.
- [ ] Review IPC local trust boundary and enable private vulnerability reporting.
- [ ] Verify clean generic generated-source placeholder, no private graph data, cached profiles, binaries, logs or proprietary reference code in Git diff. For binaries provide corresponding source, build scripts and applicable notices.
- [ ] Obtain maintainer approval, merge release branch and tag the actual merged commit `v4.2`. Do not publish the private engineering handoff.

V4.2 is the final release source package prepared for maintainer review. This archive does not itself publish anything or certify all redistribution rights.
