"""Release adapter for the static Sign-In binding and Firebase 13 support packages."""
import io
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PROJECT = 'source/Google/SignIn/SignIn.csproj'
DELIVERY = 'firebase13-static'
FRAMEWORKS = {'GoogleSignIn', 'AppAuth', 'AppAuthCore', 'GTMAppAuth'}
BUNDLES = {'GoogleSignIn_GoogleSignIn.bundle', 'AppAuth_AppAuth.bundle',
           'AppAuth_AppAuthCore.bundle', 'GTMAppAuth_GTMAppAuth.bundle'}


def metadata(release):
    from native import order, validate_schema
    manifest = release.read(ROOT / 'source/Firebase/native-assets.json')
    packages = validate_schema(manifest)
    support = order(packages, ','.join(p['id'] for p in packages.values()
                                       if p['package'] in release.SIGNIN_SHARED_IDS))
    release.require({p['package'] for p in support} == release.SIGNIN_SHARED_IDS,
                    'Unexpected Sign-In support closure')
    records = [
        {'key': p['id'], 'id': p['package'], 'version': p['version'], 'project': p['project'],
         'dependencies': p['dependencies'], 'firebase': False, 'delivery': 'firebase-xbd',
         'archive': manifest['source'], 'nativePackage': p, 'pods': []}
        for p in support
    ]
    project = ET.parse(ROOT / PROJECT)
    refs = [(p.get('Include'), p.get('Version')) for p in project.iter('PackageReference')]
    expected = {p['id']: '[' + release.normalized(p['version']) + ']' for p in records}
    release.require(len(refs) == len(expected) and dict(refs) == expected,
                    'Sign-In package references differ from the exact Firebase 13 closure')
    records.append({'key': 'Google.SignIn', 'id': project.findtext('.//PackageId'),
                    'version': project.findtext('.//PackageVersion'), 'project': PROJECT,
                    'dependencies': [p['id'] for p in records], 'firebase': False,
                    'delivery': DELIVERY, 'firebaseVersion': manifest['releaseVersion'], 'pods': []})
    release.validate_scope(records)
    return records


def integration_metadata(release):
    records = release.read(ROOT / 'source/Firebase/native-assets.json')['packages']
    return [{**p, 'id': p['package']} for p in records]


def snapshot(release, packages):
    release.require(packages == metadata(release), 'Sign-In 13 metadata differs from repository inputs')
    tracked = release.run('git', 'ls-files').splitlines()
    shared = {'global.json', 'Directory.Build.props', 'source/AssemblyInfo.cs'}
    recipe = {p for p in tracked if p.startswith(('tools/signin-native/', 'tools/SignInNative/'))}
    common = {p for p in tracked if p.startswith(('tools/firebase13/', 'source/Firebase/native/'))}
    result = []
    for package in packages:
        directory = Path(package['project']).parent.as_posix() + '/'
        files = {p for p in tracked if p.startswith(directory)} | shared
        if package['delivery'] == DELIVERY:
            files |= recipe | {'docs/NUGET_README.md', 'icons/googleiossignin_128x128.png'}
        else:
            # Include the shared producer and common targets, while taking this
            # package's declaration and the archive identity from its metadata.
            files |= {p for p in common if p.endswith(('.py', 'Common.targets', 'NOTICES.txt', '.proj'))}
        data = {'package': package, 'files': {p: release.digest((ROOT / p).read_bytes()) for p in sorted(files)}}
        if package['delivery'] == DELIVERY:
            data['nativeBuild'] = release.read(ROOT / release.CONFIG)['nativeBuild']
        result.append({**package, 'fingerprint': release.digest(json.dumps(data, sort_keys=True).encode())})
    return result


def static_archive(data):
    if data.startswith(b'!<arch>\n'):
        return True
    if data[:4] != b'\xca\xfe\xba\xbe' or len(data) < 8:
        return False
    count = struct.unpack_from('>I', data, 4)[0]
    if not 1 <= count <= 2 or len(data) < 8 + count * 20:
        return False
    return all(data[offset:offset + 8] == b'!<arch>\n'
               for offset in (struct.unpack_from('>I', data, 16 + i * 20)[0] for i in range(count)))


def inspect(release, data, package, packages):
    if package['delivery'] == 'firebase-xbd':
        from native import inspect_package
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / (package['id'] + '.nupkg')
            file.write_bytes(data)
            inspect_package(file, package['nativePackage'])
        return
    info = release.package_info(data)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        spec = ET.fromstring(archive.read(next(n for n in info['files'] if n.endswith('.nuspec'))))
        groups = spec.findall('./{*}metadata/{*}dependencies/{*}group')
        expected = {p: '[' + release.normalized(packages[p]['version']) + ']' for p in package['dependencies']}
        release.require(len(groups) == 2, 'Expected two Sign-In dependency groups')
        for group in groups:
            deps = [(d.get('id'), d.get('version')) for d in group.findall('{*}dependency')]
            release.require(len(deps) == len(expected) and dict(deps) == expected,
                            'Sign-In dependency groups differ from the Firebase 13 closure')
        for platform, variants in [('ios', {'', 'simulator'}), ('maccatalyst', {'maccatalyst'})]:
            prefix = f'lib/net10.0-{platform}26.0/Google.SignIn'
            release.require(prefix + '.dll' in info['files'], f'Missing Sign-In {platform} assembly')
            with zipfile.ZipFile(io.BytesIO(archive.read(prefix + '.resources.zip'))) as native:
                references = ET.fromstring(native.read('manifest')).findall('NativeReference')
                release.require(len(references) == len(FRAMEWORKS) and
                                {r.get('Name') for r in references} == {f + '.xcframework' for f in FRAMEWORKS},
                                'Unexpected packed Sign-In framework ownership')
                props = ET.parse(ROOT / Path(PROJECT).with_name('SignIn.PackedNative.props'))
                metadata_by_name = {Path(r.get('Include').split(')')[-1]).name: r
                                    for r in props.iter('NativeReference')}
                for ref in references:
                    framework = ref.get('Name')
                    expected_ref = metadata_by_name[framework]
                    for field in ('Kind', 'ForceLoad', 'SmartLink', 'Frameworks', 'WeakFrameworks', 'LinkerFlags'):
                        actual = ' '.join((ref.findtext(field) or '').split())
                        expected_value = ' '.join((expected_ref.findtext(field) or '').split())
                        if field in ('ForceLoad', 'SmartLink'):
                            actual, expected_value = actual.lower(), expected_value.lower()
                        release.require(actual == expected_value, f'Sign-In native metadata differs: {framework}/{field}')
                    libs = plistlib.loads(native.read(framework + '/Info.plist'))['AvailableLibraries']
                    release.require(len(libs) == len(variants) and
                                    {lib.get('SupportedPlatformVariant', '') for lib in libs} == variants,
                                    f'Unexpected {platform} slices: {framework}')
                    for lib in libs:
                        variant = lib.get('SupportedPlatformVariant', '')
                        architectures = {'arm64', 'x86_64'} if variant else {'arm64'}
                        release.require(lib['SupportedPlatform'] == 'ios' and set(lib['SupportedArchitectures']) == architectures,
                                        f'Unexpected {platform} architectures: {framework}')
                        binary = '/'.join((framework, lib['LibraryIdentifier'], lib['LibraryPath'], Path(framework).stem))
                        release.require(static_archive(native.read(binary)), f'Expected static archive: {binary}')
        for platform in ('ios', 'simulator', 'maccatalyst'):
            for bundle in BUNDLES:
                release.require(any(n.startswith(f'build/native-resources/{platform}/{bundle}/') and not n.endswith('/')
                                    for n in info['files']), f'Missing Sign-In resource bundle: {platform}/{bundle}')
        for folder, source in [('build', 'SignIn.targets'), ('buildTransitive', 'SignIn.buildTransitive.targets')]:
            release.require(archive.read(f'{folder}/{package["id"]}.targets') == (ROOT / Path(PROJECT).with_name(source)).read_bytes(),
                            'Stale Sign-In package targets')
        provenance = json.loads(archive.read('native-provenance/NATIVE-BUILD.json'))
        pins = release.read(ROOT / 'tools/SignInNative/Package.resolved')['pins']
        release.require(provenance['source_pins'] == pins and set(provenance['owned_frameworks']) == FRAMEWORKS,
                        'Wrong Sign-In source provenance')
        for repository in ('GoogleSignIn-iOS', 'AppAuth-iOS', 'GTMAppAuth'):
            release.require(any(n.startswith(f'native-notices/{repository}-LICENSE') for n in info['files']),
                            'Missing upstream Sign-In license: ' + repository)


def published_fixtures(release, folder):
    manifest = release.read(ROOT / 'source/Firebase/native-assets.json')
    tag = 'firebase-v' + manifest['releaseVersion']
    previous = release.github('releases/tags/' + tag)
    release.require(not previous['draft'] and not previous['prerelease'], f'{tag} must be released first')
    asset = next((a for a in previous['assets'] if a['name'] == 'manifest.json'), None)
    release.require(asset is not None, 'Firebase 13 release is missing its sealed manifest')
    sealed = json.loads(release.request(asset['browser_download_url']))
    release.require(sealed.get('channel', 'firebase') == 'firebase' and sealed['releaseVersion'] == manifest['releaseVersion'] and sealed.get('validation')
                    and all(v == 'passed' for v in sealed['validation'].values()), 'Firebase release evidence is incomplete')
    versions = {p['id']: p for p in sealed['packages']}
    registries = release.feeds()
    from native import inspect_package
    for p in manifest['packages']:
        package = versions.get(p['package'])
        release.require(package and release.version(package['version']) == release.version(p['version']),
                        'Firebase release package version differs: ' + p['package'])
        payloads = [f.fetch(package) for f in registries]
        release.require(all(data is not None and release.payload_hash(data) == package['payloadHash'] for data in payloads),
                        'Both feeds must contain the sealed Firebase package: ' + p['package'])
        path = folder / f"{p['package']}.{release.normalized(p['version'])}.nupkg"
        path.write_bytes(payloads[0])
        inspect_package(path, p)


def produce(release, plan, feed, closure):
    feed.mkdir(parents=True, exist_ok=True)
    closure.mkdir(parents=True, exist_ok=True)
    if plan['eligible']:
        published_fixtures(release, feed)
    else:
        subprocess.run([sys.executable, 'tools/firebase13/native.py', 'nuget'], cwd=ROOT, check=True)
    subprocess.run([sys.executable, 'tools/signin-native/build.py'], cwd=ROOT, check=True)
    project = ROOT / PROJECT
    subprocess.run(['dotnet', 'restore', str(project), '--source', str(feed), '--source',
                    'https://api.nuget.org/v3/index.json'], cwd=ROOT, check=True)
    subprocess.run(['dotnet', 'pack', str(project), '--no-restore', '-c', 'Release', '-o', str(feed),
                    '-p:RepositoryCommit=' + plan['sourceCommit']], cwd=ROOT, check=True)
    for p in plan['packages']:
        name = f"{p['id']}.{release.normalized(p['version'])}.nupkg"
        shutil.copyfile(feed / name, closure / name)


def validate_consumer(release, result, manifest, lane):
    release.require(result.get('sourceCommit') == manifest['sourceCommit'] == os.environ.get('BUILD_COMMIT'),
                    'Sign-In evidence has the wrong source commit')
    release.require(result.get('Lane') == lane, 'Wrong Sign-In validation lane')
    expected = {p['id']: p['sha256'] for p in manifest['packages']}
    hashes = result.get('packageHashes', {})
    release.require(all(hashes.get(k) == v for k, v in expected.items()), 'Consumer evidence has wrong Sign-In package bytes')
    fixtures = {}
    if lane == 'combined':
        versions = release.fixture_packages(integration_metadata(release))
        for package_id, version in versions.items():
            name = f"{package_id}.{release.normalized(version)}.nupkg"
            fixtures[package_id] = release.digest((ROOT / 'output/firebase13' / name).read_bytes())
    release.require(hashes == {**expected, **fixtures}, 'Wrong Firebase integration fixture bytes')
    checks = set(result.get('Checks', []))
    builds = {f'{platform}-{shape}-{rid}'
              for platform, rids in [('ios', ('iossimulator-arm64', 'ios-arm64')),
                                     ('maccatalyst', ('maccatalyst-arm64',))]
              for shape in ('direct', 'transitive') for rid in rids}
    restores = {f'restore-{platform}-{shape}' for platform in ('ios', 'maccatalyst')
                for shape in ('direct', 'transitive')}
    release.require(restores | builds | {b + '-offline-rebuild' for b in builds} <= checks,
                    'Incomplete static Sign-In platform matrix')
    suffix = ' firebase13=FIRAuth,FIRAnalytics,FIRFirestore' if lane == 'combined' else ''
    probes = [re.fullmatch(r'PASS classes=8 selectors=(\d+) claims=auth_time,amr wrapper=wrapperIdentifier' + suffix, c)
              for c in checks]
    release.require(any(p and int(p[1]) >= 73 for p in probes), 'Missing Sign-In runtime and selector evidence')
