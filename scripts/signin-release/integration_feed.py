#!/usr/bin/env python3
"""Stage Firebase-only test fixtures beside the exact sealed Sign-In candidate.

This directory is never a publication artifact. Shared Sign-In dependencies always come
from the candidate, including reused feed packages, rather than from fixture rebuilds.
"""
import importlib.util
import hashlib
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def fixture_packages(metadata):
    return release.fixture_packages(metadata)


def stage_feed(candidate, fixtures, metadata, output):
    release.configure_channel('signin')
    manifest = release.read(candidate / 'manifest.json')
    release.validate_manifest_channel(manifest)
    expected = fixture_packages(metadata)
    selected = {}
    for path in fixtures:
        info = release.package_info(path.read_bytes())
        name = info['id']
        if name in release.SIGNIN_IDS:
            continue  # The sealed candidate owns all shared Sign-In dependencies.
        if name not in expected:
            raise ValueError('Unexpected integration fixture: ' + name)
        if name in selected or release.version(info['version']) != release.version(expected[name]):
            raise ValueError('Duplicate/wrong-version integration fixture: ' + name)
        selected[name] = path
    if set(selected) != set(expected):
        raise ValueError('Missing integration fixtures: ' + ', '.join(sorted(set(expected) - set(selected))))
    sources = list(selected.values())
    for package in manifest['packages']:
        path = candidate / package['file']
        if hashlib.sha256(path.read_bytes()).hexdigest() != package['sha256']:
            raise ValueError('Altered sealed candidate: ' + package['id'])
        sources.append(path)
    if len({p.name for p in sources}) != len(sources):
        raise ValueError('Integration fixture filename collision')
    if output.exists():
        shutil.rmtree(output)
    output.mkdir()
    for path in sources:
        shutil.copy2(path, output / path.name)


def main():
    stage_feed(ROOT / 'output/signin-candidate', (ROOT / 'output').glob('*.nupkg'),
               release.read(ROOT / 'output/firebase-metadata.json'), ROOT / 'output/signin-combined')
    print('Staged validation-only Firebase 12 integration feed; sealed candidate unchanged.')


if __name__ == '__main__':
    main()
