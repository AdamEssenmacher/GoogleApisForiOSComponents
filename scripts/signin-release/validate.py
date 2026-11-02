#!/usr/bin/env python3
"""Inspect the sealed Sign-In closure and retain exact CocoaPods resolution evidence."""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import plistlib
import re
import shutil
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def pod_versions(text):
    versions = {}
    section = text.split('PODS:\n', 1)[1].split('\nDEPENDENCIES:', 1)[0]
    for name, version in re.findall(r'^  - ([^\s(]+) \(([^)]+)\)', section, re.M):
        name = name.strip('\"\'').split('/')[0]
        if name in versions and versions[name] != version:
            raise ValueError('Conflicting resolved pod versions: ' + name)
        versions[name] = version
    return versions


def native_libraries(libraries, framework):
    indexed = {}
    for lib in libraries:
        key = (lib['SupportedPlatform'], lib.get('SupportedPlatformVariant', ''))
        if key in indexed:
            raise ValueError('Duplicate native platform slice: ' + framework)
        indexed[key] = lib
    actual = {key: set(lib['SupportedArchitectures']) for key, lib in indexed.items()}
    expected = {('ios', ''): {'arm64'}, ('ios', 'simulator'): {'arm64', 'x86_64'},
                ('ios', 'maccatalyst'): {'arm64', 'x86_64'}}
    if actual != expected:
        raise ValueError('Unexpected native slices/architectures: ' + framework)
    return indexed


def framework_version(read, entries, root, expected):
    paths = [root + suffix for suffix in ('/Info.plist', '/Resources/Info.plist',
                                          '/Versions/A/Resources/Info.plist')]
    actual = None
    for path in paths:
        if path not in entries:
            continue
        try:
            actual = plistlib.loads(read(path)).get('CFBundleShortVersionString')
            break
        except plistlib.InvalidFileException:
            continue  # A versioned-framework symlink can precede its actual plist.
    if not actual or release.version(actual) != release.version(expected):
        raise ValueError(f'Native framework version mismatch: {root}; expected {expected}, found {actual}')


def error_code_values(header, managed_source, prefix='kGIDSignInErrorCode', enum_name='ErrorCode', label='Sign-In'):
    native = {name.casefold(): int(value) for name, value in re.findall(
        r'\b' + prefix + r'(\w+)\s*=\s*(-?\d+)', header)}
    enum = re.search(r'\benum ' + enum_name + r'\s*:\s*long\s*\{([^}]+)\}', managed_source)
    managed = {} if enum is None else {name.casefold(): int(value) for name, value in re.findall(
        r'\b(\w+)\s*=\s*(-?\d+)', enum[1])}
    if not native or native != managed:
        raise ValueError(f'Native/managed {label} error codes differ: native={native}, managed={managed}')


def framework_header(entries, root, name):
    paths = [root + suffix + name for suffix in ('/Headers/', '/Versions/A/Headers/')]
    return next((path for path in paths if path in entries), None)


def framework_error_codes(read, entries, root, managed_source):
    header = framework_header(entries, root, 'GIDSignIn.h')
    app_check = framework_header(entries, root, 'GIDAppCheckError.h')
    if header is None or app_check is None:
        raise ValueError('Missing packed Sign-In error-code header: ' + root)
    error_code_values(read(header).decode(), managed_source)
    error_code_values(read(app_check).decode(), managed_source,
                      prefix='kGIDAppCheck', enum_name='AppCheckErrorCode', label='App Check')


def signin_bundle(data, platform, rid):
    """Return exact resource bytes from the selected packed Sign-In framework slice."""
    with zipfile.ZipFile(io.BytesIO(data)) as package:
        resource = next(n for n in package.namelist() if n.startswith('lib/net10.0-' + platform)
                        and (n.endswith('.resources.zip') or n.endswith('.resources/manifest')))
        native = zipfile.ZipFile(io.BytesIO(package.read(resource))) if resource.endswith('.zip') else None
        try:
            if native:
                read, entries = native.read, native.namelist()
            else:
                prefix = resource[:-len('manifest')]
                read = lambda n: package.read(prefix + n)
                entries = [n[len(prefix):] for n in package.namelist() if n.startswith(prefix)]
            libs = plistlib.loads(read('GoogleSignIn.xcframework/Info.plist'))['AvailableLibraries']
            variant = 'maccatalyst' if platform == 'maccatalyst' else ('simulator' if rid.startswith('iossimulator') else '')
            lib = native_libraries(libs, 'GoogleSignIn.xcframework')[('ios', variant)]
            start = 'GoogleSignIn.xcframework/' + lib['LibraryIdentifier'] + '/'
            bundles = {n.split('GoogleSignIn.bundle/', 1)[0] + 'GoogleSignIn.bundle/' for n in entries
                       if n.startswith(start) and 'GoogleSignIn.bundle/' in n}
            if len(bundles) != 1:
                raise ValueError('Missing/ambiguous packed Sign-In resource bundle')
            prefix = bundles.pop()
            return {n[len(prefix):]: hashlib.sha256(read(n)).hexdigest() for n in entries
                    if n.startswith(prefix) and not n.endswith('/')}
        finally:
            if native:
                native.close()


def inspect(data, package):
    project = ET.parse(ROOT / package['project'])
    expected = {Path(e.attrib['Include'].replace('\\', '/')).name for e in project.iter()
                if e.tag in ('NativeReference', '_NativeReference')}
    versions = {p['FrameworkName'] + '.xcframework': p['Version'] for p in package['pods']}
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = archive.namelist()
        if any(n.endswith(('.podspec', '/Podfile', '/Podfile.lock')) for n in names):
            raise ValueError('Consumer package contains producer CocoaPods inputs')
        for platform in ('ios', 'maccatalyst'):
            paths = [n for n in names if n.startswith('lib/net10.0-' + platform) and
                     (n.endswith('.resources.zip') or n.endswith('.resources/manifest'))]
            if len(paths) != 1:
                raise ValueError('Missing/duplicate native resource package: ' + package['id'])
            resource = paths[0]
            if resource.endswith('.zip'):
                native = zipfile.ZipFile(io.BytesIO(archive.read(resource)))
                read, entries = native.read, native.namelist()
            else:
                native = None
                prefix = resource[:-len('manifest')]
                read = lambda n: archive.read(prefix + n)
                entries = [n[len(prefix):] for n in names if n.startswith(prefix)]
            try:
                manifest = ET.fromstring(read('manifest'))
                references = {r.attrib['Name'] for r in manifest.findall('NativeReference')}
                payloads = {part for n in entries for part in n.split('/') if part.endswith('.xcframework')}
                if references != expected or payloads != expected:
                    raise ValueError('Native ownership differs from project: ' + package['id'])
                for framework in expected:
                    libraries = plistlib.loads(read(framework + '/Info.plist'))['AvailableLibraries']
                    native_libraries(libraries, framework)
                    for lib in libraries:
                        root = framework + '/' + lib['LibraryIdentifier'] + '/' + lib['LibraryPath']
                        framework_version(read, entries, root, versions[framework])
                        if framework == 'GoogleSignIn.xcframework':
                            framework_error_codes(read, entries, root,
                                                  (ROOT / 'source/Google/SignIn/Enums.cs').read_text())
                        binary = lib.get('BinaryPath', lib['LibraryPath'].removesuffix('.framework'))
                        # Versioned Catalyst frameworks retain their symlink-shaped package layout.
                        candidates = ([framework + '/' + lib['LibraryIdentifier'] + '/' + binary]
                                      if lib.get('BinaryPath') else [root + '/' + binary, root + '/Versions/A/' + binary])
                        if not any(n in entries and len(read(n)) > 1024 for n in candidates):
                            raise ValueError('Missing native binary: ' + root)
                if package['id'].endswith('.SignIn') and not any('GoogleSignIn.bundle/' in n for n in entries):
                    raise ValueError('Missing embedded Sign-In resources')
            finally:
                if native is not None:
                    native.close()


def validate_resolution(actual, metadata):
    expected = {}
    for package in metadata:
        for pod in package['pods']:
            if pod['Name'] in expected and expected[pod['Name']] != pod['Version']:
                raise ValueError('Conflicting metadata pod pins: ' + pod['Name'])
            expected[pod['Name']] = pod['Version']
    if actual != expected:
        raise ValueError('Sign-In CocoaPods closure differs from exact metadata pins: ' + str(actual))
    return expected


def main():
    release.configure_channel('signin')
    metadata = release.read(ROOT / 'output/signin-metadata.json')
    release.validate_scope(metadata)
    folder = ROOT / 'output/signin-candidate'
    candidate = release.read(folder / 'manifest.json')
    release.validate_manifest_channel(candidate)
    expected = {pod['Name']: pod['Version'] for package in metadata for pod in package['pods']}
    root = next(p for p in metadata if p['id'] == 'AdamE.Google.iOS.SignIn')
    native_version = '.'.join(map(str, release.version(root['version'])[:3]))
    private_versions = {'9.0.0': ('2.0.0', '5.0.0'), '9.1.0': ('2.0.0', '5.0.0'),
                        '9.2.0': ('2.1.0', '5.0.0'), '10.0.0': ('3.0.0', '6.0.0'), '10.1.0': ('3.0.0', '6.0.0')}
    if native_version not in private_versions or expected['GoogleSignIn'] != native_version:
        raise ValueError('Sign-In managed/native versions differ')
    if (expected['AppAuth'], expected['GTMAppAuth']) != private_versions[native_version]:
        raise ValueError('Sign-In private native dependency versions differ from the release sequence')
    if release.version(expected['AppCheckCore'])[0] != 11:
        raise ValueError('This release lane requires the Firebase-12-compatible AppCheckCore 11 baseline')
    lock = ROOT / 'externals/build/Google.SignIn/Podfile.lock'
    actual = pod_versions(lock.read_text())
    validate_resolution(actual, metadata)
    evidence = ROOT / 'output/signin-resolution'; evidence.mkdir(parents=True, exist_ok=True)
    shutil.copy2(lock, evidence / 'Podfile.lock')
    release.write(evidence / 'native-versions.json', actual)
    packages = {p['id']: p for p in metadata}
    for package in candidate['packages']:
        data = (folder / package['file']).read_bytes()
        if hashlib.sha256(data).hexdigest() != package['sha256']:
            raise ValueError('Candidate hash mismatch')
        release.inspect_package(data, packages[package['id']], packages)
        if any(d['id'] == 'Xamarin.Build.Download' for d in release.package_info(data)['dependencies']):
            raise ValueError('Packed Sign-In closure unexpectedly depends on XBD')
        inspect(data, packages[package['id']])
    print('Validated five packed Sign-In packages and exact native resolution:', actual)


if __name__ == '__main__':
    main()
