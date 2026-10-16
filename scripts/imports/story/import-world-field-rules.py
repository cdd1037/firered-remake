#!/usr/bin/env python3
"""Compile source-derived field contracts without region map preparation stages."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'lib'))
import argparse
import json
import tempfile
import importlib

ART_COMPILERS = {
    "vermilion-gym": ("vermilion_gym_features", "compile_gym"),
    "rocket-hideout": ("rocket_hideout_features", "compile_hideout"),
    "pokemon-tower": ("pokemon_tower_features", "compile_tower"),
}
from world_source import WorldSource
from world_field_rules import compile_rules, REGIONS
from project_paths import PROJECT_ROOT
from map_source import require_safe_target
from runtime_json import runtime_json_bytes


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=Path,default=PROJECT_ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=Path,default=PROJECT_ROOT/'content/generated')
    parser.add_argument('--selection',type=Path,default=PROJECT_ROOT/'data/world-maps.json')
    parser.add_argument('--region',action='append',choices=(*REGIONS,*ART_COMPILERS))
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    if args.out.resolve().is_relative_to(args.raw.resolve()) or args.raw.resolve().is_relative_to(args.out.resolve()):raise ValueError('Source and output overlap')
    regions=args.region or (*REGIONS,*ART_COMPILERS)
    source=WorldSource(args.raw)
    documents=compile_rules(source,args.selection,[r for r in regions if r not in ART_COMPILERS])
    writes={path:runtime_json_bytes(value,sort_keys=True) for path,value in documents.items()}
    for region in regions:
        if region not in ART_COMPILERS:continue
        module,function=ART_COMPILERS[region]
        compile_art=getattr(importlib.import_module(module),function)
        with tempfile.TemporaryDirectory(prefix='field-rule-art-') as temporary:
            stage=Path(temporary)
            documents=compile_art(source,stage)
            writes.update({path:runtime_json_bytes(value,sort_keys=True) for path,value in documents.items()})
            writes.update({path.relative_to(stage).as_posix():path.read_bytes() for path in stage.rglob('*') if path.is_file()})
    for path,data in writes.items():
        target=args.out/path;require_safe_target(args.out,target)
        if args.check:
            if not target.is_file() or target.read_bytes()!=data:raise ValueError('Field rules differ: '+path)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(files=len(writes),regions=regions)))


if __name__=='__main__':main()
