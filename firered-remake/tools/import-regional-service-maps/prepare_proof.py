#!/usr/bin/env python3
"""Snapshot only fixed Domain/Content compiler inputs; never edit or build the product checkout."""
from pathlib import Path
import argparse,json,os,shutil,subprocess
ROOT=Path(__file__).resolve().parents[2]
BASE='2760a79'
p=argparse.ArgumentParser();p.add_argument('baseline',type=Path);a=p.parse_args()
env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')};env.update(GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull,GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0')
def git(*args):return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(a.baseline),*args],env=env)
commit=git('rev-parse',BASE+'^{commit}').decode().strip()
if not commit.startswith(BASE):raise ValueError('Baseline identity differs')
dest=ROOT/'proof';dest.mkdir(exist_ok=True);file_count=0
for row in git('ls-tree','-rz',commit,'--','src/Game.Domain','src/Game.Content','Directory.Build.props','global.json').split(b'\0'):
    if not row:continue
    fields,path=row.split(b'\t');mode,kind,blob=fields.decode().split();path=path.decode()
    if not(path.endswith(('.cs','.csproj','/packages.lock.json')) or path in ('Directory.Build.props','global.json')):continue
    if (mode,kind)!=('100644','blob'):raise ValueError('Unexpected source mode')
    raw=git('cat-file','blob',blob);target=dest/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
    file_count += 1
(dest/'NuGet.Config').write_text('<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear/></packageSources></configuration>\n')
shutil.copyfile(ROOT/'src/Game.Content/PretRegionalServiceMaps.cs',dest/'src/Game.Content/PretRegionalServiceMaps.cs')
tests=dest/'tests/Game.RegionalServiceMaps.Tests';tests.mkdir(parents=True,exist_ok=True)
for name in ('Program.cs','Game.RegionalServiceMaps.Tests.csproj','packages.lock.json'):shutil.copyfile(ROOT/'tests/Game.RegionalServiceMaps.Tests'/name,tests/name)
print(json.dumps(dict(baseline=commit,files=file_count,destination=str(dest))))
