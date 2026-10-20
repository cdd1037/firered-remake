#!/usr/bin/env python3
"""Actual-font native dialogue check; requires built Content/Domain, generated zh-Hans
font resources and official Godot/.NET from `dev env`. Defaults to real rendering;
--headless checks layout only and explicitly skips pixels. No saves/assets modified.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--headless', action='store_true')
parser.add_argument('--output', type=Path, help='Retain probe/logs/image here; defaults to a new temp directory')
args = parser.parse_args()
out = args.output.resolve() if args.output else Path(tempfile.mkdtemp(prefix='firered-battle-dialog-'))
out.mkdir(parents=True, exist_ok=True)
env = os.environ | json.loads(subprocess.check_output([sys.executable, str(ROOT/'dev'), 'env', '--json'], text=True))
pack = ROOT/'content/generated/localization/zh-Hans'
refs = [ROOT/f'src/Game.{name}/bin/Debug/net10.0/Game.{name}.dll' for name in ['Content','Domain']]
for required in [pack/'font/cjk-font.json', ROOT/'content/generated/ui/font/source-font.json', *refs]:
    if not required.is_file(): raise SystemExit(f'Missing prerequisite: {required}; build the project/import the local language pack first.')
files = ['SourceGameFont', 'SourceDialogText', 'SourceDialogSurface', 'SourceDialogPageCursor', 'QuestLogPalettePresentation']
items = ''.join(f'<Compile Include="{escape(str(ROOT/"src/Game.Godot"/(name+".cs")))}" Link="{name}.cs" />' for name in files)
items += ''.join(f'<Reference Include="{ref.stem}"><HintPath>{escape(str(ref))}</HintPath></Reference>' for ref in refs)
(out/'Probe.csproj').write_text(f'<Project Sdk="Godot.NET.Sdk/4.7.2"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDynamicLoading>true</EnableDynamicLoading></PropertyGroup><ItemGroup>{items}</ItemGroup></Project>')
shutil.copyfile(Path(__file__).with_name('BattleDialogProbe.cs'), out/'BattleDialogProbe.cs')
(out/'project.godot').write_text('''config_version=5
[application]
run/main_scene="res://Probe.tscn"
[display]
window/size/viewport_width=240
window/size/viewport_height=160
window/size/window_width_override=720
window/size/window_height_override=480
[rendering]
renderer/rendering_method="gl_compatibility"
[dotnet]
project/assembly_name="Probe"
''')
(out/'Probe.tscn').write_text('[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://BattleDialogProbe.cs" id="1"]\n[node name="Probe" type="Node2D"]\nscript=ExtResource("1")\n')
env |= {'FIRERED_PROJECT_ROOT':str(ROOT),'FIRERED_TEXT_PACK':str(pack),'FIRERED_PROBE_IMAGE':str(out/'render.png')}
for key,folder in [('XDG_DATA_HOME','data'),('XDG_CONFIG_HOME','config'),('XDG_CACHE_HOME','cache')]: env[key]=str(out/folder)
def run(command, filename, timeout):
    print('Running:', subprocess.list2cmdline([str(x) for x in command]), flush=True)
    result=subprocess.run(command,env=env,cwd=out,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=timeout)
    (out/filename).write_text(result.stdout)
    print(result.stdout, end='')
    if result.returncode or 'ERROR:' in result.stdout: raise SystemExit(result.returncode or 1)
    return result.stdout
run([env['FIRERED_DOTNET'],'build',str(out/'Probe.csproj'),'-m:1','-nr:false','-p:UseSharedCompilation=false','--source',env['FIRERED_GODOT_NUGET_SOURCE'],'--source','https://api.nuget.org/v3/index.json'],'build.log',180)
log=run([env['GODOT'],'--audio-driver','Dummy',*(['--headless'] if args.headless else []),'--path',str(out)],'probe.log',45)
if 'BATTLE_DIALOG_LAYOUT_PASS' not in log or (not args.headless and 'BATTLE_DIALOG_PIXELS_PASS' not in log): raise SystemExit('Required probe completion missing')
print('Probe artifacts:',out)
