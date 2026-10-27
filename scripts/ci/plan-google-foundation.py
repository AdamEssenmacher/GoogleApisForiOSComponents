#!/usr/bin/env python3
"""Select Maps/Places validation from exact selected Cake inputs; errors never skip validation."""
import argparse
import hashlib
import fnmatch
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
COMPONENTS = ('Maps', 'Places')
SHARED = ('Directory.Build.props', 'global.json', '.config/dotnet-tools.json',
          'source/AssemblyInfo.cs', 'Xamarin.Google.sln', '.gitignore')
COVERAGE = ('.github/workflows/google-foundation-validation.yml', 'scripts/ci/', 'tests/ci/',
            'scripts/FirebaseReleaseFingerprint/', 'tests/E2E/Google.Foundation/',
            'tools/e2e/check-consumer-shapes.sh', 'tools/e2e/check-package-structure.sh',
            'tools/e2e/check-offline-build.sh', 'tools/e2e/run-google-foundation.sh')


def run(*args, cwd=ROOT):
    return subprocess.check_output(args, cwd=cwd, text=True).strip()


def hash_files(root, paths):
    return {p: hashlib.sha256((root / p).read_bytes()).hexdigest() for p in sorted(paths)}


def snapshot(root, tracked, component, work):
    # The same metadata exporter and fingerprint implementation interpret both revisions.
    script = root / 'scripts/ci/google-foundation-metadata.cake'
    script.parent.mkdir(parents=True, exist_ok=True)
    if root != ROOT:
        script.write_bytes((ROOT / 'scripts/ci/google-foundation-metadata.cake').read_bytes())
    metadata_path = work / (component + '-metadata.json')
    subprocess.run(['dotnet', 'tool', 'run', 'dotnet-cake', '--', str(script), '--target=metadata',
                    '--component=Google.' + component,
                    '--metadata-output=' + str(metadata_path)], cwd=ROOT, check=True,
                   stdout=subprocess.DEVNULL)
    metadata = json.loads(metadata_path.read_text())
    selected = work / (component + '-selected.json')
    selected.write_text(json.dumps([p['artifactId'] for p in metadata]))
    normalized = work / (component + '-normalized.json')
    subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'scripts/FirebaseReleaseFingerprint'),
                    '--verbosity', 'quiet', '--', str(root), str(selected), str(normalized),
                    '--include-components'], cwd=ROOT, check=True,
                   env={**os.environ, 'MSBuildEnableWorkloadResolver': 'false'}, stdout=subprocess.DEVNULL)
    inputs = set(SHARED)
    for package in metadata:
        project = Path(package['project'])
        inputs.update(p for p in tracked if p.startswith(project.parent.as_posix() + '/'))
        for element in ET.parse(root / project).iter():
            if element.get('Pack', '').lower() == 'true' or element.tag in ('Compile', 'Import'):
                include = element.get('Include') or element.get('Project')
                if include:
                    # Fail closed for dynamic inputs, rather than ignoring dependencies.
                    if '$(' in include:
                        raise ValueError('Cannot scope dynamic project input: ' + include)
                    path = (root / project.parent / include.replace('\\', '/')).resolve()
                    if not path.is_relative_to(root):
                        raise ValueError('Project input outside repository: ' + include)
                    # Native bundles under externals are generated from the pinned
                    # pods/downloads and selected build logic already fingerprinted.
                    if path.is_relative_to(root / 'externals'):
                        continue
                    if '*' in include or '?' in include:
                        pattern = path.relative_to(root).as_posix()
                        inputs.update(p for p in tracked if fnmatch.fnmatchcase(p, pattern))
                    elif path.is_file():
                        inputs.add(path.relative_to(root).as_posix())
    return {'metadata': metadata, 'files': hash_files(root, inputs),
            'build': hashlib.sha256(normalized.read_bytes()).hexdigest()}


def select(before, after, files, manual=False):
    known_cake = {'components.cake', 'build.cake', 'common.cake', 'poco.cake', 'custom_externals_download.cake',
                  'firebase_release.cake', 'firebase_publish.cake', 'signin_publish.cake'}
    forced = manual or any(p.startswith(COVERAGE) or (p.endswith('.cake') and p not in known_cake) for p in files)
    return {c.lower(): forced or before[c] != after[c] for c in COMPONENTS}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base')
    parser.add_argument('--manual', action='store_true')
    args = parser.parse_args()
    if args.manual or not args.base or set(args.base) == {'0'}:
        result = {c.lower(): True for c in COMPONENTS}
    else:
        files = run('git', 'diff', '--name-only', args.base, 'HEAD').splitlines()
        with tempfile.TemporaryDirectory(prefix='google-foundation-plan-') as directory:
            work = Path(directory).resolve()
            old = work / 'base'; old.mkdir()
            archive = work / 'base.tar'
            with archive.open('wb') as stream:
                subprocess.run(['git', 'archive', args.base], cwd=ROOT, stdout=stream, check=True)
            with tarfile.open(archive) as tar:
                for member in tar.getmembers():
                    target = (old / member.name).resolve()
                    if not target.is_relative_to(old) or not (member.isdir() or member.isfile()):
                        raise ValueError('Unsupported archive input: ' + member.name)
                    if member.isdir():
                        target.mkdir(parents=True, exist_ok=True)
                    else:
                        target.parent.mkdir(parents=True, exist_ok=True)
                        target.write_bytes(tar.extractfile(member).read())
            tracked = run('git', 'ls-files').splitlines()
            old_tracked = run('git', 'ls-tree', '-r', '--name-only', args.base).splitlines()
            before = {c: snapshot(old, old_tracked, c, work) for c in COMPONENTS}
            after = {c: snapshot(ROOT, tracked, c, work) for c in COMPONENTS}
            result = select(before, after, files)
    print(json.dumps(result, sort_keys=True))
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
            for name, needed in result.items():
                stream.write(f'{name}={str(needed).lower()}\n')


if __name__ == '__main__':
    main()
