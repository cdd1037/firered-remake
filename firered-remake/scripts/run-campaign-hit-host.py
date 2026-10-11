#!/usr/bin/env python3
"""Build linked production CampaignView host. --native requires coordinator GUI slot."""
import argparse,json,os,subprocess,hashlib
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('output',type=Path);p.add_argument('--native',action='store_true');p.add_argument('--skip-build',action='store_true');p.add_argument('--quit-only',action='store_true');p.add_argument('--no-audio',action='store_true');p.add_argument('--quit-repeats',type=int,default=1);p.add_argument('--substitute-back-only',action='store_true');p.add_argument('--slices',action='store_true');p.add_argument('--doubles',action='store_true');p.add_argument('--motion',action='store_true');p.add_argument('--stats',action='store_true');p.add_argument('--stat-mask',action='store_true');p.add_argument('--stat-moves',action='store_true');a=p.parse_args()
root=Path(__file__).resolve().parent.parent;out=a.output.resolve()
if out.exists():raise SystemExit('Output must be new; keep failed evidence')
out.mkdir(parents=True);host=root/'tests/CampaignHitHost'
tools=Path(os.environ.get('FIRERED_TOOLCHAIN_ROOT',(root/'.tools/toolchain-root').read_text().strip()));dotnet=tools/'dotnet/dotnet';godot=tools/'godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64'
env=dict(os.environ);env.update(FIRERED_TOOLCHAIN_ROOT=str(tools),DOTNET_ROOT=str(tools/'dotnet'),DOTNET_CLI_HOME=str(root/'.toolhome'),NUGET_PACKAGES=os.environ.get('NUGET_PACKAGES',str(tools/'nuget-cache')),PATH=str(tools/'dotnet')+os.pathsep+env['PATH'],FIRERED_PROJECT_ROOT=str(root),FIRERED_FUCHSIA_GYM='1',FIRERED_AUDIO='1',FIRERED_AUDIO_BACKEND='synth',FIRERED_PLAYER_ENTRY='0',FIRERED_EVIDENCE=str(out/'campaign'),HIT_CAMPAIGN_EVIDENCE=str(out),GODOT_SILENCE_ROOT_WARNING='1',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
if a.quit_only:env.update(HIT_QUIT_ONLY='1',HIT_QUIT_REPEATS=str(a.quit_repeats))
if a.no_audio:env['FIRERED_AUDIO']='0'
if a.substitute_back_only:env['HIT_SUBSTITUTE_BACK_ONLY']='1'
if a.slices:env['HIT_SLICE_GROUP']='1'
if a.stat_moves:env.update(HIT_STAT_MOVE_GROUP='1',FIRERED_PRESET_DOUBLES='1')
if a.stat_mask:env.update(HIT_STAT_MASK_GROUP='1',FIRERED_PRESET_DOUBLES='1')
if a.stats:env.update(HIT_STAT_GROUP='1',FIRERED_PRESET_DOUBLES='1')
if a.motion:env.update(HIT_MOTION_GROUP='1',FIRERED_PRESET_DOUBLES='1')
if a.doubles:env.update(HIT_DOUBLE_GROUP='1',FIRERED_PRESET_DOUBLES='1')
for key in ['FIRERED_CAMPAIGN_CHECKPOINT','FIRERED_SOURCE_HIT_ANIMATIONS']:env.pop(key,None)
for key,name in [('XDG_DATA_HOME','data'),('XDG_CONFIG_HOME','config'),('XDG_CACHE_HOME','cache')]:env[key]=str(out/name)
(out/'campaign').mkdir();(host/'Assets').mkdir(exist_ok=True)
font=host/'Assets/ProbeFont.ttc'
if not font.exists():font.symlink_to('../../../src/Game.Godot/Assets/ProbeFont.ttc')
config=out/'nuget.config';config.write_text('<configuration><packageSources><clear/><add key="official" value="'+str(tools/'godot/Godot_v4.7.2-stable_mono_linux_x86_64/GodotSharp/Tools/nupkgs')+'"/></packageSources></configuration>')
import xml.etree.ElementTree as ET
ET.parse(config)
def run(cmd,name):
 try:result=subprocess.run([str(x) for x in cmd],cwd=root,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=90 if a.quit_only and name=='runtime.log' else 240)
 except subprocess.TimeoutExpired as error:
  (out/name).write_bytes(error.stdout or b'');(out/'timeout.json').write_text(json.dumps(dict(passed=False,timeout=error.timeout,stage=name)));raise
 (out/name).write_text(result.stdout);print(name+': exit '+str(result.returncode)+'; '+str(len(result.stdout))+' logged characters')
 if result.returncode:raise SystemExit(result.returncode)
 if name=='runtime.log' and any(x in result.stdout for x in ['Leaked instance:','ObjectDB instance','ERROR:']):raise SystemExit('Host engine error/leak')
def source_manifest():
 files=[]
 for assembly in ['Game.Domain','Game.Application','Game.Content','Game.Godot']:
  files.extend(p for p in (root/'src'/assembly).glob('**/*.cs') if not any(x in p.parts for x in ['bin','obj','.godot']))
 files.extend(host.glob('*.cs'));files.extend([root/'tests/Shared/SourceHitCampaignFixture.cs',root/'tests/Shared/SourceDoubleHitFixture.cs'])
 return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
sources=source_manifest();(out/'source-manifest.json').write_text(json.dumps(sources,indent=2)+'\n')
built=host/'.build-source-manifest.json'
if a.skip_build:
 if not built.exists() or json.loads(built.read_text())!=sources:raise SystemExit('Skip-build sources differ from verified compiled host')
if not a.skip_build:
 run(['flock','/tmp/firered-remake-build.lock',dotnet,'build',host/'CampaignHitHost.csproj','--configfile',config,'--disable-build-servers','-m:1','-nr:false','-p:UseSharedCompilation=false','-p:NuGetAudit=false','-p:RestoreLockedMode=true'],'build.log')
 built.write_text(json.dumps(sources,indent=2)+'\n')
 run([godot,'--headless','--editor','--import','--path',host,'--quit'],'import.log')
assembly=host/'.godot/mono/temp/bin/Debug/CampaignHitHost.dll'
assembly_sha=hashlib.sha256(assembly.read_bytes()).hexdigest()
# Persist executable identity before launch so timeout/failure evidence is bound too.
(out/'assembly-identity.json').write_text(json.dumps(dict(sha256=assembly_sha,godotSha256=hashlib.sha256(godot.read_bytes()).hexdigest(),sourceFiles=len(sources)),indent=2)+'\n')
run([godot,*([] if a.native else ['--headless']),'--audio-driver','Dummy','--path',host,'--verbose'],'runtime.log')
assert source_manifest()==sources,'Source changed during build/host verification'
assert hashlib.sha256(assembly.read_bytes()).hexdigest()==assembly_sha,'Compiled host changed during verification'
if a.quit_only:
 assert all(json.loads((out/'quit-result.json').read_text()).values());print('PASS minimal campaign quit');raise SystemExit(0)
result=json.loads((out/'result.json').read_text());assert result['passed'] and result['runtime'].startswith('10.') and result['checks']>100 and (len(result['frames'])>=1 if a.stats or a.stat_mask or a.stat_moves else len(result['frames'])==(8 if a.motion and a.native else 20 if a.motion else 20 if a.doubles else 2 if a.substitute_back_only else 14 if a.slices else 8))
if a.doubles and not a.motion:
 baseline=json.loads((host/'Fixtures/six-script-field-hashes.json').read_text())
 observed=[{k:r[k] for k in ['label','SourceMoveId','actor','target','Frame','fieldSha256']} for r in result['frames']]
 assert observed==baseline['frames'],'Six-script four-slot frame hashes changed'
 (out/'six-script-field-regression.json').write_text(json.dumps(dict(passed=True,frames=len(observed),sourceReportSha256=baseline['sourceReportSha256'],hashes=[r['fieldSha256'] for r in observed]),indent=2)+'\n')
quit_result=json.loads((out/'quit-result.json').read_text());assert all(v for k,v in quit_result.items() if not (a.substitute_back_only and k=='pendingBeforeQuit'))
print(json.dumps(dict(passed=True,checks=result['checks'],native=a.native)))
