#!/usr/bin/env python3
"""Check that a Google package delivered through Xamarin.Build.Download carries no native SDK.

The Maps and Places SDKs are proprietary, so their packages ship only the managed binding and an
MSBuild hook that downloads the SDK from Google during the consumer build. This fails when the
package, or the source it is packed from, would put Google's binaries or resources into our nupkg,
fetch them from anywhere but Google, or stop handing consumers the Xamarin.Build.Download hook.

Without --package-dir only the source is checked, which needs no build. The source checks read
text and give early warning; the package checks are the ones that inspect what would ship.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
XBD = 'Xamarin.Build.Download'

# Consumers fetch the SDK from Google under Google's terms, never from a copy we host.
DOWNLOAD_HOST = 'dl.google.com'

# Uncompressed size of every entry together. A package is about 200-320 KB per target framework,
# and the smallest SDK involved (Places) is several MB even compressed, so neither the SDK nor a
# copy split across entries fits. SDK resources small enough to fit are caught at the source.
MAX_PACKAGE_BYTES = 2 * 1024 * 1024

# No item in the binding project, or in an MSBuild file it imports, may point at native SDK content.
SDK_PATH = re.compile(r'(^|[\\/])externals[\\/]|\.(xcframework|framework|bundle|a|zip|tgz)([\\/]|$)|\.tar\.gz$',
                      re.IGNORECASE)

TFM = r'net\d+\.\d+-ios\d+(\.\d+)?'

TARGETS = {
    'Places': {'artifact': 'Google.Places', 'package_id': 'AdamE.Google.iOS.Places',
               'assembly': 'Google.Places', 'pod': 'GooglePlaces', 'icon': 'googleiosplaces_128x128.png'},
    'Maps': {'artifact': 'Google.Maps', 'package_id': 'AdamE.Google.iOS.Maps',
             'assembly': 'Google.Maps', 'pod': 'GoogleMaps', 'icon': 'googleiosmaps_128x128.png'},
}


def local(tag):
    return tag.rsplit('}', 1)[-1]


def metadata(item, name):
    """An MSBuild item's metadata, written either as an attribute or as a child element."""
    value = item.get(name)
    if value is None:
        child = next((c for c in item if local(c.tag) == name), None)
        value = child.text if child is not None else None
    return (value or '').strip()


def normalize(version):
    """Apply NuGet's normalization: a zero fourth component is dropped."""
    parts = (version or '').split('.')
    return '.'.join(parts[:3]) if len(parts) == 4 and parts[3] == '0' else version


def declared_version(root, spec):
    text = (root / 'components.cake').read_text(encoding='utf-8')
    match = re.search(r'new Artifact \(\s*"' + re.escape(spec['artifact']) + r'",\s*"([^"]+)"', text)
    return match.group(1) if match else None


def calls(text, pattern):
    """The source of each call that starts at a match of pattern, through its closing parenthesis."""
    for match in re.finditer(pattern, text):
        depth = 0
        for index in range(text.index('(', match.start()), len(text)):
            depth += {'(': 1, ')': -1}.get(text[index], 0)
            if depth == 0:
                yield text[match.start():index + 1]
                break


def msbuild_files(root, project):
    """The project, the Directory.Build files above it, and the local files any of them import."""
    pending, folder = [project], project.parent
    while True:
        pending += [folder / n for n in ('Directory.Build.props', 'Directory.Build.targets') if (folder / n).is_file()]
        if folder == root or folder.parent == folder:
            break
        folder = folder.parent
    seen = []
    while pending:
        path = pending.pop(0).resolve()
        if path in seen:
            continue
        seen.append(path)
        for element in ET.parse(path).getroot().iter():
            target = element.get('Project') if local(element.tag) == 'Import' else None
            if target and '$(' not in target:
                imported = (path.parent / target.replace('\\', '/')).resolve()
                if imported.is_file():
                    pending.append(imported)
    return seen


def check_source(root, name, spec):
    errors = []
    root = Path(root).resolve()
    folder = root / 'source' / 'Google' / name
    project = folder / f'{name}.csproj'

    for path in msbuild_files(root, project):
        label = path.relative_to(root).as_posix() if path.is_relative_to(root) else str(path)
        for element in ET.parse(path).getroot().iter():
            tag, include = local(element.tag), element.get('Include') or element.get('Update') or ''
            if tag == 'NativeReference':
                errors.append(f'{label} declares a NativeReference; the binding project would pack '
                              'the native SDK, so it belongs in the consumer-side targets instead')
            elif SDK_PATH.search(include):
                errors.append(f'{label} has a {tag} item for {include}; native SDK content must reach '
                              f'consumers through {XBD}, not through the binding project')

    items = list(ET.parse(project).getroot().iter())
    references = [e for e in items if local(e.tag) == 'PackageReference' and (e.get('Include') or '').lower() == XBD.lower()]
    if not references:
        errors.append(f'{name}.csproj does not reference {XBD}')
    elif any(metadata(e, 'PrivateAssets').lower() != 'none' for e in references):
        errors.append(f'{name}.csproj must reference {XBD} with PrivateAssets="None", '
                      'or its build targets never reach consumers')

    downloads = [e for e in ET.parse(folder / f'{name}.targets').getroot().iter() if local(e.tag) == 'XamarinBuildDownload']
    if not downloads:
        errors.append(f'{name}.targets has no XamarinBuildDownload item')
    for download in downloads:
        url = metadata(download, 'Url')
        parts = urlsplit(url)
        if parts.scheme != 'https' or parts.hostname != DOWNLOAD_HOST:
            errors.append(f'{name}.targets downloads {download.get("Include")} from {url or "no Url"}; '
                          f'consumers must fetch the SDK from https://{DOWNLOAD_HOST}')

    components = (root / 'components.cake').read_text(encoding='utf-8')
    if any('FrameworkSource.Custom' in call for call in calls(components, r'PodSpec\.Create\s*\(\s*"' + re.escape(spec['pod']) + '"')):
        errors.append(f'components.cake fetches {spec["pod"]} with FrameworkSource.Custom')
    if declared_version(root, spec) is None:
        errors.append(f'components.cake declares no version for {spec["artifact"]}')

    externals = (root / 'custom_externals_download.cake').read_text(encoding='utf-8')
    if f'"{spec["pod"]}"' in externals:
        errors.append(f'custom_externals_download.cake names {spec["pod"]}, so the build would download it into the package')
    return errors


def allowed_entries(spec):
    package, assembly = re.escape(spec['package_id']), re.escape(spec['assembly'])
    return [re.compile(pattern) for pattern in (
        r'\[Content_Types\]\.xml', r'_rels/\.rels', r'package/services/metadata/core-properties/[^/\\]+\.psmdcp',
        r'\.signature\.p7s', package + r'\.nuspec', r'License\.md', re.escape(spec['icon']),
        r'build/' + package + r'\.targets', r'buildTransitive/' + package + r'\.targets',
        r'lib/' + TFM + '/' + assembly + r'\.(dll|xml)',
    )]


def check_nuspec(nuspec, spec, version):
    metadata_element = next((e for e in nuspec if local(e.tag) == 'metadata'), None)
    if metadata_element is None:
        return ['nuspec has no metadata element']

    def field(tag):
        return next((e.text for e in metadata_element if local(e.tag) == tag), None)

    errors = []
    if (field('id') or '').lower() != spec['package_id'].lower():
        errors.append(f"nuspec id is {field('id')!r}, expected {spec['package_id']!r}")
    if normalize(field('version')) != normalize(version):
        errors.append(f"nuspec version is {field('version')!r}, but components.cake declares {version!r}")

    dependencies = next((e for e in metadata_element if local(e.tag) == 'dependencies'), None)
    if dependencies is None:
        return errors + [f'nuspec declares no {XBD} dependency']
    groups = [g for g in dependencies if local(g.tag) == 'group'] or [dependencies]
    for group in groups:
        label = group.get('targetFramework', 'all frameworks')
        found = [d for d in group if local(d.tag) == 'dependency' and (d.get('id') or '').lower() == XBD.lower()]
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
        entries = archive.infolist()
        names = [e.filename for e in entries]
        duplicates = sorted({n for n in names if names.count(n) > 1})
        if duplicates:
            errors.append('duplicate entries, which readers resolve differently: ' + ', '.join(duplicates))
        # Only an empty directory entry is skipped; one that carries data is checked like a file.
        files = [e for e in entries if not (e.filename.endswith('/') and e.file_size == 0)]

        unexpected = sorted(e.filename for e in files if not any(p.fullmatch(e.filename) for p in patterns))
        if unexpected:
            errors.append('unexpected entries; only the binding, its targets and package metadata '
                          'may ship: ' + ', '.join(unexpected))
        if sum(1 for n in names if n.endswith('.psmdcp')) != 1:
            errors.append('expected exactly one core-properties .psmdcp entry')
        total = sum(e.file_size for e in entries)
        if total > MAX_PACKAGE_BYTES:
            largest = ', '.join(f'{e.filename} ({e.file_size:,})' for e in sorted(entries, key=lambda e: -e.file_size)[:3])
            errors.append(f'entries total {total:,} bytes uncompressed, over the {MAX_PACKAGE_BYTES:,}-byte cap; '
                          f'native SDK content may be embedded (largest: {largest})')

        if not any(re.fullmatch(r'lib/' + TFM + '/' + re.escape(spec['assembly']) + r'\.dll', n) for n in names):
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
    # NuGet's caches lower-case file names, so match the package ID without regard to case.
    pattern = re.compile(re.escape(spec['package_id']) + r'\.\d[^/]*\.nupkg', re.IGNORECASE)
    return sorted(p for p in Path(directory).iterdir()
                  if pattern.fullmatch(p.name) and not p.name.lower().endswith('.symbols.nupkg'))


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
    print(f'{spec["package_id"]}: {scope} checked; delivered through {XBD} from {DOWNLOAD_HOST} with no native SDK content')
    return 0


if __name__ == '__main__':
    sys.exit(main())
