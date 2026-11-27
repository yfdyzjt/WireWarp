import hashlib, struct, tempfile, unittest, json, subprocess, sys
from pathlib import Path
from aot_generate import generate,parse,topology,Node,load_mask_profile
from aot_fixtures import grouped,nodes,fixture,write_fixture

def data(groups):return grouped([nodes(g) for g in groups],b'\x42'*32)

class GeneratorTests(unittest.TestCase):
    def test_round_trip(self):
        for variant in ('chain','mixed','fanout'):
            g=fixture(17,variant);self.assertEqual(parse(data(g)),g)
    def test_exact_digest(self):
        d=data(fixture());s,t=generate(d);self.assertEqual(t['sha256'],hashlib.sha256(d).hexdigest());self.assertIn(', '.join(map(str,hashlib.sha256(d).digest())),s)
    def test_chain(self):
        s,t=generate(data(fixture(5)));self.assertEqual(t['wires'],7);self.assertEqual(t['gates'],5);self.assertIn('self.dirty.push(0)',s);self.assertIn('try self.out.append(self.alloc, 1)',s)
    def test_sort_and_duplicates(self):
        g=fixture(1);w=g[-1][0];g[-1][0]=Node(w.kind,w.id,(2,4,4));ts,*_=topology(g);self.assertEqual(ts[0],[(0,0),(0,0),(3,0)]);s,_=generate(data(g));self.assertEqual(s.count('self.g.lamps[0].on = !self.g.lamps[0].on;'),2)
    def test_gate_specializations(self):
        s,t=generate(data(fixture(8,'mixed')));self.assertIn(' and ',s);self.assertIn(' != ',s);self.assertIn('6 => return self.g.lamps[',s);self.assertIn('self.fault_hits[7] += 1;',s);self.assertEqual(t['gates'],7)
    def test_limits(self):
        s,t=generate(data(fixture(128,'fanout')),max_wire=2,max_actions=3,max_gates=1);self.assertEqual(t['actions'],3);self.assertEqual(t['wires'],3);self.assertEqual(t['gates'],1)
    def test_empty_specializations(self):
        s,t=generate(data(fixture(1)),0,0,0);self.assertEqual(t['wires'],0);self.assertEqual(s.count('_ = self;'),2)
    def test_truncation(self):
        d=data(fixture());self.assertRaises(ValueError,parse,d[:-1]);self.assertRaises(ValueError,parse,b'')
    def test_bad_id(self):
        g=fixture(2);w=g[-1][0];g[-1][0]=Node(w.kind,w.id+1,w.edges);self.assertRaises(ValueError,parse,data(g))
    def test_bad_target(self):
        g=fixture(2);w=g[-1][0];g[-1][0]=Node(w.kind,w.id,(9999,));self.assertRaises(ValueError,generate,data(g))
    def test_fault_validation(self):
        g=fixture(1);n=g[3][0];g[3][0]=Node(7,n.id,n.edges);self.assertRaises(ValueError,generate,data(g))
    def test_mask_profile_validation(self):
        d=data(fixture(5)); digest=hashlib.sha256(d).hexdigest()
        with tempfile.TemporaryDirectory() as t:
            p=Path(t)/"profile.json"
            for wires,valid in [([2,0],True),([1,1],False),([-1],False),([999],False),([True],False)]:
                p.write_text(json.dumps(dict(sha256=digest,wires=wires)))
                if valid: self.assertEqual(load_mask_profile(p,d,digest),[0,2])
                else: self.assertRaises(ValueError,load_mask_profile,p,d,digest)
            p.write_text(json.dumps(dict(sha256="wrong",wires=[0])))
            self.assertRaises(ValueError,load_mask_profile,p,d,digest)
    def test_profiling_release_generation(self):
        with tempfile.TemporaryDirectory() as t:
            source=Path(t)/'graph.wwir'; source.write_bytes(data(fixture(5)))
            output=Path(t)/'generated.zig'
            generator=Path(__file__).with_name('aot_generate.py')
            for mode in ('v3.3','v3.4','v3.5','v4.0','v4.1'):
                subprocess.run([sys.executable,str(generator),str(source),'--mode',mode,'--output',str(output)],check=True,capture_output=True)
                text=output.read_text()
                self.assertIn('pub const direct_regions = true;',text)
                self.assertEqual('pub const profiling_release = true;' in text,mode in ('v3.4','v3.5','v4.0','v4.1'))
                self.assertEqual('pub const fast_wires = true;' in text,mode in ('v3.5','v4.0','v4.1'))
                self.assertEqual('pub const packed_gates = true;' in text,mode in ('v4.0','v4.1'))
                self.assertEqual('pub const continuations = true;' in text,mode=='v4.1')
    def test_fixture_io(self):
        with tempfile.TemporaryDirectory() as t:
            p=write_fixture(Path(t)/'demo.wwld',2);d=p.read_bytes();io=p.with_suffix('.wwio').read_bytes();self.assertEqual(d[8:40],io[8:40]);self.assertEqual(struct.unpack_from('<i',io,40)[0],8);self.assertEqual(struct.unpack_from('<i',io,76)[0],2)
if __name__=='__main__':unittest.main(verbosity=2)
