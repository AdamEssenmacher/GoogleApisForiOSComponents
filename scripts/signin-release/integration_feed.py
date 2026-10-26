#!/usr/bin/env python3
"""Stage Firebase-only test fixtures beside the exact sealed Sign-In candidate.

This directory is never a publication artifact. Shared Sign-In dependencies always come
from the candidate, including reused feed packages, rather than from fixture rebuilds.
"""
import importlib.util
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def main():
    release.configure_channel('signin')
    candidate = ROOT / 'output/signin-candidate'
    manifest = release.read(candidate / 'manifest.json')
    release.validate_manifest_channel(manifest)
    output = ROOT / 'output/signin-combined'
    if output.exists():
        shutil.rmtree(output)
    output.mkdir()
    for path in (ROOT / 'output').glob('*.nupkg'):
        info = release.package_info(path.read_bytes())
        if info['id'] not in release.SIGNIN_IDS:
            if info['id'].startswith('AdamE.Firebase.iOS.') and release.version(info['version'])[0] != 12:
                raise ValueError('Only Firebase 12 fixtures are permitted')
            shutil.copy2(path, output / path.name)
    for package in manifest['packages']:
        shutil.copy2(candidate / package['file'], output / package['file'])
    print('Staged validation-only Firebase 12 integration feed; sealed candidate unchanged.')


if __name__ == '__main__':
    main()
