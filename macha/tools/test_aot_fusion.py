"""Independent stateful reference checks for offline root-plan generation."""
import random, unittest
from aot_generate import Node, plan_event, topology, generate_fused
from aot_fixtures import fixture, grouped, nodes

def data(g): return grouped([nodes(x) for x in g],b'\x42'*32)
def plan(g,p,budget=100000): return plan_event(g,*topology(g),p,budget)

def reference(g, lamps, states, port):
    # Non-fault reference evaluator: dynamic dirty queue, waves and fired set.
    targets,owners,normal,faults=topology(g)
    assert not any(faults)
    wb=sum(map(len,g[:4]));pending=[sorted(w-wb for w in g[0][port].edges)]
    outputs=[];fired=set();checks=0
    while pending:
        dirty=[]
        for wires in pending:
            for wi in wires:
                for kind,idx in targets[wi]:
                    if kind==3: outputs.append(idx)
                    else:
                        assert kind==0
                        lamps[idx]=not lamps[idx]
                        if owners[idx] not in dirty:dirty.append(owners[idx])
        pending=[]
        for gi in dirty:
            checks+=1;vals=[lamps[i] for i in normal[gi]]
            cur=sum(vals)==1 if g[3][gi].kind in (5,6) else all(vals)
            changed=cur!=states[gi];states[gi]=cur
            if changed and gi not in fired:
                fired.add(gi);pending.append(sorted(w-wb for w in g[3][gi].edges))
    return outputs,checks

class FusionTests(unittest.TestCase):
    def test_chain(self):
        g=fixture(128);ls,gs,os=plan(g,0)
        self.assertEqual(ls,tuple(range(128)));self.assertEqual(gs,ls);self.assertEqual(os,(0,))
        self.assertEqual(plan(g,1),((),(),(1,)))
        s,t=generate_fused(data(g));self.assertEqual(t['fused_ports'],2);self.assertIn('appendSlice',s)
    def test_fanout(self):
        g=fixture(128,'fanout')
        for p in range(2):
            ls,gs,os=plan(g,p);self.assertEqual(ls,tuple(range(p,256,2)));self.assertEqual(gs,tuple(range(128)));self.assertEqual(os,(0,1)*64)
    def test_mixed_rejected(self):
        g=fixture(16,'mixed');self.assertIsNone(plan(g,0));self.assertIsNone(plan(g,1));self.assertEqual(generate_fused(data(g))[1]['fused_ports'],0)
    def test_hybrid(self):
        g=fixture(8,'hybrid');self.assertIsNotNone(plan(g,0));self.assertIsNone(plan(g,1));self.assertEqual(generate_fused(data(g))[1]['fused_ports'],1)
    def test_feedback_rejected(self):
        g=fixture(3);w=g[-1][-1];g[-1][-1]=Node(w.kind,w.id,(g[2][0].id,));self.assertIsNone(plan(g,0))
    def test_repeated_lamp_rejected(self):
        g=fixture(3);w=g[-1][0];g[-1][0]=Node(w.kind,w.id,w.edges*2);self.assertIsNone(plan(g,0))
    def test_two_lamps_same_gate_rejected(self):
        g=fixture(2,'fanout');w=g[-1][0];g[-1][0]=Node(w.kind,w.id,(g[2][0].id,g[2][1].id));self.assertIsNone(plan(g,0))
    def test_order_and_duplicate_outputs(self):
        g=fixture(2);w=g[-1][0];g[-1][0]=Node(w.kind,w.id,(3,2,g[2][0].id,3));self.assertEqual(plan(g,0)[2],(0,1,1,0))
    def test_budget(self):
        g=fixture(128);self.assertIsNone(plan(g,0,10));self.assertEqual(generate_fused(data(g),0)[1]['fused_ports'],0);self.assertEqual(generate_fused(data(g),100000,1)[1]['fused_ports'],1)
    def test_differential(self):
        rng=random.Random(71)
        for variant in ('chain','fanout','hybrid'):
            g=fixture(16,variant);_,_,normal,_=topology(g);plans=[plan(g,p) for p in range(2)]
            for trial in range(16):
                lamps=[bool(rng.getrandbits(1)) for _ in g[2]]
                states=[sum(lamps[i] for i in ls)==1 if g[3][gi].kind in (5,6) else all(lamps[i] for i in ls) for gi,ls in enumerate(normal)]
                fast_lamps=lamps.copy();fast_states=states.copy()
                for _ in range(128):
                    p=rng.randrange(2);expected=reference(g,lamps,states,p)
                    if plans[p] is None: actual=reference(g,fast_lamps,fast_states,p)
                    else:
                        ls,gs,os=plans[p]
                        for li,gi in zip(ls,gs): fast_lamps[li]=not fast_lamps[li];fast_states[gi]=not fast_states[gi]
                        actual=(list(os),len(gs))
                    self.assertEqual(actual,expected);self.assertEqual(fast_lamps,lamps);self.assertEqual(fast_states,states)
if __name__=='__main__':unittest.main(verbosity=2)
