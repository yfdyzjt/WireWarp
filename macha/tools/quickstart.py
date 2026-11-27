# SPDX-License-Identifier: GPL-3.0-only
"""Discover local exports, build V4.1 and connect; no benchmark required."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]

def complete(prefix):
    return all(Path(str(prefix) + ext).is_file() for ext in ('.wwir', '.wwio'))

def normalize(value):
    value = value.strip().strip('"')
    if not value or any(c in value for c in ('"', '\r', '\n', '%')):
        raise ValueError('Enter a local world prefix or export path without quotes inside it or percent signs.')
    path = Path(os.path.expanduser(value))
    if path.suffix.lower() in ('.wwir', '.wwio', '.wwld', '.wld', '.twld'):
        # A literal prefix may itself end in .wld etc.; prefer a complete literal pair.
        if not complete(path):
            path = path.with_suffix('')
    return path.resolve()

def discover(roots):
    found = set()
    for root in roots:
        for graph in root.glob('*.wwir'):
            prefix = graph.with_suffix('').resolve()
            if complete(prefix):
                found.add(prefix)
    return sorted(found, key=lambda p: (str(p).casefold(), str(p)))

def select_world(roots, explicit=None, unattended=False, ask=input):
    if explicit:
        prefix = normalize(explicit)
        if not complete(prefix):
            raise ValueError(f'Missing .wwir/.wwio export pair: {prefix}')
        return prefix
    candidates = discover(roots)
    if unattended:
        if len(candidates) != 1:
            raise ValueError('Set WWORLD to the exact export prefix for unattended setup (zero or multiple pairs found).')
        return candidates[0]
    print('Choose the world currently waiting in Terraria:')
    for i, path in enumerate(candidates, 1):
        print(f'  {i}. {path}')
    if not candidates:
        print('  No complete export pairs found in the usual local Worlds folders.')
    print('  P. Enter a custom world/export path\n  Q. Cancel without building')
    while True:
        answer = ask('Selection: ').strip().lower()
        if answer == 'q':
            return None
        if answer == 'p':
            try:
                prefix = normalize(ask('World path (or .wwir file): '))
                if complete(prefix):
                    return prefix
                print('No complete export pair at that path. Open the local world first.')
            except (OSError, ValueError) as exc:
                print(f'Invalid path: {exc}')
        elif answer.isdecimal() and len(answer) <= 9 and 1 <= int(answer) <= len(candidates):
            return candidates[int(answer) - 1]
        else:
            print('Choose a listed number, P, or Q.')

def build_and_launch(root, prefix, env, run=subprocess.run):
    child_env = dict(env, WWORLD=str(prefix))
    # The command is fixed; the selected path is passed as an environment value, not shell source.
    result = run([env.get('COMSPEC', 'cmd.exe'), '/d', '/c', 'BuildV42.cmd v4.1 /nopause'], cwd=root, env=child_env)
    if result.returncode:
        print('Build failed. Backend was NOT launched; fix the error and rerun Setup.', flush=True)
        return result.returncode
    exe = root / 'zig-out' / 'bin' / 'macha.exe'
    if not exe.is_file():
        print('ERROR: Build reported success but macha.exe is missing.', flush=True)
        return 1
    print('Connecting to Terraria. Confirm backend execution: v4.1 and matching graph hashes.', flush=True)
    return run([str(exe), 'serve'], cwd=root, env=child_env).returncode

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unattended', action='store_true', help='Use WWORLD or the sole discovered pair; never prompt.')
    args = parser.parse_args()
    base = Path(os.environ.get('USERPROFILE', str(Path.home()))) / 'Documents' / 'My Games' / 'Terraria'
    roots = [base / name / 'Worlds' for name in ('tModLoader', 'tModLoader-dev')]
    try:
        prefix = select_world(roots, os.environ.get('WWORLD'), args.unattended)
        if prefix is None:
            print('Cancelled. No build or launch performed.')
            return 0
        # Also reject percent signs in auto-discovered paths passed through CMD.
        if '%' in str(prefix):
            raise ValueError('World paths containing percent signs are not supported by the CMD builder.')
        if not shutil.which('zig'):
            raise ValueError('Zig 0.16.0 is required on PATH. Install it and reopen CMD.')
        print(f'Selected export: {prefix}', flush=True)
        return build_and_launch(ROOT, prefix, os.environ)
    except (OSError, ValueError, EOFError) as exc:
        print(f'ERROR: {exc}', file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print('\nSetup interrupted. If a build was interrupted, rerun Setup before launching.')
        return 130

if __name__ == '__main__':
    sys.exit(main())
