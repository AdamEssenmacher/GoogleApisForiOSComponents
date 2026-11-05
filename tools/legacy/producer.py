#!/usr/bin/env python3
"""Produce non-Firebase packages with the pinned, isolated pre-migration inputs.

This obtains this repository's historical source, not native SDK binaries. Existing
CocoaPods production runs only in the legacy workspace; Firebase 13 never falls back.
"""
import argparse
from contextlib import contextmanager
import json
import os
import re
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
LOCK='eng/legacy-producer.json'
PRODUCTION={'externals','libs','nuget','build','ci'}


def run(args, root=ROOT, **kwargs):
    return subprocess.run(args,cwd=root,check=True,**kwargs)


def lock(root=ROOT):
    data=json.loads((root/LOCK).read_text())
    if data['schema']!=1 or not re.fullmatch('[a-f0-9]{40}',data['commit']) or not re.fullmatch('[a-f0-9]{40}',data['tree']):
        raise ValueError('Invalid legacy producer lock')
    keys=[p['key'] for p in data['artifacts']]
    for field in ('key','artifactId','id','project'):
        values=[p[field] for p in data['artifacts']]
        if len(values)!=len(set(values)):raise ValueError('Duplicate locked legacy artifact '+field)
    if any(not set(p['dependencies'])<=set(keys) for p in data['artifacts']):raise ValueError('Unknown locked legacy dependency')
    return data


def ensure_commit(root, data):
    check=subprocess.run(['git','cat-file','-e',data['commit']+'^{commit}'],cwd=root,capture_output=True)
    if check.returncode:
        # Shallow CI checkouts may omit this reviewed ancestor. Fetch only its pinned object.
        run(['git','fetch','--no-tags','origin',data['commit']],root)
    tree=subprocess.check_output(['git','rev-parse',data['commit']+'^{tree}'],cwd=root,text=True).strip()
    if tree!=data['tree']:raise ValueError('Legacy baseline tree differs from its lock')


def closure(data, names):
    bykey={p['key']:p for p in data['artifacts']};result=[];visiting=set();done=set()
    def visit(key):
        if key not in bykey:raise ValueError('Unknown legacy artifact: '+key)
        if key in visiting:raise ValueError('Legacy dependency cycle: '+key)
        if key in done:return
        visiting.add(key)
        for dep in bykey[key]['dependencies']:visit(dep)
        visiting.remove(key);done.add(key);result.append(bykey[key])
    for name in names:visit(name)
    return result


def plan(data, manifest, names, mode='auto'):
    registered={p['key'] for p in data['artifacts']}
    selected=names.split(',') if names else [p['key'] for p in data['artifacts']]
    if not set(selected)<=registered:raise ValueError('Unknown component selection')
    migrated_ids={p['id'] for p in manifest['packages']}
    migrated={p['key'] for p in data['artifacts'] if p['artifactId'] in migrated_ids}
    legacy=selected if mode=='legacy' else [n for n in selected if n not in migrated]
    modern=[] if mode=='legacy' else [n for n in selected if n in migrated]
    return {'legacy':legacy,'firebase13':modern,'legacyPackages':closure(data,legacy)}


@contextmanager
def source_tree(root=ROOT):
    """Fresh immutable inputs for fingerprints; build workspaces are separate copies."""
    root=root.resolve()
    data=lock(root);ensure_commit(root,data)
    directory=root/'output/legacy-sources';directory.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='source-',dir=directory) as tmp:
        stage=Path(tmp).resolve()
        archive=stage/'source.tar'
        run(['git','archive','--format=tar','--output='+str(archive),data['commit']],root)
        with tarfile.open(archive) as tar:
            for item in tar:
                path=PurePosixPath(item.name)
                if path.is_absolute() or '..' in path.parts or item.isdev() or item.islnk():
                    raise ValueError('Unsafe path in legacy source archive')
                if item.issym():
                    target=PurePosixPath(item.linkname)
                    resolved=os.path.normpath(str(path.parent/target))
                    if target.is_absolute() or resolved.startswith('../'):raise ValueError('Escaping legacy source link')
            tar.extractall(stage)
        archive.unlink()
        yield stage


def matches_metadata(metadata, root=ROOT):
    if not (root/LOCK).exists() or not metadata:return False
    versions={p['id']:p['version'] for p in lock(root)['artifacts']}
    def norm(v):return tuple(int(n) for n in v.split('.'))+(0,)*(4-len(v.split('.')))
    return all(p['id'] in versions and norm(p['version'])==norm(versions[p['id']]) for p in metadata)


def verify_nonfirebase_inputs(root, stage, data, manifest):
    """Never silently ignore a later product edit while using a frozen baseline."""
    root=root.resolve();stage=stage.resolve()
    migrated_ids={p['id'] for p in manifest['packages']}
    migrated={p['key'] for p in data['artifacts'] if p['artifactId'] in migrated_ids}
    tracked=set(subprocess.check_output(['git','ls-files'],cwd=root,text=True).splitlines())
    for p in data['artifacts']:
        if p['key'] in migrated:continue
        prefix=str(Path(p['project']).parent)+'/'
        for name in tracked:
            if name.startswith(prefix):
                if not (stage/name).is_file() or (root/name).read_bytes()!=(stage/name).read_bytes():
                    raise ValueError('Non-Firebase product changed; review/update its legacy baseline before production: '+name)
    # Include packed readmes/icons and global build inputs, not just files beside the project.
    shared=['Directory.Build.props','global.json','.config/dotnet-tools.json','source/AssemblyInfo.cs','common.cake','poco.cake','custom_externals_download.cake']
    for artifact in data['artifacts']:
        if artifact['key'] in migrated:continue
        project=stage/artifact['project']
        for node in ET.parse(project).iter():
            include=(node.get('Include') or '').replace('\\','/')
            if node.get('Pack','').lower()=='true' and '$(' not in include and '*' not in include:
                source=(project.parent/include).resolve()
                if source.is_relative_to(stage) and source.is_file():shared.append(str(source.relative_to(stage)))
    for name in set(shared):
        if not (root/name).is_file() or (root/name).read_bytes()!=(stage/name).read_bytes():
            raise ValueError('Legacy producer input changed; review/update its baseline: '+name)
    prefixes=[str(Path(p['project']).parent)+'/' for p in data['artifacts'] if p['key'] not in migrated]
    untracked=subprocess.check_output(['git','ls-files','--others','--exclude-standard'],cwd=root,text=True).splitlines()
    if any(any(name.startswith(prefix) for prefix in prefixes) for name in untracked):
        raise ValueError('Untracked non-Firebase source would be ignored by the legacy baseline; review it before production')
    # Metadata-only identities, pins and graph are also compared by the existing AST normalizer.
    selected=root/'output/legacy-selected.json';output=root/'output/legacy-components.json'
    selected.write_text(json.dumps([p['artifactId'] for p in data['artifacts'] if p['key'] not in migrated]))
    results=[]
    for source in (root,stage):
        run(['dotnet','run','--project',str(root/'scripts/FirebaseReleaseFingerprint'),'--',str(source),str(selected),str(output),'--components-only'],root,
            env={**os.environ,'MSBuildEnableWorkloadResolver':'false'})
        results.append(output.read_bytes())
    if results[0]!=results[1]:raise ValueError('Non-Firebase Cake declarations changed; review/update the locked producer baseline')


def cake(stage, target, names='', output=None, channel=None):
    args=['dotnet','tool','run','dotnet-cake','--']
    if channel=='signin':args+=['signin_publish.cake']
    else:args+=['--target='+('firebase-publish-metadata' if channel=='firebase' else ('nuget' if target=='build' else target))]
    if names:args+=['--names='+names]
    if output:args+=['--metadata-output='+str(output)]
    run(args,stage)


def copy_outputs(root, stage, selected, require_packages=False):
    allowed={p['id']:p['version'] for p in selected}
    output=root/'output';output.mkdir(exist_ok=True)
    import zipfile
    seen=set()
    for package in (stage/'output').glob('*.nupkg'):
        with zipfile.ZipFile(package) as z:
            spec=ET.fromstring(z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))))
            values={n.tag.split('}')[-1]:n.text for n in spec.iter()}
            if values['id'] not in allowed:raise ValueError('Unexpected legacy output: '+values['id'])
            if values['id'] in seen:raise ValueError('Duplicate legacy output identity')
            seen.add(values['id'])
            expected=allowed[values['id']].split('.')
            while len(expected)>3 and expected[-1]=='0':expected.pop()
            actual=values['version'].split('.')
            while len(actual)>3 and actual[-1]=='0':actual.pop()
            if actual!=expected:raise ValueError('Legacy package version changed: '+values['id'])
        shutil.copy2(package,output/package.name)
    if require_packages and seen!=set(allowed):raise ValueError('Missing legacy output packages')
    # Keep the established resolution-evidence path used by Sign-In validation.
    for artifact in selected:
        source=stage/'externals/build'/artifact['key']
        for name in ('Podfile','Podfile.lock'):
            if (source/name).exists():
                destination=root/'externals/build'/artifact['key'];destination.mkdir(parents=True,exist_ok=True)
                shutil.copy2(source/name,destination/name)


def copy_product_externals(root, stage, selected, manifest):
    migrated={p['id'] for p in manifest['packages']}
    assets=set()
    for artifact in selected:
        if artifact['artifactId'] in migrated:continue
        for node in ET.parse(stage/artifact['project']).iter():
            include=(node.get('Include') or '').replace('\\','/')
            if '/externals/' in include:
                name=include.split('/externals/',1)[1].split('/',1)[0]
                if name and '*' not in name and name not in ('.','..'):assets.add(name)
    for name in assets:
        source=stage/'externals'/name;target=root/'externals'/name
        if not source.exists():continue
        if target.is_symlink():target.unlink()
        elif target.is_dir():shutil.rmtree(target)
        elif target.exists():target.unlink()
        target.parent.mkdir(parents=True,exist_ok=True)
        if source.is_dir():shutil.copytree(source,target,symlinks=True)
        else:shutil.copy2(source,target)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['build','metadata','plan'])
    parser.add_argument('--target',choices=sorted(PRODUCTION),default='nuget')
    parser.add_argument('--names',default='')
    parser.add_argument('--mode',choices=['auto','legacy'],default='auto')
    parser.add_argument('--channel',choices=['signin','firebase'],default='signin')
    parser.add_argument('--output',type=Path)
    parser.add_argument('--archive');parser.add_argument('--cache')
    args=parser.parse_args();data=lock();manifest=json.loads((ROOT/'source/Firebase/native-assets.json').read_text())
    selection=plan(data,manifest,args.names,args.mode)
    if args.command=='plan':print(json.dumps(selection,indent=2));return
    if args.command=='metadata' or selection['legacy']:
        with source_tree() as source:
            verify_nonfirebase_inputs(ROOT,source,data,manifest)
            if args.command=='metadata':
                destination=(args.output or ROOT/('output/'+args.channel+'-metadata.json')).resolve();destination.parent.mkdir(parents=True,exist_ok=True)
                cake(source,'',output=destination,channel=args.channel);return
            # Build mutations, Pods and externals stay inside this private workspace.
            stage=Path(tempfile.mkdtemp(prefix='build-',dir=ROOT/'output/legacy-sources'))
            shutil.copytree(source,stage,dirs_exist_ok=True,symlinks=True)
        if 'Google.SignIn' in selection['legacy']:
            expected=json.loads((stage/'eng/signin-release.json').read_text())['nativeBuild']['cocoapods']
            actual=subprocess.check_output(['pod','--version'],text=True).strip()
            if actual!=expected:raise ValueError('Sign-In legacy production requires CocoaPods '+expected+'; found '+actual)
        cake(stage,args.target,','.join(selection['legacy']))
        copy_outputs(ROOT,stage,selection['legacyPackages'],args.target in ('nuget','build','ci'))
        copy_product_externals(ROOT,stage,selection['legacyPackages'],manifest)
        print('Legacy production workspace:',stage)
    if selection['firebase13']:
        command='externals' if args.target=='externals' else ('libs' if args.target=='libs' else 'nuget')
        cmd=['python3','tools/firebase13/native.py',command,'--names',','.join(selection['firebase13'])]
        for key in ('archive','cache'):
            if getattr(args,key):cmd+=['--'+key,getattr(args,key)]
        run(cmd)


if __name__=='__main__':main()
