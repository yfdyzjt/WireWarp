#!/usr/bin/env python3
"""Compare the reference and pruned Python models on calibration and holdout traces."""
import argparse, hashlib, json
from pathlib import Path
from aot_generate import parse, topology, load_mask_profile
from profile_regions import Model, compile_regions


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('wwir',type=Path)
    p.add_argument('profile',type=Path)
    p.add_argument('--mask-cap',type=int,default=2048)
    p.add_argument('--holdout',nargs='+',type=int,default=[9,9,3,3,2,8,1,6,5,9,8,2,3,0,16])
    a=p.parse_args()
    data=a.wwir.read_bytes(); h=hashlib.sha256(data).hexdigest(); g=parse(data); t,o,n,f=topology(g)
    allow=set(load_mask_profile(a.profile,data,h)); full=compile_regions(t,f,n); pruned=compile_regions(t,f,n,cap=a.mask_cap,allow=allow)
    print(json.dumps(dict(sha256=h,profile_wires=len(allow),full_regions=len(full),kept_regions=len(pruned),full_blocks=sum(len(b) for _,_,b in full.values()),kept_blocks=sum(len(b) for _,_,b in pruned.values()),full_entries=sum(e-s for s,e,_ in full.values()),kept_entries=sum(e-s for s,e,_ in pruned.values()))),flush=True)
    for label,ports in [('calibration',json.loads(a.profile.read_text())['trace']),('holdout',a.holdout)]:
        if any(not 0<=port<len(g[0]) for port in ports): p.error('Trace port outside graph input range')
        ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,pruned,True)
        for idx,port in enumerate(ports):
            x,r,_=ref.event(port); y,s,_=opt.event(port)
            assert x==y and ref.lamps==opt.lamps and ref.state==opt.state and r['checks']==s['checks'] and ref.wave_order==opt.wave_order and ref.rng.getstate()==opt.rng.getstate()
            skipped={w:v for w,v in opt.all_visits.items() if w in full and w not in pruned}
            print(json.dumps(dict(trace=label,event=idx,port=port,parity='PASS',checks=s['checks'],outputs=len(y),pruned_visits=sum(skipped.values()),pruned_triggers=sum(v*(full[w][1]-full[w][0]) for w,v in skipped.items()))),flush=True)

if __name__=='__main__':main()
