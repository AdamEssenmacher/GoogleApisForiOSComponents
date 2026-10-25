#!/usr/bin/env python3
"""Firebase release planning, artifact validation, and resumable publication.

Only `publish` writes to registries/GitHub. Other commands write local artifacts.
"""
import argparse
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import time
import tempfile
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CONFIG = 'eng/firebase-release.json'
BASELINE = 'eng/firebase-release-baseline.json'
REPO = os.environ.get('GITHUB_REPOSITORY', 'AdamEssenmacher/GoogleApisForiOSComponents')
VALIDATIONS = ['unit-tests', 'package-structure', 'smoke', 'surface', 'nullability',
               'firestore-aggregate', 'firestore-filters']


def require(condition, message):
    if not condition:
        raise ValueError(message)


def run(*args):
    return subprocess.check_output(args, cwd=ROOT, text=True).strip()


def read(path):
    return json.loads(Path(path).read_text())


def write(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')


def digest(data):
    return hashlib.sha256(data).hexdigest()


def version(value):
    require(bool(re.fullmatch(r'\d+\.\d+\.\d+(?:\.\d+)?', value)),
            f'Expected a stable three/four-part version: {value}')
    return tuple(map(int, value.split('.'))) + (0,) * (4 - len(value.split('.')))


def normalized(value):
    parts = version(value)
    return '.'.join(map(str, parts[:3] if parts[3] == 0 else parts))


def native_build(config):
    settings = config['nativeBuild']
    require(bool(re.fullmatch(r'macos-\d+', settings['runner'])), 'Expected a pinned hosted macOS runner')
    require(bool(re.fullmatch(r'\d+(?:\.\d+){1,2}', settings['xcode'])), 'Expected a pinned stable Xcode version')
    return settings


class SafeRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        redirected = super().redirect_request(req, fp, code, msg, headers, newurl)
        if urllib.parse.urlsplit(req.full_url).netloc != urllib.parse.urlsplit(newurl).netloc:
            redirected.remove_header('Authorization')
        return redirected


def request(url, headers=None, missing=False):
    try:
        opener = urllib.request.build_opener(SafeRedirect())
        with opener.open(urllib.request.Request(url, headers=headers or {}), timeout=90) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        if missing and error.code == 404:
            return None
        raise RuntimeError(f'HTTP {error.code} reading {urllib.parse.urlsplit(url).netloc}') from None


def github(path):
    headers = {'Accept': 'application/vnd.github+json'}
    if os.environ.get('GH_TOKEN'):
        headers['Authorization'] = 'Bearer ' + os.environ['GH_TOKEN']
    return json.loads(request('https://api.github.com/repos/' + REPO + '/' + path, headers))


def previous_release():
    releases = []
    page = 1
    while True:
        batch = github(f'releases?per_page=100&page={page}')
        releases.extend(r for r in batch if r['tag_name'].startswith('firebase-v') and not r['draft'])
        if len(batch) < 100:
            break
        page += 1
    if not releases:
        return None
    latest = max(releases, key=lambda r: version(r['tag_name'].removeprefix('firebase-v')))
    asset = next((a for a in latest['assets'] if a['name'] == 'manifest.json'), None)
    require(asset is not None, 'Previous Firebase release has no manifest; refusing to guess a baseline')
    return json.loads(request(asset['browser_download_url']))


def build_fingerprint(metadata):
    with tempfile.TemporaryDirectory() as temp:
        selected = Path(temp) / 'selected.json'
        output = Path(temp) / 'normalized.json'
        write(selected, [p['key'] for p in metadata])
        # The result is a file; stream build output so compiler/restore errors reach CI logs.
        subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'scripts/FirebaseReleaseFingerprint'),
                        '--verbosity', 'minimal', '--', str(ROOT), str(selected), str(output)],
                       cwd=ROOT, check=True)
        return digest(output.read_bytes())


def snapshot(metadata):
    """Fingerprint tracked inputs, not generated outputs or archive timestamps."""
    tracked = set(run('git', 'ls-files').splitlines())
    shared = ['Directory.Build.props', 'global.json', '.config/dotnet-tools.json', 'source/AssemblyInfo.cs']
    shared_data = {p: digest((ROOT / p).read_bytes()) for p in shared}
    shared_data['cakeBuild'] = build_fingerprint(metadata)
    shared_data['nativeBuild'] = native_build(read(ROOT / CONFIG))
    ids = {p['id']: p for p in metadata}
    require(len(ids) == len(metadata), 'Duplicate package IDs')
    records = []
    seen = set()
    for package in metadata:
        require(package['id'].startswith(('AdamE.Firebase.iOS.', 'AdamE.Google.iOS.')), 'Unexpected package scope')
        require(set(package['dependencies']) <= seen, 'Metadata is not in dependency order')
        seen.add(package['id'])
        project = ROOT / package['project']
        xml = ET.parse(project)
        require(xml.findtext('.//PackageId') == package['id'], 'Package ID differs from Cake export')
        require(version(xml.findtext('.//PackageVersion')) == version(package['version']),
                f"Project/Cake versions differ: {package['id']}")
        files = {p for p in tracked if p.startswith(project.parent.relative_to(ROOT).as_posix() + '/')}
        for element in xml.iter():
            if element.get('Pack', '').lower() == 'true' or element.tag in ('Compile', 'Import'):
                include = element.get('Include') or element.get('Project')
                if include and '$(' not in include and '*' not in include:
                    path = (project.parent / include.replace('\\', '/')).resolve()
                    require(path.is_relative_to(ROOT), f'Input outside repository: {include}')
                    if path.is_file():
                        files.add(path.relative_to(ROOT).as_posix())
        references = {}
        for element in xml.findall('.//ProjectReference'):
            path = (project.parent / element.attrib['Include'].replace('\\', '/')).resolve()
            dependency_id = ET.parse(path).findtext('.//PackageId')
            require(dependency_id in ids, f'Out-of-scope project reference: {dependency_id}')
            references[dependency_id] = ids[dependency_id]['version']
        inputs = {'metadata': package, 'shared': shared_data, 'projectReferences': references,
                  'files': {p: digest((ROOT / p).read_bytes()) for p in sorted(files)}}
        records.append({**package, 'fingerprint': digest(json.dumps(inputs, sort_keys=True).encode())})
    return records


def check_versions(packages, baseline):
    old = {p['id']: p for p in baseline['packages']}
    changed = []
    for p in packages:
        before = old.get(p['id'])
        if before:
            require(version(p['version']) >= version(before['version']), f"Version downgrade: {p['id']}")
            if p['fingerprint'] != before['fingerprint']:
                require(version(p['version']) > version(before['version']),
                        f"Package inputs changed without a version bump: {p['id']}")
        if before is None or version(p['version']) > version(before['version']):
            changed.append(p['id'])
    return changed


def git_file(ref, path):
    result = subprocess.run(['git', 'show', f'{ref}:{path}'], cwd=ROOT, text=True, capture_output=True)
    if result.returncode:
        return None
    return json.loads(result.stdout)


def plan(args):
    packages = snapshot(read(args.metadata))
    config = read(ROOT / CONFIG)
    require(version(config['minimumFirebaseVersion']) >= version('12.12.0'), 'Firebase publication floor cannot precede 12.12')
    latest = previous_release() if args.online else None
    baseline = latest or read(ROOT / BASELINE)
    if args.base:
        original = git_file(args.base, BASELINE)
        require(original is None or original == read(ROOT / BASELINE), 'The initial baseline is immutable')
        require(original is not None or config['releaseVersion'] is None, 'Bootstrap must have publishing disabled')
    changed = check_versions(packages, baseline)
    release_version = config['releaseVersion']
    if release_version:
        version(release_version)
        require(version(release_version) >= version(config['minimumFirebaseVersion']), 'Release predates 12.12')
        for p in packages:
            if p['firebase']:
                require(version(p['version']) >= version(config['minimumFirebaseVersion']), 'Firebase package predates 12.12')
        if latest:
            require(version(release_version) >= version(latest['releaseVersion']), 'Release version went backwards')
            require(not changed or version(release_version) > version(latest['releaseVersion']),
                    'Package changes require a new release version')
    files = run('git', 'diff', '--name-only', args.base, 'HEAD').splitlines() if args.base else ['build.cake']
    project_dirs = tuple(str(Path(p['project']).parent) + '/' for p in packages)
    relevant = any(
        p.startswith(project_dirs) or p == 'source/AssemblyInfo.cs'
        or p.endswith('.cake') or p.startswith(('eng/', 'scripts/', 'tests/E2E/Firebase.Foundation/',
                                           'tools/e2e/', '.github/workflows/firebase-release.yml'))
        or p in ('global.json', 'Directory.Build.props', '.config/dotnet-tools.json') for p in files)
    for package in packages:
        project = ROOT / package['project']
        for element in ET.parse(project).iter():
            if element.get('Pack', '').lower() == 'true' or element.tag in ('Compile', 'Import'):
                include = element.get('Include') or element.get('Project')
                if include and '$(' not in include and '*' not in include:
                    path = (project.parent / include.replace('\\', '/')).resolve()
                    if path.is_relative_to(ROOT):
                        relevant = relevant or path.relative_to(ROOT).as_posix() in files
    old_config = git_file(args.base, CONFIG) if args.base else None
    triggered = args.manual or old_config is None or old_config.get('releaseVersion') != release_version
    eligible = bool(release_version and triggered and (not latest or version(release_version) > version(latest['releaseVersion'])))
    result = {'schemaVersion': 1, 'sourceCommit': run('git', 'rev-parse', 'HEAD'),
              'releaseVersion': release_version, 'packages': packages, 'changed': changed,
              'eligible': eligible, 'relevant': relevant or eligible, 'nativeBuild': native_build(config)}
    write(args.output, result)
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
            output.write(f"relevant={str(result['relevant']).lower()}\neligible={str(eligible).lower()}\n")
            output.write(f"runner={result['nativeBuild']['runner']}\nxcode={result['nativeBuild']['xcode']}\n")
    print(f"Firebase validation required: {result['relevant']}; release eligible: {eligible}; changed packages: {len(changed)}")


def payload_hash(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), 'Duplicate ZIP entries')
        return digest(json.dumps([(n, digest(archive.read(n))) for n in sorted(names)
                                  if not n.endswith('/') and n != '.signature.p7s']).encode())


def package_info(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        specs = [n for n in archive.namelist() if n.endswith('.nuspec')]
        require(len(specs) == 1, 'Expected exactly one nuspec')
        root = ET.fromstring(archive.read(specs[0]))
        for element in root.iter():
            element.tag = element.tag.split('}')[-1]
        meta = root.find('metadata')
        repository = meta.find('repository')
        return {'id': meta.findtext('id'), 'version': meta.findtext('version'),
                'commit': repository.get('commit') if repository is not None else None,
                'dependencies': [dict(d.attrib) for d in meta.findall('.//dependency')],
                'files': archive.namelist()}


class Feed:
    def __init__(self, name, source, token=None):
        self.name, self.source = name, source
        self.headers = {}
        if token:
            user = os.environ.get('GITHUB_ACTOR', REPO.split('/')[0])
            self.headers['Authorization'] = 'Basic ' + base64.b64encode(f'{user}:{token}'.encode()).decode()
        index = json.loads(request(source, self.headers))
        self.base = next((r['@id'] for r in index['resources'] if r['@type'].startswith('PackageBaseAddress/')), None)
        self.registration = next((r['@id'] for r in index['resources'] if r['@type'].startswith('RegistrationsBaseUrl')), None)
        require(self.base or self.registration, f'No package download resource in {name}')

    def fetch(self, package):
        package_id, v = package['id'].lower(), normalized(package['version'])
        if self.base:
            url = f"{self.base.rstrip('/')}/{package_id}/{v}/{package_id}.{v}.nupkg"
        else:
            leaf = request(f"{self.registration.rstrip('/')}/{package_id}/{v}.json", self.headers, missing=True)
            if leaf is None:
                return None
            url = json.loads(leaf)['packageContent']
        # Authentication is scoped to the feed host, never sent to a foreign content host.
        headers = self.headers if urllib.parse.urlsplit(url).netloc == urllib.parse.urlsplit(self.source).netloc else {}
        return request(url, headers, missing=True)


def feeds():
    return [Feed('nuget', 'https://api.nuget.org/v3/index.json'),
            Feed('github', f'https://nuget.pkg.github.com/{REPO.split("/")[0]}/index.json', os.environ.get('GH_TOKEN'))]


def contains_version(spec, value):
    """NuGet stable version ranges used by this repository."""
    v = version(value)
    def bound(s):
        parts = s.split('.')
        return version('.'.join(parts + ['0'] * max(0, 3 - len(parts))))
    if not spec.startswith(('[', '(')):
        return v >= bound(spec)
    inner = spec[1:-1]
    if ',' not in inner:
        return v == bound(inner)
    low, high = (s.strip() for s in inner.split(','))
    return ((not low or v > bound(low) or (spec[0] == '[' and v == bound(low)))
            and (not high or v < bound(high) or (spec[-1] == ']' and v == bound(high))))


def inspect_package(data, package, packages, new_commit=None):
    info = package_info(data)
    require(info['id'] == package['id'] and version(info['version']) == version(package['version']), 'Package identity mismatch')
    if new_commit:
        require(info['commit'] == new_commit, f"Wrong repository commit: {info['id']}")
    for dependency in info['dependencies']:
        if dependency['id'].startswith('AdamE.'):
            require(dependency['id'] in packages, f"Out-of-scope dependency: {dependency['id']}")
            require(contains_version(dependency['version'], packages[dependency['id']]['version']),
                    f"Unsatisfied dependency: {info['id']} -> {dependency}")
    names = info['files']
    for platform in ('net10.0-ios', 'net10.0-maccatalyst'):
        require(any(n.startswith('lib/' + platform) and n.endswith('.dll') for n in names),
                f"Missing {platform} assembly: {info['id']}")
        require(any(n.startswith('lib/' + platform) and ('.resources/' in n or n.endswith('.resources.zip')) for n in names),
                f"Missing {platform} native resources: {info['id']}")
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for name in names:
            if name.startswith('lib/net10.0-') and name.endswith('.resources.zip'):
                with zipfile.ZipFile(io.BytesIO(archive.read(name))) as native:
                    references = ET.fromstring(native.read('manifest')).findall('NativeReference')
                    require(bool(references), f"Empty native manifest: {info['id']}")
                    for reference in references:
                        framework = reference.attrib['Name']
                        require(reference.findtext('Kind') == 'Framework', f'Unexpected native kind: {framework}')
                        require(framework.endswith('.xcframework'), f'Unexpected native reference: {framework}')
                        libraries = plistlib.loads(native.read(framework + '/Info.plist'))['AvailableLibraries']
                        variants = {lib.get('SupportedPlatformVariant', '') for lib in libraries if lib['SupportedPlatform'] == 'ios'}
                        require({'', 'simulator', 'maccatalyst'} <= variants, f'Missing native platform slices: {framework}')
                        for lib in libraries:
                            prefix = framework + '/' + lib['LibraryIdentifier'] + '/' + lib['LibraryPath']
                            require(any(n.startswith(prefix + '/') for n in native.namelist()), f'Missing native library: {prefix}')
                    if new_commit and package.get('project'):
                        project = ET.parse(ROOT / package['project'])
                        expected = {Path(e.attrib['Include'].replace('\\', '/')).name: e for e in project.iter()
                                    if e.tag in ('NativeReference', '_NativeReference')}
                        actual_names = {r.attrib['Name'] for r in references}
                        require(set(expected) == actual_names,
                                f"Native references differ from project: {info['id']} ({name}); "
                                f"missing={sorted(set(expected) - actual_names)}, extra={sorted(actual_names - set(expected))}")
                        for reference in references:
                            for field in ('Kind', 'ForceLoad', 'SmartLink', 'Frameworks', 'WeakFrameworks', 'LinkerFlags'):
                                value = expected[reference.attrib['Name']].findtext(field)
                                if value is not None:
                                    actual = reference.findtext(field) or ''
                                    if field in ('ForceLoad', 'SmartLink'):
                                        value, actual = value.lower(), actual.lower()
                                    require(' '.join(value.split()) == ' '.join(actual.split()),
                                            f"Native {field} metadata differs: {reference.attrib['Name']}")
    return info['dependencies']


def prepare(args):
    manifest = read(args.plan)
    packages = {p['id']: p for p in manifest['packages']}
    candidates = {}
    for path in Path(args.package_dir).glob('*.nupkg'):
        info = package_info(path.read_bytes())
        require(info['id'] in packages, f"Unexpected output package: {info['id']}")
        require(info['id'] not in candidates, f"Multiple candidates: {info['id']}")
        candidates[info['id']] = path
    require(set(candidates) == set(packages), 'Build output does not match Firebase closure')
    registries = feeds() if args.online and manifest['eligible'] else []
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    for p in manifest['packages']:
        existing = {f.name: f.fetch(p) for f in registries}
        available = [data for data in existing.values() if data is not None]
        if available:
            require(len({payload_hash(data) for data in available}) == 1, f"Feeds disagree: {p['id']}")
        data = available[0] if available else candidates[p['id']].read_bytes()
        expected_commit = manifest['sourceCommit'] if not available or p['id'] in manifest['changed'] else None
        p['packageDependencies'] = inspect_package(data, p, packages, expected_commit)
        p['file'] = f"{p['id']}.{normalized(p['version'])}.nupkg"
        p['sha256'], p['payloadHash'] = digest(data), payload_hash(data)
        p['reused'] = bool(available)
        p['destinations'] = [name for name, value in existing.items() if value is None]
        (output / p['file']).write_bytes(data)
    # Validate dependency-first order using the actual package graph as well as Cake.
    order = {p['id']: i for i, p in enumerate(manifest['packages'])}
    for p in manifest['packages']:
        for d in p['packageDependencies']:
            if d['id'] in order:
                require(order[d['id']] < order[p['id']], f"Packed dependency order differs from Cake: {p['id']} -> {d['id']}")
    write(output / 'manifest.json', manifest)


def finalize(args):
    folder = Path(args.package_dir)
    manifest = read(folder / 'manifest.json')
    results = read(args.results)
    require(set(results) == set(VALIDATIONS) and all(v == 'passed' for v in results.values()), 'Incomplete validation results')
    manifest['validation'] = results
    write(folder / 'manifest.json', manifest)
    (folder / 'SHA256SUMS').write_text(''.join(f"{p['sha256']}  {p['file']}\n" for p in manifest['packages']))
    lines = [f"# Firebase {manifest['releaseVersion'] or 'validation (publishing disabled)'}",
             '', f"Commit: `{manifest['sourceCommit']}`", '', 'All required validations passed.', '',
             '| Package | Version | Upload destinations | Reused |', '|---|---|---|---|']
    for p in manifest['packages']:
        label = f"[{p['id']}](https://www.nuget.org/packages/{p['id']}/{normalized(p['version'])})"
        lines.append(f"| {label} | {p['version']} | {', '.join(p['destinations']) or 'none'} | {p['reused']} |")
    lines += ['', f'[GitHub Packages](https://github.com/{REPO.split("/")[0]}?tab=packages&repo_name={REPO.split("/")[1]})']
    (folder / 'release-notes.md').write_text('\n'.join(lines) + '\n')
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
            summary.write('\n'.join(lines) + '\n')


def collect_validation(args):
    results = {}
    for name, variable in [('unit-tests', 'UNIT_TEST_OUTCOME'), ('package-structure', 'PACKAGE_INSPECTION_OUTCOME')]:
        require(os.environ.get(variable) == 'success', f'{name} did not succeed')
        results[name] = 'passed'
    for name in VALIDATIONS[2:]:
        result = read(Path(args.results_dir) / (name + '.json'))
        require(result.get('Success') is True and not result.get('FatalError'), f'{name} failed')
        cases = result.get('Cases', [])
        require(cases and all(c.get('Success') is True for c in cases), f'{name} has missing or failed cases')
        if name == 'surface':
            coverage = result.get('BindingSurfaceCoverage') or {}
            require(coverage.get('Target') == 'all' and coverage.get('SurfaceCount', 0) > 0
                    and coverage.get('FailedCount') == 0
                    and coverage.get('ExercisedCount', 0) + coverage.get('WaivedCount', 0) == coverage['SurfaceCount'],
                    'Incomplete binding surface coverage')
        results[name] = 'passed'
    write(Path(args.results_dir) / 'validation.json', results)


def publish_packages(folder, packages, registries, wait_seconds=1800, poll_seconds=15):
    # Cache verified identities, not package bytes. Existing immutable versions need one download.
    verified = set()
    pending = []
    for p in packages:
        for feed in registries:
            data = feed.fetch(p)
            require(data is None or payload_hash(data) == p['payloadHash'], f"Existing package conflict: {feed.name}/{p['id']}")
            if data is not None:
                verified.add((feed.name, p['id'], p['version']))
    # Submit the entire dependency-ordered set before waiting for registry indexing.
    for p in packages:
        for feed in registries:
            if (feed.name, p['id'], p['version']) in verified:
                continue
            data = feed.fetch(p)  # Recheck missing destinations immediately before writing.
            if data is not None:
                require(payload_hash(data) == p['payloadHash'], f"Existing package conflict: {feed.name}/{p['id']}")
                continue
            key = os.environ['NUGET_API_KEY'] if feed.name == 'nuget' else os.environ['GH_TOKEN']
            subprocess.run(['dotnet', 'nuget', 'push', str(folder / p['file']), '--source', feed.source,
                            '--api-key', key], check=False)
            pending.append((feed, p))
    # A push result (including a conflict) is never proof of matching published bytes.
    deadline = time.monotonic() + wait_seconds
    while pending:
        remaining = []
        for feed, p in pending:
            data = feed.fetch(p)
            if data is None:
                remaining.append((feed, p))
            else:
                require(payload_hash(data) == p['payloadHash'], f"Published package conflict: {feed.name}/{p['id']}")
                print(f"Verified {feed.name}: {p['id']} {p['version']}")
        pending = remaining
        if pending:
            outstanding = ', '.join(f"{f.name}/{p['id']} {p['version']}" for f, p in pending)
            require(time.monotonic() < deadline, f'Publication incomplete: {outstanding}; rerun failed job')
            print(f'Waiting for registry validation/indexing: {outstanding}', flush=True)
            time.sleep(min(poll_seconds, max(0, deadline - time.monotonic())))


def publish(args):
    folder = Path(args.package_dir)
    manifest = read(folder / 'manifest.json')
    require(os.environ.get('GITHUB_REF') == 'refs/heads/main' and os.environ.get('GITHUB_EVENT_NAME') in ('push', 'workflow_dispatch'), 'Only main can publish')
    require(manifest['eligible'] and manifest['sourceCommit'] == os.environ.get('GITHUB_SHA'), 'Ineligible or wrong-commit artifacts')
    require(manifest.get('validation') == dict.fromkeys(VALIDATIONS, 'passed'), 'Missing successful validation')
    require(version(manifest['releaseVersion']) >= version('12.12.0'), 'Release predates 12.12')
    latest = previous_release()
    if latest:
        require(version(latest['releaseVersion']) <= version(manifest['releaseVersion']), 'A newer release already shipped')
        if version(latest['releaseVersion']) == version(manifest['releaseVersion']):
            require(latest['sourceCommit'] == manifest['sourceCommit'], 'Release version belongs to another commit')
    metadata = read(args.metadata)
    allowed = {p['id']: p for p in metadata}
    require(set(allowed) == {p['id'] for p in manifest['packages']}, 'Manifest scope differs from Cake')
    for p in manifest['packages']:
        require(Path(p['file']).name == p['file'], 'Invalid artifact filename')
        data = (folder / p['file']).read_bytes()
        require(digest(data) == p['sha256'] and payload_hash(data) == p['payloadHash'], 'Artifact hash mismatch')
        expected_commit = manifest['sourceCommit'] if not p['reused'] or p['id'] in manifest['changed'] else None
        inspect_package(data, allowed[p['id']], allowed, expected_commit)
    registries = feeds()
    publish_packages(folder, manifest['packages'], registries)
    tag = 'firebase-v' + manifest['releaseVersion']
    # gh creates the tag at the exact tested SHA. Existing tags must never be moved.
    tags = github('git/matching-refs/tags/' + urllib.parse.quote(tag, safe=''))
    exact = next((t for t in tags if t['ref'] == 'refs/tags/' + tag), None)
    if exact:
        obj = exact['object']
        while obj['type'] == 'tag':
            obj = github('git/tags/' + obj['sha'])['object']
        require(obj['sha'] == manifest['sourceCommit'], 'Existing tag points to another commit')
    if latest and latest['releaseVersion'] == manifest['releaseVersion']:
        print('Release already complete')
        return
    assets = [str(folder / p['file']) for p in manifest['packages']]
    assets += [str(folder / n) for n in ('manifest.json', 'SHA256SUMS')]
    existing = subprocess.run(['gh', 'release', 'view', tag, '--repo', REPO, '--json', 'isDraft,targetCommitish'],
                              text=True, capture_output=True)
    if existing.returncode == 0:
        draft = json.loads(existing.stdout)
        require(draft['isDraft'], 'An unexpected published release already exists')
        require(draft['targetCommitish'] == manifest['sourceCommit'], 'Existing draft targets another commit')
    else:
        run('gh', 'release', 'create', tag, '--repo', REPO, '--target', manifest['sourceCommit'], '--draft',
            '--title', 'Firebase ' + manifest['releaseVersion'], '--generate-notes', '--notes-file', str(folder / 'release-notes.md'))
    # A draft makes interrupted release-asset uploads resumable without advertising completion.
    run('gh', 'release', 'upload', tag, *assets, '--repo', REPO, '--clobber')
    run('gh', 'release', 'edit', tag, '--repo', REPO, '--draft=false')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    s = sub.add_parser('snapshot')
    s.add_argument('--metadata', default='output/firebase-metadata.json')
    s.add_argument('--output', default=BASELINE)
    p = sub.add_parser('plan')
    p.add_argument('--metadata', default='output/firebase-metadata.json')
    p.add_argument('--output', default='output/firebase-plan.json')
    p.add_argument('--base')
    p.add_argument('--online', action='store_true')
    p.add_argument('--manual', action='store_true')
    p = sub.add_parser('prepare')
    p.add_argument('--plan', default='output/firebase-plan.json')
    p.add_argument('--package-dir', default='output')
    p.add_argument('--output', default='output/firebase-candidate')
    p.add_argument('--online', action='store_true')
    p = sub.add_parser('finalize')
    p.add_argument('--package-dir', default='output/firebase-candidate')
    p.add_argument('--results', required=True)
    p = sub.add_parser('collect_validation')
    p.add_argument('--results-dir', required=True)
    p = sub.add_parser('publish')
    p.add_argument('--package-dir', default='output/firebase-candidate')
    p.add_argument('--metadata', default='output/firebase-metadata.json')
    sub.add_parser('probe-feeds')
    args = parser.parse_args()
    if args.command == 'snapshot':
        packages = [{k: p[k] for k in ('id', 'version', 'fingerprint')} for p in snapshot(read(args.metadata))]
        write(args.output, {'schemaVersion': 1, 'published': False, 'packages': packages})
    elif args.command == 'probe-feeds':
        # Small existing dependency exercises actual download authorization, not just the public index.
        for feed in feeds():
            data = feed.fetch({'id': 'AdamE.Google.iOS.PromisesObjC', 'version': '2.4.0.5'})
            print(f'{feed.name}: authenticated lookup succeeded ({"present" if data else "absent"})')
    else:
        globals()[args.command](args)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, RuntimeError, urllib.error.URLError) as error:
        print(f'ERROR: {error}', file=sys.stderr)
        sys.exit(1)
