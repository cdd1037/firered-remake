#!/usr/bin/env python3
"""Regenerate the postgame resources using their existing region dependencies. No reference scripts are executed."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,sys,json,subprocess,time
from pathlib import Path
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project-root',type=Path,default=_PROJECT_ROOT);parser.add_argument('--reference-root',required=True,type=Path);parser.add_argument('--output',required=True,type=Path);options=parser.parse_args()
root=options.project_root.resolve();ref=options.reference_root.resolve();out=options.output.resolve();out.mkdir(parents=True,exist_ok=False)
steps=[("field-strength-art",["scripts/imports/presentation/import-field-strength-art.py","--reference-root",str(ref)]),("field-surf-art",["scripts/imports/presentation/import-field-surf-art.py","--reference-root",str(ref)]),("national-dex-source",["scripts/imports/pokemon/import-national-dex-source.py","--reference-root",str(ref),"--project-root",str(root)])]
for region in ['sevii-ruby','four-island','six-island','five-island']:
 steps.append((region+'-region',['scripts/imports/maps/import-'+region+'-region.py','--reference-root',str(ref),'--dependency-root',str(root),'--project-root',str(root)]))
 args=[str(ref),str(root)]+([str(root)] if region=='sevii-ruby' else [])+[str(root)]
 steps.append((region+'-rules',['scripts/imports/story/import-'+region+'-rules.py']+args))
 if region in ['sevii-ruby','four-island','six-island']:
  item=('import_'+region.replace('-','_')+'_items.py') if region!='six-island' else 'import-six-island-items.py'
  steps.append((region+'-items',['scripts/imports/items/'+item,str(ref),str(root),str(root)]))
 if region in ['four-island','six-island']:
  steps.append((region+'-encounters',['scripts/imports/story/import-'+region+'-encounters.py',str(ref),str(root)]))
 steps.append((region+'-presentation',['scripts/imports/presentation/import-'+region+'-presentation.py','--reference-root',str(ref),'--project-root',str(root)]+(['--dependency-root',str(root)] if region in ['six-island','five-island'] else [])))
steps.extend([
 ("celio-pokemon",["scripts/imports/pokemon/import-celio-pokemon.py",str(ref),str(root),str(root)]),
 ("celio-story",["scripts/imports/story/import-celio-story.py",str(ref),str(root)]),
 ("celio-presentation",["scripts/imports/presentation/import-celio-presentation.py","--reference-root",str(ref),"--dependency-root",str(root),"--project-root",str(root)]),
 ("roamer-contract",["scripts/imports/story/import-roamer-lifecycle-contract.py","--reference-root",str(ref)])])
# Shared source compilation plus final compact-byte validation. Individual
# regional producers already emit shared references; no expansion bridge exists.
steps = [("shared-item-source", ["scripts/imports/items/import-item-catalog.py", "--project-root", str(root)])] + [("shared-machine-source", ["scripts/imports/items/import-machine-catalog.py", "--project-root", str(root), "--references-only"])] + steps + [
 ("shared-machine-views", ["scripts/imports/items/import-machine-catalog.py", "--project-root", str(root), "--references-only"]),
 ("shared-machine-check", ["scripts/imports/items/import-machine-catalog.py", "--project-root", str(root), "--references-only", "--check"]),
 ("current-items", ["scripts/imports/items/import-current-items.py", "--project-root", str(root)]),
 ("source-battle-items", ["scripts/imports/items/import-source-battle-items.py", "--project-root", str(root)]) ]
results=[]
for label,args in steps:
 start=time.monotonic();run=subprocess.run([sys.executable,*args],cwd=root,capture_output=True,text=True)
 (out/(label+'.log')).write_text(run.stdout+run.stderr)
 result={'step':label,'exitCode':run.returncode,'seconds':round(time.monotonic()-start,3)};results.append(result)
 (out/'progress.json').write_text(json.dumps(results,indent=2)+'\n');print(json.dumps(result),flush=True)
 if run.returncode:raise SystemExit(run.stdout[-1200:]+run.stderr[-1200:])
print(json.dumps({'status': 'completed', 'steps': len(results)}))
