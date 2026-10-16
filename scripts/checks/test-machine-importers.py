"""Direct early machine importer parity against compact runtime membership."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util,json,sys,pathlib,unittest
from unittest.mock import patch
root=_PROJECT_ROOT;sys.path.insert(0,str(root/'scripts/lib'));raw=root/'content/raw/pokefirered/037335f4'
from item_catalog import item_json
def load(name, family="items"):
 s=importlib.util.spec_from_file_location(name,root/'scripts/imports'/family/('import-'+name+'.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def refs(v):
 if isinstance(v,list):return [r for x in v for r in refs(x)]
 if not isinstance(v,dict):return []
 if set(v)=={'item','species'} and isinstance(v['item'],str):return [v]
 return [r for x in v.values() for r in refs(x)]
def wrappers(value):
 if isinstance(value,list):return [row for child in value for row in wrappers(child)]
 if not isinstance(value,dict):return []
 if 'description' in value and isinstance(value.get('item'),str) and set(value)=={'item','description'}:
  assert set(value)=={'item','description'}, 'Retired item proof fields emitted'
  return [value]
 return [row for child in value.values() for row in wrappers(child)]

class EarlyMachineImporterTests(unittest.TestCase):
 def test_direct_shared_references_match_runtime_membership(self):
  for name, family, folder in [('pewter-machines', 'items', 'data/machine-learning/pewter.json'), ('mtmoon-items', 'items', 'data/items/mtmoon'), ('cerulean-machines', 'items', 'data/machine-learning/cerulean.json'), ('bill-machines', 'items', 'data/machine-learning/bill.json'), ('vermilion-machines', 'items', 'data/machine-learning/vermilion.json'), ('ssanne-machines', 'items', 'data/machine-learning/ssanne.json'), ('route11-machines', 'items', 'data/machine-learning/route11.json'), ('rocktunnel-items', 'items', 'data/items/rocktunnel'), ('celadon-gym-catalog', 'items', 'data/items/celadon-gym'), ('rocket-hideout-items', 'items', 'data/items/rocket-hideout')]:
   regional = name in ('cerulean-machines','bill-machines','ssanne-machines','route11-machines')
   m=load('machine-views' if regional else name, family)
   if regional:outputs=m.convert(name.removesuffix('-machines'),root,raw)
   elif name=='pewter-machines':
    lock=m.read_lock();d={x['path']:(raw/x['path']).read_bytes() for x in lock['files']};doc,_=m.build(lock,d);outputs={'pewter-machines.json':json.dumps(doc,default=item_json).encode()}
   elif name=='vermilion-machines':outputs={'machines.json':json.dumps(m.convert({p:(raw/p).read_bytes() for p in m.PATHS})).encode()}
   elif name=='celadon-gym-catalog':
    lock=json.loads((root/'scripts/recipes/items/celadon-gym-items.lock.json').read_bytes());d={x['path']:(raw/x['path']).read_bytes() for x in lock['files']};outputs=m.item_outputs(lock,d,root)
   elif name=='rocket-hideout-items':
    lock=json.loads((root/'scripts/recipes/items/rocket-hideout-items.lock.json').read_bytes());d={x['path']:(raw/x['path']).read_bytes() for x in lock['files']};outputs=m.outputs(lock,d,root)
   else:
    lock=m.lock_read();outputs=m.convert(lock,m.read_sources(lock,raw))
   for p,b in outputs.items():
    if not p.endswith('.json'):continue
    actual=refs(json.loads(b));expected=refs(json.loads((root/'content/generated'/folder if folder.endswith('.json') else root/'content/generated'/folder/p).read_bytes()))
    assert actual==expected,(name,p,len(actual),len(expected))
    assert wrappers(json.loads(b))==wrappers(json.loads((root/'content/generated'/folder if folder.endswith('.json') else root/'content/generated'/folder/p).read_bytes())),(name,p,'item values differ')


def git(args,**kwargs):
 target=next(x for x in args if x.startswith('037335f4c725d7c9aecdac87066f2002b4bd7e14:')).split(':',1)[1]
 return (raw/target).read_bytes()

class LateMachineImporterTests(unittest.TestCase):
 def test_source_projections_preserve_gameplay_and_shared_references(self):
  for name, family, folder in [('power-plant-pokemon', 'pokemon', 'data/pokemon/power-plant.json')]:
   m=load(name, family)
   if name=='power-plant-pokemon':
    # Exercise its explicit retained-raw reader; no Git transport is needed.
    with patch('subprocess.run', side_effect=AssertionError('Raw conversion invoked a process')):
     result=m.generate(None,root,raw=raw)
   else:
    with patch('subprocess.check_output',git):result=m.generate(root,root)
   if name=='power-plant-pokemon':docs={'catalog.json':result[0]}
   elif isinstance(result,tuple):docs=result[0]
   else:docs=result
   for file,value in docs.items():
    value=json.loads(json.dumps(value,default=item_json))
    a=refs(value)
    if not a:continue
    b=refs(json.loads((root/'content/generated'/folder if folder.endswith('.json') else root/'content/generated'/folder/file).read_bytes()));assert a==b,(name,file,len(a),len(b))
    def clean(v):
     if isinstance(v,list):return [clean(x) for x in v]
     if not isinstance(v,dict):return v
     return {k:clean(x) for k,x in v.items() if k not in ('inputs','dependencyInputs','rulesSources','inheritedInputs','sourceFiles')}
    expected=json.loads((root/'content/generated'/folder if folder.endswith('.json') else root/'content/generated'/folder/file).read_bytes())
    assert clean(value)==clean(expected),(name,file,'non-proof document differs')


if __name__ == "__main__": unittest.main()
