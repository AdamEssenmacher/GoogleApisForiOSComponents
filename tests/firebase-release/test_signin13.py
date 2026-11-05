import argparse
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)
adapter = release.signin13()


class SignIn13Tests(unittest.TestCase):
    def setUp(self):
        release.configure_channel('signin')
        self.addCleanup(release.configure_channel, 'firebase')

    def test_static_revision_requires_the_published_1010_predecessor(self):
        with self.assertRaisesRegex(ValueError, 'expected 10.1.0'):
            release.require_signin_predecessor('10.1.0.1', {'releaseVersion': '10.0.0'})
        release.require_signin_predecessor('10.1.0.1', {'releaseVersion': '10.1.0'})

    def test_missing_support_from_one_feed_blocks_all_writes(self):
        package = {'id': 'AdamE.Google.iOS.AppCheckCore', 'version': '12.0.0', 'payloadHash': 'verified'}
        feeds = [Mock(name='nuget'), Mock(name='github')]
        feeds[0].name, feeds[1].name = 'nuget', 'github'
        feeds[0].fetch.return_value = b'available'
        feeds[1].fetch.return_value = None
        with patch.object(release, 'payload_hash', return_value='verified'), patch.object(release.subprocess, 'run') as push:
            with self.assertRaisesRegex(ValueError, 'Required dependency missing: github'):
                release.publish_packages(Path('.'), [package], feeds, reuse_only=release.SIGNIN_SHARED_IDS)
            push.assert_not_called()

    def test_static_pr_plan_stays_ineligible_even_with_version_set(self):
        metadata = [
            {'id': package, 'version': '10.1.0.1', 'firebase': False}
            for package in sorted(release.SIGNIN_IDS)
        ]
        config = {'releaseVersion': '10.1.0.1',
                  'nativeBuild': {'runner': 'xcode-27.1', 'xcode': '27.1', 'delivery': 'firebase13-static'}}
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            release.write(directory / release.CONFIG, config)
            release.write(directory / release.BASELINE, {'packages': []})
            release.write(directory / 'metadata.json', metadata)
            with patch.object(release, 'ROOT', directory), patch.object(release, 'snapshot', return_value=metadata), \
                    patch.object(release, 'run', return_value='abc'), patch.dict(os.environ, GITHUB_EVENT_NAME='pull_request'):
                release.plan(argparse.Namespace(metadata=directory/'metadata.json', online=False, manual=True,
                                                base=None, output=directory/'plan.json'))
            plan = release.read(directory/'plan.json')
            self.assertTrue(plan['relevant'])
            self.assertFalse(plan['eligible'])

    def test_static_planning_classifies_firebase13_fixture_changes(self):
        integration = adapter.integration_metadata(release)
        fixtures = release.fixture_packages(integration)
        self.assertEqual(release.normalized(fixtures['AdamE.Firebase.iOS.CloudFirestore']), '13.1.1')
        path = 'source/Firebase/CloudFirestore/ApiDefinition.cs'
        self.assertEqual(release.validation_reasons([path], [], 'signin', integration), [path])

    def test_consumer_evidence_requires_exact_hashes_matrix_and_runtime(self):
        package = {'id': release.SIGNIN_ROOT_ID, 'sha256': 'sealed'}
        manifest = {'sourceCommit': 'abc', 'packages': [package]}
        builds = release.signin_checks('arm64') - {'ios-direct-iossimulator-arm64-rebuild'}
        checks = builds | {c + '-offline-rebuild' for c in builds if not c.startswith('restore-')}
        checks.add('PASS classes=8 selectors=75 claims=auth_time,amr wrapper=wrapperIdentifier')
        valid = {'Lane': 'consumers', 'sourceCommit': 'abc', 'Checks': sorted(checks),
                 'packageHashes': {package['id']: 'sealed'}}
        with patch.dict(os.environ, BUILD_COMMIT='abc'):
            adapter.validate_consumer(release, valid, manifest, 'consumers')
            for field, value, error in [
                ('sourceCommit', 'def', 'wrong source commit'),
                ('packageHashes', {package['id']: 'different'}, 'wrong Sign-In package bytes'),
                ('Checks', sorted(checks - {'ios-transitive-ios-arm64-offline-rebuild'}), 'Incomplete'),
                ('Checks', sorted(c for c in checks if not c.startswith('PASS ')), 'runtime and selector'),
            ]:
                with self.subTest(field=field), self.assertRaisesRegex(ValueError, error):
                    adapter.validate_consumer(release, {**valid, field: value}, manifest, 'consumers')
