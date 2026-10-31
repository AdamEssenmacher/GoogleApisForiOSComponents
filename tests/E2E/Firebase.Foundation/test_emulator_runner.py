"""Runner regressions without downloads, native builds, or a booted simulator.

Run: python3 -m unittest discover -s tests/E2E/Firebase.Foundation -p 'test_*.py'
"""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[3]
RUNNER = ROOT / 'tools/e2e/run-firebase-foundation.sh'
STUB = '''#!/usr/bin/env python3
import json, os, pathlib, subprocess, sys
name = pathlib.Path(sys.argv[0]).name
with open(os.environ['TEST_CALLS'], 'a') as log:
    log.write(json.dumps(dict(name=name, args=sys.argv[1:], xdg=os.getenv('XDG_CONFIG_HOME'), cache=os.getenv('FIREBASE_EMULATORS_PATH'))) + '\\n')
if name == 'java':
    print('openjdk version "' + os.getenv('TEST_JAVA_VERSION', '21.0.12') + '"', file=sys.stderr)
    sys.exit(int(os.getenv('TEST_JAVA_EXIT', '0')))
if name == 'firebase':
    env = dict(os.environ, FIRESTORE_EMULATOR_HOST=os.getenv('TEST_ENDPOINT', '127.0.0.1:19123'))
    sys.exit(subprocess.run(['/bin/sh', '-c', sys.argv[-1]], env=env).returncode)
# Stop at restore, before any real build or simulator work.
sys.exit(42)
'''


class EmulatorRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "repo space ' $dollar"
        self.runner = self.root / 'tools/e2e/run-firebase-foundation.sh'
        self.runner.parent.mkdir(parents=True)
        shutil.copy2(RUNNER, self.runner)
        self.tests = self.root / 'tests/E2E/Firebase.Foundation'
        self.tests.mkdir(parents=True)
        shutil.copy2(ROOT / 'tests/E2E/Firebase.Foundation/runtime-drift-cases.json', self.tests)
        shutil.copytree(ROOT / 'tests/E2E/Firebase.Foundation/emulator', self.tests / 'emulator')
        self.project = self.tests / 'FirebaseFoundationE2E'
        self.project.mkdir()
        self.feed = self.root / "local feed's $cash"
        self.feed.mkdir()
        manifest = json.loads((self.tests / 'runtime-drift-cases.json').read_text())
        packages = set(re.findall(r'"(AdamE\.[^"\n]+)"', RUNNER.read_text()))
        for case in manifest['cases']:
            packages.add(case['bindingPackage'])
            packages.update(p['id'] for p in case['packages'])
        for package in packages:
            (self.feed / (package + '.test.nupkg')).touch()
        stub_dir = self.root / 'stubs'
        stub_dir.mkdir()
        for name in ['firebase', 'java', 'dotnet']:
            path = stub_dir / name
            path.write_text(STUB)
            path.chmod(0o755)
        self.calls_path = self.root / 'calls.jsonl'
        self.env = dict(os.environ, PATH=str(stub_dir) + os.pathsep + os.environ['PATH'], TEST_CALLS=str(self.calls_path))
        for key in ['XDG_CONFIG_HOME', 'FIREBASE_EMULATORS_PATH', 'E2E_FIRESTORE_EMULATOR_ACTIVE', 'FIRESTORE_EMULATOR_HOST']:
            self.env.pop(key, None)
        self.args = ['--package-dir', str(self.feed), '--configuration', 'Debug', '--runtime-drift-case', 'cloudfirestore-aggregate-query', '--firestore-emulator']

    def run_lane(self, args=None, **env):
        self.calls_path.unlink(missing_ok=True)
        return subprocess.run([str(self.runner), *(self.args if args is None else args)], env=dict(self.env, **env), text=True, capture_output=True)

    def calls(self, name):
        rows = [json.loads(line) for line in self.calls_path.read_text().splitlines()] if self.calls_path.exists() else []
        return [row for row in rows if row['name'] == name]

    def test_reentry_preserves_arguments_and_caller_environment(self):
        for version in ['21.0.12', '25']:
            for xdg, cache in [(None, None), ('', None), (None, ''), ("caller config ' $dollar; literal", "different cache ' $path")]:
                with self.subTest(java=version, xdg=xdg, cache=cache):
                    env = {}
                    if xdg is not None:
                        env['XDG_CONFIG_HOME'] = xdg
                    if cache is not None:
                        env['FIREBASE_EMULATORS_PATH'] = cache
                    result = self.run_lane(TEST_JAVA_VERSION=version, **env)
                    self.assertEqual(result.returncode, 42, result.stderr)
                    cli, = self.calls('firebase')
                    restore, = self.calls('dotnet')
                    self.assertNotEqual(cli['xdg'], xdg)
                    self.assertEqual(restore['xdg'], xdg)
                    self.assertEqual(restore['cache'], cache)
                    self.assertEqual(restore['args'][0], 'restore')
                    self.assertIn('-p:EnableFirestoreEmulator=true', restore['args'])
                    config = self.tests / 'artifacts/NuGet.generated.config'
                    self.assertIn(str(self.feed), config.read_text())
                    self.assertFalse((self.project / 'GoogleService-Info.plist').exists())
                    self.assertFalse((self.tests / 'artifacts/firestore-emulator/run-consumer.sh').exists())

    def test_emulator_uses_free_loopback_ports(self):
        result = self.run_lane()
        self.assertEqual(result.returncode, 42, result.stderr)
        cli, = self.calls('firebase')
        config_path = Path(cli['args'][cli['args'].index('--config') + 1])
        self.assertEqual(config_path.parent, self.tests / 'artifacts/firestore-emulator')
        self.assertTrue((config_path.parent / 'firestore.rules').is_file())
        emulators = json.loads(config_path.read_text())['emulators']
        defaults = json.loads((self.tests / 'emulator/firebase.json').read_text())['emulators']
        ports = [emulators['firestore']['port'], emulators['firestore']['websocketPort'], emulators['hub']['port'], emulators['logging']['port']]
        self.assertEqual(len(set(ports)), 4)
        self.assertNotIn(defaults['firestore']['port'], ports)
        self.assertEqual({e['host'] for e in (emulators['firestore'], emulators['hub'], emulators['logging'])}, {'127.0.0.1'})

    def test_conflicting_lanes_fail_before_startup(self):
        result = self.run_lane(self.args + ['--binding-surface-target', 'all'])
        self.assertEqual(result.returncode, 1)
        self.assertIn('mutually exclusive', result.stderr)
        self.assertFalse(self.calls('firebase'))
        self.assertFalse(self.calls('java'))

    def test_missing_package_fails_before_startup(self):
        next(self.feed.glob('AdamE.Firebase.iOS.Core.*')).unlink()
        result = self.run_lane()
        self.assertEqual(result.returncode, 1)
        self.assertIn('Missing package', result.stderr)
        self.assertFalse(self.calls('firebase'))

    def test_old_or_unavailable_java_fails_before_startup(self):
        for version, exit_code in [('17.0.1', '0'), ('1.8.0', '0'), ('20', '0'), ('unknown', '0'), ('21', '1')]:
            with self.subTest(java=version, exit_code=exit_code):
                result = self.run_lane(TEST_JAVA_VERSION=version, TEST_JAVA_EXIT=exit_code)
                self.assertEqual(result.returncode, 1)
                self.assertIn('Java 21 or newer', result.stderr)
                self.assertFalse(self.calls('firebase'))

    def test_invalid_endpoint_cannot_reach_consumer_restore(self):
        for endpoint in ['', 'example.invalid:19123', '127.0.0.1:0', '127.0.0.1:65536', 'http://127.0.0.1:19123', '127.0.0.1:19123/path']:
            with self.subTest(endpoint=endpoint):
                result = self.run_lane(TEST_ENDPOINT=endpoint)
                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertIn('valid port', result.stderr)
                self.assertFalse(self.calls('dotnet'))

    def test_ordinary_lane_still_requires_plist(self):
        result = self.run_lane(['--package-dir', str(self.feed)])
        self.assertEqual(result.returncode, 1)
        self.assertIn('Missing Firebase config file', result.stderr)
        self.assertFalse(self.calls('dotnet'))


if __name__ == '__main__':
    unittest.main()
