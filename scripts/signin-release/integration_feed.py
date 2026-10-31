#!/usr/bin/env python3
"""Stage Firebase-only test fixtures beside the exact sealed Sign-In candidate.

This directory is never a publication artifact. Shared Sign-In dependencies always come
from the candidate, including reused feed packages, rather than from fixture rebuilds.
"""
import importlib.util
import hashlib
import os
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def fixture_packages(metadata):
    selected = release.fixture_packages(metadata)
    for name, value in selected.items():
        if name.startswith('AdamE.Firebase.iOS.'):
            release.require(release.version(value)[0] == 12, 'Only Firebase 12 fixtures are permitted')
    return selected


def published_fixtures(metadata, current, latest, fetchers):
    """Return released fixture payloads when this commit would rebuild identical inputs.

    A fingerprint or version difference means the fixtures must be rebuilt here. A published
    payload that differs from its sealed release manifest is a hard failure, never a fallback.
    """
    if latest is None:
        return None, 'no published Firebase release'
    released = {p['id']: p for p in latest['packages']}
    payloads = {}
    for name, value in sorted(fixture_packages(metadata).items()):
        record = released.get(name)
        if record is None or release.version(record['version']) != release.version(value):
            return None, f"{name} {value} is not part of Firebase {latest['releaseVersion']}"
        if record['fingerprint'] != current[name]['fingerprint']:
            return None, f'{name} inputs differ from the published build'
        found = [data for data in (fetch(record) for fetch in fetchers) if data is not None]
        if not found:
            return None, f'{name} {value} is not on a readable feed'
        for data in found:
            release.require(release.payload_hash(data) == record['payloadHash'],
                            f'Published payload differs from its release manifest: {name}')
        payloads[name] = found[0]
    return payloads, None


def fetch_published(metadata_path, output):
    release.configure_channel('firebase')
    metadata = release.read(metadata_path)
    current = {p['id']: p for p in release.snapshot(metadata)}
    # GitHub Packages needs a token; fork pull requests fall back to the public feed alone.
    registries = (release.feeds() if os.environ.get('GH_TOKEN') else
                  [release.Feed('nuget', 'https://api.nuget.org/v3/index.json')])
    payloads, reason = published_fixtures(metadata, current, release.previous_release(), [f.fetch for f in registries])
    for name, data in (payloads or {}).items():
        (output / f"{name}.{release.normalized(release.package_info(data)['version'])}.nupkg").write_bytes(data)
    source = 'published' if payloads else 'build'
    print(f'Firebase 12 integration fixtures: {source}' + (f' ({reason})' if reason else ''))
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as handle:
            handle.write(f'source={source}\n')


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
    if sys.argv[1:] == ['--fetch-published']:
        fetch_published(ROOT / 'output/firebase-metadata.json', ROOT / 'output')
        return
    stage_feed(ROOT / 'output/signin-candidate', (ROOT / 'output').glob('*.nupkg'),
               release.read(ROOT / 'output/firebase-metadata.json'), ROOT / 'output/signin-combined')
    print('Staged validation-only Firebase 12 integration feed; sealed candidate unchanged.')


if __name__ == '__main__':
    main()
