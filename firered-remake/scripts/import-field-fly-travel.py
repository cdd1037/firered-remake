#!/usr/bin/env python3
"""Authenticate pinned source blobs and rebuild one private Fly bird sheet; no upstream scripts run."""
import argparse,  io, json, pathlib, subprocess
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--reference-root',type=pathlib.Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args()
r=pathlib.Path(__file__).resolve().parents[1];lock=json.loads((r/'scripts/field-fly-travel.lock.json').read_text());commit=lock['sourceCommit']
blobs={}
for f in lock['files']:
 b=subprocess.check_output(['git','show',commit+':'+f['path']],cwd=a.reference_root)
 blobs[f['path']]=b
im=Image.open(io.BytesIO(blobs['graphics/field_effects/pics/bird.png']))
if im.mode!='P' or im.size!=(64,320):raise SystemExit('Bird indexed source dimensions differ')
palette=[tuple(map(int,line.split())) for line in blobs['graphics/object_events/palettes/player.pal'].decode().splitlines()[3:]]
if len(palette)!=16:raise SystemExit('Player palette differs')
palette=[tuple((v>>3)<<3 | (v>>3)>>2 for v in rgb) for rgb in palette]
rgba=bytes(v for i in im.getdata() for v in ((*palette[i],255) if i else (0,0,0,0)))
f=lock['output']
out=r/f['path']
if a.check:
 if not out.is_file() or out.read_bytes()!=rgba:raise SystemExit('Stored bird differs')
else:out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(rgba)
print(json.dumps({'sourceFiles':len(blobs),'birdBytes':len(rgba),'check':a.check}))
