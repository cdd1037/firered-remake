#!/usr/bin/env python3
"""Regenerate the source-only postgame delta over a restored full stage10 base. No reference scripts are executed."""
import argparse,sys,json,subprocess,hashlib,time
from pathlib import Path
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project-root',type=Path,default=Path(__file__).resolve().parents[1]);parser.add_argument('--reference-root',required=True,type=Path);parser.add_argument('--output',required=True,type=Path);options=parser.parse_args()
root=options.project_root.resolve();ref=options.reference_root.resolve();out=options.output.resolve();out.mkdir(parents=True,exist_ok=False)
steps=[("field-strength-art",["scripts/import-field-strength-art.py","--reference-root",str(ref)]),("field-surf-art",["scripts/import-field-surf-art.py","--reference-root",str(ref)]),("national-dex-source",["scripts/import-national-dex-source.py","--reference-root",str(ref),"--project-root",str(root)])]
for region in ['sevii-ruby','four-island','six-island','five-island']:
 steps.append((region+'-region',['scripts/import-'+region+'-region.py','--reference-root',str(ref),'--dependency-root',str(root),'--project-root',str(root)]))
 args=[str(ref),str(root)]+([str(root)] if region=='sevii-ruby' else [])+[str(root)]
 steps.append((region+'-rules',['scripts/import-'+region+'-rules.py']+args))
 if region in ['sevii-ruby','four-island','six-island']:
  item=('import_'+region.replace('-','_')+'_items.py') if region!='six-island' else 'import-six-island-items.py'
  steps.append((region+'-items',['scripts/'+item,str(ref),str(root),str(root)]))
 if region in ['four-island','six-island']:
  steps.append((region+'-encounters',['scripts/import-'+region+'-encounters.py',str(ref),str(root)]))
 steps.append((region+'-presentation',['scripts/import-'+region+'-presentation.py','--reference-root',str(ref),'--project-root',str(root)]+(['--dependency-root',str(root)] if region in ['six-island','five-island'] else [])))
steps.extend([
 ("celio-pokemon",["scripts/import-celio-pokemon.py",str(ref),str(root),str(root)]),
 ("celio-story",["scripts/import-celio-story.py",str(ref),str(root)]),
 ("celio-presentation",["scripts/import-celio-presentation.py","--reference-root",str(ref),"--dependency-root",str(root),"--project-root",str(root)]),
 ("roamer-contract",["scripts/import-roamer-lifecycle-contract.py","--reference-root",str(ref)])])
# Shared source compilation plus final compact-byte validation. Individual
# regional producers already emit shared references; no expansion bridge exists.
steps = [("shared-item-source", ["scripts/import-item-catalog.py", "--project-root", str(root)])] + [("shared-machine-source", ["scripts/import-machine-catalog.py", "--project-root", str(root), "--references-only"])] + steps + [
 ("shared-machine-views", ["scripts/import-machine-catalog.py", "--project-root", str(root), "--references-only"]),
 ("shared-machine-check", ["scripts/import-machine-catalog.py", "--project-root", str(root), "--references-only", "--check"]),
 ("current-items", ["scripts/import-current-items.py", "--project-root", str(root)]),
 ("source-battle-items", ["scripts/source-battle-items/import.py", "--project-root", str(root)]) ]
before={str(f.relative_to(root)):hashlib.sha256(f.read_bytes()).hexdigest() for f in (root/'src').rglob('*') if f.is_file() and f.suffix in ('.cs','.csproj') and not set(f.parts)&{'bin','obj','.godot'}}
results=[]
for label,args in steps:
 start=time.monotonic();run=subprocess.run([sys.executable,*args],cwd=root,capture_output=True,text=True)
 (out/(label+'.log')).write_text(run.stdout+run.stderr)
 result={'step':label,'exitCode':run.returncode,'seconds':round(time.monotonic()-start,3)};results.append(result)
 (out/'progress.json').write_text(json.dumps(results,indent=2)+'\n');print(json.dumps(result),flush=True)
 if run.returncode:raise SystemExit(run.stdout[-1200:]+run.stderr[-1200:])
changed=[rel for rel,sha in before.items() if hashlib.sha256((root/rel).read_bytes()).hexdigest()!=sha]
(out/'result.json').write_text(json.dumps({'passed':not changed,'steps':results,'productSourceUnchanged':not changed,'changedSource':changed},indent=2)+'\n')
if changed:raise SystemExit('regeneration changed product code: '+str(changed))
