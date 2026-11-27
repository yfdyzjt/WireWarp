#!/usr/bin/env python3
"""Archived graph differential using the independent continuation model."""
import argparse, collections, hashlib, json
from pathlib import Path
from aot_generate import parse,topology,load_mask_profile
from profile_regions import Model,compile_regions
from test_packed_gates import compare
from test_continuations import ContinuationModel
p=argparse.ArgumentParser();p.add_argument('graph',type=Path);p.add_argument('profile',type=Path);p.add_argument('--ports',nargs='+',type=int,default=[8,9,2,3]);a=p.parse_args()
d=a.graph.read_bytes();h=hashlib.sha256(d).hexdigest();g=parse(d);t,o,n,f=topology(g);allow=load_mask_profile(a.profile,d,h)
ref=Model(g,t,o,n,f,None);opt=ContinuationModel(g,t,o,n,f,compile_regions(t,f,n,2048,allow),lazy=True,fast=True);opt.compile_continuations()
print(json.dumps(dict(sha256=h,gates=len(g[3]),plans=dict(collections.Counter(p>>62 for p in opt.continuations)),descriptor_bytes=8*len(opt.continuations))),flush=True)
for port in a.ports:
    c,y=compare(ref,opt,port);assert ref.wire_wave_order==opt.wire_wave_order
    print(json.dumps(dict(port=port,checks=c,outputs=y,parity='PASS',waves=len(opt.wave_order),continuations_cumulative=dict(opt.coverage))),flush=True)
