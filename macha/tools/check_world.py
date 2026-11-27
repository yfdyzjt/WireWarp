#!/usr/bin/env python3
"""Validate export pair headers and print graph identity (no file mutation)."""
import argparse, hashlib, struct
from pathlib import Path
from aot_generate import parse, topology, MAGIC

def check(prefix):
    w = Path(str(prefix)+'.wwir').read_bytes()
    i = Path(str(prefix)+'.wwio').read_bytes()
    for name, data in [('wwir', w), ('wwio', i)]:
        if len(data)<44 or struct.unpack_from('<II',data)!=(MAGIC,1):
            raise ValueError(f'Invalid {name} header')
    if w[8:40]!=i[8:40]:
        raise ValueError('Export pair has different embedded world hashes; regenerate both in game')
    groups=parse(w)
    topology(groups)
    print('Raw wiring SHA-256:', hashlib.sha256(w).hexdigest())
    print('Embedded world hash:', w[8:40].hex())
    print('Inputs / outputs / lamps / gates / wires:', *map(len,groups))
    return len(groups[0])

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__); p.add_argument('prefix',type=Path); a=p.parse_args()
    try: check(a.prefix)
    except (OSError, ValueError, struct.error) as e: p.exit(1, f'ERROR: {e}\n')
