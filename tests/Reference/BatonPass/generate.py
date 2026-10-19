#!/usr/bin/env python3
"""Explicit compact original-C Baton Pass oracle. Ordinary tests only read vectors."""
import argparse,hashlib,importlib.util,json,subprocess,tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out):
 helper=module('baton_reference',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 extract=module('baton_extract',root/'tests/Reference/source_extract.py')
 pins=json.loads((HERE/'pinned-inputs.json').read_text());sources={}
 for path,digest in pins.items():
  data=(reference/path).read_bytes()
  if hashlib.sha256(data).hexdigest()!=digest:raise ValueError('Pinned source changed: '+path)
  if path.endswith(('.c','.h','.s')):sources[path]=data.decode()
 names={'src/battle_anim_effects_3.c':['AnimBatonPassPokeball'],'src/battle_anim_mons.c':['GetBattlerSpriteFinal_Y','GetAnimBattlerSpriteId','SetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped','PrepareBattlerSpriteForRotScale','ResetSpriteRotScale'],'src/battle_anim.c':['IsBattlerSpriteVisible']}
 functions=[extract.function(sources[path],name) for path,ns in names.items() for name in ns]
 prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in functions)
 source=(HERE/'harness.c.in').read_text().replace('@PROTOTYPES@',prototypes).replace('@FUNCTIONS@','\n'.join(functions))
 path=out/'baton-pass.c';path.write_text(source);executables=[]
 for opt in ['O0','O2']:
  executable=out/opt;helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(executable)]);executables.append(executable)
 cases=[]
 # Six canonical actor layouts and six bounded flag/coordinate variations.
 for doubles,actor in [(0,0),(0,1),(1,0),(1,1),(1,2),(1,3)]:
  for variant in range(2):
   y,elevation=(7,0) if variant==0 else (31,64)
   flags=0 if variant==0 else (actor%3)+1
   args=[doubles,actor,y,elevation,flags]
   outputs=[subprocess.check_output([str(exe),*map(str,args)],text=True) for exe in executables]
   if outputs[0]!=outputs[1]:raise ValueError('Native optimization parity differs: '+str(args))
   rows=[json.loads(line) for line in outputs[0].splitlines()]
   if not rows or rows[-1]['alive'] or rows[-1]['visualCount'] or not rows[-1]['actorInvisible']:raise ValueError('Incomplete native lifecycle')
   cases.append(dict(doubles=bool(doubles),actor=actor,yOffset=y,elevation=elevation,flags=flags,rows=rows))
 return dict(schema='firered-baton-pass-v1',sourceSha256=pins,sourceFunctions=names,optimizationParity=['O0','O2'],generatedSha256=hashlib.sha256(source.encode()).hexdigest(),semantics={'frame0':'Source callback case0 at creation; later rows call the unchanged source callback once.','stubs':'Standard canonical coordinates; species geometry supplied at the existing coordinate seam. Actual source visibility, sprite selection, prepare/reset and rot-scale functions are extracted. ObjAffineSet admits only rotation0, for which the matrix is exactly diagonal. Center-vector is unused; no contest. Dummy frame has tile0 and no timing effect.','identity':'Battler physical sprite IDs are 4,9,13,22; ball ID2. This deliberately differs from canonical actor slots.','writes':'Every native ObjAffineSet write is retained, including the intermediate fallthrough matrix and 1120/466 immediately before reset.','offsets':'Actor raw position and x2/y2 start shifted and remain untouched; ball uses source picture coordinates.'},cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='baton-pass-reference-'));out.mkdir(parents=True,exist_ok=True)
 result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(result['cases']),rows=sum(len(c['rows']) for c in result['cases']))))
