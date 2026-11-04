#!/usr/bin/env python3
"""Validate Firebase official archives and build managed-only, XBD-delivered packages.

No consumer downloader is implemented here. Acquisition uses stock Xamarin.Build.Download.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import subprocess
import zipfile
import xml.etree.ElementTree as ET
from generate import ROOT, MANIFEST, generate


def require(ok, message):
    if not ok:
        raise ValueError(message)


def digest(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def validate_schema(m):
    require(m['schema'] == 1 and m['publicationEnabled'] is False, 'Experimental publication must remain disabled')
    s = m['source']
    require(re.fullmatch(r'13\.\d+\.\d+', s['version']) and m['releaseVersion']==s['version'], 'Only pinned Firebase13 release versions are accepted')
    require(s['url'] == f"https://github.com/firebase/firebase-ios-sdk/releases/download/{s['version']}/Firebase.zip", 'Unapproved/mutable source URL')
    require(re.fullmatch(r'[a-f0-9]{64}', s['sha256']) and s['kind'] == 'Zip', 'Invalid source digest/kind')
    require(s['id'] == 'FirebaseiOSSDK-' + s['version'] and s['extractionRoot'] == 'Firebase' and s['lockTimeout'] > 0, 'Invalid shared cache declaration')
    ids, ownership, destinations = set(), set(), set()
    packages = {p['package']: p for p in m['packages']}
    require(len(packages) == len(m['packages']) == 23, 'Unexpected Firebase package closure')
    for p in m['packages']:
        require(all(n in packages for n in p['dependencies']), 'Dependency outside Firebase closure')
        project = ET.parse(ROOT / p['project']).getroot()
        require(project.findtext('PropertyGroup/PackageId') == p['package'], 'Package owner/project mismatch')
        require(project.findtext('PropertyGroup/PackageVersion') == p['version'], 'Package version mismatch')
        require(not list(project.iter('NativeReference')) and not list(project.iter('BundleResource')), 'Producer project embeds native payloads')
        xbd = [n for n in project.iter('PackageReference') if n.get('Include') == 'Xamarin.Build.Download']
        require(len(xbd) == 1 and xbd[0].get('Version') == '[0.11.6]' and xbd[0].get('PrivateAssets') == 'None', 'Exact public XBD dependency required')
        refs = {str((ROOT / p['project']).parent.joinpath(n.get('Include').replace('\\', '/')).resolve()) for n in project.iter('ProjectReference')}
        require(refs == {str((ROOT / packages[n]['project']).resolve()) for n in p['dependencies']}, 'Dependency consumption differs from ownership manifest')
        for a in p['assets']:
            require(a['id'] not in ids, 'Duplicate asset ID'); ids.add(a['id'])
            path = PurePosixPath(a['path'])
            require(not path.is_absolute() and a['path'].startswith('Firebase/') and not any(x in ('', '.', '..') for x in a['path'].split('/')) and '\\' not in a['path'] and ':' not in a['path'], 'Unsafe asset path')
            require(a['owner'] == p['id'] and a['kind'] in ('XCFramework', 'Bundle', 'Tool'), 'Invalid asset owner/kind')
            if a['kind'] == 'XCFramework':
                require(path.name not in ownership, 'Duplicate framework ownership'); ownership.add(path.name)
            if a['kind'] == 'Bundle':
                key = (a['logical'], a.get('condition'))
                require(key not in destinations, 'Duplicate logical bundle'); destinations.add(key)
    for name, content in generate(m).items():
        require((ROOT / 'source/Firebase/native' / name).read_text() == content, 'Stale generated target: ' + name)
    acquisition=ET.parse(ROOT/'tools/firebase13/acquire.proj').find('.//XamarinBuildDownload')
    require(acquisition.get('Include') == s['id'] and acquisition.findtext('Url') == s['url'] and acquisition.findtext('Kind') == s['kind'] and acquisition.findtext('Sha256') == s['sha256'] and acquisition.findtext('ExclusiveLockTimeout') == str(s['lockTimeout']), 'Producer/consumer XBD declarations differ')
    return packages


def archive_validate(m, archive):
    require(digest(archive) == m['source']['sha256'], 'Archive SHA-256 mismatch')
    # Reuse the reviewed archive preflight, never consumer-time custom extraction.
    from archive_safety import preflight
    preflight(archive)
    with zipfile.ZipFile(archive) as z:
        names = z.namelist()
        require(z.read('Firebase/NOTICES') == (ROOT/'source/Firebase/native/UPSTREAM-NOTICES.txt').read_bytes(), 'Upstream notices differ from archive')
        tagged=(ROOT/'tests/fixtures/firebase13'/m['releaseVersion']/'Package.swift').read_text()
        require('let firebaseVersion = "'+m['releaseVersion']+'"' in tagged, 'Tagged Package.swift version mismatch')
        require(z.read('Firebase/METADATA.md') == (ROOT / 'tests/fixtures/firebase13' / m['releaseVersion'] / 'METADATA.md').read_bytes(), 'METADATA differs from committed fixture')
        for p in m['packages']:
            for a in p['assets']:
                require(a['path'] in names or any(n.startswith(a['path'] + '/') for n in names), 'Missing payload: ' + a['id'])
                if a['kind'] == 'Tool':
                    require(hashlib.sha256(z.read(a['path'])).hexdigest() == a['sha256'], 'Tool digest mismatch')
                if a['kind'] == 'Bundle':
                    prefix = a['path'].split('.xcframework/')[0] + '.xcframework/'
                    copies = sorted({n[:n.index('.bundle/')+7] for n in names if n.startswith(prefix) and '/' + a['logical'] + '/' in n})
                    def resources(bundle):
                        start = bundle + '/'
                        result={}
                        for n in names:
                            if not n.startswith(start) or n.endswith('/'):
                                continue
                            relative=n[len(start):]
                            if relative in ('Info.plist','Contents/Info.plist'):
                                continue
                            relative=relative.removeprefix('Contents/Resources/')
                            result[relative]=hashlib.sha256(z.read(n)).hexdigest()
                        return result
                    # Bundle Info.plist contains SDK-specific build metadata; use each actual slice.
                    for copy in copies:
                        if any(slice in copy for slice in ('/ios-arm64/', '/ios-arm64_x86_64-simulator/', '/ios-arm64_x86_64-maccatalyst/')):
                            require(a['logical'] == 'InAppMessagingDisplayResources.bundle' or resources(copy) == resources(a['path']), 'Resource payload differs across supported slices: ' + copy)
                if a['kind'] != 'XCFramework':
                    continue
                info = plistlib.loads(z.read(a['path'] + '/Info.plist'))
                libs = info['AvailableLibraries']
                slices = {(x['SupportedPlatform'], x.get('SupportedPlatformVariant', '')):set(x['SupportedArchitectures']) for x in libs}
                expected = {('ios',''):{'arm64'}, ('ios','simulator'):{'arm64','x86_64'}, ('ios','maccatalyst'):{'arm64','x86_64'}}
                if p['id'] == 'Google.AppMeasurement' and a['path'].endswith('GoogleAdsOnDeviceConversion.xcframework'):
                    expected.pop(('ios','maccatalyst'))
                require(all(slices.get(k) == v for k,v in expected.items()), 'Missing/unexpected supported slices: ' + a['id'])
                for lib in libs:
                    if (lib['SupportedPlatform'], lib.get('SupportedPlatformVariant','')) not in expected:
                        continue
                    prefix=a['path']+'/'+lib['LibraryIdentifier']+'/'+lib['LibraryPath']
                    paths=[prefix+'/Info.plist',prefix+'/Versions/A/Resources/Info.plist']
                    actual=None
                    for path in paths:
                        if path in names:
                            try: actual=plistlib.loads(z.read(path)).get('CFBundleShortVersionString')
                            except plistlib.InvalidFileException: continue
                            if actual: break
                    require(actual == a['binaryVersion'], 'Unexpected binary version: '+prefix)
                # Compare complete product copies except their detached XCFramework signatures.
                # Google signs identical payloads separately; the full archive digest pins signatures too.
                basename = PurePosixPath(a['path']).name
                copies = sorted({n.split(basename + '/',1)[0] + basename for n in names if '/' + basename + '/' in n})
                def fingerprints(prefix):
                    start = prefix + '/'
                    return {n[len(start):]: hashlib.sha256(z.read(n)).hexdigest() for n in names if n.startswith(start) and not n.endswith('/') and not n[len(start):].startswith('_CodeSignature/')}
                canonical = fingerprints(a['path'])
                for copy in copies:
                    require(fingerprints(copy) == canonical, 'Conflicting framework product copies: ' + copy)
        supported = {PurePosixPath(a['path']).name for p in m['packages'] for a in p['assets'] if a['kind'] == 'XCFramework'}
        metadata = z.read('Firebase/METADATA.md').decode()
        sections = re.split(r'^## ', metadata, flags=re.M)
        for section in sections:
            product = section.splitlines()[0] if section.splitlines() else ''
            if product.split(' ')[0] not in {PurePosixPath(a['path']).stem for p in m['packages'] for a in p['assets'] if a['kind']=='XCFramework' and a['path'].split('/')[1].startswith('Firebase')}:
                continue
            require(set(re.findall(r'^- (\S+\.xcframework)$', section, re.M)) <= supported, 'Unowned framework in supported product: ' + product)
    print('PASS official archive: digest, paths/links, metadata, slices, ownership, duplicate frameworks and tools')


def inspect_package(path, p):
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        require(not any('.xcframework' in n or '.framework' in n or '.bundle' in n or n.endswith(('.resources.zip','.a','.dylib')) or '.resources/' in n or PurePosixPath(n).name in ('run','upload-symbols','Podfile') or '.podspec' in n for n in names), 'Native/CocoaPods payload in NuGet: ' + p['package'])
        nuspec = ET.fromstring(z.read(next(n for n in names if n.endswith('.nuspec'))))
        def value(tag):
            return next(n.text for n in nuspec.iter() if n.tag.split('}')[-1] == tag)
        def version(v):
            parts=v.split('.')
            while len(parts)>3 and parts[-1]=='0':parts.pop()
            return '.'.join(parts)
        require(value('id') == p['package'] and version(value('version')) == version(p['version']), 'NuGet identity/version mismatch')
        assembly=ET.parse(ROOT/p['project']).findtext('PropertyGroup/AssemblyName')+'.dll'
        for platform in ('ios','maccatalyst'):
            require(any(n.startswith('lib/net10.0-'+platform) and n.endswith('/'+assembly) for n in names), 'Missing managed platform assembly')
        deps = [n for n in nuspec.iter() if n.tag.split('}')[-1]=='dependency']
        require({n.get('id') for n in deps} == set(p['dependencies']) | {'Xamarin.Build.Download'}, 'Unexpected package dependency closure')
        require(z.read('UPSTREAM-NOTICES.txt') == (ROOT/'source/Firebase/native/UPSTREAM-NOTICES.txt').read_bytes(), 'Missing/upstream notices differ')
        xbd = [n for n in deps if n.get('id')=='Xamarin.Build.Download']
        require(xbd and all(n.get('version')=='[0.11.6]' for n in xbd), 'Missing exact XBD dependency in NuGet')
        for folder in ('build','buildTransitive'):
            require(z.read(folder+'/Firebase.Native.Common.targets').decode() == generate(json.loads(MANIFEST.read_text()))['Firebase.Native.Common.targets'], 'Inconsistent common XBD declaration')
            name=folder+'/'+p['package']+'.targets'
            require(z.read(name).decode()==generate(json.loads(MANIFEST.read_text()))[p['package']+'.targets'], 'Missing/stale delivery target')
        require(z.read('build/'+p['package']+'.targets')==z.read('buildTransitive/'+p['package']+'.targets'), 'Inconsistent transitive targets')
    print('PASS managed-only package:', path.name)


def order(packages, names):
    byid={p['id']:p for p in packages.values()}
    selected=list(byid) if not names else names.replace('Google.GoogleAppMeasurement','Google.AppMeasurement').split(',')
    require(all(n in byid for n in selected), 'Only Firebase and its declared native dependencies are supported here')
    done, visiting, result=set(),set(),[]
    def visit(key):
        if key in done:return
        require(key not in visiting, 'Dependency cycle');visiting.add(key)
        p=byid[key]
        for dep in p['dependencies']:visit(packages[dep]['id'])
        visiting.remove(key);done.add(key);result.append(p)
    for key in selected:visit(key)
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['native-assets-validate','archive-validate','release-check','externals','nuget','packages-validate'])
    parser.add_argument('--names', default='')
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--cache', type=Path)
    args=parser.parse_args()
    m=json.loads(MANIFEST.read_text());packages=validate_schema(m)
    selected=order(packages,args.names)
    cache=(args.cache or Path(os.environ.get('XamarinBuildDownloadDir',str(Path.home()/'Library/Caches/XamarinBuildDownload')))).resolve()
    if args.command in ('externals','nuget','release-check'):
        subprocess.run(['dotnet','restore','tools/firebase13/acquire.proj'],cwd=ROOT,check=True)
        subprocess.run(['dotnet','msbuild','tools/firebase13/acquire.proj','-t:Acquire','-p:XamarinBuildDownloadDir='+str(cache)],cwd=ROOT,check=True)
    if args.archive:archive_validate(m,args.archive)
    if args.command=='archive-validate':require(args.archive is not None,'--archive is required')
    if args.command=='release-check':
        archive=args.archive or cache/(m['source']['id']+'.zip')
        require(archive.exists(),'Release validation requires --archive when XBD no longer retains the pinned ZIP')
        if not args.archive:archive_validate(m,archive)
    if args.command=='nuget':
        # Strict producer verification uses the complete official archive before packing.
        archive=args.archive or cache/(m['source']['id']+'.zip')
        require(archive.exists(),'Producer validation requires --archive with the pinned official ZIP; warm XBD caches may discard it')
        if not args.archive:archive_validate(m,archive)
        output=ROOT/'output/firebase13';output.mkdir(parents=True,exist_ok=True)
        commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT).decode().strip()
        info=ROOT/'output/firebase13-build-info.cs'
        info.write_text((ROOT/'source/AssemblyInfo.cs').read_text().replace('{BUILD_COMMIT}',commit).replace('{BUILD_NUMBER}',os.environ.get('GITHUB_RUN_NUMBER','LOCAL')).replace('{BUILD_TIMESTAMP}',datetime.now(timezone.utc).isoformat()))
        for p in selected:
            subprocess.run(['dotnet','pack',str(ROOT/p['project']),'-c','Release','-o',str(output),'-p:UseSharedCompilation=false','-p:RepositoryCommit='+commit,'-p:Firebase13BuildInfoPath='+str(info)],cwd=ROOT,check=True)
            path=output/(p['package']+'.'+p['version'].removesuffix('.0')+'.nupkg')
            inspect_package(path,p)
    elif args.command=='packages-validate':
        if not args.names:
            expected={p['package']+'.'+p['version'].removesuffix('.0')+'.nupkg' for p in selected}
            require({p.name for p in (ROOT/'output/firebase13').glob('*.nupkg')} == expected, 'Missing/extra packages in Firebase output')
        for p in selected:
            inspect_package(ROOT/'output/firebase13'/(p['package']+'.'+p['version'].removesuffix('.0')+'.nupkg'),p)
    print('PASS',args.command,':',len(selected),'packages; publishing disabled')


if __name__=='__main__':
    main()
