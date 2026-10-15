"""Pack audited fixed normal-cry metadata. Original assets remain external."""
from pathlib import Path
import argparse,hashlib,json,struct
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('output', type=Path, help='Explicit output catalog path')
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
manifest=json.loads((root/'results/phase4/inventory.json').read_text())
rows=manifest['species'];assert len(rows)==386
output=bytearray(b'FRCN0001')+struct.pack('<I',len(rows))
for r in rows:
    output+=struct.pack('<HHIIB3x',r['internalId'],r['cryIndex'],r['sampleCount'],r['payloadBytes'],r['guardStored'])
assert hashlib.sha256(output).hexdigest()=='623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e', 'Audited catalog identity changed'
args.output.write_bytes(output)
print(json.dumps({'bytes':len(output),'sha256':hashlib.sha256(output).hexdigest()}))
