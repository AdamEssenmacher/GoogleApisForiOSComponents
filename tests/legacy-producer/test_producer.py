import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

ROOT=Path(__file__).resolve().parents[2]
def load(name,path):
    spec=importlib.util.spec_from_file_location(name,ROOT/path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
producer=load('legacy_producer_tests','tools/legacy/producer.py')
release=load('legacy_release_tests','scripts/firebase-release/release.py')


class LegacyProducerTests(unittest.TestCase):
    def setUp(self):
        self.data=producer.lock()
        self.manifest=json.loads((ROOT/'source/Firebase/native-assets.json').read_text())

    def test_signin_uses_unchanged_five_package_baseline(self):
        plan=producer.plan(self.data,self.manifest,'Google.SignIn')
        self.assertEqual(plan['firebase13'],[])
        self.assertEqual(plan['legacy'],['Google.SignIn'])
        records={p['key']:p for p in plan['legacyPackages']}
        self.assertEqual(len(records),5)
        self.assertEqual(records['Google.SignIn']['version'],'10.1.0.0')
        self.assertTrue(records['Google.AppCheckCore']['version'].startswith('11.'))
        self.assertTrue(records['Google.GTMSessionFetcher']['version'].startswith('3.'))

    def test_deferred_products_keep_their_historical_closures(self):
        for key in ('Google.Cast','Google.TagManager','Google.UserMessagingPlatform','MLKit.Core','MLKit.BarcodeScanning','Google.Maps','Google.Places'):
            with self.subTest(key=key):
                plan=producer.plan(self.data,self.manifest,key)
                self.assertFalse(plan['firebase13'])
                self.assertIn(key,[p['key'] for p in plan['legacyPackages']])
                for p in plan['legacyPackages']:
                    if p['id'].startswith('AdamE.Firebase.'):
                        self.assertTrue(p['version'].startswith('12.'))

    def test_default_and_mixed_selections_partition_without_mutating_the_manifest(self):
        default=producer.plan(self.data,self.manifest,'')
        self.assertEqual(len(default['firebase13']),23)
        self.assertEqual(set(default['legacy'])|set(default['firebase13']),{p['key'] for p in self.data['artifacts']})
        self.assertFalse(set(default['legacy'])&set(default['firebase13']))
        mixed=producer.plan(self.data,self.manifest,'Google.SignIn,Firebase.Auth')
        self.assertEqual(mixed['legacy'],['Google.SignIn']);self.assertEqual(mixed['firebase13'],['Firebase.Auth'])
        modern=producer.plan(self.data,self.manifest,'Google.GoogleAppMeasurement')
        self.assertEqual(modern['firebase13'],['Google.GoogleAppMeasurement'])
        self.assertEqual(self.manifest,json.loads((ROOT/'source/Firebase/native-assets.json').read_text()))

    def test_explicit_legacy_fixture_selection_remains_firebase12(self):
        plan=producer.plan(self.data,self.manifest,'Firebase.Auth,Firebase.Analytics,Firebase.CloudFirestore','legacy')
        self.assertFalse(plan['firebase13'])
        self.assertTrue(all(not p['id'].startswith('AdamE.Firebase.') or p['version'].startswith('12.') for p in plan['legacyPackages']))
        with self.assertRaisesRegex(ValueError,'Unknown component'):producer.plan(self.data,self.manifest,'MLKit.NotRegistered')
        with self.assertRaisesRegex(ValueError,'cycle'):
            data=copy.deepcopy(self.data);data['artifacts'][0]['dependencies']=[data['artifacts'][0]['key']]
            producer.closure(data,[data['artifacts'][0]['key']])

    def test_no_publication_command_exists(self):
        result=subprocess.run(['python3',str(ROOT/'tools/legacy/producer.py'),'publish'],capture_output=True,text=True)
        self.assertNotEqual(0,result.returncode);self.assertIn('invalid choice',result.stderr)

    def test_materialization_is_pinned_and_never_copies_untracked_secrets(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);self.git_fixture(root)
            (root/'tracked.txt').write_text('current checkout')
            (root/'secret').write_text('fixture must never be archived')
            with producer.source_tree(root) as stage:
                self.assertEqual((stage/'tracked.txt').read_text(),'baseline')
                self.assertFalse((stage/'secret').exists())
                location=stage
            self.assertFalse(location.exists())
            self.assertEqual((root/'tracked.txt').read_text(),'current checkout')

    def git_fixture(self,root):
        subprocess.run(['git','init','--quiet',str(root)],check=True)
        (root/'tracked.txt').write_text('baseline')
        (root/'.gitignore').write_text('secret\noutput\n')
        path=root/'source/Google/SignIn/SignIn.csproj';path.parent.mkdir(parents=True)
        path.write_text('<Project><ItemGroup><None Include="../../../README.md" Pack="true" /></ItemGroup></Project>')
        (root/'README.md').write_text('baseline notice')
        for name in ('Directory.Build.props','global.json','.config/dotnet-tools.json','source/AssemblyInfo.cs','common.cake','poco.cake','custom_externals_download.cake'):
            target=root/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_text('fixture input')
        subprocess.run(['git','add','.'],cwd=root,check=True)
        subprocess.run(['git','-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','--quiet','-m','fixture'],cwd=root,check=True)
        commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
        tree=subprocess.check_output(['git','rev-parse','HEAD^{tree}'],cwd=root,text=True).strip()
        data={'schema':1,'commit':commit,'tree':tree,'artifacts':[dict(key='Google.SignIn',artifactId='Google.SignIn',id='AdamE.Google.iOS.SignIn',version='10.1.0.0',project=str(path.relative_to(root)),dependencies=[])]}
        (root/'eng').mkdir();(root/producer.LOCK).write_text(json.dumps(data));return data

    def test_future_product_edits_fail_instead_of_silently_using_stale_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);data=self.git_fixture(root)
            with producer.source_tree(root) as stage:
                (root/data['artifacts'][0]['project']).write_text('changed binding')
                with self.assertRaisesRegex(ValueError,'Non-Firebase product changed'):
                    producer.verify_nonfirebase_inputs(root,stage,data,{'packages':[]})
            data['tree']='0'*40;(root/producer.LOCK).write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError,'tree differs'):
                with producer.source_tree(root):pass

    def test_packed_readme_edits_are_not_silently_ignored(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);data=self.git_fixture(root)
            with producer.source_tree(root) as stage:
                (root/'README.md').write_text('changed package documentation')
                with self.assertRaisesRegex(ValueError,'Legacy producer input changed.*README.md'):
                    producer.verify_nonfirebase_inputs(root,stage,data,{'packages':[]})

    def test_wrong_missing_or_extra_package_outputs_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);stage=root/'stage';(stage/'output').mkdir(parents=True)
            item=next(p for p in self.data['artifacts'] if p['key']=='Google.SignIn')
            with self.assertRaisesRegex(ValueError,'Missing legacy output'):producer.copy_outputs(root,stage,[item],True)
            file=stage/'output/fixture.nupkg'
            def package(id,version):
                with zipfile.ZipFile(file,'w') as z:z.writestr('fixture.nuspec',f'<package><metadata><id>{id}</id><version>{version}</version></metadata></package>')
            package(item['id'],'10.1.0.1')
            with self.assertRaisesRegex(ValueError,'version changed'):producer.copy_outputs(root,stage,[item],True)
            package('AdamE.Unrelated','10.1.0')
            with self.assertRaisesRegex(ValueError,'Unexpected legacy output'):producer.copy_outputs(root,stage,[item],True)
            package(item['id'],'10.1.0');producer.copy_outputs(root,stage,[item],True)
            self.assertEqual((root/'output/fixture.nupkg').read_bytes(),file.read_bytes())

    def test_effective_inputs_match_the_published_1010_fingerprints(self):
        fixture=json.loads((ROOT/'tests/fixtures/legacy-producer/signin-10.1.0.json').read_text())
        release.configure_channel('signin')
        try:
            actual=release.snapshot(fixture['metadata'])
            self.assertEqual([{k:p[k] for k in ('id','version','fingerprint')} for p in actual],fixture['expected'])
        finally:release.configure_channel('firebase')


if __name__=='__main__':unittest.main()
