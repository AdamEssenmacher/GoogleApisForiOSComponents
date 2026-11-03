import copy
import importlib.util
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('foundation_plan', ROOT / 'scripts/ci/plan-google-foundation.py')
plan = importlib.util.module_from_spec(spec)
spec.loader.exec_module(plan)


class FoundationPlanTests(unittest.TestCase):
    def setUp(self):
        self.before = {c: {'metadata': {'version': '1'}, 'files': {'project': 'hash'}, 'build': 'hash'} for c in plan.COMPONENTS}

    def test_unrelated_firebase_and_documentation_changes_skip_native_jobs(self):
        for files in (['components.cake'], ['source/Firebase/Core/Core.csproj'], ['Readme.md']):
            self.assertEqual({'maps': False, 'places': False}, plan.select(self.before, self.before, files))

    def test_selected_package_or_build_input_change_runs_only_affected_component(self):
        for component in plan.COMPONENTS:
            for key in ('metadata', 'files', 'build'):
                after = copy.deepcopy(self.before)
                after[component][key] = 'changed'
                self.assertEqual({c.lower(): c == component for c in plan.COMPONENTS},
                                 plan.select(self.before, after, ['components.cake']))

    def test_manual_and_validation_changes_always_run_both(self):
        for path in ('.github/workflows/google-foundation-validation.yml', 'scripts/ci/plan-google-foundation.py',
                     'scripts/FirebaseReleaseFingerprint/BuildInputs.cs', 'tests/ci/test_foundation_plan.py',
                     'tests/E2E/Google.Foundation/case.cs', 'tools/e2e/check-offline-build.sh', 'new-helper.cake'):
            with self.subTest(path=path):
                self.assertTrue(all(plan.select(self.before, self.before, [path]).values()))
        self.assertTrue(all(plan.select(self.before, self.before, [], manual=True).values()))

    def test_source_deletion_is_a_change(self):
        after = copy.deepcopy(self.before)
        after['Maps']['files'] = {}
        self.assertEqual({'maps': True, 'places': False}, plan.select(self.before, after, ['source/Google/Maps/ApiDefinition.cs']))

    def test_workflow_triggers_on_every_path_that_forces_validation(self):
        # A planner or fingerprint change that forces both components is useless if the
        # workflow's own paths filter never starts the run, so pin the two lists together.
        workflow = (ROOT / '.github/workflows/google-foundation-validation.yml').read_text().splitlines()
        start = workflow.index('    paths:') + 1
        triggers = set()
        for line in workflow[start:]:
            entry = re.fullmatch(r'      - "(.+)"', line)
            if entry:
                triggers.add(entry.group(1))
            elif not line.startswith('      #'):
                break
        for prefix in plan.COVERAGE:
            with self.subTest(prefix=prefix):
                self.assertIn(prefix + '**' if prefix.endswith('/') else prefix, triggers)

    def test_missing_selected_state_fails_instead_of_skipping(self):
        with self.assertRaises(KeyError):
            plan.select(self.before, {}, ['components.cake'])


if __name__ == '__main__':
    unittest.main()
