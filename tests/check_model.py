"""Independent ordered-dispatch model; no Terraria or C# execution."""
import random, itertools, struct, sys, hashlib
from pathlib import Path

def simulate(mapping,runs,fast,stop=-1,fail=-1):
    rng=random.Random(55); calls=[]; missing=[]; applied=0; active=True; state=0
    # Index policy matches production, holes are distinct from valid null handlers.
    index=None
    if mapping and min(mapping)>=0 and max(mapping)<1_000_000 and max(mapping)+1<=4*len(mapping):
        index=[None]*(max(mapping)+1)
        for k,v in mapping.items(): index[k]=v
    def lookup(k):
        return mapping.get(k) if index is None else index[k] if 0<=k<len(index) else None
    def hit(k,entry):
        nonlocal active,applied,state
        if not active:return
        if entry is None:missing.append(k);return
        if entry[0]!='null':
            if len(calls)==fail:raise RuntimeError('intentional')
            state=(state*31+k+entry[1]+entry[2]+rng.randrange(100))&0xffffffff
            calls.append((k,entry,state))
            if len(calls)==stop:active=False
        applied+=1
    error=False
    try:
        if fast:
            for k,n in runs:
                if n<=0 or not active:continue
                entry=lookup(k)
                for _ in range(n):hit(k,entry if entry is not None else mapping.get(k))
        else:
            for k,n in runs:
                for _ in range(n):hit(k,mapping.get(k))
    except RuntimeError:error=True
    return calls,missing,applied,state,rng.getstate(),error,active

def test():
    rng=random.Random(12345)
    for i in range(2000):
        keys=[0,2,3,6] if i%2 else [-3,0,1_000_000]
        m={k:('null' if k==3 else 'stateful',rng.randrange(10),rng.randrange(10)) for k in keys}
        runs=[(rng.choice(keys+[-2,1,7]),rng.randrange(-1,15)) for _ in range(rng.randrange(1,40))]
        stop=rng.choice([-1,-1,3,11]);fail=rng.choice([-1,-1,2,9])
        assert simulate(m,runs,False,stop,fail)==simulate(m,runs,True,stop,fail)
    print('PASS: 2000 independent model comparisons; sparse/dense, missing/null, duplicate runs, order, state, RNG, partial failure and stop.')
    if len(sys.argv)>1:
        data=Path(sys.argv[1]).read_bytes(); assert struct.unpack_from('<II',data)==(0xBADBEEF,1)
        ng,*starts=struct.unpack_from('<9I',data,40); assert ng==8
        off=starts[1]; n,=struct.unpack_from('<I',data,off); off+=4
        mapping={}
        for _ in range(n):
            k,x,y,t=struct.unpack_from('<iiiB',data,off); off+=13; mapping[k]=(str(t),x,y)
        assert off==starts[2] and len(mapping)==n
        print(f'Actual WWIO: sha256={hashlib.sha256(data).hexdigest()}, outputs={n}, min_id={min(mapping)}, max_id={max(mapping)}, dense={min(mapping)>=0 and max(mapping)+1<=4*n}')
        if len(sys.argv)>2:
            sys.path.insert(0,sys.argv[3]); from aot_generate import parse,topology; from profile_regions import Model
            g=parse(Path(sys.argv[2]).read_bytes()); t,o,n,f=topology(g); model=Model(g,t,o,n,f,None)
            for port in [8,9,2,3]:
                ids,_,_=model.event(port); runs=[(k,sum(1 for _ in group)) for k,group in itertools.groupby(ids)]
                assert all(k in mapping for k in ids)
                assert list(itertools.chain.from_iterable(itertools.repeat(k,n) for k,n in runs))==ids
                print(f'Actual ordered output trace: port={port} activations={len(ids)} runs={len(runs)} mean_run={len(ids)/max(len(runs),1):.3f}; mapping and expansion PASS',flush=True)
if __name__=='__main__':test()
