"""Simulator selection for the E2E runners and Sign-In consumers, against a fake xcrun."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SELECT = ROOT / 'tools/e2e/select-simulator.sh'
IPHONE = '11111111-1111-1111-1111-111111111111'
DEDICATED = '22222222-2222-2222-2222-222222222222'
IPAD = '33333333-3333-3333-3333-333333333333'
DEVICES = {
    'com.apple.CoreSimulator.SimRuntime.iOS-27-1': [
        {'name': 'iPad Pro 11-inch (M5)', 'udid': IPAD},
        {'name': 'iPhone 17 Pro', 'udid': IPHONE},
        {'name': 'GoogleApisCI-iPhone', 'udid': DEDICATED},
    ],
}
STUB = '''#!/usr/bin/env python3
import json, os, sys
devices = json.loads(os.environ['TEST_DEVICES'])
if sys.argv[1:] == ['simctl', 'list', 'devices', 'available', '-j']:
    print(json.dumps({'devices': devices}))
elif sys.argv[1:] == ['simctl', 'list', 'devices', 'available']:
    for runtime, group in devices.items():
        print('-- ' + runtime + ' --')
        for device in group:
            print('    %s (%s) (Shutdown)' % (device['name'], device['udid']))
else:
    sys.exit('unexpected xcrun call: %s' % sys.argv[1:])
'''


class SelectSimulatorTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        xcrun = Path(temp.name) / 'xcrun'
        xcrun.write_text(STUB)
        xcrun.chmod(0o755)
        self.path = temp.name + os.pathsep + os.environ['PATH']

    def select(self, devices=DEVICES, **env):
        environment = {k: v for k, v in os.environ.items() if not k.startswith('E2E_SIMULATOR_')}
        environment.update(env, PATH=self.path, TEST_DEVICES=json.dumps(devices))
        return subprocess.run([str(SELECT)], env=environment, capture_output=True, text=True)

    def test_udid_wins(self):
        result = self.select(E2E_SIMULATOR_UDID=IPAD, E2E_SIMULATOR_NAME='GoogleApisCI-iPhone')
        self.assertEqual((result.returncode, result.stdout), (0, IPAD + '\n'))

    def test_name_selects_that_simulator(self):
        result = self.select(E2E_SIMULATOR_NAME='GoogleApisCI-iPhone')
        self.assertEqual((result.returncode, result.stdout), (0, DEDICATED + '\n'))

    def test_missing_name_fails_with_create_command(self):
        result = self.select(E2E_SIMULATOR_NAME='Missing-iPhone')
        self.assertEqual((result.returncode, result.stdout), (1, ''))
        self.assertIn('xcrun simctl create "Missing-iPhone"', result.stderr)

    def test_name_is_exact(self):
        result = self.select(E2E_SIMULATOR_NAME='GoogleApisCI')
        self.assertEqual((result.returncode, result.stdout), (1, ''))

    def test_duplicate_name_fails(self):
        twin = dict(DEVICES)
        twin['com.apple.CoreSimulator.SimRuntime.iOS-27-0'] = [{'name': 'GoogleApisCI-iPhone', 'udid': IPHONE}]
        result = self.select(twin, E2E_SIMULATOR_NAME='GoogleApisCI-iPhone')
        self.assertEqual((result.returncode, result.stdout), (1, ''))
        self.assertIn('2 available simulators', result.stderr)

    def test_without_settings_uses_first_iphone(self):
        result = self.select()
        self.assertEqual((result.returncode, result.stdout), (0, IPHONE + '\n'))

    def test_without_settings_or_iphones_fails(self):
        result = self.select({'r': [{'name': 'iPad Pro 11-inch (M5)', 'udid': IPAD}]})
        self.assertEqual((result.returncode, result.stdout), (1, ''))

    def test_ci_workflows_pin_the_dedicated_simulator(self):
        for workflow in ('firebase-release.yml', 'google-foundation-validation.yml'):
            with self.subTest(workflow=workflow):
                text = (ROOT / '.github/workflows' / workflow).read_text()
                self.assertIn('\nenv:\n  E2E_SIMULATOR_NAME: GoogleApisCI-iPhone\n', text)


if __name__ == '__main__':
    unittest.main()
