#!/usr/bin/env python3
"""Independent archived/current graph differential. No timing claims."""
import argparse, collections, hashlib, json
from pathlib import Path
from aot_generate import parse, topology, load_mask_profile
from profile_regions import Model, compile_regions
from test_packed_gates import PackedModel, compare, decode
p=argparse.ArgumentParser(description=__doc__);p.add_argument('wwir',type=Path);p.add_argument('profile',type=Path);p.add_argument('--ports',nargs='+',type=int,default=[8,9,2,3]);a=p.parse_args()
d=a.wwir.read_bytes();h=hashlib.sha256(d).hexdigest();g=parse(d);t,o,n,f=topology(g);allow=load_mask_profile(a.profile,d,h);programs=compile_regions(t,f,n,2048,allow)
ref=Model(g,t,o,n,f,None);opt=PackedModel(g,t,o,n,f,programs,lazy=True,fast=True)
print(json.dumps(dict(sha256=h,gate_count=len(g[3]),plan_counts=dict(collections.Counter(decode(v)[0] for v in opt.plans)),plan_bytes=8*len(opt.plans))),flush=True)
for port in a.ports:
    checks,outputs=compare(ref,opt,port)
    print(json.dumps(dict(port=port,checks=checks,outputs=outputs,parity='PASS',packed_checks_cumulative=opt.packed_checks,fallback_checks_cumulative=opt.fallback_checks)),flush=True)
