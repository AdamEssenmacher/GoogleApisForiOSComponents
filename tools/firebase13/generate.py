#!/usr/bin/env python3
"""Generate deterministic MSBuild delivery targets from the Firebase ownership manifest."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'source/Firebase/native-assets.json'
ACTIVE = "'$(IsBindingProject)' != 'true' And '$(DesignTimeBuild)' != 'true' And '$(IsCrossTargetingBuild)' != 'true' And ('$(TargetPlatformIdentifier)' == 'ios' Or '$(TargetPlatformIdentifier)' == 'maccatalyst') And ('$(OutputType)' != 'Library' Or '$(IsAppExtension)' == 'True')"


def xml(root):
    ET.indent(root, space='  ')
    return ET.tostring(root, encoding='unicode') + '\n'


def generate(manifest):
    source = manifest['source']
    common = ET.Element('Project')
    props = ET.SubElement(common, 'PropertyGroup')
    ET.SubElement(props, '_FirebaseNativeSourceId', Condition="'$(_FirebaseNativeSourceId)' == ''").text = source['id']
    ET.SubElement(props, '_FirebaseNativeRoot').text = '$(XamarinBuildDownloadDir)' + source['id'] + '/'
    ET.SubElement(props, '_FirebaseNativeCleanup').text = ('Remove ' + source['id'] + ', ' + source['id'] + '.unpacked, and any ' + source['id'] + '.zip from $(XamarinBuildDownloadDir), then rebuild online.')
    group = ET.SubElement(common, 'ItemGroup', Condition=ACTIVE)
    item = ET.SubElement(group, 'XamarinBuildDownload', Include=source['id'], Exclude='@(XamarinBuildDownload)')
    for key, value in [('Url', source['url']), ('Kind', source['kind']), ('Sha256', source['sha256']), ('ExclusiveLockTimeout', str(source['lockTimeout']))]:
        ET.SubElement(item, key).text = value
    check = ET.SubElement(common, 'Target', Name='_FirebaseNativeSourceCheck_' + source['id'].replace('.', '_').replace('-', '_'),
                          BeforeTargets='_XamarinBuildDownload;_BeforeCoreCompileInterfaceDefinitions', Condition=ACTIVE)
    ET.SubElement(check, 'Error', Condition="'$(_FirebaseNativeSourceId)' != '" + source['id'] + "'",
                  Text='Conflicting Firebase binary archive versions. Align Firebase and shared dependency package versions.')
    outputs = {'Firebase.Native.Common.targets': xml(common)}
    for package in manifest['packages']:
        key = package['package'].replace('.', '_')
        root = ET.Element('Project')
        ET.SubElement(root, 'Import', Project='$(MSBuildThisFileDirectory)Firebase.Native.Common.targets')
        target = ET.SubElement(root, 'Target', Name='_FirebaseNative_' + key,
                               BeforeTargets='_BeforeCoreCompileInterfaceDefinitions',
                               DependsOnTargets='_XamarinBuildDownload', Condition=ACTIVE)
        for asset in package['assets']:
            ET.SubElement(target, 'Error', Condition=(asset.get('condition') + ' And ' if asset.get('condition') else '') + "!Exists('$(_FirebaseNativeRoot)" + asset['path'] + "')",
                          Text='Missing Firebase native asset ' + asset['id'] + '. $(_FirebaseNativeCleanup)')
        group = ET.SubElement(target, 'ItemGroup')
        for asset in package['assets']:
            kwargs = {'Include': '$(_FirebaseNativeRoot)' + asset['path']}
            if asset.get('condition'):
                kwargs['Condition'] = asset['condition']
            if asset['kind'] == 'XCFramework':
                item = ET.SubElement(group, 'NativeReference', **kwargs)
                for name, value in asset['metadata'].items():
                    ET.SubElement(item, name).text = value
            elif asset['kind'] == 'Bundle':
                kwargs['Include'] += '/**/*'
                item_name = '_FirebaseBundle_' + ''.join(c if c.isalnum() else '_' for c in asset['id'])
                ET.SubElement(group, item_name, **kwargs)
                resources = ET.SubElement(target, 'ItemGroup')
                item = ET.SubElement(resources, 'BundleResource', Include='@(' + item_name + ')')
                ET.SubElement(item, 'LogicalName').text = asset['logical'] + '/%(' + item_name + '.RecursiveDir)%(' + item_name + '.Filename)%(' + item_name + '.Extension)'
        if package['id'] == 'Firebase.Crashlytics':
            ET.SubElement(root, 'Import', Project='$(MSBuildThisFileDirectory)Firebase.Crashlytics.Upload.targets')
        outputs[package['package'] + '.targets'] = xml(root)
    return outputs


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    for name, content in generate(json.loads(MANIFEST.read_text())).items():
        path = ROOT / 'source/Firebase/native' / name
        if args.check:
            if not path.exists() or path.read_text() != content:
                raise SystemExit('Stale generated target: ' + str(path))
        else:
            path.write_text(content)
