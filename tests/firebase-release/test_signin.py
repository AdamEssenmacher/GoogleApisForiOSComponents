import importlib.util
import json
import os
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('signin_release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)
spec = importlib.util.spec_from_file_location('signin_validation', ROOT / 'scripts/signin-release/validate.py')
validation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validation)


def packages():
    return [{'id': n, 'version': '9.1.0' if n.endswith('.SignIn') else '1.0.0', 'fingerprint': 'unchanged',
             'project': n + '.csproj', 'firebase': False} for n in sorted(release.SIGNIN_IDS)]


class SignInTests(unittest.TestCase):
    def setUp(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')

    def test_configuration_is_independent_and_firebase_defaults_remain(self):
        self.assertEqual(release.CONFIG, 'eng/signin-release.json')
        self.assertEqual(release.TAG_PREFIX, 'google-signin-v')
        release.configure_channel('firebase')
        self.assertEqual(release.CONFIG, 'eng/firebase-release.json')
        self.assertEqual(release.TAG_PREFIX, 'firebase-v')
        self.assertEqual(len(release.VALIDATIONS), 8)

    def test_exact_scope(self):
        release.validate_scope(packages())
        for changed in (packages()[:-1], packages() + [packages()[0]],
                        packages() + [{'id': 'AdamE.Firebase.iOS.Core'}],
                        packages() + [{'id': 'AdamE.Google.iOS.Maps'}]):
            with self.assertRaisesRegex(ValueError, 'exactly'):
                release.validate_scope(changed)

    def test_cross_channel_artifacts_rejected(self):
        for channel in (None, 'firebase'):
            with self.assertRaisesRegex(ValueError, 'Wrong release channel'):
                release.validate_manifest_channel({'channel': channel, 'packages': packages()})
        release.validate_manifest_channel({'channel': 'signin', 'packages': packages()})

    def test_predecessors(self):
        previous = None
        for target in release.SIGNIN_VERSIONS:
            release.require_signin_predecessor(target, previous)
            previous = {'releaseVersion': target}
            release.require_signin_predecessor(target, previous)  # Completed retry.
        release.require_signin_predecessor('10.1.0.1', {'releaseVersion': '10.1.0'})
        for target, previous in [('9.2.0', None), ('10.0.0', {'releaseVersion': '9.1.0'}),
                                 ('10.1.0', {'releaseVersion': '9.2.0'}), ('9.1.0', {'releaseVersion': '10.1.0'})]:
            with self.assertRaisesRegex(ValueError, 'preceding'):
                release.require_signin_predecessor(target, previous)
        with self.assertRaisesRegex(ValueError, 'Unexpected'):
            release.require_signin_predecessor('9.0.0', None)

    def test_bootstrap_cannot_publish_even_with_forged_eligibility(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(release, 'ROOT', Path(tmp)):
            manifest = {'channel': 'signin', 'eligible': True, 'sourceCommit': 'abc', 'releaseVersion': '9.1.0',
                        'packages': packages(), 'validation': dict.fromkeys(release.VALIDATIONS, 'passed')}
            release.write(Path(tmp) / 'manifest.json', manifest)
            release.write(Path(tmp) / release.CONFIG, {'releaseVersion': None})
            with patch.dict(os.environ, GITHUB_REF='refs/heads/main', GITHUB_EVENT_NAME='push', GITHUB_SHA='abc'), \
                    patch.object(release, 'previous_release', return_value=None), patch.object(release, 'feeds') as feeds:
                with self.assertRaisesRegex(ValueError, 'disabled'):
                    release.publish(types.SimpleNamespace(package_dir=tmp))
                feeds.assert_not_called()

    def test_pr_cannot_publish(self):
        with tempfile.TemporaryDirectory() as tmp:
            release.write(Path(tmp) / 'manifest.json', {'channel': 'signin', 'packages': packages()})
            with patch.dict(os.environ, GITHUB_REF='refs/pull/42/merge', GITHUB_EVENT_NAME='pull_request'):
                with self.assertRaisesRegex(ValueError, 'Only main'):
                    release.publish(types.SimpleNamespace(package_dir=tmp))

    def test_history_ignores_firebase_releases(self):
        manifest = {'channel': 'signin', 'releaseVersion': '9.1.0', 'packages': packages()}
        releases = [{'tag_name': 'firebase-v99.0.0', 'draft': False, 'assets': []},
                    {'tag_name': 'google-signin-v9.1.0', 'draft': False,
                     'assets': [{'name': 'manifest.json', 'browser_download_url': 'https://example.test/signin'}]}]
        with patch.object(release, 'github', return_value=releases), \
                patch.object(release, 'request', return_value=json.dumps(manifest).encode()) as request:
            self.assertEqual(release.previous_release(), manifest)
            request.assert_called_once_with('https://example.test/signin')

    def test_native_runtime_is_bounded(self):
        for pod in ('1.11.0', '2.0.0'):
            with self.assertRaisesRegex(ValueError, 'Unsupported CocoaPods'):
                release.native_build({'nativeBuild': {'runner': 'macos-26', 'xcode': '26.6', 'cocoapods': pod}})

    def test_matrix_tracks_host_architecture_without_changing_device(self):
        self.assertEqual(release.signin_checks('arm64'), release.SIGNIN_CHECKS)
        intel = release.signin_checks('x86_64')
        self.assertIn('ios-direct-iossimulator-x86_64', intel)
        self.assertIn('maccatalyst-direct-maccatalyst-x86_64', intel)
        self.assertIn('ios-direct-ios-arm64', intel)
        with self.assertRaisesRegex(ValueError, 'Unsupported'):
            release.signin_checks('unknown')

    def plan_fixture(self, release_version=None, immutable=True):
        with tempfile.TemporaryDirectory() as tmp, patch.object(release, 'ROOT', Path(tmp)):
            folder = Path(tmp)
            records = packages()
            for p in records:
                (folder / p['project']).write_text('<Project/>')
            baseline = {'packages': records}
            release.write(folder / release.BASELINE, baseline)
            release.write(folder / release.CONFIG, {'releaseVersion': release_version,
                'nativeBuild': {'runner': 'macos-26', 'xcode': '26.6', 'cocoapods': '1.17.0'}})
            release.write(folder / 'metadata.json', records)
            def git_file(ref, path):
                if path == release.BASELINE:
                    return baseline if immutable else {'packages': []}
                return {'releaseVersion': release_version}
            def run(*args):
                return 'eng/signin-release.json' if args[1] == 'diff' else 'abc'
            output = folder / 'plan.json'
            with patch.object(release, 'snapshot', return_value=records), patch.object(release, 'git_file', side_effect=git_file), \
                    patch.object(release, 'run', side_effect=run):
                release.plan(types.SimpleNamespace(metadata=folder / 'metadata.json', output=output,
                                                  base='main', online=False, manual=True))
            return release.read(output)

    def test_manual_bootstrap_validation_is_not_release_eligible(self):
        manifest = self.plan_fixture()
        self.assertTrue(manifest['relevant'])
        self.assertFalse(manifest['eligible'])
        self.assertEqual(manifest['channel'], 'signin')

    def test_immutable_bootstrap_baseline(self):
        with self.assertRaisesRegex(ValueError, 'immutable'):
            self.plan_fixture(immutable=False)

    def test_only_explicit_release_version_enables_candidate(self):
        self.assertTrue(self.plan_fixture('9.1.0')['eligible'])
        with self.assertRaisesRegex(ValueError, 'root/release'):
            self.plan_fixture('9.2.0')

    def test_pod_resolution_handles_quoted_subspecs(self):
        text = 'PODS:\n  - GoogleSignIn (9.1.0):\n    - AppAuth (~> 2.0)\n  - AppAuth (2.0.0)\n  - "GoogleUtilities/NSData+zlib (8.1.0)"\nDEPENDENCIES:\n'
        self.assertEqual(validation.pod_versions(text), {'GoogleSignIn': '9.1.0', 'AppAuth': '2.0.0', 'GoogleUtilities': '8.1.0'})

    def test_incomplete_or_wrong_consumer_evidence_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            manifest = {'channel': 'signin', 'sourceCommit': 'abc', 'packages': [{**p, 'sha256': 'expected'} for p in packages()]}
            release.write(folder / 'manifest.json', manifest)
            result = {'Success': True, 'Cases': [{'Success': True}], 'sourceCommit': 'abc',
                      'Checks': ['offline'], 'packageHashes': {p['id']: 'wrong' for p in packages()}}
            release.write(folder / 'consumers.json', result)
            with patch.dict(os.environ, UNIT_TEST_OUTCOME='success', PACKAGE_INSPECTION_OUTCOME='success', BUILD_COMMIT='abc'):
                with self.assertRaisesRegex(ValueError, 'wrong packages'):
                    release.collect_validation(types.SimpleNamespace(results_dir=folder, package_dir=folder))


if __name__ == '__main__':
    unittest.main()
