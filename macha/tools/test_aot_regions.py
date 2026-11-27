"""Independent wave model tests, including mixed probabilistic continuation."""
import random, unittest
from aot_generate import Node, topology
from profile_regions import Model, compile_regions


def fixture():
    # 70 deterministic faulty gates cross a 64-bit block boundary; one
    # probabilistic fault gate, one normalized NOR, one two-input XOR.
    I,O=3,4; lb=I+O
    owners=[]; kinds=[]; params=[]; fault_lamps=[]
    for gi in range(73):
        ls=[]
        for j in range(2 if gi in (70,72) else 1):
            ls.append(lb+len(owners)); owners.append(gi); kinds.append(1 if (gi+j)%2==0 else 2)
        params.append(ls)
        if gi<=70:
            fault_lamps.append(lb+len(owners)); owners.append(gi); kinds.append(3)
    gb=lb+len(owners); wb=gb+73
    lamps=[Node(k,lb+i,(gb+owner,)) for i,(k,owner) in enumerate(zip(kinds,owners))]
    gates=[Node(7 if gi<=70 else (4 if gi==71 else 5),gb+gi,(wb+3,) if gi<71 else (wb+0,wb+1,wb+3,wb+4)) for gi in range(73)]
    w_edges=[tuple(p[0] for p in params[:71]),tuple(fault_lamps[:70])+(I,I), (fault_lamps[70],fault_lamps[70]), (I,I+1,I+1),tuple(params[71]+params[72]),tuple(fault_lamps)]
    wires=[Node(1,wb+i,tuple(e)) for i,e in enumerate(w_edges)]
    inputs=[Node(1,0,(wb,wb,wb+1,wb+2,wb+4)),Node(1,1,(wb,wb+1,wb+1,wb+2,wb+5)),Node(1,2,(wb+1,wb+3,wb+5))]
    outputs=[Node(1,I+i,()) for i in range(O)]
    return [inputs,outputs,lamps,gates,wires]


class RegionTests(unittest.TestCase):
    def test_mixed_continuation(self):
        g=fixture(); t,o,n,f=topology(g); programs=compile_regions(t,f,n)
        self.assertIn(1,programs); self.assertNotIn(5,programs)
        self.assertEqual(len(programs[1][2]),2)
        ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,programs)
        rng=random.Random(71)
        for i in range(2000):
            p=rng.randrange(3)
            x,r,_=ref.event(p); y,s,_=opt.event(p)
            self.assertEqual(x,y); self.assertEqual(r['checks'],s['checks'])
            self.assertEqual(ref.wave_order,opt.wave_order)
            self.assertEqual(ref.lamps,opt.lamps); self.assertEqual(ref.state,opt.state)
            self.assertEqual(ref.rng.getstate(),opt.rng.getstate())
    def test_pruned_mask_fallback(self):
        g=fixture(); t,o,n,f=topology(g)
        for allow in (set(),{1}):
            programs=compile_regions(t,f,n,cap=0,allow=allow)
            self.assertEqual(set(programs),allow)
            ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,programs,lazy=True)
            rng=random.Random(32)
            for _ in range(500):
                port=rng.randrange(3); x,r,_=ref.event(port); y,s,_=opt.event(port)
                self.assertEqual(x,y); self.assertEqual(r["checks"],s["checks"])
                self.assertEqual(ref.wave_order,opt.wave_order); self.assertEqual(ref.lamps,opt.lamps)
                self.assertEqual(ref.state,opt.state); self.assertEqual(ref.rng.getstate(),opt.rng.getstate())
    def test_duplicate_fault_and_control_targets(self):
        g=fixture(); w=g[-1][0]; g[-1][0]=Node(w.kind,w.id,w.edges+w.edges[:10]); w=g[-1][1]; g[-1][1]=Node(w.kind,w.id,w.edges+w.edges[:10]); t,o,n,f=topology(g); programs=compile_regions(t,f,n)
        ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,programs)
        for p in [0,1,2]*20:
            x,r,_=ref.event(p); y,s,_=opt.event(p)
            self.assertEqual(x,y); self.assertEqual(r['checks'],s['checks']); self.assertEqual(ref.wave_order,opt.wave_order)
            self.assertEqual(ref.lamps,opt.lamps); self.assertEqual(ref.state,opt.state); self.assertEqual(ref.rng.getstate(),opt.rng.getstate())
    def test_lazy_exclusive_and_rejected_controls(self):
        for overlap in (0,1,2,3):
            g=fixture(); t,o,n,f=topology(g)
            t[0]=[(k,j) for k,j in t[0] if o[j]!=70]
            if overlap==1: t[5]=sorted(t[5]+[t[0][0]]) # ordinary small writer
            if overlap==2: t[5]=sorted(t[5]+t[0][:8]) # overlapping deferred run
            if overlap==3: t[0]=sorted(t[0]+[t[0][0]]) # duplicate target
            programs=compile_regions(t,f,n)
            ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,programs,lazy=True)
            self.assertEqual(len(opt.lazy_groups),0 if overlap>=2 else 1)
            rng=random.Random(17); lazy_waves=0; commits=0
            for i in range(1000):
                p=rng.randrange(3); x,r,_=ref.event(p); y,s,_=opt.event(p)
                self.assertEqual(x,y); self.assertEqual(r['checks'],s['checks'])
                self.assertEqual(ref.wave_order,opt.wave_order)
                self.assertEqual(ref.lamps,opt.lamps); self.assertEqual(ref.state,opt.state)
                self.assertEqual(ref.rng.getstate(),opt.rng.getstate())
                lazy_waves+=s['lazy_wave_toggles']; commits+=s['lazy_commits']
            if overlap<2:
                self.assertGreater(lazy_waves,0); self.assertGreater(commits,0)
    def test_direct_lazy_and_fired_fault_feedback(self):
        g=fixture(); t,o,n,f=topology(g)
        # Pure large group, plus a small writer and probabilistic triggers.
        t[0]=[(k,j) for k,j in t[0] if o[j]!=70]
        t[5]=sorted(t[5]+[t[0][0]])
        programs=compile_regions(t,f,n,cap=0,allow={1})
        ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,programs,lazy=True,direct=True)
        # Faulty gate 0 fires into a wire that triggers the same gate again;
        # also execute output duplicates and an ordinary state-changing gate.
        ref.fan[0]=opt.fan[0]=(1,3,4)
        rng=random.Random(135); direct=skips=0
        for _ in range(1000):
            p=rng.randrange(3); x,r,_=ref.event(p); y,s,_=opt.event(p)
            self.assertEqual(x,y); self.assertEqual(r['checks'],s['checks'])
            self.assertEqual(ref.wave_order,opt.wave_order)
            self.assertEqual(ref.lamps,opt.lamps); self.assertEqual(ref.state,opt.state)
            self.assertEqual(ref.rng.getstate(),opt.rng.getstate())
            direct+=s['direct_lazy_visits']; skips+=s['skipped_fault_checks']
        self.assertGreater(direct,0); self.assertGreater(skips,0)
    def test_fast_wire_plans(self):
        g=fixture(); t,o,n,f=topology(g)
        # Duplicate ordinary and control writes, deterministic triggers, output
        # duplication and signed singleton output. Preserve unsorted fallback.
        t[0]=[(k,j) for k,j in t[0] if o[j]!=70]
        t[4]=[(0,n[71][0]),(0,n[71][0]),(1,n[0][0]),(1,n[0][0]),(2,0),(2,0)]
        t[2]=[(2,70),(2,70)] # probabilistic, never short-plan
        t[3]=[(3,-7)] # signed singleton output
        t[5]=[(2,1),(1,n[1][0])] # reversed order, never short-plan
        programs=compile_regions(t,f,n,cap=0,allow={1})
        ref=Model(g,t,o,n,f,None); opt=Model(g,t,o,n,f,programs,lazy=True,fast=True)
        self.assertNotIn(2,opt.fast_plans); self.assertNotIn(5,opt.fast_plans)
        self.assertEqual(opt.fast_plans[4][0],'short')
        self.assertEqual(opt.fast_plans[3][0],'output1')
        rng=random.Random(351); visits=ops=0
        for _ in range(1000):
            p=rng.randrange(3); x,r,_=ref.event(p); y,s,_=opt.event(p)
            self.assertEqual(x,y); self.assertEqual(r['checks'],s['checks']); self.assertEqual(ref.wave_order,opt.wave_order)
            self.assertEqual(ref.lamps,opt.lamps); self.assertEqual(ref.state,opt.state); self.assertEqual(ref.rng.getstate(),opt.rng.getstate())
            visits+=s['fast_wire_visits']; ops+=s['fast_short_ops']
        self.assertGreater(visits,0); self.assertGreater(ops,0)
if __name__=='__main__': unittest.main(verbosity=2)
