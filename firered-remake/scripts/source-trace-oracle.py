"""Extract the shared original trace/ellipse callbacks into the existing C oracle workflow."""
import hashlib,importlib.util,json,re,subprocess

def run(root,reference,out):
 spec=importlib.util.spec_from_file_location('trace_importer',root/'scripts/import-source-trace-animations.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
 raw=mod.hit.authenticated(reference);bodies=[];identities=[]
 functions={'src/task.c':['ResetTasks','CreateTask','InsertTask','DestroyTask','RunTasks','FindFirstActiveTask'],'src/battle_anim_mon_movement.c':['AnimTask_TranslateMonEllipticalRespectSide','AnimTask_TranslateMonElliptical','AnimTask_TranslateMonElliptical_Step'],'src/battle_anim_utility_funcs.c':['AnimTask_TraceMonBlended','AnimTask_TraceMonBlended_Step','AnimMonTrace'],'src/battle_anim_mons.c':['CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet']}
 for path,names in functions.items():
  source=raw[path].decode()
  for name in names:
   m=re.search(r'^((?:static )?\w+ '+name+r'\([^;]*?\))\n\{',source,re.M);pos=m.end();depth=1
   while depth:
    if source[pos]=='{':depth+=1
    if source[pos]=='}':depth-=1
    pos+=1
   body=source[m.start():pos];bodies.append((m[1],body));identities.append(dict(path=path,name=name,line=source.count('\n',0,m.start())+1,sha256=hashlib.sha256(body.encode()).hexdigest()))
 sines=[int(float(v)*256) for v in re.findall(r'Q_8_8\((-?[\d.]+)\)',raw['src/trig.c'].decode().split('};',1)[0])]
 generated='\n'.join([(root/'tests/SourceHitAnimation.Oracle/trace-host.h').read_text(),'static const s16 sine[320]={'+','.join(map(str,sines))+'};','static s16 Sin(u8 a,s16 n){return (n*sine[a])>>8;} static s16 Cos(u8 a,s16 n){return (n*sine[a+64])>>8;}','\n'.join(s+';' for s,b in bodies),'\n'.join(b for s,b in bodies),(root/'tests/SourceHitAnimation.Oracle/trace-driver.c').read_text()])
 (out/'oracle.c').write_text(generated)
 c=subprocess.run(['cc','-std=c99','-Wall','-Wextra','-Werror','-Wno-unused-parameter',str(out/'oracle.c'),'-o',str(out/'oracle')],capture_output=True,text=True);(out/'compile.log').write_text(c.stdout+c.stderr)
 if c.returncode:raise SystemExit(c.stderr)
 c=subprocess.run([str(out/'oracle')],capture_output=True,text=True,timeout=15)
 if c.returncode:raise SystemExit(c.stderr)
 (out/'source-c.csv').write_text(c.stdout);rows=len(c.stdout.splitlines())-1
 if rows<=0:raise ValueError('Empty trace oracle')
 (out/'provenance.json').write_text(json.dumps(dict(sourceCommit=mod.COMMIT,functions=identities,rows=rows,vectors=12,hostSha256=hashlib.sha256(generated.encode()).hexdigest(),originalRuntimeCompared=False,boundary='Two shared ellipse/trace argument families; singles actors 0/1, doubles actors 0..3. Original task scheduler, clone/callback bodies. Sprite callback loop, coordinates, OAM copy and allocation are bounded platform shims. Normalized frame zero is first task callback frame, not script start. No full-script or hardware-pixel equivalence claim.'),indent=2)+'\n')
 print('PASS trace original C:',len(bodies),'functions,',rows,'frames, 12 vectors')
