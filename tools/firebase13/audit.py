#!/usr/bin/env python3
"""Run the existing binding audit against the pinned, prebuilt Firebase closure only."""
import argparse
import json
from pathlib import Path
import subprocess
from generate import ROOT, MANIFEST
from native import archive_validate, validate_schema


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache',type=Path,required=True)
    parser.add_argument('--archive',type=Path)
    parser.add_argument('--sharpie-path',required=True)
    args=parser.parse_args()
    m=json.loads(MANIFEST.read_text());validate_schema(m)
    archive_validate(m,args.archive or args.cache/(m['source']['id']+'.zip'))
    directory=ROOT/'externals';directory.mkdir(exist_ok=True)
    for package in m['packages']:
        for asset in package['assets']:
            if asset['kind']!='XCFramework':continue
            source=(args.cache/m['source']['id']/asset['path']).resolve()
            if not source.exists():raise ValueError('Acquire the pinned archive before auditing: '+str(source))
            destination=directory/source.name
            if destination.is_symlink() and destination.resolve()==source:continue
            if destination.exists() or destination.is_symlink():
                raise ValueError('Existing native input would be overwritten; use a clean feature worktree: '+str(destination))
            destination.symlink_to(source,target_is_directory=True)
    targets=','.join(p['id'].split('.',1)[1] for p in m['packages'] if p['id'].startswith('Firebase.'))
    subprocess.run(['scripts/compare-firebase-bindings.sh','--targets',targets,'--sharpie-path',args.sharpie_path,
                    '--output-dir','output/firebase13-api-audit'],cwd=ROOT,check=True)


if __name__=='__main__':main()
