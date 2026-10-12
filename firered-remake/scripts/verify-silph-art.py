#!/usr/bin/env python3
"""Cold rebuild and corruption tests for the reconstructed Silph art packages."""
import argparse,base64,hashlib,importlib.util,io,json,pathlib,shutil,sys,tempfile
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
def load(name):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts'/('import-'+name+'.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);a=p.parse_args();root=a.project_root;modules={n:load(n) for n in ('silph-art','silph-trainer-art','silph-item-art','silph-door-art')};meta=modules['silph-art'].meta;passed=[]
with tempfile.TemporaryDirectory(prefix='silph-art-rebuild-') as tmp:
 cold=pathlib.Path(tmp);(cold/'scripts').mkdir();shutil.copy2(root/'scripts/silph-presentation.lock.json',cold/'scripts/silph-presentation.lock.json')
 if a.reference_root:meta.authenticated(cold,a.reference_root);passed.append('Pinned Git tree cold extraction authenticated all 116 blobs')
 else:shutil.copytree(root/'content/raw/silph-presentation',cold/'content/raw/silph-presentation')
 for name,module in modules.items():
  first=module.build(cold);second=module.build(cold)
  assert first==second and first,'Cold rebuild differs: '+name
  for rel,b in first.items():assert (root/'content/generated'/name/rel).read_bytes()==b,'Retained artifact differs: '+name+'/'+rel
  passed.append(name+': independent cold passes and retained bytes identical')
  doc=json.loads(first['manifest.json'])
  for rel,b in first.items():
   if not rel.endswith('.png'):continue
   pixels=Image.open(io.BytesIO(b)).convert('RGBA').tobytes();rgba=first.get(rel[:-4]+'.rgba')
   if rgba is None:
    icon=next(i for i in doc['icons'] if i['pngPath']==rel);rgba=base64.b64decode(icon['rgbaBase64'],validate=True)
   assert pixels==rgba,'PNG and RGBA differ '+rel
  passed.append(name+': every PNG pixel exactly matches authenticated RGBA')
 def rejected(path,work,label):
  target=cold/path;original=target.read_bytes();altered=bytearray(original);altered[len(altered)//2]^=1;target.write_bytes(altered)
  try:
   try:work();raise AssertionError('Corruption accepted: '+label)
   except ValueError:passed.append(label)
  finally:target.write_bytes(original)
 rejected('scripts/silph-presentation.lock.json',lambda:meta.authenticated(cold),'Corrupt source lock rejected')
 for path,label in [('graphics/pokemon/lapras/front.png','Pokemon source pixels'),('graphics/pokemon/mr_mime/shiny.pal','Shiny source palette'),('graphics/trainers/front_pics/rival_late_front_pic.png','Later rival source portrait'),('src/data/item_icon_table.h','Item icon source bindings'),('data/tilesets/secondary/silph_co/metatiles.bin','Door source metatile binary'),('sound/cry_tables.inc','Cry source lookup')]:
  rejected('content/raw/silph-presentation/'+path,lambda:meta.authenticated(cold),label+' corruption rejected')
 target=cold/'content/raw/silph-presentation/graphics/pokemon/lapras/front.png';target.unlink();target.symlink_to(root/'content/raw/silph-presentation/graphics/pokemon/lapras/front.png')
 try:meta.authenticated(cold);raise AssertionError('Symlink accepted')
 except ValueError:passed.append('Source pixel symlink rejected')
assert len(passed)>=16,'Empty or incomplete suite'
print(json.dumps(dict(passed=True,tests=len(passed),checks=passed),indent=2))
