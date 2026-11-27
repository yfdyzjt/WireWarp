#!/usr/bin/env python3
"""Synthetic WireWarp v1 fixture files for reproducible headless benchmarks.
Not Terraria worlds: they are serialized logical graphs with synthetic I/O coordinates.
"""
import argparse, hashlib, struct
from pathlib import Path
from aot_generate import MAGIC, Node

def grouped(groups, digest):
    start=44+4*(len(groups)+1); offs=[start]
    for g in groups: offs.append(offs[-1]+len(g))
    return struct.pack('<II32si',MAGIC,1,digest,len(offs))+struct.pack(f'<{len(offs)}I',*offs)+b''.join(groups)

def nodes(records):
    return struct.pack('<i',len(records))+b''.join(struct.pack('<Bii',n.kind,n.id,len(n.edges))+struct.pack(f'<{len(n.edges)}i',*n.edges) for n in records)

def fixture(n=128, variant='chain'):
    if n<1: raise ValueError('n must be positive')
    # Input IDs 0/1; two outputs; each gate owns one or more lamps.
    I=2; O=2
    if variant=='chain':
        kinds=[1]*n; lamp_specs=[[(2,i)] for i in range(n)]
    elif variant=='hybrid':
        if n<2: raise ValueError('hybrid needs at least two gates')
        kinds=[1]*n; lamp_specs=[[(2,0),(1,0)]]+[[(2,i)] for i in range(1,n)]
    elif variant=='mixed':
        kinds=[1,2,3,4,5,6,7,7]*((n+7)//8);kinds=kinds[:n]
        lamp_specs=[]
        for i,k in enumerate(kinds):
            lamp_specs.append([(1 if i%2==0 else 2,i),(2,i)] if k!=7 else ([(1,i),(3,i)] if i%8==6 else [(1,i),(2,i),(3,i)]))
    elif variant=='fanout':
        kinds=[5]*n;lamp_specs=[[(2,i),(2,i)] for i in range(n)]
    else:raise ValueError('Unknown variant')
    L=sum(map(len,lamp_specs)); lb=I+O; gb=lb+L; wb=gb+n
    lamps=[]; owner_lamps=[]
    for specs in lamp_specs:
        ids=[]
        for k,gi in specs:
            lid=lb+len(lamps);ids.append(lid);lamps.append(Node(k,lid,(gb+gi,)))
        owner_lamps.append(ids)
    # two input wires, one output wire per gate. Includes repeated targets to
    # distinct ports, multiple lamp hits, and multi-color input fanout ordering.
    wires=[]
    if variant in ('chain','hybrid'):
        wires=[Node(1,wb,(owner_lamps[0][0],)),Node(2,wb+1,(I+1,))]
        if variant=='hybrid':
            wires=[Node(1,wb,(owner_lamps[1][0],)),Node(2,wb+1,(owner_lamps[0][0],))]
        for i in range(n):wires.append(Node(1,wb+2+i,(owner_lamps[i+1][0],) if i+1<n else (I,)))
    else:
        control=tuple(ls[0] for ls in owner_lamps)
        clock=tuple(ls[-1] for ls in owner_lamps)
        wires=[Node(1,wb,control),Node(2,wb+1,clock)]
        for i in range(n):wires.append(Node(3,wb+2+i,(I+i%2,)))
    ins=[Node(0,0,(wb,)),Node(0,1,(wb+1,))]
    outs=[Node(0,I,()),Node(0,I+1,())]
    gates=[Node(k,gb+i,(wb+2+i,)) for i,k in enumerate(kinds)]
    return [ins,outs,lamps,gates,wires]

def write_fixture(path, n=128, variant='chain'):
    groups=fixture(n,variant);payload=[nodes(g) for g in groups];digest=hashlib.sha256(b''.join(payload)).digest()
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    path.with_suffix('.wwir').write_bytes(grouped(payload,digest))
    # .wwio group layout as consumed by upstream files.parseIo. We only need inputs;
    # these are not full frontend maps and must never be loaded into a game world.
    inputs=struct.pack('<i',2)+b''.join(struct.pack('<iiiB',i,0,i,8) for i in range(2))
    path.with_suffix('.wwio').write_bytes(grouped([inputs]+[struct.pack('<i',0)]*6,digest))
    return path.with_suffix('.wwir')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('path',type=Path);p.add_argument('--gates',type=int,default=128);p.add_argument('--variant',choices=['chain','mixed','fanout','hybrid'],default='chain');a=p.parse_args();print(write_fixture(a.path,a.gates,a.variant))
