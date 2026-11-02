#!/usr/bin/env python3
"""Check that a Google package delivered through Xamarin.Build.Download carries no native SDK.

The Maps and Places SDKs are proprietary, so their packages ship only the managed binding and an
MSBuild hook that downloads the SDK from Google during the consumer build. This fails when the
package, or the source it is packed from, would put Google's binaries into our nupkg, or would stop
handing consumers the Xamarin.Build.Download hook.

Without --package-dir only the source is checked, which needs no build.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
XBD = 'Xamarin.Build.Download'

# The binding assemblies are about 200 KB and every other entry is under 60 KB. The smallest SDK
# involved (Places) is several MB even compressed, so an SDK embedded in the binding assembly, or
# shipped as any single entry, cannot fit under this cap.
MAX_ENTRY_BYTES = 2 * 1024 * 1024

TARGETS = {
    'Places': {'artifact': 'Google.Places', 'package_id': 'AdamE.Google.iOS.Places',
               'assembly': 'Google.Places', 'pod': 'GooglePlaces', 'icon': 'googleiosplaces_128x128.png'},
    'Maps': {'artifact': 'Google.Maps', 'package_id': 'AdamE.Google.iOS.Maps',
             'assembly': 'Google.Maps', 'pod': 'GoogleMaps', 'icon': 'googleiosmaps_128x128.png'},
}


def local(tag):
    return tag.rsplit('}', 1)[-1]


def normalize(version):
    """Apply NuGet's normalization: a zero fourth component is dropped."""
    parts = (version or '').split('.')
    return '.'.join(parts[:3]) if len(parts) == 4 and parts[3] == '0' else version


def declared_version(root, spec):
    text = (root / 'components.cake').read_text(encoding='utf-8')
    match = re.search(r'new Artifact \(\s*"' + re.escape(spec['artifact']) + r'",\s*"([^"]+)"', text)
    return match.group(1) if match else None


def check_source(root, name, spec):
    errors = []
    folder = root / 'source' / 'Google' / name

    project = list(ET.parse(folder / f'{name}.csproj').getroot().iter())
    if any(local(e.tag) == 'NativeReference' for e in project):
        errors.append(f'{name}.csproj declares a NativeReference; the binding project would pack '
                      'the native SDK, so it belongs in the consumer-side targets instead')
    references = [e for e in project if local(e.tag) == 'PackageReference' and e.get('Include') == XBD]
    if not references:
        errors.append(f'{name}.csproj does not reference {XBD}')
    elif any(e.get('PrivateAssets') != 'None' for e in references):
        errors.append(f'{name}.csproj must reference {XBD} with PrivateAssets="None", '
                      'or its build targets never reach consumers')

    targets = ET.parse(folder / f'{name}.targets').getroot()
    if not any(local(e.tag) == 'XamarinBuildDownload' for e in targets.iter()):
        errors.append(f'{name}.targets has no XamarinBuildDownload item')

    components = (root / 'components.cake').read_text(encoding='utf-8')
    if re.search(r'PodSpec\.Create\s*\(\s*"' + re.escape(spec['pod']) + r'"[^)]*FrameworkSource\.Custom', components):
        errors.append(f'components.cake fetches {spec["pod"]} with FrameworkSource.Custom')
    if declared_version(root, spec) is None:
        errors.append(f'components.cake declares no version for {spec["artifact"]}')

    downloads = (root / 'custom_externals_download.cake').read_text(encoding='utf-8')
    if re.search(r'"' + re.escape(spec['pod']) + r'"\s*,\s*new ExternalDownloadSource', downloads):
        errors.append(f'custom_externals_download.cake downloads {spec["pod"]} into the build')
    return errors


def allowed_entries(spec):
    package, assembly = re.escape(spec['package_id']), re.escape(spec['assembly'])
    return [re.compile(pattern) for pattern in (
        r'\[Content_Types\]\.xml', r'_rels/\.rels', r'package/services/metadata/core-properties/[^/]+\.psmdcp',
        r'\.signature\.p7s', package + r'\.nuspec', r'License\.md', re.escape(spec['icon']),
        r'build/' + package + r'\.targets', r'buildTransitive/' + package + r'\.targets',
        r'lib/[^/]+/' + assembly + r'\.(dll|xml)',
    )]


def check_nuspec(nuspec, spec, version):
    metadata = next((e for e in nuspec if local(e.tag) == 'metadata'), None)
    if metadata is None:
        return ['nuspec has no metadata element']

    def field(tag):
        return next((e.text for e in metadata if local(e.tag) == tag), None)

    errors = []
    if field('id') != spec['package_id']:
        errors.append(f"nuspec id is {field('id')!r}, expected {spec['package_id']!r}")
    if normalize(field('version')) != normalize(version):
        errors.append(f"nuspec version is {field('version')!r}, but components.cake declares {version!r}")

    dependencies = next((e for e in metadata if local(e.tag) == 'dependencies'), None)
    if dependencies is None:
        return errors + [f'nuspec declares no {XBD} dependency']
    groups = [g for g in dependencies if local(g.tag) == 'group'] or [dependencies]
    for group in groups:
        label = group.get('targetFramework', 'all frameworks')
        found = [d for d in group if local(d.tag) == 'dependency' and d.get('id') == XBD]
        if not found:
            errors.append(f'nuspec has no {XBD} dependency for {label}')
        elif any(d.get('include') != 'All' or d.get('exclude') for d in found):
            errors.append(f'the {XBD} dependency for {label} must be include="All" with no exclude, '
                          'or consumers lose its build targets')
    return errors


def check_package(root, name, spec, package):
    errors = []
    patterns = allowed_entries(spec)
    source_targets = (root / 'source' / 'Google' / name / f'{name}.targets').read_bytes()
    with zipfile.ZipFile(package) as archive:
        entries = [e for e in archive.infolist() if not e.is_dir()]
        names = {e.filename for e in entries}

        unexpected = sorted(e.filename for e in entries if not any(p.fullmatch(e.filename) for p in patterns))
        if unexpected:
            errors.append('unexpected entries; only the binding, its targets and package metadata '
                          'may ship: ' + ', '.join(unexpected))
        for entry in entries:
            if entry.file_size > MAX_ENTRY_BYTES:
                errors.append(f'{entry.filename} is {entry.file_size:,} bytes, over the '
                              f'{MAX_ENTRY_BYTES:,}-byte cap; native SDK content may be embedded')

        if not any(re.fullmatch(r'lib/[^/]+/' + re.escape(spec['assembly']) + r'\.dll', n) for n in names):
            errors.append(f'missing lib/<tfm>/{spec["assembly"]}.dll')
        for folder in ('build', 'buildTransitive'):
            entry = f'{folder}/{spec["package_id"]}.targets'
            if entry not in names:
                errors.append(f'missing {entry}')
            elif archive.read(entry) != source_targets:
                errors.append(f'{entry} differs from source/Google/{name}/{name}.targets')

        nuspec = f'{spec["package_id"]}.nuspec'
        if nuspec not in names:
            errors.append(f'missing {nuspec}')
        else:
            errors.extend(check_nuspec(ET.fromstring(archive.read(nuspec)), spec, declared_version(root, spec)))
    return errors


def find_packages(directory, spec):
    pattern = re.compile(re.escape(spec['package_id']) + r'\.\d[^/]*\.nupkg')
    return sorted(p for p in Path(directory).iterdir()
                  if pattern.fullmatch(p.name) and not p.name.endswith('.symbols.nupkg'))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--target', required=True, choices=sorted(TARGETS))
    parser.add_argument('--package-dir', help='also check the single packed nupkg in this directory')
    args = parser.parse_args(argv)
    spec = TARGETS[args.target]

    errors = check_source(ROOT, args.target, spec)
    if args.package_dir:
        found = find_packages(args.package_dir, spec)
        if len(found) != 1:
            errors.append(f'expected exactly one {spec["package_id"]} package in {args.package_dir}, '
                          f'found {len(found)}: ' + ', '.join(p.name for p in found))
        else:
            errors.extend(check_package(ROOT, args.target, spec, found[0]))

    prefix = '::error::' if os.environ.get('GITHUB_ACTIONS') == 'true' else 'error: '
    for error in errors:
        print(prefix + error)
    if errors:
        return 1
    scope = 'source and package' if args.package_dir else 'source'
    print(f'{spec["package_id"]}: {scope} checked; delivered through {XBD} with no native SDK content')
    return 0


if __name__ == '__main__':
    sys.exit(main())
