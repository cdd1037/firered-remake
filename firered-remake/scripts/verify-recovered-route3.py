#!/usr/bin/env python3
"""Fresh Route3 recovery verification. This drives tests; it does not replay native recordings."""
import fcntl,hashlib,json,os,pathlib,re,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
if len(sys.argv)!=2 or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,70}',sys.argv[1]):raise SystemExit('Expected one new short run name')
OUT=ROOT/'evidence/recovery-route3'/sys.argv[1]
if OUT.exists():raise SystemExit('Refusing to replace prior evidence')
OUT.mkdir(parents=True)
TOOLS=pathlib.Path(os.environ.get('FIRERED_TOOLCHAIN_ROOT') or (ROOT/'.tools/toolchain-root').read_text().strip())
REFERENCE=pathlib.Path(os.environ.get('FIRERED_ORIGINAL_REFERENCE') or (ROOT/'.tools/reference-root').read_text().strip())
ENV=os.environ|{'FIRERED_TOOLCHAIN_ROOT':str(TOOLS),'FIRERED_ORIGINAL_REFERENCE':str(REFERENCE),'DOTNET_ROOT':str(TOOLS/'dotnet'),'DOTNET_CLI_HOME':str(ROOT/'.toolhome'),'NUGET_PACKAGES':str(ROOT/'.toolhome/nuget')}
DOTNET=str(TOOLS/'dotnet/dotnet');steps=[]
def hashes():return {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((ROOT/'src').rglob('*.cs')) if not {'bin','obj','.godot'}.intersection(p.parts)}
before=hashes()
def run(name,cmd,locked=False):
 log=OUT/(name+'.log')
 with log.open('w') as stream:
  if locked:
   with open('/tmp/firered-remake-build.lock','a') as lock:
    fcntl.flock(lock,fcntl.LOCK_EX);p=subprocess.run(list(map(str,cmd)),cwd=ROOT,env=ENV,stdout=stream,stderr=subprocess.STDOUT)
  else:p=subprocess.run(list(map(str,cmd)),cwd=ROOT,env=ENV,stdout=stream,stderr=subprocess.STDOUT)
 steps.append({'name':name,'exitCode':p.returncode,'log':str(log.relative_to(ROOT))})
 if p.returncode:raise RuntimeError(name+' failed; see '+str(log))
def suite(name,*args):run(name,[DOTNET,ROOT/f'tests/{name}/bin/Debug/net10.0/{name}.dll',*args])
try:
 run('build',[DOTNET,'build','FireRed.sln','-m:1','-nr:false'],True)
 for name in ['Route3RegionRecovery','Route3PokemonRecovery','Route3Battle','Route3TrainerRecovery','Route3CatalogRecovery','MtMoonContentRecovery']:
  suite('Game.'+name+'.Tests',ROOT)
 suite('Game.Route3FieldRecovery.Tests',ROOT,REFERENCE)
 suite('Game.Route3CampaignRecovery.Tests',ROOT,OUT/'route')
 suite('Game.MagikarpRecovery.Tests',ROOT,OUT/'route/before-salesman.json',OUT/'gift')
 suite('Game.MagikarpCaptureRecovery.Tests',ROOT,OUT/'gift/natural-purchased.json',OUT/'capture')
 run('save-upgrade-cli',['python3','tests/Game.RecoverySaveUpgrade.Tests/verify.py'])
 suite('Game.Route3SynthRecovery.Tests',ROOT)
 active_cues=len(json.loads((ROOT/'content/generated/synth-audio/manifest.json').read_text())['cues'])
 if active_cues not in (151,170):raise RuntimeError('Unrecognized current synth recipe')
 run('synth-manifest',['python3','scripts/export-mtmoon-synth-manifest.py' if active_cues==170 else 'scripts/export-route3-synth-manifest.py','--check'])
 run('synth-host',['python3','tests/Game.SynthAudio.Tests/run.py'])
 run('route3-presentation',['python3','tests/Game.Route3PresentationRecovery.Tests/verify_godot.py'])
 if (ROOT/'tests/Game.MagikarpPresentationRecovery.Tests/verify_godot.py').exists():run('magikarp-presentation',['python3','tests/Game.MagikarpPresentationRecovery.Tests/verify_godot.py'])
 after=hashes()
 if after!=before:raise RuntimeError('Production source changed during verification')
 result={'passed':True,'steps':steps,'sourceHashes':before,'nativeWindowVerified':False,'audibleVerified':False}
except Exception as exc:result={'passed':False,'steps':steps,'sourceHashes':before,'error':str(exc)}
(OUT/'result.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'passed':result['passed'],'steps':len(steps),'result':str(OUT/'result.json'),'error':result.get('error')}));raise SystemExit(0 if result['passed'] else 1)
