# Benchmark provenance and interpretation

The generated charts describe the validated RC2 baseline and earlier RC1 comparison, not newly measured RC3 or V4.2. Historical V4.1 data remains separate. Source: developer-provided Windows logs and gameplay benchmark exports from the final V4.1, RC1 and RC2 comparisons. Anonymized derived CSVs contain no raw world, timestamps, user path or graph fingerprint. Raw logs remain in the private handoff. Reproduce using the historical RGB workload and matching graph-bound profile.

- `data/backend-medians.csv`: same-executable native benchmark, seven rounds per workload; mixed RGB 1,000 events, fixed hot ports 100 events, synthetic fixtures 100,000. Medians/min/max in ns/event. V4.1 0.6089788 ms vs generic 4.0748316 ms (6.691×); V4.0 0.6784445 ms (10.24% time reduction to V4.1). Mixed ranges overlap; speedup is the ratio of medians. Synthetic fanout regressed 180.99→193.41 ns/event; no universal speedup claim. V4.0 remains selectable.
- `data/gameplay-v41-comparison.csv`: eight confirmed live AOT runs, same frontend graph, fast frontend and legacy campfire mode, 30-second nominal intervals. V4.0/V4.1 60 workload: 50.37/52.21 draw Hz; 120 workload: 29.28/29.19 draw Hz. V4.0 60 run had an isolated 2.24 GB process-wide allocation spike, confounding comparison. At 120, backend wait+decode improved ~0.748→0.387 ms/update, while frontend apply was ~28.93 ms/update in V4.1. Backend compute overlaps other work. Earlier generic/unreported-mode gameplay runs are deliberately excluded. Neither draw callbacks nor nominal workload labels are true GPU FPS/input frequency measurements.
- Historical V4.1 RGB: 327,611 packed gate descriptors and 327,611 packed continuation descriptors; 14,256,906 scheduled continuations in mixed native trace, zero fallback in this workload. Other graphs can fall back. Packed singleton IPC records are 2 vs 8 bytes, not a universal 75% full-wire-volume saving. Added continuation descriptors + queue capacity ~5 MiB; no total-memory reduction claim.

RC1 evidence (historical peak):

- `data/backend-rc1-medians.csv`: 81 medians with ranges from the RC1 summary logs, nine workloads and nine modes; mixed RGB 1,000 events, seven rounds. Generic 3.9999317 ms/event, V4.0 0.6488307, V4.1 0.5752477 (6.953x versus generic; 85.62% time reduction). Same simulation engine as V4.1; differences between sessions are not new optimizations. RC1 fanout medians 194.95 vs 193.90 ns/event (V4.0 vs V4.1), reversing the earlier small regression; ranges overlap.
- `data/gameplay-rc1-comparison.csv`: four runs at nominal 60/120 workload, generic and confirmed V4.1, fast frontend, legacy campfires. Draw rates 37.43/52.31 and 20.15/29.83 respectively. Separate sessions, not paired randomized trials. At 120, wait/decode was 0.218 ms/update and apply 28.40 ms/update in AOT. No missing mappings; the two AOT runs applied 255,008,884 activations.

Rebuild SVGs using `python docs/generate_svgs.py` from the package root; generated vectors are editable, use no raster images or external fonts. Values are read from the CSVs, not manually estimated.

## Latest RC2 evidence

- `data/backend-rc2-medians.csv`: 81 native medians/min/max computed directly from 567 timing records (nine workloads × nine modes × seven rounds); identical event, output and gate-check counts within each workload. Mixed: 4.1337765 ms generic, 0.6563828 ms V4.0, 0.6278148 ms V4.1 (6.584× vs generic, 84.81% less time; 4.55% lower time than V4.0). RC1 peak 6.953× is separately labeled. No execution engine changes between these versions.
- `data/backend-rc2-stages.csv`: ten aggregate rows (mixed and port 8, five instrumented modes); raw phase ns, not isolated operation costs, clock overhead not subtracted. V4.1 mixed 291.348 ms wire, 257.268 ms gate, 22.171 ms flush over 1,000 events.
- `data/gameplay-rc2.csv`: two 30-second nominal tests with V4.1/fast frontend and historical legacy campfires, 53.01567 and 30.70144 draw Hz; 260,916,782 applied, zero missing, 2,514 compact frames, zero RLE. At 120, wait/decode 0.284 ms/update and apply 27.636 ms/update. At 60, one Gen2 collection occurred; no claim of zero collections. Latest draw rates compared with RC1 generic are cross-session observations, not randomized A/B trials.
- Both compiled/loaded raw digests matched and clean accelerated exit code 0 was confirmed. The benchmark data is anonymized; no graph fingerprint or personal file paths are published.

### Engineering improvements

- RC2 native RGB logs show 2,936,876 original mask blocks and 40,537 retained (2,896,339 avoided, 98.62% of mask storage; not total RAM).
- Compact singleton encoding is 2 vs 8 bytes per record (75% smaller; not including envelope and fallback). V4.1 continuation queue records are 8 vs 16 bytes on x64 for eligible groups, not a claim of halving total memory.
- Latest RGB mixed trace: 14,256,906 packed continuations, zero continuation fallback; other graphs may fall back.
- Earlier frontend-only milestones are not claimed as controlled active-AOT comparisons. The RC3 change enables the same previously tested fast mode by default; it does not itself introduce a new acceleration algorithm.

## V4.2 promotion

RC3 native setup/launch acceptance passed on two graphs, including a Unicode filename and live digest-mismatch fallback. These were functional tests, not new benchmark exports. The general-purpose calibration selected 283 masks on the historical RGB graph instead of the 299 used by the historical benchmark trace. Do not infer identical performance for a different graph/profile. V4.2 retains the same engine and all seven SVG charts continue to show measured RC2 and earlier data.
