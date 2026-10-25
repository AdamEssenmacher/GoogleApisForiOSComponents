import importlib.util
import io
import json
import os
from pathlib import Path
import plistlib
import tempfile
import subprocess
import sys
import types
import unittest
from unittest.mock import patch
import urllib.error
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def package(package_id='AdamE.Firebase.iOS.Core', value='12.12.0', dependency='', commit='abc', extra=None):
    buffer = io.BytesIO()
    resource = io.BytesIO()
    def put(archive, name, data):
        archive.writestr(zipfile.ZipInfo(name), data)
    with zipfile.ZipFile(resource, 'w') as native:
        put(native, 'manifest', '<BindingAssembly><NativeReference Name="Core.xcframework"><Kind>Framework</Kind></NativeReference></BindingAssembly>')
        libraries = []
        for variant in ('', 'simulator', 'maccatalyst'):
            identifier = 'ios-arm64' + ('-' + variant if variant else '')
            libraries.append({'LibraryIdentifier': identifier, 'LibraryPath': 'Core.framework',
                              'SupportedPlatform': 'ios', 'SupportedPlatformVariant': variant})
            put(native, f'Core.xcframework/{identifier}/Core.framework/Core', 'binary')
        put(native, 'Core.xcframework/Info.plist', plistlib.dumps({'AvailableLibraries': libraries}))
    with zipfile.ZipFile(buffer, 'w') as archive:
        archive.writestr('package.nuspec', f'<package><metadata><id>{package_id}</id><version>{value}</version>'
                         f'<repository commit="{commit}"/><dependencies>{dependency}</dependencies></metadata></package>')
        for platform in ('ios', 'maccatalyst'):
            archive.writestr(f'lib/net10.0-{platform}/Firebase.Core.dll', 'assembly')
            archive.writestr(f'lib/net10.0-{platform}/Firebase.Core.resources.zip', resource.getvalue())
        for name, data in (extra or {}).items():
            archive.writestr(name, data)
    return buffer.getvalue()


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.p = {'id': 'AdamE.Firebase.iOS.Core', 'version': '12.12.0', 'fingerprint': 'new'}

    def test_fingerprint_build_failure_exposes_compiler_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            dotnet = Path(tmp) / 'dotnet'
            dotnet.write_text('#!/bin/sh\necho "resolver=$MSBuildEnableWorkloadResolver"\necho "error CS1002: ; expected"\nexit 1\n')
            dotnet.chmod(0o755)
            script = (f"import importlib.util; spec=importlib.util.spec_from_file_location('release', {str(ROOT / 'scripts/firebase-release/release.py')!r}); "
                      "release=importlib.util.module_from_spec(spec); spec.loader.exec_module(release); "
                      "release.build_fingerprint([{'key':'Firebase.Core'}])")
            result = subprocess.run([sys.executable, '-c', script], text=True, capture_output=True,
                                    env={**os.environ, 'PATH': tmp + os.pathsep + os.environ['PATH']})
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('error CS1002: ; expected', result.stdout + result.stderr)
            self.assertIn('resolver=false', result.stdout)

    def test_changed_input_without_bump_fails(self):
        with self.assertRaisesRegex(ValueError, 'without a version bump'):
            release.check_versions([self.p], {'packages': [{**self.p, 'fingerprint': 'old'}]})

    def test_explicit_bump_and_unchanged_dependency(self):
        dep = {'id': 'AdamE.Google.iOS.Nanopb', 'version': '3.30910.0.5', 'fingerprint': 'same'}
        self.assertEqual(release.check_versions([dep, self.p], {'packages': [dep, {**self.p, 'version': '12.11.0'}]}), [self.p['id']])

    def test_version_downgrade_fails(self):
        with self.assertRaisesRegex(ValueError, 'downgrade'):
            release.check_versions([self.p], {'packages': [{**self.p, 'version': '12.13.0'}]})

    def test_normalization(self):
        self.assertEqual(release.normalized('11.2.0.0'), '11.2.0')
        self.assertEqual(release.normalized('2.4.0.5'), '2.4.0.5')
        with self.assertRaises(ValueError):
            release.version('12.12.0-preview')

    def test_nuget_ranges(self):
        self.assertTrue(release.contains_version('[2.4,3.0)', '2.4.0.5'))
        self.assertFalse(release.contains_version('[2.4,3.0)', '3.0.0'))
        self.assertTrue(release.contains_version('[12.12.0]', '12.12.0.0'))
        self.assertFalse(release.contains_version('[12.12.0]', '12.12.0.1'))

    def test_signatures_do_not_change_payload_identity(self):
        self.assertEqual(release.payload_hash(package()), release.payload_hash(package(extra={'.signature.p7s': 'signature'})))
        self.assertNotEqual(release.payload_hash(package()), release.payload_hash(package(extra={'unexpected.txt': 'different'})))

    def test_exact_commit_and_scope(self):
        data = package()
        release.inspect_package(data, self.p, {self.p['id']: self.p}, 'abc')
        with self.assertRaisesRegex(ValueError, 'Wrong repository commit'):
            release.inspect_package(data, self.p, {self.p['id']: self.p}, 'def')
        data = package(dependency='<dependency id="AdamE.Google.iOS.SignIn" version="9.0.0"/>')
        with self.assertRaisesRegex(ValueError, 'Out-of-scope'):
            release.inspect_package(data, self.p, {self.p['id']: self.p})

    def test_dependency_range_is_enforced(self):
        data = package(dependency='<dependency id="AdamE.Google.iOS.Nanopb" version="[4.0,5.0)"/>')
        packages = {self.p['id']: self.p, 'AdamE.Google.iOS.Nanopb': {'version': '3.30910.0.5'}}
        with self.assertRaisesRegex(ValueError, 'Unsatisfied dependency'):
            release.inspect_package(data, self.p, packages)

    def test_native_reference_declaration_forms_and_missing_frameworks(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(release, 'ROOT', Path(tmp)):
            project = Path(tmp) / 'Core.csproj'
            p = {**self.p, 'project': 'Core.csproj'}
            for item in ('NativeReference', '_NativeReference'):
                with self.subTest(item=item):
                    project.write_text(f'<Project><ItemGroup><{item} Include="../Core.xcframework">'
                                       f'<Kind>Framework</Kind></{item}></ItemGroup></Project>')
                    release.inspect_package(package(), p, {p['id']: p}, 'abc')
            project.write_text('<Project><ItemGroup><_NativeReference Include="Missing.xcframework"/></ItemGroup></Project>')
            with self.assertRaisesRegex(ValueError, 'missing=.*Missing.xcframework'):
                release.inspect_package(package(), p, {p['id']: p}, 'abc')

    def test_only_404_means_missing(self):
        for code in (401, 403, 429, 500, 404):
            with self.subTest(code=code), patch('urllib.request.OpenerDirector.open', side_effect=urllib.error.HTTPError('https://feed', code, '', {}, None)):
                if code == 404:
                    self.assertIsNone(release.request('https://feed', missing=True))
                else:
                    with self.assertRaises(RuntimeError):
                        release.request('https://feed', missing=True)

    def test_redirect_drops_credentials(self):
        req = urllib.request.Request('https://feed/package', headers={'Authorization': 'secret'})
        redirected = release.SafeRedirect().redirect_request(req, None, 302, '', {}, 'https://cdn/package')
        self.assertIsNone(redirected.get_header('Authorization'))

    def test_feed_states_reuse_and_conflict(self):
        for state in ('neither', 'nuget', 'both', 'conflict'):
            with self.subTest(state=state), tempfile.TemporaryDirectory() as tmp:
                folder = Path(tmp)
                built = folder / 'built'
                built.mkdir()
                (built / 'core.nupkg').write_bytes(package())
                plan = {'eligible': True, 'sourceCommit': 'abc', 'packages': [self.p], 'changed': []}
                release.write(folder / 'plan.json', plan)
                published = package(commit='original')
                nuget = None if state == 'neither' else published
                github = None if state in ('neither', 'nuget') else package(commit='conflict') if state == 'conflict' else published
                registries = [types.SimpleNamespace(name=name, fetch=lambda p, data=data: data) for name, data in [('nuget', nuget), ('github', github)]]
                args = types.SimpleNamespace(plan=folder / 'plan.json', package_dir=built, output=folder / 'candidate', online=True)
                with patch.object(release, 'feeds', return_value=registries):
                    if state == 'conflict':
                        with self.assertRaisesRegex(ValueError, 'Feeds disagree'):
                            release.prepare(args)
                    else:
                        release.prepare(args)
                        result = release.read(folder / 'candidate/manifest.json')['packages'][0]
                        self.assertEqual(result['destinations'], {'neither': ['nuget', 'github'], 'nuget': ['github'], 'both': []}[state])
                        self.assertEqual(result['reused'], state != 'neither')
                        expected = package() if state == 'neither' else published
                        self.assertEqual((folder / 'candidate' / result['file']).read_bytes(), expected)

    def test_unexpected_output_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            release.write(folder / 'plan.json', {'eligible': False, 'packages': [self.p]})
            (folder / 'signin.nupkg').write_bytes(package('AdamE.Google.iOS.SignIn'))
            with self.assertRaisesRegex(ValueError, 'Unexpected output'):
                release.prepare(types.SimpleNamespace(plan=folder / 'plan.json', package_dir=folder, output=folder / 'candidate', online=False))

    def test_new_version_cannot_reuse_another_commits_package(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            (folder / 'core.nupkg').write_bytes(package())
            release.write(folder / 'plan.json', {'eligible': True, 'sourceCommit': 'abc',
                                               'changed': [self.p['id']], 'packages': [self.p]})
            feed = types.SimpleNamespace(name='nuget', fetch=lambda p: package(commit='different'))
            with patch.object(release, 'feeds', return_value=[feed]):
                with self.assertRaisesRegex(ValueError, 'Wrong repository commit'):
                    release.prepare(types.SimpleNamespace(plan=folder / 'plan.json', package_dir=folder,
                                                          output=folder / 'candidate', online=True))

    def test_incomplete_validation_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            release.write(folder / 'manifest.json', {})
            release.write(folder / 'results.json', {'smoke': 'passed'})
            with self.assertRaisesRegex(ValueError, 'Incomplete validation'):
                release.finalize(types.SimpleNamespace(package_dir=folder, results=folder / 'results.json'))

    def test_pr_cannot_publish(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            release.write(folder / 'manifest.json', {})
            with patch.dict(os.environ, {'GITHUB_REF': 'refs/pull/1/merge', 'GITHUB_EVENT_NAME': 'pull_request'}):
                with self.assertRaisesRegex(ValueError, 'Only main'):
                    release.publish(types.SimpleNamespace(package_dir=folder))

    def test_disabled_configuration_and_unrelated_changes(self):
        baseline = {'packages': [self.p]}
        config = {'releaseVersion': None, 'minimumFirebaseVersion': '12.12.0',
                  'nativeBuild': {'runner': 'macos-26', 'xcode': '26.6'}}
        with tempfile.TemporaryDirectory() as tmp, patch.object(release, 'snapshot', return_value=[{**self.p, 'project': 'source/Firebase/Core/Core.csproj'}]), \
                patch.object(release, 'read', side_effect=[[], config, baseline, baseline]), \
                patch.object(release, 'git_file', side_effect=[baseline, config]), \
                patch.object(release, 'run', side_effect=['Readme.md', 'abc']):
            dest = Path(tmp) / 'plan.json'
            release.plan(types.SimpleNamespace(metadata='unused', online=False, base='base', manual=False, output=dest))
            result = json.loads(dest.read_text())
            self.assertFalse(result['eligible'])
            self.assertFalse(result['relevant'])

    def test_pending_release_only_validates_changes_in_current_diff(self):
        p = {**self.p, 'project': 'source/Firebase/Core/Core.csproj', 'firebase': True}
        baseline = {'packages': [{**p, 'version': '12.11.0', 'fingerprint': 'old'}]}
        config = {'releaseVersion': '12.12.0', 'minimumFirebaseVersion': '12.12.0',
                  'nativeBuild': {'runner': 'macos-26', 'xcode': '26.6'}}
        for filename, manual, relevant in [('Readme.md', False, False),
                ('source/Google/Maps/ApiDefinition.cs', False, False),
                ('source/Firebase/Core/ApiDefinition.cs', False, True),
                ('source/AssemblyInfo.cs', False, True), ('Readme.md', True, True)]:
            with self.subTest(filename=filename, manual=manual), tempfile.TemporaryDirectory() as tmp, \
                    patch.object(release, 'snapshot', return_value=[p]), \
                    patch.object(release, 'read', side_effect=[[], config, baseline, baseline]), \
                    patch.object(release, 'git_file', side_effect=[baseline, config]), \
                    patch.object(release, 'run', side_effect=[filename, 'abc']):
                dest = Path(tmp) / 'plan.json'
                release.plan(types.SimpleNamespace(metadata='unused', online=False, base='base', manual=manual, output=dest))
                result = json.loads(dest.read_text())
                self.assertEqual(result['changed'], [p['id']])
                self.assertEqual(result['relevant'], relevant)
                self.assertEqual(result['eligible'], manual)

    def test_validation_evidence_rejects_failure_skipped_and_incomplete_results(self):
        for fault in (None, 'unit', 'inspection', 'case', 'surface', 'missing'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as tmp:
                folder = Path(tmp)
                for name in release.VALIDATIONS[2:]:
                    release.write(folder / (name + '.json'), {
                        'Success': True, 'FatalError': None, 'Cases': [{'Success': fault != 'case'}],
                        'BindingSurfaceCoverage': {'Target': 'all', 'SurfaceCount': 2,
                            'ExercisedCount': 1 if fault == 'surface' else 2, 'WaivedCount': 0, 'FailedCount': 0}})
                if fault == 'missing':
                    (folder / 'smoke.json').unlink()
                env = {'UNIT_TEST_OUTCOME': 'failure' if fault == 'unit' else 'success',
                       'PACKAGE_INSPECTION_OUTCOME': 'skipped' if fault == 'inspection' else 'success'}
                with patch.dict(os.environ, env):
                    args = types.SimpleNamespace(results_dir=folder)
                    if fault:
                        with self.assertRaises((ValueError, FileNotFoundError)):
                            release.collect_validation(args)
                        self.assertFalse((folder / 'validation.json').exists())
                    else:
                        release.collect_validation(args)
                        self.assertEqual(release.read(folder / 'validation.json'), dict.fromkeys(release.VALIDATIONS, 'passed'))

    def test_publish_submits_all_before_waiting_and_caches_existing_versions(self):
        packages = [{**self.p, 'id': name, 'file': name + '.nupkg', 'payloadHash': release.payload_hash(package())}
                    for name in ('dependency', 'consumer', 'existing')]
        calls, pushes = [], []
        clock = [0]
        def fetch(p):
            calls.append(p['id'])
            return package() if p['id'] == 'existing' or clock[0] >= 150 else None
        def push(args, **kwargs):
            pushes.append(Path(args[3]).stem)
        def sleep(seconds):
            self.assertEqual(pushes, ['dependency', 'consumer'])
            clock[0] += seconds
        feed = types.SimpleNamespace(name='nuget', source='nuget', fetch=fetch)
        with patch.dict(os.environ, {'NUGET_API_KEY': 'test'}), \
                patch.object(release.subprocess, 'run', side_effect=push), \
                patch.object(release.time, 'monotonic', side_effect=lambda: clock[0]), \
                patch.object(release.time, 'sleep', side_effect=sleep):
            release.publish_packages(Path('/tmp'), packages, [feed], wait_seconds=1800)
        self.assertEqual(calls.count('existing'), 1)
        self.assertEqual(pushes, ['dependency', 'consumer'])
        self.assertGreater(clock[0], 120)

    def test_publish_wait_reports_all_outstanding_and_rejects_conflicts_and_errors(self):
        packages = [{**self.p, 'id': name, 'file': name + '.nupkg', 'payloadHash': release.payload_hash(package())}
                    for name in ('dependency', 'consumer')]
        for state in ('timeout', 'conflict', 'unavailable'):
            calls = [0]
            def fetch(p):
                calls[0] += 1
                if calls[0] <= 4:
                    return None
                if state == 'conflict':
                    return package(commit='other')
                if state == 'unavailable':
                    raise RuntimeError('HTTP 503')
                return None
            feed = types.SimpleNamespace(name='nuget', source='nuget', fetch=fetch)
            with self.subTest(state=state), patch.dict(os.environ, {'NUGET_API_KEY': 'test'}), \
                    patch.object(release.subprocess, 'run'), patch.object(release.time, 'sleep'):
                expected = {'timeout': 'dependency.*consumer', 'conflict': 'conflict', 'unavailable': 'HTTP 503'}[state]
                with self.assertRaisesRegex((ValueError, RuntimeError), expected):
                    release.publish_packages(Path('/tmp'), packages, [feed], wait_seconds=0)

    def test_toolchain_pins_affect_package_inputs_but_release_labels_do_not(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(release, 'ROOT', Path(tmp)), \
                patch.object(release, 'run', return_value='source/Core/Core.csproj'), \
                patch.object(release, 'build_fingerprint', return_value='normalized-cake'):
            root = Path(tmp)
            for name in ('build.cake', 'common.cake', 'poco.cake', 'Directory.Build.props', 'global.json',
                         '.config/dotnet-tools.json', 'source/AssemblyInfo.cs', 'custom_externals_download.cake'):
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('shared input')
            project = root / 'source/Core/Core.csproj'
            project.parent.mkdir(parents=True)
            project.write_text(f'<Project><PropertyGroup><PackageId>{self.p["id"]}</PackageId>'
                               '<PackageVersion>12.12.0</PackageVersion></PropertyGroup></Project>')
            metadata = [{'id': self.p['id'], 'version': '12.12.0', 'dependencies': [], 'project': 'source/Core/Core.csproj'}]
            config = {'releaseVersion': '12.12.0', 'nativeBuild': {'runner': 'macos-26', 'xcode': '26.6'}}
            release.write(root / release.CONFIG, config)
            before = release.snapshot(metadata)
            config['releaseVersion'] = '12.12.0.1'
            release.write(root / release.CONFIG, config)
            self.assertEqual(before, release.snapshot(metadata))
            for setting, value in (('xcode', '26.7'), ('runner', 'macos-27')):
                with self.subTest(setting=setting):
                    config['nativeBuild'] = {'runner': 'macos-26', 'xcode': '26.6'}
                    config['nativeBuild'][setting] = value
                    release.write(root / release.CONFIG, config)
                    with self.assertRaisesRegex(ValueError, 'without a version bump'):
                        release.check_versions(release.snapshot(metadata), {'packages': before})

            config['nativeBuild'] = {'runner': 'macos-26', 'xcode': '26.6'}
            release.write(root / release.CONFIG, config)
            for name in ('global.json', '.config/dotnet-tools.json', 'Directory.Build.props'):
                with self.subTest(shared_file=name):
                    path = root / name
                    original = path.read_text()
                    path.write_text('changed shared toolchain or packaging input')
                    with self.assertRaisesRegex(ValueError, 'without a version bump'):
                        release.check_versions(release.snapshot(metadata), {'packages': before})
                    path.write_text(original)

    def test_publish_retry_only_writes_missing_feed(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            data = package()
            p = {**self.p, 'file': 'core.nupkg', 'sha256': release.digest(data),
                 'payloadHash': release.payload_hash(data), 'reused': False}
            manifest = {'eligible': True, 'sourceCommit': 'abc', 'releaseVersion': '12.12.0', 'changed': [p['id']],
                        'validation': dict.fromkeys(release.VALIDATIONS, 'passed'), 'packages': [p]}
            release.write(folder / 'manifest.json', manifest)
            release.write(folder / 'metadata.json', [self.p])
            (folder / p['file']).write_bytes(data)
            state = {'nuget': None, 'github': data}
            registries = [types.SimpleNamespace(name=name, source=name, fetch=lambda p, n=name: state[n]) for name in state]
            pushes = []
            existing_draft = None
            def execute(args, **kwargs):
                if args[:3] == ['dotnet', 'nuget', 'push']:
                    feed = args[args.index('--source') + 1]
                    pushes.append(feed)
                    state[feed] = data
                    return types.SimpleNamespace(returncode=0)
                return types.SimpleNamespace(returncode=0 if existing_draft else 1,
                                             stdout=json.dumps(existing_draft) if existing_draft else '')
            env = {'GITHUB_REF': 'refs/heads/main', 'GITHUB_EVENT_NAME': 'push', 'GITHUB_SHA': 'abc',
                   'GH_TOKEN': 'test', 'NUGET_API_KEY': 'test'}
            with patch.dict(os.environ, env), patch.object(release, 'feeds', return_value=registries), \
                    patch.object(release, 'previous_release', return_value=None), patch.object(release, 'github', return_value=[]), \
                    patch.object(release.subprocess, 'run', side_effect=execute), patch.object(release, 'run'):
                args = types.SimpleNamespace(package_dir=folder, metadata=folder / 'metadata.json')
                release.publish(args)
                release.publish(args)
                self.assertEqual(pushes, ['nuget'])
                existing_draft = {'isDraft': True, 'targetCommitish': 'different'}
                with self.assertRaisesRegex(ValueError, 'draft targets another commit'):
                    release.publish(args)
                (folder / p['file']).write_bytes(b'tampered')
                with self.assertRaisesRegex(ValueError, 'hash mismatch'):
                    release.publish(args)


if __name__ == '__main__':
    unittest.main()
