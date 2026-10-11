#!/usr/bin/env python3
"""Materialize or check the exact Surf script and text blobs; execute no upstream scripts."""
import argparse,hashlib,json,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
SHA='ed42e3c477cd43a2ef3b5ba6bb23378624d33785f9ad9a3a690889fbadceefb2'
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);a=p.parse_args()
 b=(a.project_root/'scripts/surf-scripts.lock.json').read_bytes()
 if len(b)!=767 or hashlib.sha256(b).hexdigest()!=SHA:raise ValueError('Surf script lock differs')
 lock=json.loads(b)
 for row in lock['files']:
  path=a.project_root/'content/raw/surf-scripts'/row['path'];b=subprocess.check_output(['git','-C',str(a.reference_root),'show',lock['sourceCommit']+':'+row['path']]) if a.reference_root else path.read_bytes()
  if (len(b),hashlib.sha256(b).hexdigest(),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())!=(row['byteLength'],row['sha256'],row['gitBlobSha1']):raise ValueError('Surf source differs: '+row['path'])
  if a.reference_root:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
 print('Authenticated3 Surf script/text/constants blobs')
if __name__=='__main__':main()
