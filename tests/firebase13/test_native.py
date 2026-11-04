import copy
import importlib.util
import json
from pathlib import Path
import stat
import subprocess
import xml.etree.ElementTree as ET
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/firebase13'))
import native
from archive_safety import preflight
from generate import generate


class NativeTests(unittest.TestCase):
    def setUp(self):
        self.manifest = json.loads(native.MANIFEST.read_text())

    def test_committed_schema_and_generation(self):
        self.assertEqual(len(native.validate_schema(self.manifest)), 23)

    def test_conflicting_declarations_are_rejected(self):
        for fault, message in [('url','source URL'),('hash','digest'),('id','asset ID'),
                               ('owner','owner'),('path','Unsafe asset'),('native-owner','framework ownership'),
                               ('dependency','outside Firebase')]:
            m=copy.deepcopy(self.manifest)
            with self.subTest(fault=fault):
                if fault=='url':m['source']['url']='https://github.com/firebase/firebase-ios-sdk/releases/latest/download/Firebase.zip'
                elif fault=='hash':m['source']['sha256']='bad'
                elif fault=='id':m['packages'][1]['assets'][0]['id']=m['packages'][0]['assets'][0]['id']
                elif fault=='owner':m['packages'][0]['assets'][0]['owner']='Firebase.Auth'
                elif fault=='path':m['packages'][0]['assets'][0]['path']='Firebase/../escape'
                elif fault=='native-owner':m['packages'][1]['assets'][0]['path']=m['packages'][0]['assets'][0]['path']
                elif fault=='dependency':m['packages'][0]['dependencies'].append('AdamE.Google.iOS.SignIn')
                with self.assertRaisesRegex(ValueError,message):native.validate_schema(m)

    def test_hostile_archive_paths_and_links(self):
        for fault in ('traversal','absolute','escaping-link','cycle','write-through','case-collision','file-parent'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as tmp:
                path=Path(tmp)/'input.zip'
                with zipfile.ZipFile(path,'w') as z:
                    def link(name,target):
                        item=zipfile.ZipInfo(name);item.external_attr=(stat.S_IFLNK | 0o777)<<16;z.writestr(item,target)
                    if fault=='traversal':z.writestr('Firebase/../escape','x')
                    elif fault=='absolute':z.writestr('/escape','x')
                    elif fault=='escaping-link':link('Firebase/link','../../escape')
                    elif fault=='cycle':link('Firebase/a','b');link('Firebase/b','a')
                    elif fault=='write-through':link('Firebase/link','other');z.writestr('Firebase/link/payload','x')
                    elif fault=='case-collision':z.writestr('Firebase/a','x');z.writestr('Firebase/A','y')
                    elif fault=='file-parent':z.writestr('Firebase/a','x');z.writestr('Firebase/a/payload','y')
                with self.assertRaises(ValueError):preflight(path)

    def test_safe_internal_symlink_is_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'input.zip'
            with zipfile.ZipFile(path,'w') as z:
                z.writestr('Firebase/F.framework/Versions/A/payload','x')
                item=zipfile.ZipInfo('Firebase/F.framework/Current');item.external_attr=(stat.S_IFLNK|0o777)<<16
                z.writestr(item,'Versions/A')
            preflight(path)

    def test_checksum_mismatch_fails_before_archive_is_opened(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'bad.zip';path.write_bytes(b'not an archive')
            with self.assertRaisesRegex(ValueError,'SHA-256 mismatch'):native.archive_validate(self.manifest,path)

    def test_msbuild_resource_metadata_survives_two_import_paths(self):
        m=self.manifest
        p=next(p for p in m['packages'] if p['id']=='Firebase.Core')
        with tempfile.TemporaryDirectory() as tmp:
            work=Path(tmp);cache=work/'cache';source=cache/m['source']['id']
            for a in p['assets']:
                target=source/a['path'];target.mkdir(parents=True,exist_ok=True)
                if a['kind']=='Bundle':
                    (target/'Info.plist').write_text('metadata')
                    (target/'PrivacyInfo.xcprivacy').write_text('privacy')
            imports=''
            for folder in ('build','buildTransitive'):
                directory=work/folder;directory.mkdir()
                targets=generate(m)
                for name in ('Firebase.Native.Common.targets',p['package']+'.targets'):
                    (directory/name).write_text(targets[name])
                imports+='<Import Project="'+str(directory/(p['package']+'.targets'))+'"/>'
            project=work/'probe.proj'
            project.write_text('<Project><PropertyGroup><XamarinBuildDownloadDir>'+str(cache)+'/</XamarinBuildDownloadDir><TargetPlatformIdentifier>ios</TargetPlatformIdentifier><RuntimeIdentifier>iossimulator-arm64</RuntimeIdentifier><OutputType>Exe</OutputType></PropertyGroup>'+imports+'<Target Name="_XamarinBuildDownload"/><Target Name="Test" DependsOnTargets="_FirebaseNative_AdamE_Firebase_iOS_Core"><WriteLinesToFile File="'+str(work/'resources.txt')+'" Lines="@(BundleResource -&gt; &apos;%(LogicalName)&apos;)" Overwrite="true"/><WriteLinesToFile File="'+str(work/'downloads.txt')+'" Lines="@(XamarinBuildDownload)" Overwrite="true"/></Target></Project>')
            for label, properties, active, variant in (
                ('application', [], True, '-simulator'),
                ('extension', ['-p:OutputType=Library','-p:IsAppExtension=True'], True, '-simulator'),
                ('library', ['-p:OutputType=Library'], False, '-simulator'),
                ('binding', ['-p:IsBindingProject=true'], False, '-simulator'),
                ('design', ['-p:DesignTimeBuild=true'], False, '-simulator'),
                ('outer', ['-p:IsCrossTargetingBuild=true'], False, '-simulator'),
                ('catalyst', ['-p:TargetPlatformIdentifier=maccatalyst','-p:RuntimeIdentifier=maccatalyst-arm64'], True, '-catalyst')):
                with self.subTest(shape=label):
                    for name in ('resources.txt','downloads.txt'):(work/name).unlink(missing_ok=True)
                    result=subprocess.run(['dotnet','msbuild',str(project),'-t:Test','-nologo']+properties,capture_output=True,text=True)
                    self.assertEqual(result.returncode,0,result.stdout+result.stderr)
                    resources=(work/'resources.txt').read_text().splitlines() if (work/'resources.txt').exists() else []
                    expected={a['logical']+'/'+name for a in p['assets'] if a['kind']=='Bundle' and variant in a['id'] for name in ('Info.plist','PrivacyInfo.xcprivacy')} if active else set()
                    self.assertEqual(set(resources),expected)
                    self.assertEqual(len(resources),len(expected))
                    downloads=(work/'downloads.txt').read_text().splitlines() if (work/'downloads.txt').exists() else []
                    self.assertEqual(downloads,[m['source']['id']] if active else [])

    def test_missing_declared_payload_is_rejected(self):
        m=copy.deepcopy(self.manifest)
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'missing.zip'
            with zipfile.ZipFile(path,'w') as z:
                z.writestr('Firebase/NOTICES',(ROOT/'source/Firebase/native/UPSTREAM-NOTICES.txt').read_bytes())
                z.writestr('Firebase/METADATA.md',(ROOT/'tests/fixtures/firebase13/13.1.1/METADATA.md').read_bytes())
            m['source']['sha256']=native.digest(path)
            with self.assertRaisesRegex(ValueError,'Missing payload'):
                native.archive_validate(m,path)

    def test_both_import_paths_use_identical_shared_acquisition(self):
        targets=generate(self.manifest)
        self.assertEqual(sum('XamarinBuildDownload Include=' in t for t in targets.values()),1)
        for p in self.manifest['packages']:
            text=targets[p['package']+'.targets']
            self.assertIn('DependsOnTargets="_XamarinBuildDownload"',text)
            self.assertIn('maccatalyst',text)
            self.assertIn('DesignTimeBuild',text)
            self.assertIn('IsAppExtension',text)
        gam=targets['AdamE.Google.iOS.GoogleAppMeasurement.targets']
        self.assertIn('FirebaseWithoutAdIdSupport',gam)


if __name__=='__main__':unittest.main()
