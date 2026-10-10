#!/usr/bin/env python3
"""Rebuild the two ID-only Snorlax synth cues; no rendered audio or tool download."""
from pathlib import Path
import argparse,hashlib,json
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');a=p.parse_args()
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
for route,pin in [(12,'ea70631fa17e3042cf08b32c94fbc26fbb128936b61c00e61127b79af780368e'),(16,'680c86b2457428a348f8dd7ff006d13681c510cd5098c04b44f8f09a755a17a5')]:
 source=(ROOT/f'content/raw/snorlax-region/data/maps/Route{route}/scripts.inc').read_bytes()
 assert hashlib.sha256(source).hexdigest()==pin
 assert b'playmoncry SPECIES_SNORLAX, CRY_MODE_ENCOUNTER' in source
cues=[]
for name,mode in [('NORMAL',0),('ENCOUNTER',2)]:
 cues.append(dict(id='CRY_SNORLAX_'+name,sourceCommit=commit,sourceSymbol='SPECIES_SNORLAX',sourcePath='sound/direct_sound_samples/cries/snorlax.wav',sourcePlayer=4,priority=10,sourceCounter=None,songAddress=0,species=143,looping=False,cryMode=mode))
b=(json.dumps(dict(schemaVersion=1,sourceCommit=commit,cues=cues),indent=2)+'\n').encode()
assert len(b)==880 and hashlib.sha256(b).hexdigest()=='a11a8933f1052e4c9f74e8e372323e4b51193d08eaa47fe837957a741d934814'
out=ROOT/'content/generated/snorlax-synth-audio/manifest.json'
if a.check:assert out.read_bytes()==b
else:out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(b)
print('PASS Snorlax two synth cue IDs, source maps, exact manifest bytes; no audio produced')
