import copy
import hashlib
import importlib.util
import json
import os
import plistlib
import re
from pathlib import Path
import shutil
import tempfile
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


consumers = load('review_consumers', 'scripts/signin-release/consumers.py')
validation = load('review_validation', 'scripts/signin-release/validate.py')
feed = load('review_feed', 'scripts/signin-release/integration_feed.py')
release = feed.release


class ReviewRegressionTests(unittest.TestCase):
    def test_consumer_probe_matches_candidate_version_in_both_lanes(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')
        for version in ('9.1.0', '9.1.0.1', '9.2.0', '9.2.0.1', '10.0.0', '10.0.0.1'):
            amr = release.version(version) >= release.version('9.2.0')
            wrapper = release.version(version) >= release.version('10.0.0')
            with self.subTest(version=version), tempfile.TemporaryDirectory() as tmp:
                folder = Path(tmp)
                packages = [dict(id=n, version=version, sha256='expected') for n in sorted(release.SIGNIN_IDS)]
                release.write(folder / 'manifest.json', dict(channel='signin', packages=packages))
                for combined, lane in ((False, 'consumers'), (True, 'combined')):
                    for transitive in (False, True):
                        source = consumers.probe_source(combined, transitive, version)
                        for api in ('CreateAmrClaim()', 'CreateEssentialAmrClaim()', 'Constants.AmrClaimName'):
                            self.assertEqual(api in source, amr, (version, lane, transitive, api))
                        self.assertEqual('SignIn.WrapperIdentifier' in source, wrapper, (version, lane, transitive))
                        self.assertIn('CreateAuthTimeClaim()', source)
                        self.assertIn('CreateEssentialAuthTimeClaim()', source)
                        self.assertIn('user.RefreshToken.ExpirationDate', source)
                        for api in ('ConfigureWithCompletion(null)', 'ConfigureDebugProvider(key, null)', 'Constants.AppCheckErrorDomain'):
                            self.assertEqual(api in source, amr, (version, lane, transitive, api))
                        if amr:
                            guarded = source[source.index('#if !__MACCATALYST__'):source.index('#endif')]
                            self.assertIn('ConfigureDebugProvider(key, null)', guarded)
                            self.assertIn('Constants.AppCheckErrorDomain', guarded)
                        self.assertEqual('BindingProbe.Describe();' in source, transitive)
                        floor = int(re.search(r'selectors < (\d+)', source)[1])
                        self.assertEqual(floor, 75 if wrapper else 73 if amr else 67)
                        marker = re.search(r' claims=([^";]+)";', source)[1]
                        self.assertEqual(marker, ('auth_time,amr' if amr else 'auth_time') +
                                         (' wrapper=wrapperIdentifier' if wrapper else '') +
                                         (' firebase12=FIRAuth,FIRAnalytics,FIRFirestore' if combined else ''))
                        runtime = f'PASS classes=8 selectors={floor} claims={marker}'
                        release.write(folder / (lane + '.json'), dict(
                            Success=True, Cases=[dict(Success=True)], sourceCommit='abc', Lane=lane,
                            packageHashes={p['id']: p['sha256'] for p in packages},
                            Checks=sorted(release.SIGNIN_CHECKS) + [runtime]))
                with patch.dict(os.environ, UNIT_TEST_OUTCOME='success', PACKAGE_INSPECTION_OUTCOME='success', BUILD_COMMIT='abc'):
                    release.collect_validation(types.SimpleNamespace(results_dir=folder, package_dir=folder))

    def test_error_codes_compare_native_header_to_managed_values(self):
        header = 'kGIDSignInErrorCodeEMM = -6, kGIDSignInErrorCodeAmbiguousClaims = -7, kGIDSignInErrorCodeJSONSerializationFailure = -10, kGIDSignInErrorCodeRefreshTokenExpired = -11'
        managed = 'enum ErrorCode : long { Emm = -6, AmbiguousClaims = -7, JsonSerializationFailure = -10, RefreshTokenExpired = -11 }'
        validation.error_code_values(header, managed)
        for value in ('-6', '-7', '-10', '-11'):
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'error codes differ'):
                validation.error_code_values(header.replace(value, '-99'), managed)
            with self.subTest(managed=value), self.assertRaisesRegex(ValueError, 'error codes differ'):
                validation.error_code_values(header, managed.replace(value, '-99'))
        for missing in ('', 'kGIDSignInErrorCodeEMM = -6'):
            with self.assertRaisesRegex(ValueError, 'error codes differ'):
                validation.error_code_values(missing, managed)
        app_check = 'typedef NS_ERROR_ENUM(kGIDAppCheckErrorDomain, GIDAppCheckErrorCode) { kGIDAppCheckUnexpectedError = 1, };'
        managed += ' enum AppCheckErrorCode : long { UnexpectedError = 1 }'
        validation.error_code_values(header, managed)
        root = 'GoogleSignIn.framework'
        for suffix in ('/Headers/', '/Versions/A/Headers/'):
            entries = {root + suffix + 'GIDSignIn.h': header.encode(), root + suffix + 'GIDAppCheckError.h': app_check.encode()}
            validation.framework_error_codes(entries.__getitem__, entries, root, managed)
            for drift in (dict(header=app_check.replace('= 1', '= 2')), dict(managed=managed.replace('UnexpectedError = 1', 'UnexpectedError = 2')),
                          dict(managed=managed.replace(' enum AppCheckErrorCode : long { UnexpectedError = 1 }', ''))):
                with self.subTest(suffix=suffix, drift=drift), self.assertRaisesRegex(ValueError, 'App Check error codes differ'):
                    changed = dict(entries, **{root + suffix + 'GIDAppCheckError.h': drift.get('header', app_check).encode()})
                    validation.framework_error_codes(changed.__getitem__, changed, root, drift.get('managed', managed))
            for name in ('GIDSignIn.h', 'GIDAppCheckError.h'):
                partial = {k: v for k, v in entries.items() if not k.endswith('/' + name)}
                with self.subTest(suffix=suffix, missing=name), self.assertRaisesRegex(ValueError, 'Missing packed'):
                    validation.framework_error_codes(partial.__getitem__, partial, root, managed)

    def test_stale_native_archive_version_is_rejected(self):
        root = 'GoogleSignIn.xcframework/ios-arm64/GoogleSignIn.framework'
        for location in ('/Info.plist', '/Versions/A/Resources/Info.plist'):
            for native in ('9.0.0', '9.1.0'):
                with self.subTest(location=location, native=native):
                    entries = {root + location: plistlib.dumps(dict(CFBundleShortVersionString=native))}
                    if native == '9.1.0':
                        validation.framework_version(entries.__getitem__, entries, root, '9.1.0')
                    else:
                        with self.assertRaisesRegex(ValueError, 'version mismatch'):
                            validation.framework_version(entries.__getitem__, entries, root, '9.1.0')
        with self.assertRaisesRegex(ValueError, 'version mismatch'):
            validation.framework_version({}.__getitem__, [], root, '9.1.0')

    def test_rebuild_resources_are_inspected(self):
        for damage in ('none', 'missing', 'changed', 'duplicate', 'duplicate-shared'):
            with self.subTest(damage=damage), tempfile.TemporaryDirectory() as tmp:
                project = Path(tmp) / 'Probe.csproj'
                app = Path(tmp) / 'bin/iossimulator-arm64/Probe.app'
                bundle = app / 'GoogleSignIn.bundle'
                bundle.mkdir(parents=True)
                (bundle / 'resource').write_text('original')
                expected = consumers.files(bundle)
                calls = []

                def run(name, command, offline):
                    self.assertTrue(offline)
                    calls.append(name)
                    if '-t:Rebuild' in command:
                        if damage == 'missing':
                            shutil.rmtree(bundle)
                        elif damage == 'changed':
                            (bundle / 'resource').write_text('altered')
                        elif damage == 'duplicate':
                            shutil.copytree(bundle, app / 'nested/GoogleSignIn.bundle')
                        elif damage == 'duplicate-shared':
                            (app / 'AppAuth_Privacy.bundle').mkdir()
                            (app / 'nested/AppAuth_Privacy.bundle').mkdir(parents=True)

                def build():
                    return consumers.build_application(run, 'probe', ['build'], project,
                                                       'iossimulator-arm64', expected, rebuild=True)
                if damage == 'none':
                    self.assertEqual(build(), app)
                else:
                    with self.assertRaisesRegex(ValueError, 'resource bundle'):
                        build()
                self.assertEqual(calls, ['probe', 'probe-rebuild'])

    def test_native_platform_and_variant_both_identify_slice(self):
        libraries = [dict(SupportedPlatform='ios', SupportedArchitectures=['arm64'], LibraryIdentifier='device'),
                     dict(SupportedPlatform='ios', SupportedPlatformVariant='simulator',
                          SupportedArchitectures=['arm64', 'x86_64']),
                     dict(SupportedPlatform='ios', SupportedPlatformVariant='maccatalyst',
                          SupportedArchitectures=['arm64', 'x86_64'])]
        validation.native_libraries(libraries, 'fixture')
        missing_device = copy.deepcopy(libraries)
        missing_device[0]['SupportedPlatform'] = 'macos'
        wrong_device = copy.deepcopy(libraries)
        wrong_device[0]['SupportedArchitectures'] = ['x86_64']
        wrong_device.append(dict(SupportedPlatform='macos', SupportedArchitectures=['arm64']))
        foreign_simulator = copy.deepcopy(libraries)
        foreign_simulator[1]['SupportedPlatform'] = 'tvos'
        for invalid in (missing_device, wrong_device, foreign_simulator, libraries + [libraries[0]]):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                validation.native_libraries(invalid, 'fixture')

    def test_combined_requires_distinct_lane_and_runtime_evidence(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            packages = [{'id': n, 'sha256': 'expected'} for n in sorted(release.SIGNIN_IDS)]
            release.write(folder / 'manifest.json', {'channel': 'signin', 'packages': packages})
            standalone = dict(Success=True, Cases=[{'Success': True}], sourceCommit='abc', Lane='consumers',
                              Checks=sorted(release.SIGNIN_CHECKS) + ['PASS classes=8 selectors=58'],
                              packageHashes={p['id']: p['sha256'] for p in packages})
            release.write(folder / 'consumers.json', standalone)
            args = types.SimpleNamespace(results_dir=folder, package_dir=folder)
            with patch.dict(os.environ, UNIT_TEST_OUTCOME='success', PACKAGE_INSPECTION_OUTCOME='success', BUILD_COMMIT='abc'):
                release.write(folder / 'combined.json', standalone)
                with self.assertRaisesRegex(ValueError, 'wrong lane'):
                    release.collect_validation(args)
                combined = {**standalone, 'Lane': 'combined'}
                release.write(folder / 'combined.json', combined)
                with self.assertRaisesRegex(ValueError, 'combined Firebase'):
                    release.collect_validation(args)
                combined['Checks'] = sorted(release.SIGNIN_CHECKS) + [
                    'PASS classes=8 selectors=58 firebase12=FIRAuth,FIRAnalytics,FIRFirestore']
                release.write(folder / 'combined.json', combined)
                release.collect_validation(args)
                self.assertEqual(release.read(folder / 'validation.json')['combined'], 'passed')

    def test_fixture_feed_enforces_exact_closure_and_versions(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')
        roots = ['AdamE.Firebase.iOS.' + n for n in ('Auth', 'Analytics', 'CloudFirestore')]
        support = 'AdamE.Google.iOS.GoogleAppMeasurement'
        metadata = [dict(id=n, version='12.13.0', dependencies=[support]) for n in roots]
        metadata += [dict(id=support, version='12.13.0', dependencies=['AdamE.Google.iOS.GoogleUtilities'])]
        for fault in ('none', 'google', 'third-party', 'extra-firebase', 'missing', 'duplicate', 'version', 'firebase13', 'tamper'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as tmp:
                folder = Path(tmp)
                candidate = folder / 'candidate'
                candidate.mkdir()
                packages = []
                for index, name in enumerate(sorted(release.SIGNIN_IDS)):
                    path = candidate / f'sealed-{index}.nupkg'
                    path.write_text(name)
                    packages.append(dict(id=name, file=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
                release.write(candidate / 'manifest.json', dict(channel='signin', packages=packages))
                identities = {n: dict(id=n, version='12.13.0') for n in roots + [support]}
                identities['shared'] = dict(id='AdamE.Google.iOS.GoogleUtilities', version='99.0.0')
                if fault in ('google', 'third-party', 'extra-firebase'):
                    identities['stray'] = dict(id={'google': 'AdamE.Google.iOS.Maps', 'third-party': 'Other.Package',
                                                  'extra-firebase': 'AdamE.Firebase.iOS.Messaging'}[fault], version='12.13.0')
                elif fault == 'missing':
                    del identities[roots[0]]
                elif fault == 'duplicate':
                    identities['duplicate'] = identities[roots[0]]
                elif fault == 'version':
                    identities[roots[0]] = dict(id=roots[0], version='12.12.0')
                elif fault == 'tamper':
                    (candidate / packages[0]['file']).write_text('tampered')
                records = copy.deepcopy(metadata)
                if fault == 'firebase13':
                    records[0]['version'] = '13.0.0'
                paths = []
                for index, key in enumerate(identities):
                    path = folder / f'fixture-{index}.nupkg'
                    path.write_text(json.dumps(identities[key]))
                    paths.append(path)
                output = folder / 'feed'
                output.mkdir()
                sentinel = output / 'previous'
                sentinel.touch()
                with patch.object(release, 'package_info', side_effect=lambda b: json.loads(b)):
                    if fault == 'none':
                        feed.stage_feed(candidate, paths, records, output)
                        self.assertEqual(len(list(output.glob('*.nupkg'))), 9)
                        self.assertFalse(sentinel.exists())
                    else:
                        with self.assertRaises(ValueError):
                            feed.stage_feed(candidate, paths, records, output)
                        self.assertTrue(sentinel.exists())

    def test_fixture_staging_retains_major_version_policy_for_roots_and_dependencies(self):
        roots = ['AdamE.Firebase.iOS.' + n for n in ('Auth', 'Analytics', 'CloudFirestore')]
        dependency = 'AdamE.Firebase.iOS.Core'
        baseline = [dict(id=n, version='12.14.0', dependencies=[dependency]) for n in roots]
        baseline.append(dict(id=dependency, version='12.14.0', dependencies=[]))
        self.assertEqual(set(feed.fixture_packages(baseline)), set(roots + [dependency]))
        for name in (roots[0], dependency):
            with self.subTest(package=name):
                metadata = [{**p, 'version': '13.0.0'} if p['id'] == name else p for p in baseline]
                with self.assertRaisesRegex(ValueError, 'Only Firebase 12 fixtures are permitted'):
                    feed.fixture_packages(metadata)

    def test_91_release_requires_claims_probe_in_each_lane(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            packages = [dict(id=n, version='9.1.0.0', sha256='expected') for n in sorted(release.SIGNIN_IDS)]
            release.write(folder / 'manifest.json', dict(channel='signin', packages=packages))
            args = types.SimpleNamespace(results_dir=folder, package_dir=folder)
            def evidence(lane, claims):
                runtime = 'PASS classes=8 selectors=67' + (' claims=auth_time' if claims else '')
                if lane == 'combined':
                    runtime += ' firebase12=FIRAuth,FIRAnalytics,FIRFirestore'
                return dict(Success=True, Cases=[dict(Success=True)], sourceCommit='abc', Lane=lane,
                            packageHashes={p['id']: p['sha256'] for p in packages},
                            Checks=sorted(release.SIGNIN_CHECKS) + [runtime])
            with patch.dict(os.environ, UNIT_TEST_OUTCOME='success', PACKAGE_INSPECTION_OUTCOME='success', BUILD_COMMIT='abc'):
                for missing in ('consumers', 'combined', None):
                    for lane in ('consumers', 'combined'):
                        release.write(folder / (lane + '.json'), evidence(lane, lane != missing))
                    if missing:
                        with self.assertRaisesRegex(ValueError, 'claims runtime'):
                            release.collect_validation(args)
                    else:
                        release.collect_validation(args)
                for counts in ('classes=7 selectors=67', 'classes=8 selectors=66'):
                    incomplete = evidence('consumers', True)
                    incomplete['Checks'][-1] = 'PASS ' + counts + ' claims=auth_time'
                    release.write(folder / 'consumers.json', incomplete)
                    with self.assertRaisesRegex(ValueError, 'claims runtime'):
                        release.collect_validation(args)

    def test_92_requires_amr_app_check_and_its_complete_selector_inventory(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            packages = [dict(id=n, version='9.2.0.0', sha256='expected') for n in sorted(release.SIGNIN_IDS)]
            release.write(folder / 'manifest.json', dict(channel='signin', packages=packages))
            args = types.SimpleNamespace(results_dir=folder, package_dir=folder)
            def evidence(lane, runtime):
                if lane == 'combined':
                    runtime += ' firebase12=FIRAuth,FIRAnalytics,FIRFirestore'
                return dict(Success=True, Cases=[dict(Success=True)], sourceCommit='abc', Lane=lane,
                            packageHashes={p['id']: p['sha256'] for p in packages},
                            Checks=sorted(release.SIGNIN_CHECKS) + [runtime])
            valid = 'PASS classes=8 selectors=73 claims=auth_time,amr'
            with patch.dict(os.environ, UNIT_TEST_OUTCOME='success', PACKAGE_INSPECTION_OUTCOME='success', BUILD_COMMIT='abc'):
                for lane in ('consumers', 'combined'):
                    release.write(folder / (lane + '.json'), evidence(lane, valid))
                release.collect_validation(args)
                for lane in ('consumers', 'combined'):
                    for invalid in ('PASS classes=8 selectors=73 claims=auth_time',
                                    'PASS classes=8 selectors=69 claims=auth_time,amr',
                                    'PASS classes=8 selectors=72 claims=auth_time,amr',
                                    'PASS classes=7 selectors=73 claims=auth_time,amr'):
                        with self.subTest(lane=lane, runtime=invalid):
                            release.write(folder / (lane + '.json'), evidence(lane, invalid))
                            with self.assertRaisesRegex(ValueError, 'claims runtime'):
                                release.collect_validation(args)
                            release.write(folder / (lane + '.json'), evidence(lane, valid))

    def test_100_requires_wrapper_identifier_and_its_selectors(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            packages = [dict(id=n, version='10.0.0.0', sha256='expected') for n in sorted(release.SIGNIN_IDS)]
            release.write(folder / 'manifest.json', dict(channel='signin', packages=packages))
            args = types.SimpleNamespace(results_dir=folder, package_dir=folder)
            def evidence(lane, runtime):
                if lane == 'combined':
                    runtime += ' firebase12=FIRAuth,FIRAnalytics,FIRFirestore'
                return dict(Success=True, Cases=[dict(Success=True)], sourceCommit='abc', Lane=lane,
                            packageHashes={p['id']: p['sha256'] for p in packages},
                            Checks=sorted(release.SIGNIN_CHECKS) + [runtime])
            valid = 'PASS classes=8 selectors=75 claims=auth_time,amr wrapper=wrapperIdentifier'
            with patch.dict(os.environ, UNIT_TEST_OUTCOME='success', PACKAGE_INSPECTION_OUTCOME='success', BUILD_COMMIT='abc'):
                for lane in ('consumers', 'combined'):
                    release.write(folder / (lane + '.json'), evidence(lane, valid))
                release.collect_validation(args)
                for lane in ('consumers', 'combined'):
                    for invalid in ('PASS classes=8 selectors=75 claims=auth_time,amr',
                                    'PASS classes=8 selectors=73 claims=auth_time,amr wrapper=wrapperIdentifier',
                                    'PASS classes=8 selectors=74 claims=auth_time,amr wrapper=wrapperIdentifier',
                                    'PASS classes=7 selectors=75 claims=auth_time,amr wrapper=wrapperIdentifier'):
                        with self.subTest(lane=lane, runtime=invalid):
                            release.write(folder / (lane + '.json'), evidence(lane, invalid))
                            with self.assertRaisesRegex(ValueError, 'claims runtime'):
                                release.collect_validation(args)
                            release.write(folder / (lane + '.json'), evidence(lane, valid))


if __name__ == '__main__':
    unittest.main()
