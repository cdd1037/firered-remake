#!/usr/bin/env python3
import argparse,os,subprocess
from pathlib import Path
root=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('output',type=Path);p.add_argument('--stats',action='store_true');a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
tools=Path(os.environ.get('FIRERED_TOOLCHAIN_ROOT',(root/'.tools/toolchain-root').read_text().strip()));env=dict(os.environ,FIRERED_TOOLCHAIN_ROOT=str(tools),DOTNET_ROOT=str(tools/'dotnet'),DOTNET_CLI_HOME=str(root/'.toolhome'),NUGET_PACKAGES=str(tools/'nuget-cache'),DOTNET_CLI_TELEMETRY_OPTOUT='1')
project=root/'tests/SourceHitAnimation.Tests'
steps=[('oracle.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'oracle']),
 ('double-oracle.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'double-oracle','--doubles']),
 ('vacant-oracle.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'vacant-oracle','--doubles','--vacant-partner']),
 ('motion-vectors.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'motion-vectors','--doubles','--motion-vectors']),
 ('build.log',['flock','/tmp/firered-remake-build.lock',tools/'dotnet/dotnet','build',project,'--disable-build-servers','-m:1','-nr:false','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-p:RestoreLockedMode=true','-p:RestoreConfigFile='+str(project/'NuGet.Config')]),
 ('tests.log',[tools/'dotnet/dotnet',project/'bin/Debug/net10.0/SourceHitAnimation.Tests.dll',root,out/'oracle/source-c.csv',out,out/'double-oracle/source-c.csv',out/'vacant-oracle/source-c.csv',out/'motion-vectors/source-c.csv'])]
if a.stats:
 steps=[('oracle.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'oracle','--stats']),
 ('double-oracle.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'double-oracle','--stats','--doubles']),
 ('vacant-oracle.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'vacant-oracle','--stats','--doubles','--vacant-partner']),steps[4],
 ('tests.log',[tools/'dotnet/dotnet',project/'bin/Debug/net10.0/SourceHitAnimation.Tests.dll',root,'--stats',out])]
for name,cmd in steps:
 r=subprocess.run([str(x) for x in cmd],cwd=root,env=env,capture_output=True,text=True,timeout=240);(out/name).write_text(r.stdout+r.stderr);print(r.stdout+r.stderr,flush=True)
 if r.returncode:raise SystemExit(r.returncode)
