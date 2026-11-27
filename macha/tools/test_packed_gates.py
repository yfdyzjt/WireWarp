#!/usr/bin/env python3
"""Independent packed descriptor/scheduler model; not a native Zig execution test."""
import argparse, collections, hashlib, json, random, unittest
from pathlib import Path
from aot_generate import parse, topology, load_mask_profile
from profile_regions import Model, compile_regions
from test_aot_regions import fixture

MASK32=(1<<32)-1; MASK29=(1<<29)-1
FALLBACK,NORMAL1,FAULT1,LAZY1,AND2,XOR2=range(6)

def encode(op, first=0, second=0):
    if not 0<=first<=MASK32 or not 0<=second<=MASK29: return 0
    return first | (second<<32) | (op<<61)

def decode(plan): return plan>>61,plan&MASK32,(plan>>32)&MASK29

def evaluate(plan, lamps, old, parity, lazy=True):
    op,a,b=decode(plan)
    if op==FALLBACK: return None
    x=bool(lamps[a])
    if op==FAULT1: return x,old
    if op==LAZY1: return x ^ bool(lazy and parity.get(b,0)&1),old
    cur=x if op==NORMAL1 else x and bool(lamps[b]) if op==AND2 else x!=bool(lamps[b])
    return cur!=old,cur

class PackedModel(Model):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        self.plans=[]
        for gi,ls in enumerate(self.normal):
            if self.faulty[gi]:
                wi=self.lazy_gate.get(gi)
                p=encode(FAULT1 if wi is None else LAZY1,ls[0],wi or 0) if len(ls)==1 else 0
            elif len(ls)==1: p=encode(NORMAL1,ls[0])
            elif len(ls)==2: p=encode(XOR2 if self.kinds[gi] in (5,6) else AND2,*ls)
            else: p=0
            self.plans.append(p)
        self.packed_checks=0; self.fallback_checks=0
    def try_packed_gate(self,gi,lazy_parity):
        result=evaluate(self.plans[gi],self.lamps,self.state[gi],lazy_parity)
        if result is None:
            self.fallback_checks+=1; return None
        self.packed_checks+=1
        emit,cur=result; self.state[gi]=cur
        return emit

def compare(ref,opt,port):
    x,r,_=ref.event(port); y,s,_=opt.event(port)
    assert x==y and r['checks']==s['checks'] and ref.wave_order==opt.wave_order
    assert ref.lamps==opt.lamps and ref.state==opt.state and ref.rng.getstate()==opt.rng.getstate()
    return s['checks'],len(y)

class PackedTests(unittest.TestCase):
    def test_truth_tables(self):
        for op in range(6):
            for bits in range(4):
                lamps=[bool(bits&1),bool(bits&2)]
                for old in (False,True):
                    for par in range(4):
                        for lazy in (False,True):
                            got=evaluate(encode(op,0,1),lamps,old,{1:par},lazy)
                            if op==FALLBACK: self.assertIsNone(got);continue
                            if op in (FAULT1,LAZY1): exp=(lamps[0] ^ bool(op==LAZY1 and lazy and par&1),old)
                            else:
                                cur={NORMAL1:lamps[0],AND2:all(lamps),XOR2:sum(lamps)==1}[op]
                                exp=(old!=cur,cur)
                            self.assertEqual(got,exp)
    def test_boundaries(self):
        self.assertEqual(decode(encode(LAZY1,MASK32,MASK29)),(LAZY1,MASK32,MASK29))
        for a,b in ((MASK32+1,0),(0,MASK29+1),(-1,0),(0,-1)):
            self.assertEqual(encode(LAZY1,a,b),0)
    def test_mixed_feedback_trace(self):
        g=fixture(); t,o,n,f=topology(g)
        t[0]=[(k,j) for k,j in t[0] if o[j]!=70]
        t[4]=[(0,n[71][0]),(0,n[71][0]),(1,n[0][0]),(1,n[0][0]),(2,0),(2,0)]
        t[2]=[(2,70),(2,70)]
        t[5]=[(2,1),(1,n[1][0])]
        programs=compile_regions(t,f,n,cap=0,allow={1})
        for seed in range(4):
            ref=Model(g,t,o,n,f,None); opt=PackedModel(g,t,o,n,f,programs,lazy=True,fast=True)
            ref.fan[0]=opt.fan[0]=(1,3,4)
            rng=random.Random(seed)
            for _ in range(1000): compare(ref,opt,rng.randrange(3))
            self.assertGreater(opt.packed_checks,0); self.assertGreater(opt.fallback_checks,0)
    def test_random_descriptors(self):
        rng=random.Random(40)
        for _ in range(10000):
            op=rng.randrange(1,6); a=rng.randrange(1<<32); b=rng.randrange(1<<29)
            self.assertEqual(decode(encode(op,a,b)),(op,a,b))

if __name__=='__main__':
    unittest.main(verbosity=2)
