# Third-party notices and outstanding review

| Component | Relationship / terms | Action before publication |
|---|---|---|
| Original WireWarp and macha | Source derived from yfdyzjt/WireWarp; maintainer reports GPL-3.0 permission | Confirm covered frontend/backend, contributors, original revision and retained copyright notices. |
| Original `WireWarp/icon.png` and `icon_small.png` | Retained upstream assets | Confirm grant includes these assets, or replace with independently licensed originals. |
| tModLoader | External build/runtime dependency; upstream MIT license and separate terms of use | Review exact tested revision and terms; retain applicable notices for any copied material. Not bundled. |
| Terraria | Proprietary game/runtime | Not bundled; audit adapted device logic and do not redistribute decompiled game code or assets. Confirm GPL compatibility for the actual integration and distributed artifacts, including any exception if needed. |
| Zig 0.16.0, Python, .NET 8 | External tools/runtime; no vendored compiler/runtime packages | Review exact licenses and notices if bundling later; source archive requires users to install separately. |

The Zig package manifest declares no external packages. Standalone C# test projects use host stubs, not the game's assemblies. This is an inventory, not a legal clearance or exhaustive source-provenance determination. The full GNU GPL text is provided in LICENSE; third-party rights are not overridden by it.

References: https://github.com/tModLoader/tModLoader/blob/1.4.5/LICENSE ; https://github.com/tModLoader/tModLoader/blob/1.4.5/TERMS_OF_USE.md ; https://www.gnu.org/licenses/gpl-3.0.html .
