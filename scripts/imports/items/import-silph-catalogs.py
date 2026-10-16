#!/usr/bin/env python3
"""Regenerate Silph source metadata from immutable pret Git blobs, without upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import convert_battles, TextSources
from item_catalog import described_items, item_json
from pokemon_import_io import SourceReader, safe, write_files
from pathlib import Path
import argparse, json, re
from generated_output_compare import check_item_outputs, generated_output_matches

COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-silph-catalogs-v1'
CORE=[f'SilphCo_{i}F' for i in range(1,12)]+['SilphCo_Elevator']
SCRIPT_PATHS=tuple('data/maps/'+n+'/scripts.inc' for n in CORE)
TRAINERS=()
def canonical(doc): return (json.dumps(doc, sort_keys=True, indent=2, default=item_json)+'\n').encode()
def trainer_convert(sources):
 recipe = dict(trainers=list(TRAINERS), scripts=list(SCRIPT_PATHS), moves='party')
 recipe['dialogueContexts'] = [dict(trainers=[short for short in TRAINERS if short.startswith('RIVAL_SILPH_')],
     script='data/maps/SilphCo_7F/scripts.inc', label='SilphCo_7F_EventScript_RivalScene', introIndex=1)]
 return convert_battles(recipe, sources)

ITEMS=('CARD_KEY','MASTER_BALL','ULTRA_BALL','TM01','TM08','TM41','HYPER_POTION','MAX_REVIVE','ESCAPE_ROPE','FULL_HEAL','PROTEIN','HP_UP','X_SPECIAL','CALCIUM','CARBOS','RARE_CANDY','IRON','ZINC','ELIXIR','PP_UP','NUGGET','MAX_POTION','REVIVE')

def need(ok,message):
 if not ok:raise ValueError(message)
def metadata(src,dependency):
 return dict(schemaVersion=1,items=described_items(src.read, ['ITEM_'+name for name in ITEMS]))

def main():
 global TRAINERS
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path);ap.add_argument('--raw-root',type=Path,default=_PROJECT_ROOT/'content/raw/pokefirered/037335f4');ap.add_argument('--project-root',type=Path,default=_PROJECT_ROOT);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
 src=SourceReader(a.raw_root,commit=COMMIT,repository=a.reference_root)
 TRAINERS=tuple(n[8:] for p in SCRIPT_PATHS for n in re.findall(r'trainerbattle_(?:single|no_intro) (TRAINER_\w+)',src.text(p)))
 need(TRAINERS and len(TRAINERS)==len(set(TRAINERS)),'Expected nonempty unique source trainer bindings')
 trainers=trainer_convert(TextSources(src.text))
 items=metadata(src,a.dependency_root)
 docs={'trainers.json':trainers,'items.json':items}
 output={f'content/generated/data/regions/silph/{p}':(trainer_json(a.project_root/'content/generated', d, check=a.check) if p == 'trainers.json' else canonical(d)) for p,d in docs.items()}
 output.update({'content/raw/pokefirered/037335f4/'+p:b for p,b in src.data.items()})
 manifest=dict(schemaVersion=1,outputs=[dict(path=p) for p,b in sorted(output.items()) if p.startswith('content/generated/')])
 output['content/generated/data/regions/silph/manifest.json']=canonical(manifest)
 if a.check:
  check_item_outputs(a.project_root/'content/generated', {
   path.removeprefix('content/generated/'): data for path,data in output.items()
   if path.startswith('content/generated/') and not path.endswith('/manifest.json')})
  for rel,b in output.items():
   if not rel.startswith('content/generated/'):need(generated_output_matches(safe(a.project_root,rel),b),'Source cache differs: '+rel)
 else:
  write_files(a.project_root,output)
 print(json.dumps(dict(trainers=len(TRAINERS),members=sum(len(t['party']) for t in trainers['trainers']),sourceFiles=len(src.data),items=len(items['items']),outputs=manifest['outputs']), default=item_json))
if __name__=='__main__':main()
