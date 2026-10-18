"""Extract native Role Play initializer/helper/phase callbacks; no upstream scripts execute."""
import argparse,hashlib,json,subprocess,sys,tempfile
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--repo',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a=p.parse_args();here=Path(__file__).resolve().parent
if a.output.exists():raise FileExistsError(a.output)
sys.path.insert(0,str(a.repo/'tests/Reference'));sys.path.insert(0,str(a.repo/'tests/Reference/CallerPrelude'))
from reference_c import compile_c,verify_reference
from source_extract import function,macros,identity,COMMIT
verify_reference(a.reference)
names={'src/battle_anim_effects_3.c':['AnimTask_RolePlaySilhouette','AnimTask_RolePlaySilhouette_Step1','AnimTask_RolePlaySilhouette_Step2'],
'src/battle_anim_mons.c':['CreateAdditionalMonSpriteForMoveAnim','GetBattlerSpriteBGPriority','TrySetSpriteRotScale','TryResetSpriteAffineState','DestroyAnimVisualTaskAndDisableBlend'],
'src/decompress.c':['LoadSpecialPokePic_DontHandleDeoxys']}
paths=list(names)+['include/gba/types.h','include/gba/io_reg.h','include/constants/battle_anim.h'];pins=json.loads((here/'pinned-inputs.json').read_text());s={path:(a.reference/path).read_text() for path in paths}
for path in paths:
 if hashlib.sha256((a.reference/path).read_bytes()).hexdigest()!=pins[path]:raise ValueError('Pinned source mismatch '+path)
functions=[];extracted=[]
for path,ns in names.items():
 for name in ns:
  f=function(s[path],name);functions.append(f);at=s[path].index(f.rstrip());extracted.append(dict(path=path,name=name,startLine=s[path][:at].count('\n')+1,endLine=s[path][:at+len(f.rstrip())].count('\n')+1,sha256=hashlib.sha256(f.encode()).hexdigest()))
prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in functions)
oam=s['include/gba/types.h'];types=oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
defs=macros(list(s.values()),['ST_OAM_OBJ_BLEND','ST_OAM_AFFINE_DOUBLE_MASK','ANIM_ATTACKER','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDALPHA_BLEND'])
c=(here/'prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@FUNCTIONS@','\n'.join(functions))
build=(a.build_dir or Path(tempfile.mkdtemp(prefix='role-play-oracle-'))).resolve();build.mkdir(parents=True,exist_ok=True);(build/'reference.c').write_text(c)
for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(build/'reference.c'),'-o',str(build/opt)])
cases=[]
for d in [0,1]:
 for actor in range(4 if d else 2):
  for target in ([actor^1] if not d else [actor^1,actor^2,actor^3]):
   for species,transformed,letter in [(25,0,0),(201,0,0),(201,0,1),(201,1,1),(410,0,0),(385,1,0)]:
    args=[d,actor,target,species,transformed,letter];runs=[subprocess.check_output([str(build/opt),*map(str,args)]) for opt in ['O0','O2']];assert runs[0]==runs[1],args;rows=[json.loads(r) for r in runs[0].splitlines()];assert not rows[-1]['alive'];cases.append(dict(id=f'role-play-{len(cases):03}',doubles=bool(d),actor=actor,target=target,species=species,transformed=bool(transformed),letter=letter,rows=rows))
def representative(c):
    # All six species/transformation/Unown-letter paths once, plus reversed
    # side, selected ally and cross-position doubles with front/back tables.
    return not c['doubles'] and c['actor'] == 0 or (
        c['doubles'] and (c['actor'], c['target'], c['species'], c['transformed'], c['letter']) in [
            (3, 2, 25, False, 0), (3, 1, 201, False, 1), (2, 0, 410, False, 0)])

if not a.full:
    cases = [c for c in cases if representative(c)]
result=dict(profile='full' if a.full else 'representative',schema='firered-role-play-source-v1',commit=COMMIT,sources=identity(a.reference,paths),extractedFunctions=extracted,optimizationParity=['O0','O2'],semantics={'frame0':'Native initializer; following rows capture OAM before invoking the source task.','allocation':'A free sprite, matrix and palette exist. Source ignores allocator sentinels and would access invalid cells if exhausted; product validates bounded scene closure before effects.','identity':'Real helper and decompression selector are extracted. Front/back tables are marker resources; first DMA size0x800 is asserted. Unown base coordinates differ deliberately from the selected letter.','hardware':'Native matrix inputs have rotation0, making BIOS ObjAffineSet identity-angle exact integer copy; all phase inputs extracted. Palette filled white once; free keeps bank colors intact.','bitmap':'Separate import tests validate local Deoxys first frame and Castform base input. Spinda changes only nonzero indices and does not change white silhouette.'},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');report=dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),terminalFrames=sorted({c['rows'][-1]['frame'] for c in cases}),fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest());(build/'verification.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
