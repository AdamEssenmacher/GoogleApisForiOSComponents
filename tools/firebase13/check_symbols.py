#!/usr/bin/env python3
"""Check every explicitly named Firebase Objective-C binding class against native exports.

This catches class renames that package layout checks cannot detect. It does not establish
selector coverage, authentication behavior, or service correctness.
"""
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]


def main():
    manifest = json.loads((ROOT / 'source/Firebase/native-assets.json').read_text())
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--cache', type=Path, required=True)
    args = parser.parse_args()
    import plistlib
    exports = set()
    binaries = []
    architecture_checks = 0
    for package in manifest['packages']:
        for asset in package['assets']:
            if asset['kind'] != 'XCFramework':
                continue
            framework = args.cache / manifest['source']['id'] / asset['path']
            info = plistlib.loads((framework / 'Info.plist').read_bytes())
            for lib in info['AvailableLibraries']:
                if lib['SupportedPlatform'] != 'ios':
                    continue
                binary = framework / lib['LibraryIdentifier'] / lib['LibraryPath'] / framework.stem
                architectures = subprocess.check_output(['xcrun', 'lipo', '-archs', str(binary)]).decode().split()
                if set(architectures) != set(lib['SupportedArchitectures']):
                    raise ValueError('Mach-O architectures differ from XCFramework metadata: ' + str(binary))
                architecture_checks += 1
                if lib.get('SupportedPlatformVariant', '') == '':
                    binaries.append(binary)
    if len(binaries) < 35:
        raise ValueError('Acquire the verified Firebase archive before native-symbol validation')
    for binary in binaries:
        result = subprocess.run(['xcrun', 'nm', '-arch', 'arm64', '-gU', str(binary)], check=True,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        exports.update(re.findall(r'_OBJC_CLASS_\$_(\w+)', result.stdout))
        projects = [ROOT / p['project'] for p in manifest['packages']]
    failures = []
    checked = 0
    for project in projects:
        for source in project.parent.glob('*.cs'):
            text = source.read_text(encoding='utf-8-sig')
            for match in re.finditer(r'((?:\s*\[[^\]]*\])+\s*)interface\s+(\w+)', text):
                attributes, managed_name = match.groups()
                if '[Protocol' in attributes:
                    continue
                base_type = re.search(r'\[BaseType\s*\([^\]]*Name\s*=\s*"([^"]+)"', attributes)
                if base_type:
                    native_name = base_type.group(1)
                    checked += 1
                    if native_name not in exports:
                        failures.append(f'{source.relative_to(ROOT)}: {managed_name} maps to missing {native_name}')
    if failures:
        raise ValueError('\n'.join(failures))
    print(f'Validated {checked} explicit Objective-C binding class names against {len(binaries)} native iOS libraries and {architecture_checks} actual Mach-O slices.')


if __name__ == '__main__':
    main()
