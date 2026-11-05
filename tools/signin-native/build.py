#!/usr/bin/env python3
"""Producer-only pinned Sign-In source build. Consumers receive ordinary binding resources.

Only the four owned SDK targets are archived. Shared Firebase support targets built by SwiftPM
are deliberately excluded, preventing a second AppCheckCore/GTMSessionFetcher/Promises owner.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / 'tools/SignInNative'
DESTINATION = ROOT / 'externals/signin-native'
TARGETS = {'GoogleSignIn': ('GoogleSignIn-iOS', '10.1.0'), 'AppAuth': ('AppAuth-iOS', '3.0.0'),
           'AppAuthCore': ('AppAuth-iOS', '3.0.0'), 'GTMAppAuth': ('GTMAppAuth', '6.0.0')}
PLATFORMS = {'ios': ('generic/platform=iOS', 'Release-iphoneos', ['arm64']),
             'simulator': ('generic/platform=iOS Simulator', 'Release-iphonesimulator', ['arm64', 'x86_64']),
             'maccatalyst': ('generic/platform=macOS,variant=Mac Catalyst', 'Release-maccatalyst', ['arm64', 'x86_64'])}
BUNDLES = ['GoogleSignIn_GoogleSignIn.bundle', 'AppAuth_AppAuth.bundle',
           'AppAuth_AppAuthCore.bundle', 'GTMAppAuth_GTMAppAuth.bundle']


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def snapshot(root):
    return {p.relative_to(root).as_posix(): sha(p) for p in sorted(root.rglob('*'))
            if p.is_file() and p.name != 'NATIVE-BUILD.json'}


def run(args, **kwargs):
    subprocess.run([str(a) for a in args], check=True, **kwargs)


def validate_contract():
    pins = json.loads((PACKAGE / 'Package.resolved').read_text())['pins']
    expected = {'googlesignin-ios': '10.1.0', 'appauth-ios': '3.0.0', 'gtmappauth': '6.0.0',
                'app-check': '12.0.0', 'gtm-session-fetcher': '5.3.1', 'googleutilities': '8.1.5', 'promises': '2.4.1',
                'interop-ios-for-google-sdks': '101.0.0'}
    actual = {p['identity']: p['state'].get('version') for p in pins}
    if actual != expected:
        raise ValueError('Sign-In source lock does not match the reviewed Firebase 13 closure')
    origins = {'appauth-ios': 'openid/AppAuth-iOS', 'googlesignin-ios': 'google/GoogleSignIn-iOS',
               'gtmappauth': 'google/GTMAppAuth', 'app-check': 'google/app-check',
               'gtm-session-fetcher': 'google/gtm-session-fetcher', 'googleutilities': 'google/GoogleUtilities',
               'promises': 'google/promises', 'interop-ios-for-google-sdks': 'google/interop-ios-for-google-sdks'}
    for pin in pins:
        if pin['location'] != 'https://github.com/' + origins[pin['identity']] + '.git' or len(pin['state']['revision']) != 40:
            raise ValueError('Unpinned or non-official Sign-In source dependency')
    project = ET.parse(ROOT / 'source/Google/SignIn/SignIn.csproj')
    manifest = json.loads((ROOT / 'source/Firebase/native-assets.json').read_text())
    shared_names = {'AppCheckCore', 'GTMSessionFetcher', 'GoogleUtilities', 'PromisesObjC'}
    shared_versions = {
        p['package']: f"[{p['version'].removesuffix('.0') if p['version'].count('.') == 3 else p['version']}]"
        for p in manifest['packages'] if p['id'].rsplit('.', 1)[-1] in shared_names
    }
    references = {p.get('Include'): p.get('Version') for p in project.iter('PackageReference')}
    if references != shared_versions or len(references) != len(shared_names):
        raise ValueError('Sign-In NuGet dependencies must match the exact Firebase archive closure')
    if project.findtext('.//PackageVersion') != '10.1.0.1':
        raise ValueError('Expected the Firebase 13 binding revision 10.1.0.1')
    props = ET.parse(ROOT / 'source/Google/SignIn/SignIn.PackedNative.props')
    frameworks = {p.get('Include').split(')')[-1] for p in props.iter('NativeReference')}
    if frameworks != {n + '.xcframework' for n in TARGETS}:
        raise ValueError('Sign-In producer framework ownership mismatch')
    targets = (ROOT / 'source/Google/SignIn/SignIn.targets').read_text()
    if any(b not in targets for b in BUNDLES) or any(s in targets for s in ['XamarinBuildDownload', 'xcodebuild', 'Package.swift']):
        raise ValueError('Sign-In consumer resource contract mismatch')
    return pins


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--validate-only', action='store_true')
    args = parser.parse_args()
    pins = validate_contract()
    if args.validate_only:
        print('Validated pinned Sign-In 10.1 producer source graph without acquisition.')
        return
    xcode = subprocess.check_output(['xcodebuild', '-version']).decode().strip()
    key = {'recipe': sha(Path(__file__)), 'manifest': sha(PACKAGE / 'Package.swift'),
           'lock': sha(PACKAGE / 'Package.resolved'), 'xcode': xcode}
    marker = DESTINATION / 'NATIVE-BUILD.json'
    if marker.exists():
        previous = json.loads(marker.read_text())
        if previous['inputs'] == key and previous['files'] == snapshot(DESTINATION):
            print('Reusing verified source-built Sign-In 10.1 native artifact.')
            return
    derived_root = ROOT / 'externals/signin10-build'
    derived_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.signin-native-', dir=ROOT / 'externals') as temp:
        out = Path(temp)
        slices = {name: [] for name in TARGETS}
        for platform, (destination, product_directory, architectures) in PLATFORMS.items():
            derived = derived_root / platform
            log = derived_root / (platform + '.log')
            print('Building Sign-In producer closure for ' + platform, flush=True)
            with log.open('w') as stream:
                run(['xcodebuild', '-scheme', 'AdamESignInNative', '-destination', destination,
                     '-configuration', 'Release', '-derivedDataPath', derived,
                     '-clonedSourcePackagesDirPath', PACKAGE / '.build',
                     '-onlyUsePackageVersionsFromResolvedFile', 'CODE_SIGNING_ALLOWED=NO', 'ONLY_ACTIVE_ARCH=NO',
                     'ARCHS=' + ' '.join(architectures), 'GCC_GENERATE_DEBUGGING_SYMBOLS=NO',
                     'SWIFT_SERIALIZE_DEBUGGING_OPTIONS=NO', 'build'], cwd=PACKAGE, stdout=stream, stderr=subprocess.STDOUT)
            for pin in pins:
                checkout = PACKAGE / '.build/checkouts' / pin['identity']
                revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=checkout, text=True).strip()
                changes = subprocess.check_output(['git', 'status', '--porcelain'], cwd=checkout, text=True)
                if revision != pin['state']['revision'] or changes:
                    raise ValueError('Source checkout does not match the reviewed lock: ' + pin['identity'])
            products = derived / 'Build/Products' / product_directory
            for name, (repository, version) in TARGETS.items():
                framework = out / 'slices' / platform / (name + '.framework')
                framework.mkdir(parents=True)
                archives = []
                for architecture in architectures:
                    intermediates = derived / 'Build/Intermediates.noindex'
                    object_dirs = [
                        directory for suffix in ('.build', '-t.build')
                        for directory in intermediates.glob(f'**/{name}{suffix}/Objects-normal/{architecture}')
                    ]
                    objects = sorted(object_dirs[0].glob('*.o')) if len(object_dirs) == 1 else []
                    if not objects:
                        raise ValueError(f'Missing {name}/{architecture} target objects; see {log}')
                    archive = out / 'slices' / platform / (name + '-' + architecture + '.a')
                    run(['xcrun', 'libtool', '-static', '-o', archive, *sorted(objects)],
                        env=dict(os.environ, ZERO_AR_DATE='1'))
                    archives.append(archive)
                run(['xcrun', 'lipo', '-create', *archives, '-output', framework / name])
                headers = framework / 'Headers'; headers.mkdir()
                checkout = PACKAGE / '.build/checkouts' / repository
                if name == 'GoogleSignIn':
                    sources = (checkout / 'GoogleSignIn/Sources/Public/GoogleSignIn').glob('*.h')
                elif name in ('AppAuth', 'AppAuthCore'):
                    sources = (checkout / 'Sources' / name).rglob('*.h')
                else:
                    sources = (header for suffix in ('.build', '-t.build')
                               for header in (derived / 'Build/Intermediates.noindex').glob(
                                   f'**/GTMAppAuth{suffix}/**/GTMAppAuth-Swift.h'))
                for header in sources:
                    shutil.copy2(header, headers / header.name)
                info = {'CFBundleIdentifier': 'org.adame.native.' + name, 'CFBundleName': name,
                        'CFBundleExecutable': name, 'CFBundlePackageType': 'FMWK',
                        'CFBundleShortVersionString': version, 'CFBundleVersion': version,
                        'MinimumOSVersion': '15.0'}
                (framework / 'Info.plist').write_bytes(plistlib.dumps(info))
                slices[name].append(framework)
            resource_root = out / 'resources' / platform; resource_root.mkdir(parents=True)
            for bundle in BUNDLES:
                source = products / bundle
                if not source.is_dir():
                    raise ValueError('Missing producer resource ' + str(source))
                shutil.copytree(source, resource_root / bundle, symlinks=True)
        for name, frameworks in slices.items():
            xcframework = out / (name + '.xcframework')
            command = ['xcodebuild', '-create-xcframework']
            for framework in frameworks:
                command += ['-framework', framework]
            run(command + ['-output', xcframework])
            info = plistlib.loads((xcframework / 'Info.plist').read_bytes())
            for platform in ['ios', 'maccatalyst']:
                selected = [s for s in info['AvailableLibraries'] if
                            (s.get('SupportedPlatformVariant') == 'maccatalyst') == (platform == 'maccatalyst')]
                wrapper = out / platform / xcframework.name; wrapper.mkdir(parents=True)
                for library in selected:
                    shutil.copytree(xcframework / library['LibraryIdentifier'], wrapper / library['LibraryIdentifier'], symlinks=True)
                (wrapper / 'Info.plist').write_bytes(plistlib.dumps(dict(info, AvailableLibraries=selected)))
        notices = out / 'native-notices'; notices.mkdir()
        for repository in sorted({r for r, v in TARGETS.values()}):
            checkout = PACKAGE / '.build/checkouts' / repository
            files = [p for p in checkout.iterdir() if p.is_file() and p.name.upper().startswith(('LICENSE', 'NOTICE', 'COPYING'))]
            if not files:
                raise ValueError('Missing upstream source license: ' + repository)
            for file in files:
                shutil.copy2(file, notices / (repository + '-' + file.name))
        shutil.rmtree(out / 'slices')
        (out / 'NATIVE-BUILD.json').write_text(json.dumps({'inputs': key, 'source_pins': pins,
            'owned_frameworks': list(TARGETS), 'excluded_shared_targets': ['AppCheckCore', 'GTMSessionFetcherCore', 'GoogleUtilities', 'FBLPromises'],
            'files': snapshot(out)}, indent=2) + '\n')
        # All inputs are locked and every generated native file is checksummed before packing.
        # No producer files or build tools are needed on consumer machines.
        archive = ROOT / 'externals/signin-native.zip'
        run(['ditto', '-c', '-k', out, archive])
        if DESTINATION.exists():
            backup = out.parent / (out.name + '-old')
            DESTINATION.rename(backup)
            try:
                shutil.copytree(out, DESTINATION, symlinks=True)
            except Exception:
                if DESTINATION.exists():
                    shutil.rmtree(DESTINATION)
                backup.rename(DESTINATION)
                raise
            shutil.rmtree(backup)
        else:
            shutil.copytree(out, DESTINATION, symlinks=True)
        print('Produced source-built Sign-In native archive SHA-256: ' + sha(archive))


if __name__ == '__main__':
    main()
