#!/usr/bin/env python3
"""Independent V4.1 descriptor and wave-queue differential, not native Zig."""
import random, unittest, collections
from aot_generate import topology
from profile_regions import Model, compile_regions
from test_aot_regions import fixture
from test_packed_gates import PackedModel, compare

MAX=(1<<20)-1

def encode(gi,wires):
    assert 0<=gi<(1<<60)
    if not 1<=len(wires)<=3 or any(not 0<=w<=MAX for w in wires): return gi
    return (len(wires)<<62) | sum(w<<(20*i) for i,w in enumerate(wires))

def decode(p,fan):
    n=p>>62
    return tuple((p>>(20*i))&MAX for i in range(n)) if n else tuple(fan[p&((1<<60)-1)])

class ContinuationModel(PackedModel):
    def compile_continuations(self):
        self.continuations=[encode(i,ws) for i,ws in enumerate(self.fan)]
        self.coverage=collections.Counter()
    def append_continuation(self,queue,gi):
        p=self.continuations[gi]; queue.append(p);self.coverage[p>>62]+=1
    def expand_continuations(self,queue):
        return [w for p in queue for w in decode(p,self.fan)]

class ContinuationTests(unittest.TestCase):
    def test_boundaries(self):
        fan=[(),(0,),(MAX,MAX),(3,2,1),(MAX+1,), (1,2,3,4)]
        self.assertEqual([encode(i,ws)>>62 for i,ws in enumerate(fan)],[0,1,2,3,0,0])
        for i,ws in enumerate(fan): self.assertEqual(decode(encode(i,ws),fan),ws)
    def test_random_queues(self):
        rng=random.Random(41)
        for _ in range(10000):
            fan=[tuple(rng.choice([0,MAX,MAX+1,rng.randrange(MAX+1)]) for _ in range(rng.randrange(8))) for j in range(20)]
            gates=[rng.randrange(len(fan)) for j in range(rng.randrange(100))]
            queue=[encode(i,fan[i]) for i in gates]
            # Group equality, not just flattening, includes empty groups, duplicates and order.
            self.assertEqual([decode(p,fan) for p in queue],[fan[i] for i in gates])
    def test_state_dependent_feedback(self):
        g=fixture();t,o,n,f=topology(g)
        t[0]=[(k,j) for k,j in t[0] if o[j]!=70]
        t[4]=[(0,n[71][0]),(0,n[71][0]),(1,n[0][0]),(1,n[0][0]),(2,0),(2,0)]
        t[2]=[(2,70),(2,70)]
        t[5]=[(2,1),(1,n[1][0])]
        programs=compile_regions(t,f,n,cap=0,allow={1})
        total=collections.Counter()
        for seed in range(4):
            ref=Model(g,t,o,n,f,None); opt=ContinuationModel(g,t,o,n,f,programs,lazy=True,fast=True)
            # Exercise mixed widths, ordered duplicates, and empty continuation.
            for i,ws in {0:(1,3,4),1:(3,3),2:(),3:(3,1,3,4),4:(4,)}.items():
                ref.fan[i]=opt.fan[i]=ws
            opt.compile_continuations(); rng=random.Random(seed)
            for _ in range(1000):
                compare(ref,opt,rng.randrange(3))
                self.assertEqual(ref.wire_wave_order,opt.wire_wave_order)
            total.update(opt.coverage)
            self.assertGreater(opt.fallback_checks,0)
        self.assertTrue(all(total[i]>0 for i in range(4)),total)
        print('Feedback model: 4,000 events; continuation coverage fallback/1/2/3:', [total[i] for i in range(4)])
if __name__=='__main__': unittest.main(verbosity=2)
