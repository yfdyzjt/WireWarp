#!/usr/bin/env python3
"""Build an optional hash-bound optimization hint; never changes circuit reachability."""
import argparse, hashlib, json
from pathlib import Path
from aot_generate import parse, topology
from profile_regions import Model, compile_regions
from generate_for_world import default_trace


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('wwir', type=Path)
    p.add_argument('output', type=Path)
    p.add_argument('--trace', nargs='+', type=int, default=None)
    a = p.parse_args()
    data = a.wwir.read_bytes()
    groups = parse(data)
    if a.trace is None: a.trace = default_trace(len(groups[0]))
    if any(not 0 <= port < len(groups[0]) for port in a.trace):
        p.error('Trace port outside graph input range')
    t,o,n,f = topology(groups)
    full = compile_regions(t,f,n)
    m = Model(groups,t,o,n,f,full,lazy=True)
    seen = set()
    for port in a.trace:
        m.event(port)
        seen.update(set(m.all_visits) & set(full))
    result = dict(sha256=hashlib.sha256(data).hexdigest(), wires=sorted(seen), trace=a.trace)
    a.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(f'{len(seen)} of {len(full)} mask candidates observed; {a.output}')

if __name__=='__main__': main()
