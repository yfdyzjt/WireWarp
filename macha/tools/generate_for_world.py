#!/usr/bin/env python3
"""Select or calibrate a SHA-256-bound mask profile, then run the unchanged AOT generator."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from aot_generate import load_mask_profile, parse

ROOT = Path(__file__).resolve().parents[1]
PROFILE_MODES = ('v3.2', 'v3.3', 'v3.4', 'v3.5', 'v4.0', 'v4.1')



def default_trace(count):
    # Sample up to 16 evenly spaced inputs, twice each. Hints only; never
    # change reachability. Large workloads can supply an explicit trace.
    if count <= 0:
        return []
    n = min(count, 16)
    ports = [i * (count - 1) // max(1, n - 1) for i in range(n)]
    return [port for port in ports for _ in range(2)]


def choose_profile(graph, directory, trace=None):
    data = graph.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    count = len(parse(data)[0])
    if trace is not None and any(not 0 <= port < count for port in trace):
        raise ValueError("Calibration port outside graph input range")
    directory.mkdir(parents=True, exist_ok=True)
    cache = directory / ('mask-' + digest + '.json')
    candidates = [cache]
    candidates += sorted(directory.glob('*.json'))
    print(f'Graph SHA-256: {digest}', flush=True)
    for path in dict.fromkeys(candidates):
        if not path.is_file():
            continue
        try:
            profile = json.loads(path.read_text(encoding='utf-8'))
            if not isinstance(profile, dict) or profile.get('sha256') != digest:
                continue
            wires = load_mask_profile(path, data, digest)
        except (OSError, ValueError, TypeError, UnicodeError) as err:
            print(f'Ignoring invalid profile {path}: {err}', flush=True)
            continue
        print(f'Using matching profile: {path} ({len(wires)} wires)', flush=True)
        return path
    trace = default_trace(count) if trace is None else trace
    if any(not 0 <= port < count for port in trace):
        raise ValueError("Calibration port outside graph input range")
    print('No matching profile; calibrating a new hash-bound profile.', flush=True)
    # Write to a temporary file first; preserve every archived profile.
    with tempfile.TemporaryDirectory(dir=directory) as tmp:
        output = Path(tmp) / 'calibrated.json'
        if trace:
            print(f'Calibration trace: {trace}', flush=True)
            subprocess.run([sys.executable, str(ROOT/'tools'/'calibrate_mask_profile.py'),
                            str(graph), str(output), '--trace', *map(str,trace)], check=True)
        else:
            output.write_text(json.dumps(dict(sha256=digest,wires=[],trace=[]))+'\n', encoding='utf-8')
        load_mask_profile(output, data, digest)
        output.replace(cache)
    print(f'Saved matching profile: {cache}', flush=True)
    return cache


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('wwir', type=Path)
    p.add_argument('--mode', default='v4.1', choices=('node','fused','v3','v3.1',*PROFILE_MODES))
    p.add_argument('--profiles', type=Path, default=ROOT/'profiles')
    p.add_argument('--output', type=Path)
    p.add_argument('--trace', nargs='+', type=int, help='calibration ports if no matching cached profile exists')
    a = p.parse_args()
    cmd = [sys.executable, str(ROOT/'tools'/'aot_generate.py'), str(a.wwir), '--mode', a.mode]
    if a.mode in PROFILE_MODES:
        cmd += ['--mask-profile', str(choose_profile(a.wwir,a.profiles,a.trace))]
    target = a.output or ROOT/'src'/'core'/'AotGenerated.zig'
    target.parent.mkdir(parents=True, exist_ok=True)
    # Generate next to the target so successful replacement is atomic. A failed
    # generator leaves the previous source intact, but callers must stop the build.
    with tempfile.TemporaryDirectory(dir=target.parent) as tmp:
        output = Path(tmp)/target.name
        subprocess.run(cmd + ['--output', str(output)], check=True)
        output.replace(target)
    print(f'Installed generated source: {target}', flush=True)

if __name__ == '__main__':
    main()
