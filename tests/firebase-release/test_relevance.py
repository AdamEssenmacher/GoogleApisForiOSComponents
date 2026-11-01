"""Regression coverage for native lane scheduling and its observable plan reasons."""
from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('relevance_release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class RelevanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.root_patch = patch.object(release, 'ROOT', self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)
        self.signin = [self.package('Google', name) for name in
                       ('SignIn', 'AppCheckCore', 'GTMSessionFetcher', 'PromisesObjC', 'GoogleUtilities')]
        self.firebase = [self.package('Firebase', name, ['AdamE.Firebase.iOS.Core',
                         'AdamE.Google.iOS.GoogleUtilities']) for name in ('Auth', 'Analytics', 'CloudFirestore')]
        self.firebase += [self.package('Firebase', 'Core'), self.package('Firebase', 'Crashlytics')]
        self.firebase += self.signin[1:]
        for p in self.firebase + self.signin:
            path = self.root / p['project']
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('<Project><ItemGroup>'
                            '<Compile Include="../../../linked/shared.cs"/>'
                            '<None Include="../../../icons/shared.png" Pack="true"/>'
                            '<Import Project="../../../eng/shared.targets"/>'
                            '</ItemGroup></Project>')
        release.write(self.root / 'integration.json', self.firebase)

    @staticmethod
    def package(group, name, dependencies=()):
        return {'id': f'AdamE.{group}.iOS.{name}', 'key': f'{group}.{name}',
                'project': f'source/{group}/{name}/{name}.csproj',
                'version': '9.1.0' if name == 'SignIn' else '12.14.0',
                'firebase': group == 'Firebase', 'dependencies': list(dependencies), 'fingerprint': name}

    def plan(self, channel, filename, manual=False, base='base', eligible=False):
        release.configure_channel(channel)
        packages = self.signin if channel == 'signin' else self.firebase
        config = {'releaseVersion': ('9.1.0' if channel == 'signin' else '12.14.0') if eligible else None,
                  'minimumFirebaseVersion': '12.12.0',
                  'nativeBuild': {'runner': 'xcode-27.1', 'xcode': '27.1', 'cocoapods': '1.17.0'}}
        baseline = {'packages': packages}
        release.write(self.root / release.CONFIG, config)
        release.write(self.root / release.BASELINE, baseline)
        release.write(self.root / 'metadata.json', packages)
        def git_file(ref, path):
            return baseline if path == release.BASELINE else {**config, 'releaseVersion': None}
        def run(*args):
            return filename if args[1] == 'diff' else 'abc'
        output = self.root / 'plan.json'
        with redirect_stdout(io.StringIO()), patch.object(release, 'snapshot', return_value=packages), \
                patch.object(release, 'git_file', side_effect=git_file), \
                patch.object(release, 'run', side_effect=run):
            release.plan(types.SimpleNamespace(metadata=self.root / 'metadata.json', online=False,
                         base=base, manual=manual, output=output,
                         integration_metadata=self.root / 'integration.json'))
        return json.loads(output.read_text())

    def test_changed_inputs_schedule_only_consuming_lanes(self):
        both = {'firebase', 'signin'}
        cases = [
            ('eng/signin-release.json', {'signin'}),
            ('source/Google/SignIn/ApiDefinition.cs', {'signin'}),
            ('scripts/signin-release/consumers.py', {'signin'}),
            ('signin_publish.cake', {'signin'}),
            ('source/Firebase/Crashlytics/ApiDefinition.cs', {'firebase'}),
            ('eng/firebase-release.json', {'firebase'}),
            ('firebase_publish.cake', both),
            ('firebase_release.cake', both),
            ('scripts/FirebaseBindingAudit/AuditRunner.cs', {'firebase'}),
            ('scripts/FirebaseBindingAudit.Tests/AuditRunnerTests.cs', {'firebase'}),
            ('scripts/firebase-binding-audit.json', {'firebase'}),
            ('tests/E2E/Firebase.Foundation/emulator/firestore.rules', {'firebase'}),
            ('tools/e2e/run-firebase-foundation.sh', {'firebase'}),
            ('source/Firebase/Auth/ApiDefinition.cs', both),
            ('source/Firebase/Analytics/ApiDefinition.cs', both),
            ('source/Firebase/CloudFirestore/ApiDefinition.cs', both),
            ('source/Firebase/Core/ApiDefinition.cs', both),
            ('source/Google/GoogleUtilities/ApiDefinition.cs', both),
            ('global.json', both), ('Directory.Build.props', both),
            ('.config/dotnet-tools.json', both), ('source/AssemblyInfo.cs', both),
            ('build.cake', both), ('common.cake', both), ('components.cake', both),
            ('poco.cake', both), ('custom_externals_download.cake', both),
            ('scripts/firebase-release/release.py', both),
            ('scripts/FirebaseReleaseFingerprint/BuildInputs.cs', both),
            ('tests/firebase-release/test_release.py', both),
            ('.github/workflows/firebase-release.yml', both), ('tools/ci/select-xcode.sh', both),
            ('linked/shared.cs', both), ('icons/shared.png', both), ('eng/shared.targets', both),
            ('source/Google/Maps/ApiDefinition.cs', set()),
            ('source/Google/Places/ApiDefinition.cs', set()),
            ('tests/E2E/Google.Foundation/README.md', set()),
            ('tools/e2e/run-google-foundation.sh', set()),
            ('scripts/unrelated.py', set()), ('eng/unrelated.json', set()),
            ('unrelated.cake', set()), ('docs/FIREBASE_RELEASES.md', set()),
        ]
        for filename, expected in cases:
            for channel in ('firebase', 'signin'):
                with self.subTest(filename=filename, channel=channel):
                    result = self.plan(channel, filename)
                    self.assertEqual(result['relevant'], channel in expected)
                    self.assertEqual(result['reasons'], [filename] if channel in expected else [])
                    self.assertFalse(result['eligible'])

    def test_forced_validation_reasons(self):
        for channel in ('firebase', 'signin'):
            for kwargs, reason in [({'manual': True}, 'manual validation'),
                                   ({'base': None}, 'missing comparison history'),
                                   ({'eligible': True}, 'eligible release')]:
                with self.subTest(channel=channel, kwargs=kwargs):
                    result = self.plan(channel, 'Readme.md', **kwargs)
                    self.assertTrue(result['relevant'])
                    self.assertIn(reason, result['reasons'])
                    self.assertEqual(result['eligible'], kwargs.get('eligible', False))

    def test_reasons_are_sorted_and_deduplicated(self):
        reasons = release.validation_reasons(['global.json', 'common.cake', 'global.json'],
                                             self.firebase, 'firebase')
        self.assertEqual(reasons, ['common.cake', 'global.json'])

    def test_new_fixture_major_does_not_enforce_staging_policy_during_planning(self):
        # Exercise roots and a transitive dependency independently, without changing
        # Sign-In's own package inputs or allowing unrelated changes to schedule it.
        for name in ('Auth', 'Core'):
            with self.subTest(package=name):
                metadata = [{**p, 'version': '13.0.0'} if p['id'] == f'AdamE.Firebase.iOS.{name}' else p
                            for p in self.firebase]
                release.write(self.root / 'integration.json', metadata)
                for path, expected in [('Readme.md', []),
                        ('eng/firebase-release.json', []),
                        ('source/Firebase/Crashlytics/ApiDefinition.cs', []),
                        (f'source/Firebase/{name}/ApiDefinition.cs', [f'source/Firebase/{name}/ApiDefinition.cs']),
                        ('source/Google/SignIn/ApiDefinition.cs', ['source/Google/SignIn/ApiDefinition.cs'])]:
                    with self.subTest(path=path):
                        result = self.plan('signin', path)
                        self.assertEqual(result['reasons'], expected)
                        self.assertEqual(result['relevant'], bool(expected))
                        self.assertFalse(result['eligible'])

    def test_incomplete_integration_metadata_fails_closed(self):
        for metadata in ([], [p for p in self.firebase if p['id'] != 'AdamE.Firebase.iOS.Core']):
            with self.subTest(ids=[p['id'] for p in metadata]):
                with self.assertRaisesRegex(ValueError, 'Missing integration metadata'):
                    release.validation_reasons(['Readme.md'], self.signin, 'signin', metadata)


if __name__ == '__main__':
    unittest.main()
