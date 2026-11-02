import importlib.util
from pathlib import Path
import tempfile
import unittest
import warnings
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('check_xbd_package', ROOT / 'scripts/check-xbd-package.py')
xbd = importlib.util.module_from_spec(spec)
spec.loader.exec_module(xbd)

PLACES = xbd.TARGETS['Places']
URL = 'https://dl.google.com/dl/geosdk/key/GooglePlaces-7.4.0.tar.gz'
TARGETS_XML = (f'<Project><ItemGroup><XamarinBuildDownload Include="GPlcs-7.4.0"><Url>{URL}</Url><Kind>Tgz</Kind>'
               '</XamarinBuildDownload></ItemGroup></Project>\n').encode()
CSPROJ = ('<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>'
          '<Compile Include="ApiDefinition.cs" />'
          '<PackageReference Include="Xamarin.Build.Download" Version="0.11.4" PrivateAssets="None" />'
          '<None Include="Places.targets" Pack="True" PackagePath="build/AdamE.Google.iOS.Places.targets" />'
          '</ItemGroup></Project>')
COMPONENTS = ('Artifact GOOGLE_PLACES_ARTIFACT = new Artifact ("Google.Places", "7.4.0.4", "15.0");\n'
              'GOOGLE_PLACES_ARTIFACT.PodSpecs = new [] { PodSpec.Create ("GooglePlaces", "7.4.0") };\n')
DLL = 'lib/net10.0-ios26.0/Google.Places.dll'


def nuspec(version='7.4.0.4', dependency='<dependency id="Xamarin.Build.Download" version="0.11.4" include="All" />',
           package_id='AdamE.Google.iOS.Places', dependencies=True):
    body = f'<dependencies><group targetFramework="net10.0-ios26.0">{dependency}</group></dependencies>' if dependencies else ''
    return (f'<?xml version="1.0"?><package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
            f'<metadata><id>{package_id}</id><version>{version}</version>{body}</metadata></package>').encode()


class XbdPackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name).resolve()
        self.write('components.cake', COMPONENTS)
        self.write('custom_externals_download.cake',
                   'var ExternalDownloads = new Dictionary<string, ExternalDownloadSource> {\n'
                   '\t{ "FirebaseAnalytics", new ExternalDownloadSource ("FirebaseAnalytics", "12.16.0", "key") },\n};\n')
        self.write('source/Google/Places/Places.csproj', CSPROJ)
        self.write('source/Google/Places/Places.targets', TARGETS_XML)
        self.entries = {
            '[Content_Types].xml': b'<Types />', '_rels/.rels': b'<Relationships />',
            'package/services/metadata/core-properties/abc.psmdcp': b'<coreProperties />',
            'AdamE.Google.iOS.Places.nuspec': nuspec(), 'License.md': b'MIT',
            'googleiosplaces_128x128.png': b'png',
            'build/AdamE.Google.iOS.Places.targets': TARGETS_XML,
            'buildTransitive/AdamE.Google.iOS.Places.targets': TARGETS_XML,
            DLL: b'MZ' + bytes(150_000),
        }

    def tearDown(self):
        self.temp.cleanup()

    def write(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content if isinstance(content, bytes) else content.encode())

    def source_errors(self):
        return xbd.check_source(self.root, 'Places', PLACES)

    def package_errors(self, extra=(), **changes):
        entries = {**self.entries, **changes}
        package = self.root / 'output' / 'AdamE.Google.iOS.Places.7.4.0.4.nupkg'
        package.parent.mkdir(exist_ok=True)
        with zipfile.ZipFile(package, 'w', zipfile.ZIP_DEFLATED) as archive, warnings.catch_warnings():
            warnings.simplefilter('ignore')  # duplicate names are deliberate in one test
            for name, content in [*entries.items(), *extra]:
                if content is not None:
                    archive.writestr(name, content)
        return xbd.check_package(self.root, 'Places', PLACES, package)

    def assertRejected(self, errors, fragment):
        self.assertTrue(any(fragment in e for e in errors), f'expected an error containing {fragment!r}, got {errors}')

    def test_binding_package_with_download_hook_passes(self):
        self.assertEqual([], self.source_errors())
        self.assertEqual([], self.package_errors())

    # What ships in the package

    def test_native_library_anywhere_in_package_is_rejected(self):
        self.assertRejected(self.package_errors(**{'runtimes/ios-arm64/native/libGooglePlaces.a': b'!<arch>'}),
                            'runtimes/ios-arm64/native/libGooglePlaces.a')

    def test_zipped_xcframework_is_rejected(self):
        self.assertRejected(self.package_errors(**{'build/GooglePlaces.xcframework.zip': b'PK'}),
                            'build/GooglePlaces.xcframework.zip')

    def test_binding_resources_payload_is_rejected(self):
        self.assertRejected(self.package_errors(**{'lib/net10.0-ios26.0/Google.Places.resources.zip': b'PK'}),
                            'Google.Places.resources.zip')

    def test_sdk_embedded_in_binding_assembly_is_rejected(self):
        # With NoBindingEmbedding=false a NativeReference is embedded in the assembly itself.
        self.assertRejected(self.package_errors(**{DLL: b'MZ' + bytes(xbd.MAX_PACKAGE_BYTES)}), 'byte cap')

    def test_sdk_split_across_allowed_entries_is_rejected(self):
        chunk = bytes(xbd.MAX_PACKAGE_BYTES // 3)
        split = {f'lib/net{n}.0-ios26.0/Google.Places.xml': chunk for n in (10, 11, 12, 13)}
        self.assertRejected(self.package_errors(**split), 'byte cap')

    def test_unrecognised_framework_folder_is_rejected(self):
        self.assertRejected(self.package_errors(**{'lib/payload/Google.Places.dll': b'MZ'}), 'lib/payload/Google.Places.dll')
        self.assertRejected(self.package_errors(**{'lib/net10.0-ios26.0\\x/Google.Places.xml': b'x'}), 'unexpected entries')

    def test_extra_core_properties_entry_is_rejected(self):
        self.assertRejected(self.package_errors(**{'package/services/metadata/core-properties/def.psmdcp': b'x'}),
                            'exactly one core-properties')

    def test_directory_entry_carrying_data_is_rejected(self):
        self.assertRejected(self.package_errors(**{'GooglePlaces.xcframework/': b'payload'}), 'GooglePlaces.xcframework/')

    def test_duplicate_entries_are_rejected(self):
        # A reader that takes the first nuspec would see no download dependency.
        shadow = ('AdamE.Google.iOS.Places.nuspec', nuspec())
        changes = {'AdamE.Google.iOS.Places.nuspec': nuspec(dependency='')}
        self.assertRejected(self.package_errors(extra=[shadow], **changes), 'duplicate entries')

    def test_missing_binding_assembly_is_rejected(self):
        self.assertRejected(self.package_errors(**{DLL: None}), 'missing lib/<tfm>/Google.Places.dll')

    def test_missing_download_dependency_is_rejected(self):
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(dependency='')}),
                            'no Xamarin.Build.Download dependency')
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(dependencies=False)}),
                            'declares no Xamarin.Build.Download dependency')

    def test_download_dependency_without_build_assets_is_rejected(self):
        # What NuGet writes when the PackageReference loses PrivateAssets="None".
        stripped = '<dependency id="Xamarin.Build.Download" version="0.11.4" exclude="Build,Analyzers" />'
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(dependency=stripped)}),
                            'include="All"')

    def test_nuspec_identity_must_match(self):
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(package_id='Other.Package')}),
                            'nuspec id')
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(version='7.4.0.3')}),
                            'components.cake declares')

    def test_version_comparison_uses_nuget_normalization(self):
        self.write('components.cake', COMPONENTS.replace('"7.4.0.4"', '"7.4.0.0"'))
        self.assertEqual([], self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(version='7.4.0')}))

    def test_packaged_targets_must_match_source(self):
        self.assertRejected(self.package_errors(**{'buildTransitive/AdamE.Google.iOS.Places.targets': b'<Project />'}),
                            'differs from source')
        self.assertRejected(self.package_errors(**{'build/AdamE.Google.iOS.Places.targets': None}), 'missing build/')

    # What the source would pack or download

    def test_native_reference_in_binding_project_is_rejected(self):
        self.write('source/Google/Places/Places.csproj',
                   CSPROJ.replace('</ItemGroup>', '<NativeReference Include="GooglePlaces.xcframework" /></ItemGroup>'))
        self.assertRejected(self.source_errors(), 'NativeReference')

    def test_native_reference_in_imported_msbuild_file_is_rejected(self):
        native = '<Project><ItemGroup><NativeReference Include="GooglePlaces.xcframework" /></ItemGroup></Project>'
        self.write('source/Google/Places/Directory.Build.targets', native)
        self.assertRejected(self.source_errors(), 'source/Google/Places/Directory.Build.targets declares a NativeReference')
        (self.root / 'source/Google/Places/Directory.Build.targets').unlink()

        self.write('source/Google/Places/Extra.targets', native)
        self.write('source/Google/Places/Places.csproj', CSPROJ.replace('</Project>', '<Import Project="Extra.targets" /></Project>'))
        self.assertRejected(self.source_errors(), 'source/Google/Places/Extra.targets declares a NativeReference')

    def test_sdk_resources_in_binding_project_are_rejected(self):
        # Small enough to slip under the size cap inside the binding assembly.
        for item in ('<EmbeddedResource Include="../../../externals/GooglePlaces.bundle/**" />',
                     '<BundleResource Include="GooglePlaces.bundle/Info.plist" />',
                     '<None Include="GooglePlaces.xcframework/Info.plist" Pack="True" PackagePath="build" />'):
            with self.subTest(item=item):
                self.write('source/Google/Places/Places.csproj', CSPROJ.replace('</ItemGroup>', item + '</ItemGroup>'))
                self.assertRejected(self.source_errors(), 'native SDK content must reach consumers')

    def test_download_must_come_from_google(self):
        for url in ('https://github.com/example/mirror/releases/download/v1/GooglePlaces-7.4.0.tar.gz',
                    'http://dl.google.com/dl/geosdk/key/GooglePlaces-7.4.0.tar.gz',
                    'https://dl.google.com.example.net/GooglePlaces-7.4.0.tar.gz',
                    '$(PlacesMirror)/GooglePlaces-7.4.0.tar.gz'):
            with self.subTest(url=url):
                self.write('source/Google/Places/Places.targets', TARGETS_XML.replace(URL.encode(), url.encode()))
                self.assertRejected(self.source_errors(), 'must fetch the SDK from https://dl.google.com')

    def test_targets_without_download_item_is_rejected(self):
        self.write('source/Google/Places/Places.targets', b'<Project><ItemGroup /></Project>')
        self.assertRejected(self.source_errors(), 'no XamarinBuildDownload item')

    def test_download_reference_must_reach_consumers(self):
        self.write('source/Google/Places/Places.csproj', CSPROJ.replace(' PrivateAssets="None"', ''))
        self.assertRejected(self.source_errors(), 'PrivateAssets="None"')

    def test_download_reference_metadata_forms_are_accepted(self):
        child = CSPROJ.replace(' PrivateAssets="None" />', '><PrivateAssets>none</PrivateAssets></PackageReference>')
        self.write('source/Google/Places/Places.csproj', child)
        self.assertEqual([], self.source_errors())

    def test_build_time_sdk_download_is_rejected(self):
        self.write('components.cake', COMPONENTS.replace('"7.4.0")', 'PlacesVersion (), frameworkSource: FrameworkSource.Custom)'))
        self.write('custom_externals_download.cake', (self.root / 'custom_externals_download.cake').read_text().replace(
            '};', '\t{ "Places", new ExternalDownloadSource ("GooglePlaces", "7.4.0", "key") },\n};'))
        errors = self.source_errors()
        self.assertRejected(errors, 'FrameworkSource.Custom')
        self.assertRejected(errors, 'custom_externals_download.cake names GooglePlaces')

    def test_package_selection_ignores_symbols_and_lookalikes(self):
        output = self.root / 'output'
        output.mkdir()
        for name in ('AdamE.Google.iOS.Places.7.4.0.4.nupkg', 'AdamE.Google.iOS.Places.7.4.0.4.symbols.nupkg',
                     'AdamE.Google.iOS.PlacesExtra.1.0.0.nupkg', 'adame.google.ios.places.7.4.0.5.nupkg'):
            (output / name).touch()
        self.assertEqual(['AdamE.Google.iOS.Places.7.4.0.4.nupkg', 'adame.google.ios.places.7.4.0.5.nupkg'],
                         [p.name for p in xbd.find_packages(output, PLACES)])

    def test_repository_packages_are_delivered_through_xbd(self):
        # Runs on the Linux planner job, so a regression fails before any native build.
        for name, target in xbd.TARGETS.items():
            with self.subTest(target=name):
                self.assertEqual([], xbd.check_source(ROOT, name, target))


if __name__ == '__main__':
    unittest.main()
