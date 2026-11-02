import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('check_xbd_package', ROOT / 'scripts/check-xbd-package.py')
xbd = importlib.util.module_from_spec(spec)
spec.loader.exec_module(xbd)

PLACES = xbd.TARGETS['Places']
TARGETS_XML = b'<Project><ItemGroup><XamarinBuildDownload Include="GPlcs-7.4.0" /></ItemGroup></Project>\n'
CSPROJ = ('<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>'
          '<PackageReference Include="Xamarin.Build.Download" Version="0.11.4" PrivateAssets="None" />'
          '</ItemGroup></Project>')


def nuspec(version='7.4.0.4', dependency='<dependency id="Xamarin.Build.Download" version="0.11.4" include="All" />'):
    return (f'<?xml version="1.0"?><package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
            f'<metadata><id>AdamE.Google.iOS.Places</id><version>{version}</version><dependencies>'
            f'<group targetFramework="net10.0-ios26.0">{dependency}</group>'
            f'</dependencies></metadata></package>').encode()


class XbdPackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.write('components.cake',
                   'Artifact GOOGLE_PLACES_ARTIFACT = new Artifact ("Google.Places", "7.4.0.4", "15.0");\n'
                   'GOOGLE_PLACES_ARTIFACT.PodSpecs = new [] { PodSpec.Create ("GooglePlaces", "7.4.0") };\n')
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
            'lib/net10.0-ios26.0/Google.Places.dll': b'MZ' + bytes(150_000),
        }

    def tearDown(self):
        self.temp.cleanup()

    def write(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content if isinstance(content, bytes) else content.encode())

    def package_errors(self, **changes):
        entries = {**self.entries, **changes}
        package = self.root / 'output' / 'AdamE.Google.iOS.Places.7.4.0.4.nupkg'
        package.parent.mkdir(exist_ok=True)
        with zipfile.ZipFile(package, 'w', zipfile.ZIP_DEFLATED) as archive:
            for name, content in entries.items():
                if content is not None:
                    archive.writestr(name, content)
        return xbd.check_package(self.root, 'Places', PLACES, package)

    def assertRejected(self, errors, fragment):
        self.assertTrue(any(fragment in e for e in errors), f'expected an error containing {fragment!r}, got {errors}')

    def test_binding_package_with_download_hook_passes(self):
        self.assertEqual([], xbd.check_source(self.root, 'Places', PLACES))
        self.assertEqual([], self.package_errors())

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
        oversized = b'MZ' + bytes(xbd.MAX_ENTRY_BYTES)
        self.assertRejected(self.package_errors(**{'lib/net10.0-ios26.0/Google.Places.dll': oversized}), 'byte cap')

    def test_missing_download_dependency_is_rejected(self):
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(dependency='')}),
                            'no Xamarin.Build.Download dependency')

    def test_download_dependency_without_build_assets_is_rejected(self):
        # What NuGet writes when the PackageReference loses PrivateAssets="None".
        stripped = '<dependency id="Xamarin.Build.Download" version="0.11.4" exclude="Build,Analyzers" />'
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(dependency=stripped)}),
                            'include="All"')

    def test_version_drift_from_components_is_rejected(self):
        self.assertRejected(self.package_errors(**{'AdamE.Google.iOS.Places.nuspec': nuspec(version='7.4.0.3')}),
                            'components.cake declares')

    def test_packaged_targets_must_match_source(self):
        self.assertRejected(self.package_errors(**{'buildTransitive/AdamE.Google.iOS.Places.targets': b'<Project />'}),
                            'differs from source')
        self.assertRejected(self.package_errors(**{'build/AdamE.Google.iOS.Places.targets': None}), 'missing build/')

    def test_native_reference_in_binding_project_is_rejected(self):
        self.write('source/Google/Places/Places.csproj',
                   CSPROJ.replace('</ItemGroup>', '<NativeReference Include="GooglePlaces.xcframework" /></ItemGroup>'))
        self.assertRejected(xbd.check_source(self.root, 'Places', PLACES), 'NativeReference')

    def test_download_reference_must_reach_consumers(self):
        self.write('source/Google/Places/Places.csproj', CSPROJ.replace(' PrivateAssets="None"', ''))
        self.assertRejected(xbd.check_source(self.root, 'Places', PLACES), 'PrivateAssets="None"')

    def test_build_time_sdk_download_is_rejected(self):
        self.write('components.cake', (self.root / 'components.cake').read_text().replace(
            '"7.4.0")', '"7.4.0", frameworkSource: FrameworkSource.Custom)'))
        self.write('custom_externals_download.cake', (self.root / 'custom_externals_download.cake').read_text().replace(
            '};', '\t{ "GooglePlaces", new ExternalDownloadSource ("GooglePlaces", "7.4.0", "key") },\n};'))
        errors = xbd.check_source(self.root, 'Places', PLACES)
        self.assertRejected(errors, 'FrameworkSource.Custom')
        self.assertRejected(errors, 'downloads GooglePlaces')

    def test_package_selection_ignores_symbols_and_lookalikes(self):
        output = self.root / 'output'
        output.mkdir()
        for name in ('AdamE.Google.iOS.Places.7.4.0.4.nupkg', 'AdamE.Google.iOS.Places.7.4.0.4.symbols.nupkg',
                     'AdamE.Google.iOS.PlacesExtra.1.0.0.nupkg', 'AdamE.Google.iOS.Places.7.4.0.5.nupkg'):
            (output / name).touch()
        self.assertEqual(['AdamE.Google.iOS.Places.7.4.0.4.nupkg', 'AdamE.Google.iOS.Places.7.4.0.5.nupkg'],
                         [p.name for p in xbd.find_packages(output, PLACES)])

    def test_repository_packages_are_delivered_through_xbd(self):
        # Runs on the Linux planner job, so a regression fails before any native build.
        for name, target in xbd.TARGETS.items():
            with self.subTest(target=name):
                self.assertEqual([], xbd.check_source(ROOT, name, target))


if __name__ == '__main__':
    unittest.main()
