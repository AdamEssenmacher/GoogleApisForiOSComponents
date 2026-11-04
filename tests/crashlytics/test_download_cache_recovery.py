"""Exercise stock XBD ZIP caching with a small local archive, not a raw script download.

The fixture imports the shipped common declaration, then replaces its URL/digest
only inside the test project. Production declarations stay pinned to Google.
The full official archive and consumer matrix run in firebase13-xbd.yml.
"""
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
COMMON = ROOT / 'source/Firebase/native/Firebase.Native.Common.targets'
TOOL = b'#!/bin/sh\nexit 0\n'


@unittest.skipIf(os.environ.get('CRASHLYTICS_SKIP_NETWORK_TESTS') == '1', 'XBD package restore/local HTTP tests disabled')
class DownloadCacheRecoveryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._work = tempfile.TemporaryDirectory(prefix='crashlytics-archive-')
        cls.work = Path(cls._work.name).resolve()
        cls.source = json.loads((ROOT/'source/Firebase/native-assets.json').read_text())['source']
        payload = io.BytesIO()
        with zipfile.ZipFile(payload, 'w') as archive:
            archive.writestr('Firebase/FirebaseCrashlytics/upload-symbols', TOOL)
        cls.payload = payload.getvalue()
        cls.requests = 0
        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                cls.requests += 1
                self.send_response(200); self.end_headers(); self.wfile.write(cls.payload)
            def log_message(self, *_): pass
        cls.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True); cls.thread.start()
        cls.project = cls.work/'fixture.csproj'
        cls.project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
<ItemGroup><PackageReference Include="Xamarin.Build.Download" Version="[0.11.6]" /></ItemGroup>
<Import Project="{COMMON}" />
<ItemGroup><XamarinBuildDownload Update="{cls.source['id']}">
<Url>http://127.0.0.1:{cls.server.server_port}/Firebase.zip</Url>
<Sha256 Condition="'$(FixtureDigest)'==''">{hashlib.sha256(cls.payload).hexdigest()}</Sha256>
<Sha256 Condition="'$(FixtureDigest)'!=''">$(FixtureDigest)</Sha256>
</XamarinBuildDownload></ItemGroup>
<Target Name="Acquire" DependsOnTargets="_XamarinBuildDownload" /></Project>''')
        cls.env = dict(os.environ, MSBuildEnableWorkloadResolver='false', NO_PROXY='127.0.0.1', no_proxy='127.0.0.1')
        result = subprocess.run(['dotnet','restore',str(cls.project)],cwd=cls.work,env=cls.env,capture_output=True,text=True)
        if result.returncode: raise AssertionError(result.stdout+result.stderr)

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown(); cls.server.server_close(); cls.thread.join(); cls._work.cleanup()

    def setUp(self):
        self.cache = self.work/self.id().split('.')[-1]; self.cache.mkdir()
        self.directory = self.cache/self.source['id']
        self.tool = self.directory/'Firebase/FirebaseCrashlytics/upload-symbols'

    def acquire(self, digest=None):
        args=['dotnet','msbuild',str(self.project),'-t:Acquire','-nologo',
              '-p:OutputType=Exe','-p:TargetPlatformIdentifier=ios',
              '-p:XamarinBuildDownloadAllowUnsecure=true',  # Loopback fixture only; shipped URLs stay HTTPS.
              '-p:XamarinBuildDownloadDir='+str(self.cache)+'/']
        if digest: args.append('-p:FixtureDigest='+digest)
        result=subprocess.run(args,cwd=self.work,env=self.env,capture_output=True,text=True)
        return result.returncode,result.stdout+result.stderr

    def test_zip_download_and_warm_cache_reuse(self):
        code,log=self.acquire(); self.assertEqual(0,code,log); self.assertEqual(TOOL,self.tool.read_bytes())
        before=self.requests
        code,log=self.acquire(); self.assertEqual(0,code,log); self.assertEqual(before,self.requests)

    def test_documented_archive_cache_cleanup_recovers_corrupted_tool(self):
        code,log=self.acquire(); self.assertEqual(0,code,log)
        self.tool.write_bytes(b'corrupt')
        # Completed XBD caches intentionally do not rehash extracted files.
        code,log=self.acquire(); self.assertEqual(0,code,log); self.assertEqual(b'corrupt',self.tool.read_bytes())
        shutil.rmtree(self.directory)
        for suffix in ('.unpacked','.zip'): (self.cache/(self.source['id']+suffix)).unlink(missing_ok=True)
        code,log=self.acquire(); self.assertEqual(0,code,log); self.assertEqual(TOOL,self.tool.read_bytes())

    def test_stock_xbd_cold_download_does_not_enforce_digest_metadata(self):
        before=self.requests
        code,log=self.acquire('0'*64); self.assertEqual(0,code,log); self.assertEqual(TOOL,self.tool.read_bytes())
        self.assertGreater(self.requests,before,log)
        # 0.11.6 only checks an existing ZIP before download; it does not recheck
        # freshly downloaded bytes. Producer validation, not XBD, enforces our pin.


if __name__ == '__main__': unittest.main()
