import contextlib, io, unittest
from profile_summary import summarize

def sample():
    lines = []
    for m in ('v3.2', 'v3.3'):
        lines.extend([
            'PARITY PASS: 2 events',
            f'profile_meta,{m},2,10,20,100,200,13,10000,70',
            f'stage,{m},all,2,2,1,13,10,20,40,10,20,10,70',
            f'stage,{m},0,1,2,0,10,5,5,30,10,15,5,70',
            f'stage,{m},1,1,0,1,3,5,15,10,0,5,5,40',
            f'STAGE PROFILE PASS: {m}',
        ])
    return '\n'.join(lines)

class ProfileTests(unittest.TestCase):
    def check(self, s):
        with contextlib.redirect_stdout(io.StringIO()): summarize(s)
    def test_valid(self): self.check(sample())
    def test_three_modes(self):
        self.check(sample()+"\n"+"\n".join(line.replace("v3.3","v3.5") for line in sample().splitlines()[-6:]))
    def test_four_modes(self):
        third = "\n".join(line.replace("v3.3","v3.5") for line in sample().splitlines()[-6:])
        fourth = third.replace("v3.5","v4.0") + "\nV4.0 RESET PASS"
        with contextlib.redirect_stdout(io.StringIO()): summarize(sample()+"\n"+third+"\n"+fourth, True, True)
    def test_five_modes(self):
        tail = "\n".join(sample().splitlines()[-6:])
        full = sample()+"\n"+tail.replace("v3.3","v3.5")+"\n"+tail.replace("v3.3","v4.0")+"\nV4.0 RESET PASS\n"+tail.replace("v3.3","v4.1")+"\nV4.1 RESET PASS"
        with contextlib.redirect_stdout(io.StringIO()): summarize(full, True, True, True)
        with self.assertRaises(ValueError): summarize(full.replace('V4.1 RESET PASS','FAIL'), True, True, True)
    def test_missing_parity(self):
        with self.assertRaises(ValueError): self.check(sample().replace('PARITY PASS:', 'FAIL:', 1))
    def test_bad_total(self):
        with self.assertRaises(ValueError): self.check(sample().replace('all,2,2,1,13,10,20,40','all,2,2,1,13,10,20,41',1))
    def test_mismatched_workload(self):
        with self.assertRaises(ValueError): self.check(sample().replace('profile_meta,v3.3,2,10,20','profile_meta,v3.3,2,11,20'))
if __name__ == '__main__': unittest.main(verbosity=2)
