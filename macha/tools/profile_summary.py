#!/usr/bin/env python3
"""Validate stage-profile CSV and report inclusive stage costs, not corrected timings."""
import csv
import sys
from pathlib import Path

STAGES = ('clear', 'dispatch/fused', 'wire traversal', 'wave flush', 'gate phase', 'commit')
MODES = ('v3.2', 'v3.3', 'v3.5', 'v4.0', 'v4.1')

def summarize(text, expect_v35=False, expect_v40=False, expect_v41=False):
    rows = list(csv.reader(text.splitlines()))
    modes = MODES if any(r[:2] == ['profile_meta', 'v4.1'] for r in rows) else MODES[:4] if any(r[:2] == ['profile_meta', 'v4.0'] for r in rows) else MODES[:3] if any(r[:2] == ['profile_meta', 'v3.5'] for r in rows) else MODES[:2]
    if expect_v35 and 'v3.5' not in modes: raise ValueError('Missing v3.5 profile')
    if expect_v40 and 'v4.0' not in modes: raise ValueError('Missing v4.0 profile')
    if expect_v41 and 'v4.1' not in modes: raise ValueError('Missing v4.1 profile')
    if 'v4.1' in modes and text.count('V4.1 RESET PASS') != 1: raise ValueError('Missing v4.1 reset parity')
    if 'v4.0' in modes and text.count('V4.0 RESET PASS') != 1: raise ValueError('Missing v4.0 reset parity')
    meta, totals, ports = {}, {}, {m: {} for m in modes}
    for r in rows:
        if not r or r[0] not in ('profile_meta', 'stage') or r[:2] == ['stage', 'mode']:
            continue
        m = r[1]
        if m not in modes:
            raise ValueError('Unknown profiling mode')
        if r[0] == 'profile_meta':
            if len(r) != 10 or m in meta:
                raise ValueError('Malformed or duplicate metadata')
            meta[m] = tuple(map(int, r[2:]))
        else:
            if len(r) != 14:
                raise ValueError('Malformed stage row')
            data = tuple(map(int, r[3:]))
            if r[2] == 'all':
                if m in totals: raise ValueError('Duplicate totals')
                totals[m] = data
            else:
                port = int(r[2])
                if port in ports[m] or port < 0: raise ValueError('Duplicate/invalid port')
                ports[m][port] = data
    if set(meta) != set(modes) or set(totals) != set(modes) or text.count('PARITY PASS:') != len(modes):
        raise ValueError('Incomplete profiled parity or stage data')
    if any(meta[m][:3] != meta['v3.2'][:3] for m in modes): raise ValueError('Workload differs')
    for m in modes:
        if text.count('STAGE PROFILE PASS: '+m) != 1: raise ValueError('Missing profile pass')
        events, outputs, checks, base, elapsed, reads, clock, maximum = meta[m]
        t = totals[m]
        if events <= 0 or base <= 0 or elapsed <= 0 or any(x < 0 for x in (*meta[m], *t)):
            raise ValueError('Invalid timing or counts')
        if t[0] != events or t[3] != reads or t[-1] != maximum:
            raise ValueError('Metadata mismatch')
        if t[2] > events or reads != 4*events - t[2] + 3*t[1]:
            raise ValueError('Clock read accounting mismatch')
        if not ports[m] or any(p[0] <= 0 or any(x < 0 for x in p) for p in ports[m].values()):
            raise ValueError('Invalid port rows')
        if tuple(sum(p[i] for p in ports[m].values()) for i in range(10)) != t[:-1] or max(p[-1] for p in ports[m].values()) != maximum:
            raise ValueError('Per-port totals mismatch')
        accounted = sum(t[4:10])
        if not 0 < accounted <= elapsed: raise ValueError('Invalid accounted elapsed time')
        print(f'\n{m}: {events} events, {t[1]} waves, {t[2]} fused; outputs={outputs}, checks={checks}')
        print(f'  Uninstrumented: {base/events/1e6:.6f} ms/event; instrumented: {elapsed/events/1e6:.6f} ms/event; ratio {elapsed/base:.3f}x')
        print(f'  Clock reads: {reads}; adjacent clock interval: {clock/10000:.1f} ns (not subtracted)')
        for name, n in zip(STAGES,t[4:10]):
            print(f'  {name:17} {n/1e6:12.3f} ms total  {n/accounted:7.2%} of accounted time')
        print(f'  Unaccounted outside stage boundaries: {(elapsed-accounted)/1e6:.3f} ms; maximum instrumented event: {maximum/1e6:.3f} ms')
    if any({k:v[:3] for k,v in ports['v3.2'].items()} != {k:v[:3] for k,v in ports[m].items()} for m in modes):
        raise ValueError('Per-port workload shape differs')
    print('\nPROFILE SUMMARY PASS (raw instrumented phase timings; not isolated sub-operation timings).')

if __name__ == '__main__':
    try:
        summarize(Path(sys.argv[1]).read_text(encoding='utf-8-sig',errors='replace'), '--expect-v35' in sys.argv[2:], '--expect-v40' in sys.argv[2:], '--expect-v41' in sys.argv[2:])
    except (IndexError, ValueError, OSError) as e:
        raise SystemExit(f'PROFILE SUMMARY ERROR: {e}')
