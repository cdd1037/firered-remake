"""Direct early machine importer parity against compact runtime membership."""
import importlib.util,json,sys,pathlib,unittest
from unittest.mock import patch
root=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'scripts'));raw=root/'content/raw/pokefirered/037335f4'
def load(name):
 s=importlib.util.spec_from_file_location(name,root/'scripts'/('import-'+name+'.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def refs(v):
 if isinstance(v,list):return [r for x in v for r in refs(x)]
 if not isinstance(v,dict):return []
 if set(v)=={'item','species'} and isinstance(v['item'],str):return [v]
 return [r for x in v.values() for r in refs(x)]
class EarlyMachineImporterTests(unittest.TestCase):
 def test_direct_shared_references_match_runtime_membership(self):
  for name in ['pewter-machines','mtmoon-items','cerulean-machines','bill-machines','vermilion-machines','ssanne-machines','route11-machines','rocktunnel-items','celadon-gym-catalog','rocket-hideout-items']:
   m=load(name)
   if name=='pewter-machines':
    lock=m.read_lock();d={x['path']:(raw/x['path']).read_bytes() for x in lock['files']};doc,_=m.build(lock,d);outputs={'pewter-machines.json':json.dumps(doc).encode()};folder=name
   elif name=='vermilion-machines':outputs={'machines.json':json.dumps(m.convert({p:(raw/p).read_bytes() for p in m.PATHS})).encode()};folder=name
   elif name=='celadon-gym-catalog':
    lock=json.loads((root/'scripts/celadon-gym-items.lock.json').read_bytes());d={x['path']:(raw/x['path']).read_bytes() for x in lock['files']};outputs=m.item_outputs(lock,d,root);folder='celadon-gym-items'
   elif name=='rocket-hideout-items':
    lock=json.loads((root/'scripts/rocket-hideout-items.lock.json').read_bytes());d={x['path']:(raw/x['path']).read_bytes() for x in lock['files']};outputs=m.outputs(lock,d,root);folder=name
   else:
    lock=m.lock_read();outputs=m.convert(lock,m.read_sources(lock,raw));folder=name
   for p,b in outputs.items():
    if not p.endswith('.json'):continue
    actual=refs(json.loads(b));expected=refs(json.loads((root/'content/generated'/folder/p).read_bytes()))
    assert actual==expected,(name,p,len(actual),len(expected))
  

def git(args,**kwargs):
 target=next(x for x in args if x.startswith('037335f4c725d7c9aecdac87066f2002b4bd7e14:')).split(':',1)[1]
 return (raw/target).read_bytes()

class LateMachineImporterTests(unittest.TestCase):
 def test_source_projections_preserve_gameplay_and_shared_references(self):
  for name in ['power-plant-pokemon','fighting-dojo-rules','fossil-lab-rules','npc-trade-owned-rules','seafoam-rules','super-rod-species']:
   m=load(name)
   with patch('subprocess.check_output',git):result=m.generate(root,root)
   if name=='power-plant-pokemon':docs={'catalog.json':result[0]}
   elif isinstance(result,tuple):docs=result[0]
   else:docs=result
   for file,value in docs.items():
    a=refs(value)
    if not a:continue
    b=refs(json.loads((root/'content/generated'/name/file).read_bytes()));assert a==b,(name,file,len(a),len(b))
    def clean(v):
     if isinstance(v,list):return [clean(x) for x in v]
     if not isinstance(v,dict):return v
     return {k:clean(x) for k,x in v.items() if k not in ('inputs','dependencyInputs','rulesSources','inheritedInputs','sourceFiles')}
    expected=json.loads((root/'content/generated'/name/file).read_bytes())
    assert clean(value)==clean(expected),(name,file,'non-proof document differs')
  

if __name__ == "__main__": unittest.main()
