# SPDX-License-Identifier: GPL-3.0-only
"""No-build paths: verify the actual batch script on Windows in an isolated tree."""
import os, shutil, subprocess, tempfile, unittest
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
class BuilderTests(unittest.TestCase):
    def test_layout(self):
        for name in ('tests/FrontendParity.csproj', 'device-tests/DeviceParity.csproj', 'transport-tests/TransportParity.csproj', 'backend-pipe-tests/BackendPipeSmoke.csproj', 'WireWarp/GlobalUsings.cs'):
            self.assertTrue((ROOT.parent / name).is_file(), name)
    @unittest.skipUnless(os.name == 'nt', 'Windows CMD is required')
    def test_no_build_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory)
            shutil.copyfile(ROOT / 'BuildV42.cmd', p / 'BuildV42.cmd')
            target = p / 'src/core/AotGenerated.zig'
            target.parent.mkdir(parents=True)
            target.write_bytes(b'unchanged sentinel')
            # CALL intercepts /? itself; use the portable --help option.
            # Execute the batch file directly in a child CMD, not via CALL.
            for args, code in [('/nopause',2), ('/nopause bogus',2), ('generic v4.1 /nopause',2), ('v4.1 generic /nopause',2), ('/nopause --help',0), ('--help /nopause',0)]:
                with self.subTest(args=args):
                    r = subprocess.run([os.environ.get('COMSPEC', 'cmd.exe'), '/d', '/c', 'BuildV42.cmd '+args], cwd=p, capture_output=True, timeout=15)
                    self.assertEqual(r.returncode, code, r.stdout+r.stderr)
                    if code == 0:
                        self.assertIn(b'Usage: BuildV42.cmd', r.stdout)
                    else:
                        self.assertIn(b'No build or AOT reset performed.', r.stdout)
                    self.assertEqual(target.read_bytes(), b'unchanged sentinel')
                    self.assertFalse((p/'zig-out').exists())
if __name__ == '__main__': unittest.main(verbosity=2)
