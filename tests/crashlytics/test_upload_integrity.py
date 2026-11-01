"""Execute the real upload targets with a fixture tool and a recording remote-task shim."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
TARGETS = ROOT / 'source/Firebase/Crashlytics/Crashlytics.targets'
TOOL = b'#!/bin/sh\nprintf executed >> "$TEST_UPLOAD_MARKER"\n'


def targets_property(name):
    value = ET.parse(TARGETS).findtext('.//' + name)
    if not value:
        raise AssertionError('Crashlytics.targets is missing ' + name)
    return value.strip()
SHIMS = r'''
<UsingTask TaskName="Exec" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll" Override="true">
<ParameterGroup><Command ParameterType="System.String" Required="true"/><SessionId ParameterType="System.String"/></ParameterGroup>
<Task><Using Namespace="System"/><Using Namespace="System.IO"/><Using Namespace="System.Diagnostics"/>
<Code Type="Fragment" Language="cs"><![CDATA[
var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
var script = Path.GetTempFileName(); File.WriteAllText(script, Command);
psi.Arguments = "\"" + script + "\"";
if (!string.IsNullOrEmpty(SessionId)) psi.WorkingDirectory = Environment.GetEnvironmentVariable("TEST_REMOTE_ROOT");
try { using (var p = Process.Start(psi)) { p.WaitForExit(); if (p.ExitCode != 0) { Log.LogError("Fixture Exec failed: " + p.ExitCode); return false; } } }
finally { File.Delete(script); }
return true;
]]></Code></Task></UsingTask>
<UsingTask TaskName="CopyFilesToBuildServer" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
<ParameterGroup><Files ParameterType="Microsoft.Build.Framework.ITaskItem[]" Required="true"/><SessionId ParameterType="System.String"/></ParameterGroup>
<Task><Using Namespace="System"/><Using Namespace="System.IO"/>
<Code Type="Fragment" Language="cs"><![CDATA[
var root = Environment.GetEnvironmentVariable("TEST_REMOTE_ROOT"); Directory.CreateDirectory(root);
foreach (var file in Files) { var path = Path.Combine(root, file.GetMetadata("TargetPath")); File.Copy(file.ItemSpec, path, true);
 if (Environment.GetEnvironmentVariable("TEST_TAMPER_REMOTE") == "true") File.WriteAllText(path, "substituted after copy"); }
return true;
]]></Code></Task></UsingTask>
'''


class UploadIntegrityTests(unittest.TestCase):
    def assertRecoveryStepsNamed(self, log):
        """Guard the recovery steps themselves, which only live in the error text.

        test_download_cache_recovery.py proves what the cache does; nothing but this
        checks that the messages tell a consumer to do it.
        """
        folder = targets_property('_FirebaseCrashlyticsItemsFolder')
        suffix = Path(targets_property('_FirebaseScriptName')).suffix
        # The marker is what makes deleting the directory alone a no-op.
        self.assertIn(folder + '.unpacked', log)
        # The download cache restores its bytes instead of re-downloading.
        self.assertIn(folder + suffix, log)
        # Deleting it is immaterial to recovery, so the steps must not ask for it.
        self.assertNotIn(folder + '.locked', log)

    def exercise(self, remote=False, content=TOOL, enabled=True, tamper=False, continue_on_error=False, recover=False, output_type="Exe", app_extension=False):
        with tempfile.TemporaryDirectory(prefix='crashlytics-integrity-') as tmp:
            root = Path(tmp).resolve()
            (root / 'global.json').write_bytes((ROOT / 'global.json').read_bytes())
            cache_name = targets_property('_FirebaseCrashlyticsItemsFolder')
            cache = root / 'cache' / cache_name; cache.mkdir(parents=True)
            script = cache / 'upload-symbols.sh'
            if content is not None: script.write_bytes(content)
            marker = root / 'executed'
            project = root / 'fixture.proj'
            project.write_text('<Project>' + SHIMS + '<Import Project="' + str(ROOT / 'source/Firebase/Crashlytics/Crashlytics.targets') + '"/></Project>')
            env = dict(os.environ, TEST_UPLOAD_MARKER=str(marker), TEST_REMOTE_ROOT=str(root / 'remote'), TEST_TAMPER_REMOTE=str(tamper).lower())
            name = '_FirebaseCrashlyticsUploadDSymToFirebaseOn' + ('Windows' if remote else 'Mac')
            command = ['dotnet', 'msbuild', str(project), '-nologo', '-t:' + name,
                       '-p:XamarinBuildDownloadDir=' + str(root / 'cache') + '/',
                       '-p:_FirebaseCrashlyticsScriptSha256=' + hashlib.sha256(TOOL).hexdigest(),
                       '-p:FirebaseCrashlyticsUploadSymbolsEnabled=' + str(enabled),
                       '-p:FirebaseCrashlyticsUploadSymbolsContinueOnError=' + str(continue_on_error),
                       '-p:IsMacEnabled=true', '-p:BuildSessionId=fixture',
                       '-p:OutputType=' + output_type, '-p:IsAppExtension=' + str(app_extension)]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True)
            if recover:
                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertFalse(marker.exists())
                # Stands in for a cache the download has repopulated; the documented
                # Xamarin.Build.Download recovery itself is covered by
                # test_download_cache_recovery.py, which drives the real download path.
                shutil.rmtree(cache)
                cache.mkdir()
                script.write_bytes(TOOL)
                result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True)
            return result.returncode, marker.exists(), result.stdout + result.stderr

    def test_library_consumers_skip_verification_and_execution(self):
        for remote in (False, True):
            for content in (None, b'corrupt'):
                with self.subTest(remote=remote, content=content):
                    code, executed, log = self.exercise(remote=remote, content=content, output_type="Library")
                    self.assertEqual(0, code, log)
                    self.assertFalse(executed)

    def test_app_extensions_still_require_and_execute_verified_tool(self):
        for remote in (False, True):
            for content in (None, b'corrupt', TOOL):
                with self.subTest(remote=remote, content=content):
                    code, executed, log = self.exercise(remote=remote, content=content, output_type="Library", app_extension=True)
                    self.assertEqual(content == TOOL, code == 0, log)
                    self.assertEqual(content == TOOL, executed, log)

    def test_build_recovers_once_the_cache_holds_the_pinned_tool(self):
        for remote in (False, True):
            with self.subTest(remote=remote):
                code, executed, log = self.exercise(remote=remote, content=b'corrupt', recover=True)
                self.assertEqual(0, code, log)
                self.assertTrue(executed, log)

    def test_matching_tool_runs_locally_and_after_remote_copy(self):
        for remote in (False, True):
            with self.subTest(remote=remote):
                code, executed, log = self.exercise(remote=remote)
                self.assertEqual(0, code, log)
                self.assertTrue(executed, log)

    def test_missing_or_substituted_download_never_executes_even_when_upload_errors_continue(self):
        for remote in (False, True):
            for content in (None, b'corrupt existing cache', b'substituted download'):
                with self.subTest(remote=remote, content=content):
                    code, executed, log = self.exercise(remote=remote, content=content, continue_on_error=True)
                    self.assertNotEqual(0, code, log)
                    self.assertFalse(executed)
                    self.assertRecoveryStepsNamed(log)

    def test_remote_copy_is_reverified(self):
        code, executed, log = self.exercise(remote=True, tamper=True, continue_on_error=True)
        self.assertNotEqual(0, code, log)
        self.assertFalse(executed)
        self.assertIn('FAILED', log)

    def test_upload_disabled_does_not_require_or_execute_tool(self):
        for remote in (False, True):
            code, executed, log = self.exercise(remote=remote, content=None, enabled=False)
            self.assertEqual(0, code, log)
            self.assertFalse(executed)


if __name__ == '__main__':
    unittest.main()
