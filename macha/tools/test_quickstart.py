# SPDX-License-Identifier: GPL-3.0-only
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock
from quickstart import discover, select_world, build_and_launch

class QuickStartTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.stable = self.root/'stable'; self.dev = self.root/'dev'
        self.stable.mkdir(); self.dev.mkdir()
    def pair(self, root, name):
        prefix = root/name
        for ext in ('.wwir','.wwio'):
            Path(str(prefix)+ext).touch()
        return prefix.resolve()
    def test_discover_and_select(self):
        a = self.pair(self.stable, 'My World.1')
        b = self.pair(self.dev, 'Same name')
        (self.dev/'incomplete.wwir').touch()
        self.assertEqual(set(discover([self.stable,self.dev])),{a,b})
        self.assertEqual(select_world([self.stable,self.dev],ask=lambda _: '1'), b)
        with self.assertRaises(ValueError): select_world([self.stable,self.dev],unattended=True)
        self.assertEqual(select_world([self.stable],unattended=True),a)
    def test_explicit_and_custom(self):
        a = self.pair(self.root,'Other World')
        self.assertEqual(select_world([],str(a)+'.wwir',True),a)
        inputs=iter(['p',str(a)+'.wwio'])
        self.assertEqual(select_world([],ask=lambda _: next(inputs)),a)
        with self.assertRaises(ValueError): select_world([],str(a)+'missing',True)
    def test_cancel_and_empty(self):
        self.assertIsNone(select_world([],ask=lambda _: 'q'))
        with self.assertRaises(ValueError): select_world([],unattended=True)
    def test_build_failure_no_launch(self):
        exe = self.root/'zig-out/bin/macha.exe'; exe.parent.mkdir(parents=True); exe.touch()
        run = Mock(return_value=subprocess.CompletedProcess([],1))
        self.assertEqual(build_and_launch(self.root,self.root/'world',{},run),1)
        self.assertEqual(run.call_count,1)
    def test_missing_exe_no_launch(self):
        run=Mock(return_value=subprocess.CompletedProcess([],0))
        self.assertEqual(build_and_launch(self.root,self.root/'world',{},run),1)
        self.assertEqual(run.call_count,1)
    def test_success_propagates_launch_exit(self):
        exe = self.root/'zig-out/bin/macha.exe'; exe.parent.mkdir(parents=True); exe.touch()
        run = Mock(side_effect=[subprocess.CompletedProcess([],0),subprocess.CompletedProcess([],7)])
        prefix=self.root/'world with spaces & parentheses (1)'
        self.assertEqual(build_and_launch(self.root,prefix,{},run),7)
        first,second=run.call_args_list
        self.assertEqual(first.args[0][-1],'BuildV42.cmd v4.1 /nopause')
        self.assertEqual(first.kwargs['env']['WWORLD'],str(prefix))
        self.assertEqual(second.args[0],[str(exe),'serve'])

if __name__ == '__main__': unittest.main(verbosity=2)
