"""Drive the real Xamarin.Build.Download path to check the digest pin and what the documented recovery depends on.

The recovery steps themselves live in the integrity error text, which
test_upload_integrity.py asserts; this suite establishes that the cache behaves the
way those steps claim.

These tests download the pinned upload-symbols source, so they need network access.
Set CRASHLYTICS_SKIP_NETWORK_TESTS=1 to skip them when working offline.
"""
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
TARGETS = ROOT / 'source/Firebase/Crashlytics/Crashlytics.targets'
CSPROJ = ROOT / 'source/Firebase/Crashlytics/Crashlytics.csproj'
POISON = b'stale download cache, not the pinned tool\n'
# The fixture imports the shipped targets, so the download item, cache folder and digest
# under test are the ones consumers get rather than a copy that can drift.
PROJECT = '''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Xamarin.Build.Download" Version="{version}" PrivateAssets="None" />
  </ItemGroup>
  <Import Project="{targets}" />
  <Target Name="DownloadOnly" DependsOnTargets="_XamarinBuildDownloadCore" />
</Project>
'''


def targets_property(name):
    value = ET.parse(TARGETS).findtext('.//' + name)
    if not value:
        raise AssertionError('Crashlytics.targets is missing ' + name)
    return value.strip()


def xbd_version():
    for reference in ET.parse(CSPROJ).iter('PackageReference'):
        if reference.get('Include') == 'Xamarin.Build.Download':
            return reference.get('Version')
    raise AssertionError('Crashlytics.csproj no longer references Xamarin.Build.Download')


@unittest.skipIf(os.environ.get('CRASHLYTICS_SKIP_NETWORK_TESTS') == '1', 'network-dependent')
class DownloadCacheRecoveryTests(unittest.TestCase):
    """One shared baseline download; each test works on its own copy of that cache."""

    @classmethod
    def setUpClass(cls):
        cls.folder = targets_property('_FirebaseCrashlyticsItemsFolder')
        cls.script = targets_property('_FirebaseScriptName')
        cls.digest = targets_property('_FirebaseCrashlyticsScriptSha256')
        cls._work = tempfile.TemporaryDirectory(prefix='crashlytics-download-')
        work = Path(cls._work.name).resolve()
        cls.project = work / 'fixture.csproj'
        cls.project.write_text(PROJECT.format(version=xbd_version(), targets=TARGETS))
        # No global.json beside the fixture: its pinned workloadVersion would make the
        # workload resolver fail wherever workloads are not installed, and this project
        # needs none. Disabling the resolver keeps that true however the fixture is invoked.
        cls.env = dict(os.environ, MSBuildEnableWorkloadResolver='false')
        packages = work / 'packages'
        restore = subprocess.run(['dotnet', 'restore', str(cls.project), '--packages', str(packages)],
                                 cwd=work, env=cls.env, capture_output=True, text=True)
        if restore.returncode != 0:
            raise AssertionError(restore.stdout + restore.stderr)
        # Build each starting state once and reuse it, so a run of this suite fetches the
        # pinned source only for the cases that must observe a fresh download.
        cls.baseline = work / 'baseline'
        cls.baseline.mkdir()
        code, log = cls.run_download(cls.baseline)
        if code != 0:
            raise AssertionError(log)
        # The one state that leaves a download cache behind: the marker is gone while the
        # versioned directory remains, so the download succeeds but cannot be moved in.
        cls.failed = work / 'failed'
        shutil.copytree(cls.baseline, cls.failed)
        (cls.failed / (cls.folder + '.unpacked')).unlink()
        code, cls.failed_log = cls.run_download(cls.failed)
        if code == 0:
            raise AssertionError('expected the interrupted download state to fail\n' + cls.failed_log)
        leftover = cls.failed / (cls.folder + Path(cls.script).suffix)
        if not leftover.exists():
            raise AssertionError('expected a leftover download cache\n' + cls.failed_log)

    @classmethod
    def tearDownClass(cls):
        cls._work.cleanup()

    @classmethod
    def run_download(cls, cache):
        result = subprocess.run(
            ['dotnet', 'msbuild', str(cls.project), '-nologo', '-t:DownloadOnly',
             '-p:OutputType=Exe', '-p:FirebaseCrashlyticsUploadSymbolsEnabled=True',
             '-p:XamarinBuildDownloadDir=' + str(cache) + '/'],
            cwd=cls.project.parent, env=cls.env, capture_output=True, text=True)
        return result.returncode, result.stdout + result.stderr

    def setUp(self):
        parent = Path(tempfile.mkdtemp(prefix='crashlytics-cache-')).resolve()
        self.addCleanup(shutil.rmtree, parent, ignore_errors=True)
        self.cache = parent / 'cache'
        shutil.copytree(self.baseline, self.cache)
        self.directory = self.cache / self.folder
        self.tool = self.directory / self.script
        self.marker = self.cache / (self.folder + '.unpacked')
        # Xamarin.Build.Download names its download cache after the item plus the
        # ToFile extension. A successful download moves it into the versioned
        # directory, so it is absent until an attempt fails partway.
        self.download_cache = self.cache / (self.folder + Path(self.script).suffix)

    def download(self):
        return self.run_download(self.cache)

    def assertPinnedToolPresent(self, log):
        self.assertTrue(self.tool.exists(), log)
        self.assertEqual(self.digest, hashlib.sha256(self.tool.read_bytes()).hexdigest(), log)

    def use_failed_attempt_state(self):
        """Switch this test's cache to the state a failed attempt leaves behind."""
        shutil.rmtree(self.cache)
        shutil.copytree(self.failed, self.cache)
        self.assertTrue(self.download_cache.exists())
        self.assertFalse(self.marker.exists())

    def test_pinned_source_download_matches_the_pinned_digest(self):
        self.assertPinnedToolPresent('baseline download')
        self.assertFalse(self.download_cache.exists(),
                         'a successful download consumes the download cache')

    def test_documented_recovery_after_a_tampered_tool(self):
        self.tool.write_bytes(POISON)
        shutil.rmtree(self.directory)
        self.marker.unlink()
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertPinnedToolPresent(log)

    def test_documented_recovery_after_a_failed_attempt(self):
        self.use_failed_attempt_state()
        shutil.rmtree(self.directory)
        self.download_cache.unlink()
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertPinnedToolPresent(log)

    def test_deleting_only_the_versioned_directory_does_not_restore_the_tool(self):
        shutil.rmtree(self.directory)
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertFalse(self.tool.exists(), 'the marker must still suppress the download')

    def test_leaving_the_download_cache_restores_the_rejected_bytes(self):
        self.use_failed_attempt_state()
        self.download_cache.write_bytes(POISON)
        shutil.rmtree(self.directory)
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertEqual(POISON, self.tool.read_bytes(),
                         'the download cache must be deleted too, or its bytes come back')

    def test_deleting_the_marker_while_the_directory_remains_fails_the_build(self):
        # setUpClass already proved this fails and strands a download cache; assert the
        # failure names the tool, rather than the runtime's localizable "already exists".
        self.assertIn(self.script, self.failed_log)


if __name__ == '__main__':
    unittest.main()
