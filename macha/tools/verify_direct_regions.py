"""Untimed real graph differential check for the direct v3.3 path."""
import argparse, hashlib, json
from pathlib import Path
from aot_generate import parse, topology, load_mask_profile
from profile_regions import Model, compile_regions

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('wwir',type=Path); p.add_argument('profile',type=Path)
p.add_argument('--ports',nargs='+',type=int,default=[8,8,9,2,3,2,3,9,6,16])
a=p.parse_args(); data=a.wwir.read_bytes(); digest=hashlib.sha256(data).hexdigest()
g=parse(data); t,o,n,f=topology(g)
allow=load_mask_profile(a.profile,data,digest); programs=compile_regions(t,f,n,2048,allow)
ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,programs,lazy=True,direct=True)
print(json.dumps(dict(sha256=digest,lazy_groups=len(opt.lazy_groups),pure_lazy_groups=sum(opt.controls[w]==(0,len(t[w])) for w in opt.lazy_groups))),flush=True)
for idx,port in enumerate(a.ports):
    if not 0<=port<len(g[0]): p.error('Invalid port')
    x,r,_=ref.event(port); y,s,_=opt.event(port)
    assert x==y and r['checks']==s['checks'] and ref.wave_order==opt.wave_order
    assert ref.lamps==opt.lamps and ref.state==opt.state and ref.rng.getstate()==opt.rng.getstate()
    print(json.dumps(dict(event=idx,port=port,parity='PASS',checks=s['checks'],outputs=len(y),direct_lazy_visits=s['direct_lazy_visits'],skipped_fault_checks=s['skipped_fault_checks'],physical_toggles=s['control_toggles_executed'],lazy_commits=s['lazy_commits'])),flush=True)
