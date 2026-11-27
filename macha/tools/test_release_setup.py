import contextlib, hashlib, io, json, subprocess, sys, tempfile, unittest
from pathlib import Path
from aot_fixtures import write_fixture
from generate_for_world import default_trace, choose_profile
from check_world import check

class ReleaseSetupTests(unittest.TestCase):
    def test_default_trace(self):
        self.assertEqual(default_trace(0), [])
        self.assertEqual(default_trace(1), [0,0])
        self.assertEqual(default_trace(2), [0,0,1,1])
        t=default_trace(100000); self.assertEqual(len(t),32)
        self.assertEqual((t[0],t[-1]),(0,99999))
        self.assertEqual(len(set(t)),16)
    def test_export_pair(self):
        with tempfile.TemporaryDirectory() as d:
            prefix=Path(d)/'a world'; w=write_fixture(prefix.with_suffix('.wwld'),4)
            self.assertEqual(check(prefix),2)
            p=w.with_suffix('.wwio'); b=bytearray(p.read_bytes()); b[8]^=1; p.write_bytes(b)
            with self.assertRaises(ValueError): check(prefix)
    def test_profile_cache(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d); w=write_fixture(p/'demo.wwld',4); profiles=p/'profiles'; profiles.mkdir()
            old=profiles/'old.json'; old.write_text('{"sha256":"wrong","wires":[]}')
            match=choose_profile(w,profiles)
            self.assertEqual(json.loads(match.read_text())['sha256'],hashlib.sha256(w.read_bytes()).hexdigest())
            self.assertEqual(json.loads(match.read_text())['trace'],[0,0,1,1])
            self.assertEqual(choose_profile(w,profiles,[1]),match)
            self.assertEqual(old.read_text(),'{"sha256":"wrong","wires":[]}')
            with self.assertRaises(ValueError): choose_profile(w,profiles,[2])
    def test_atomic_output(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d); w=write_fixture(p/'demo.wwld',4); out=p/'generated.zig'; out.write_text('original')
            script=Path(__file__).with_name('generate_for_world.py')
            cmd=[sys.executable,str(script),str(w),'--mode','v4.1','--profiles',str(p/'profiles'),'--output',str(out)]
            subprocess.run(cmd,check=True,capture_output=True)
            text=out.read_bytes(); self.assertIn(b'pub const continuations = true;',text)
            w.write_bytes(b'bad')
            self.assertNotEqual(subprocess.run(cmd,capture_output=True).returncode,0)
            self.assertEqual(out.read_bytes(),text)

if __name__=='__main__': unittest.main(verbosity=2)
