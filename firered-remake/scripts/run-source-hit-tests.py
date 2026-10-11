#!/usr/bin/env python3
import argparse,os,subprocess
from pathlib import Path
root=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('output',type=Path);a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
tools=Path(os.environ.get('FIRERED_TOOLCHAIN_ROOT',(root/'.tools/toolchain-root').read_text().strip()));env=dict(os.environ,FIRERED_TOOLCHAIN_ROOT=str(tools),DOTNET_ROOT=str(tools/'dotnet'),DOTNET_CLI_HOME=str(root/'.toolhome'),NUGET_PACKAGES=str(tools/'nuget-cache'),DOTNET_CLI_TELEMETRY_OPTOUT='1')
project=root/'tests/SourceHitAnimation.Tests'
steps=[('oracle.log',['python3',root/'scripts/run-source-hit-oracle.py',out/'oracle']),
 ('build.log',['flock','/tmp/firered-remake-build.lock',tools/'dotnet/dotnet','build',project,'--disable-build-servers','-m:1','-nr:false','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-p:RestoreLockedMode=true','-p:RestoreConfigFile='+str(project/'NuGet.Config')]),
 ('tests.log',[tools/'dotnet/dotnet',project/'bin/Debug/net10.0/SourceHitAnimation.Tests.dll',root,out/'oracle/source-c.csv',out])]
for name,cmd in steps:
 r=subprocess.run([str(x) for x in cmd],cwd=root,env=env,capture_output=True,text=True,timeout=240);(out/name).write_text(r.stdout+r.stderr);print(r.stdout+r.stderr,flush=True)
 if r.returncode:raise SystemExit(r.returncode)
