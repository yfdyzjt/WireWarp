#!/usr/bin/env python3
"""Summarize generic/AOT/v2/v3 paired CSV benchmark lines."""
import argparse, csv, statistics
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__); p.add_argument('log',type=Path); p.add_argument('--expect-v35',action='store_true'); p.add_argument('--expect-v40',action='store_true'); p.add_argument('--expect-v41',action='store_true'); a=p.parse_args()
b=a.log.read_bytes(); s=b.decode('utf-16' if b[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig',errors='replace')
rows=[r for r in csv.reader(s.splitlines()) if len(r)==7 and r[0] in ('generic','aot','v2','v3','v3.1','v3.2','v3.3','v3.5','v4.0','v4.1')]
groups={}
for m,r,n,t,ns,o,c in rows:
    r,n,t,o,c=map(int,(r,n,t,o,c)); group=groups.setdefault(m,{})
    if n<=0 or t<=0 or r in group: raise SystemExit('Invalid or duplicate measurement')
    group[r]=(n,t,o,c)
expected={'generic','v2','v3','v3.1','v3.2','v3.3','v3.5'} if 'v3.5' in groups else {'generic','v2','v3','v3.1','v3.2','v3.3'} if 'v3.3' in groups else {'generic','v2','v3','v3.1','v3.2'} if 'v3.2' in groups else {'generic','v2','v3','v3.1'} if 'v3.1' in groups else {'generic','v2','v3'} if 'v3' in groups or 'v2' in groups else {'generic','aot'}
if 'v4.0' in groups:
    if 'v3.5' not in groups: raise SystemExit('Missing v3.5 baseline for v4.0')
    expected.add('v4.0')
if 'v4.1' in groups:
    if 'v4.0' not in groups: raise SystemExit('Missing v4.0 baseline for v4.1')
    expected.add('v4.1')
    if any(s.count(x) != 1 for x in ('PARITY MODE: v4.1','V4.1 RESET PASS','V4.1 CONTINUATION COVERAGE:','V4.1 GATE COVERAGE:')): raise SystemExit('Missing v4.1 coverage or parity markers')
if a.expect_v41 and 'v4.1' not in groups: raise SystemExit('Missing v4.1 comparison mode')
if a.expect_v40 and 'v4.0' not in groups: raise SystemExit('Missing v4.0 comparison mode')
if 'v4.0' in groups and (s.count('PARITY MODE: v4.0') != 1 or s.count('V4.0 RESET PASS') != 1 or s.count('V4.0 GATE COVERAGE:') != 1): raise SystemExit('Missing v4.0 coverage or parity markers')
if a.expect_v35 and 'v3.5' not in groups: raise SystemExit('Missing v3.5 comparison mode')
if set(groups)!=expected or not groups['generic']: raise SystemExit('Missing benchmark modes')
if s.count('PARITY PASS:') < (8 if 'v4.1' in groups else 7 if 'v4.0' in groups else 6 if 'v3.5' in groups else 5 if 'v3.3' in groups else 4 if 'v3.2' in groups else 3 if 'v3.1' in groups else 2 if 'v3' in groups else 1): raise SystemExit('Missing parity pass marker(s)')
for m,g in groups.items():
    if g.keys()!=groups['generic'].keys(): raise SystemExit('No complete paired rounds')
    for i,x in groups['generic'].items():
        y=g[i]
        if (x[0],x[2],x[3])!=(y[0],y[2],y[3]): raise SystemExit(f'Workload mismatch: {m} round {i}')
med={m:statistics.median(x[1]/x[0] for x in v.values()) for m,v in groups.items()}
for m in ('generic','aot','v2','v3','v3.1','v3.2','v3.3','v3.5','v4.0','v4.1'):
    if m not in groups: continue
    values=[x[1]/x[0] for x in groups[m].values()]
    print(f'{m:7} median {med[m]:12.2f} ns/event; min {min(values):.2f}; max {max(values):.2f}; vs generic {med["generic"]/med[m]:.3f}x')
if 'v3' in med: print(f'v3 vs v2: {med["v2"]/med["v3"]:.3f}x (ratio of medians)')
if 'v3.1' in med: print(f'v3.1 vs v3: {med["v3"]/med["v3.1"]:.3f}x (ratio of medians)')
if 'v3.2' in med: print(f'v3.2 vs v3.1: {med["v3.1"]/med["v3.2"]:.3f}x (ratio of medians)')
if 'v3.3' in med: print(f'v3.3 vs v3.2: {med["v3.2"]/med["v3.3"]:.3f}x (ratio of medians)')
if 'v3.5' in med: print(f'v3.5 vs v3.2: {med["v3.2"]/med["v3.5"]:.3f}x (ratio of medians)')
if 'v4.0' in med: print(f'v4.0 vs v3.5: {med["v3.5"]/med["v4.0"]:.3f}x (ratio of medians)')
if 'v4.1' in med: print(f'v4.1 vs v4.0: {med["v4.0"]/med["v4.1"]:.3f}x (ratio of medians)')
print(f'Paired rounds: {len(groups["generic"])}')
