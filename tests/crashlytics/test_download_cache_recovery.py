"""Drive the real Xamarin.Build.Download path to check the digest pin and the recovery the integrity errors document.

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
    assert value, 'Crashlytics.targets is missing ' + name
    return value.strip()


def xbd_version():
    for reference in ET.parse(CSPROJ).iter('PackageReference'):
        if reference.get('Include') == 'Xamarin.Build.Download':
            return reference.get('Version')
    raise AssertionError('Crashlytics.csproj no longer references Xamarin.Build.Download')


@unittest.skipIf(os.environ.get('CRASHLYTICS_SKIP_NETWORK_TESTS') == '1', 'network-dependent')
class DownloadCacheRecoveryTests(unittest.TestCase):
    """Each test owns a cache directory and shares one restored fixture project."""

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
        # needs none. Disabling the resolver keeps that true however it is invoked.
        cls.env = dict(os.environ, MSBuildEnableWorkloadResolver='false')
        cls.packages = work / 'packages'
        restore = subprocess.run(['dotnet', 'restore', str(cls.project), '--packages', str(cls.packages)],
                                 cwd=work, env=cls.env, capture_output=True, text=True)
        assert restore.returncode == 0, restore.stdout + restore.stderr

    @classmethod
    def tearDownClass(cls):
        cls._work.cleanup()

    def setUp(self):
        self.cache = Path(tempfile.mkdtemp(prefix='crashlytics-cache-')).resolve()
        self.addCleanup(shutil.rmtree, self.cache, ignore_errors=True)
        self.directory = self.cache / self.folder
        self.tool = self.directory / self.script
        self.marker = self.cache / (self.folder + '.unpacked')
        # Xamarin.Build.Download names its cache file after the item plus the ToFile extension.
        self.download_cache = self.cache / (self.folder + Path(self.script).suffix)

    def download(self):
        result = subprocess.run(
            ['dotnet', 'msbuild', str(self.project), '-nologo', '-t:DownloadOnly',
             '-p:OutputType=Exe', '-p:FirebaseCrashlyticsUploadSymbolsEnabled=True',
             '-p:RestorePackagesPath=' + str(self.packages),
             '-p:XamarinBuildDownloadDir=' + str(self.cache) + '/'],
            cwd=self.project.parent, env=self.env, capture_output=True, text=True)
        return result.returncode, result.stdout + result.stderr

    def assertPinnedToolPresent(self, log):
        self.assertTrue(self.tool.exists(), log)
        self.assertEqual(self.digest, hashlib.sha256(self.tool.read_bytes()).hexdigest(), log)

    def test_pinned_source_download_matches_the_pinned_digest(self):
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertPinnedToolPresent(log)

    def test_documented_recovery_restores_the_pinned_tool(self):
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.tool.write_bytes(POISON)
        # Exactly the three removals both integrity errors and the NuGet readme name.
        shutil.rmtree(self.directory)
        self.marker.unlink()
        self.download_cache.unlink(missing_ok=True)
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertPinnedToolPresent(log)

    def test_deleting_only_the_versioned_directory_does_not_restore_the_tool(self):
        code, log = self.download()
        self.assertEqual(0, code, log)
        shutil.rmtree(self.directory)
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertFalse(self.tool.exists(), 'the .unpacked marker must still suppress the download')
        self.assertTrue(self.marker.exists(), log)

    def test_leaving_the_download_cache_restores_the_rejected_bytes(self):
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.download_cache.write_bytes(POISON)
        shutil.rmtree(self.directory)
        self.marker.unlink()
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.assertEqual(POISON, self.tool.read_bytes(),
                         'the download cache must be removed too, or its bytes come back')

    def test_deleting_the_marker_while_the_directory_remains_fails_the_build(self):
        code, log = self.download()
        self.assertEqual(0, code, log)
        self.marker.unlink()
        code, log = self.download()
        self.assertNotEqual(0, code, log)
        self.assertIn('already exists', log)
        self.assertTrue(self.download_cache.exists(),
                        'this is how a stale download cache is left behind')


if __name__ == '__main__':
    unittest.main()
