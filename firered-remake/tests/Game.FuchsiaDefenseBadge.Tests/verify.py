#!/usr/bin/env python3
"""Focused source/profile regression checks with the repository's shared build lock."""
import fcntl, hashlib, json, os, pathlib, subprocess, sys
root=pathlib.Path(__file__).resolve().parents[2]
out=pathlib.Path(__file__).resolve().parent/'evidence'; out.mkdir(exist_ok=True)
toolroot=pathlib.Path(os.environ.get('FIRERED_TOOLCHAIN_ROOT',(root/'.tools/toolchain-root').read_text().strip()))
dotnet=toolroot/'dotnet/dotnet'; env=os.environ.copy()
env.update(DOTNET_ROOT=str(dotnet.parent),DOTNET_CLI_HOME=str(root/'.toolhome'),NUGET_PACKAGES=str(root/'.toolhome/nuget'),DOTNET_NOLOGO='1',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_MULTILEVEL_LOOKUP='0')
results=[]
def step(name,args):
    proc=subprocess.run(list(map(str,args)),cwd=root,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    (out/(name+'.log')).write_text(proc.stdout)
    results.append({'name':name,'command':list(map(str,args)),'exitCode':proc.returncode})
    print(name+': '+str(proc.returncode),flush=True)
    if proc.returncode: print(proc.stdout); raise RuntimeError(name)
    return proc.stdout
try:
    step('sdk',[dotnet,'--version']); step('runtime',[dotnet,'--list-runtimes'])
    if len(sys.argv)!=2: raise ValueError('Pass pinned reference repository root.')
    step('source-oracle',[sys.executable,'tests/Game.FuchsiaDefenseBadge.Tests/generate-source-vectors.py','--reference-root',sys.argv[1],'--check'])
    for suite in ['Game.FuchsiaDefenseBadge.Tests','Game.TransformBattle.Tests','Game.CalledPartyEffects.Tests','Game.CalledVolatileEffects.Tests']:
        with open('/tmp/firered-remake-build.lock','a') as lock:
            fcntl.flock(lock,fcntl.LOCK_EX)
            step(suite+'.build',[dotnet,'build','tests/'+suite+'/'+suite+'.csproj','-p:RestoreLockedMode=true','-m:1','-nr:false'])
            args=[dotnet,'tests/'+suite+'/bin/Debug/net10.0/'+suite+'.dll',root]
            if suite=='Game.TransformBattle.Tests':args.append(root)
            output=step(suite+'.run',args)
            assert output.strip(),suite+' empty output'
    paths=[root/'src/Game.Domain/WildBattle'/x for x in ['WildBattleContracts.cs','ForestBattleDamage.cs','WildBattleEarlyPartyExecution.cs','WildBattleCalledPartyEffects.cs','WildBattleRules.cs']]
    paths += list((root/'tests/Game.FuchsiaDefenseBadge.Tests').glob('*.*'))
    manifest={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths if p.is_file()}
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
finally:
    (out/'commands.json').write_text(json.dumps(results,indent=2)+'\n')
assert len(results)==11 and all(x['exitCode']==0 for x in results)
print(json.dumps({'passed':True,'commands':len(results),'suites':4}))
