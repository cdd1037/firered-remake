#!/usr/bin/env python3
"""Focused source, branch and material checks; expected vectors are never rewritten."""
import argparse,hashlib,importlib.util,json,re,struct,sys,unittest
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
class PetalIceImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.elemental=json.loads((GENERATED/'elemental.json').read_text());cls.physical=json.loads((GENERATED/'physical.json').read_text());cls.roots={r['moveId']:r for pack in [cls.elemental,cls.physical] for r in pack['scripts']};cls.recipes={r['template']:r for r in cls.elemental['spriteRecipes']};cls.source=(REFERENCE/'data/battle_anim_scripts.s').read_text();cls.importer=module('petal_ice_importer',ROOT/'scripts/imports/battle/import-source-elemental-animations.py')
 def test_exact_authored_commands_scoped_labels_and_owner(self):
  self.assertIn(80,{r['moveId'] for r in self.elemental['scripts']});self.assertNotIn(301,{r['moveId'] for r in self.elemental['scripts']});self.assertIn(301,{r['moveId'] for r in self.physical['scripts']});self.assertNotIn(80,{r['moveId'] for r in self.physical['scripts']})
  lines=self.source.splitlines()
  for move,pack in [(80,self.elemental),(301,self.physical)]:
   root=self.roots[move];commands=root['commands']+[c for label in {r['args'][0] for r in root['commands'] if r['op']=='call'} for c in pack['subroutines'][label]]
   for c in commands:
    op,*tail=lines[c['line']-1].split('@',1)[0].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
   for label,pc in root.get('labels',{}).items():
    start=lines.index(label+':')+1
    while not lines[start].split('@',1)[0].strip() or lines[start].strip().endswith(':'):start+=1
    self.assertEqual(start+1,root['commands'][pc]['line'])
  self.assertEqual(13,sum(c['op']=='createsprite' and 'Flower' in c['args'][0] for c in self.roots[80]['commands']))
 def test_every_counter_branch_preserves_source_call_counts_and_fifth_background(self):
  root=self.roots[301];self.assertEqual(15,self.physical['backgroundSymbols']['BG_ICE'])
  for count,expected in enumerate([5,7,9,10,10]):
   pc=0;calls=0;backgrounds=[];registers=[998]*8;steps=0
   while True:
    steps+=1;self.assertLess(steps,128);c=root['commands'][pc];op,args=c['op'],c['args']
    if op=='end':break
    if op=='createvisualtask' and args[0]=='AnimTask_GetRolloutCounter':registers[int(args[2])]=count
    if op=='call':self.assertEqual('IceBallImpactShard',args[0]);calls+=1
    if op in {'fadetobg','restorebg'}:backgrounds.append(op)
    if op=='goto':pc=root['labels'][args[0]];continue
    if op=='jumpargeq' and registers[int(args[0])]==int(args[1]):pc=root['labels'][args[2]];continue
    pc+=1
   self.assertEqual(expected,calls);self.assertEqual(['fadetobg','restorebg'] if count==4 else [],backgrounds)
  self.assertEqual([('createsprite',['gIceBallImpactShardSpriteTemplate','ANIM_TARGET','4','-12','-16']),('return',[])],[(c['op'],c['args']) for c in self.physical['subroutines']['IceBallImpactShard']])
 def test_recipe_shapes_and_frame_affine_commands_are_direct_source(self):
  for name,file,callback in [('gPetalDanceBigFlowerSpriteTemplate','battle_anim_effects_1.c','AnimPetalDanceBigFlower'),('gPetalDanceSmallFlowerSpriteTemplate','battle_anim_effects_1.c','AnimPetalDanceSmallFlower'),('gIceBallChunkSpriteTemplate','battle_anim_ice.c','InitIceBallAnim'),('gIceBallImpactShardSpriteTemplate','battle_anim_ice.c','InitIceBallParticle')]:
   source=(REFERENCE/('src/'+file)).read_text();self.assertEqual(self.importer.recipe(source,name,callback),self.recipes[name]);self.assertNotIn(name,{r['template'] for r in self.physical['spriteRecipes']})
   with self.assertRaises(ValueError):self.importer.recipe(source.replace('.callback = '+callback+',','.callback = UnreviewedCallback,'),name,callback)
  chunk=self.recipes['gIceBallChunkSpriteTemplate'];self.assertEqual([224,280,336,384,448],[p[0]['x'] for p in chunk['affineAnimations']]);self.assertEqual([16,32,48,64],[f['tile'] for f in chunk['animations'][1]])
 def test_every_ice_chunk_index_palette_color_and_rgb_pixel(self):
  row=next(r for r in self.elemental['indexedAssets'] if r['id']=='ice-chunk')
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/ice_chunk.png') as im:
   self.assertEqual(('P',(32,160)),(im.mode,im.size));indices=list(im.tobytes());palette=im.getpalette()[:48];tiled=[im.getpixel((x+dx,y+dy)) for y in range(0,160,8) for x in range(0,32,8) for dy in range(8) for dx in range(8)]
  colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual('ANIM_TAG_ICE_CHUNK',row['paletteTag']);self.assertEqual((32,160),(row['width'],row['height']));self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),(GENERATED/row['indicesPath']).read_bytes());self.assertEqual(struct.pack('<16H',*colors),(GENERATED/row['palettePath']).read_bytes());self.assertEqual(struct.pack('<HH',32,160)+struct.pack('<5120H',*[colors[i]|(0x8000 if i else 0) for i in indices]),(GENERATED/'elemental-ice-chunk.rgb555').read_bytes())
 def test_registered_source_ice_chunk_and_shared_flower_authority(self):
  source=(REFERENCE/'src/data/battle_anim.h').read_text();graphics=(REFERENCE/'src/graphics.c').read_text();self.assertRegex(source,r'\{gBattleAnimSpriteGfx_IceChunk, 0x0A00, ANIM_TAG_ICE_CHUNK\}');self.assertRegex(source,r'\{gBattleAnimSpritePal_IceChunk, ANIM_TAG_ICE_CHUNK\}')
  for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:self.assertIn('gBattleAnimSprite'+kind+'_IceChunk[] = INCBIN_U32("graphics/battle_anims/sprites/ice_chunk.'+suffix+'.lz")',graphics)
  self.assertEqual(1,sum(r['id']=='flower' for r in self.physical['indexedAssets']));self.assertNotIn('flower',{r['id'] for r in self.elemental['assets']+self.elemental['indexedAssets']});self.assertEqual('flower',self.recipes['gPetalDanceBigFlowerSpriteTemplate']['asset']);self.assertEqual('flower',self.recipes['gPetalDanceSmallFlowerSpriteTemplate']['asset'])
  tags=(REFERENCE/'include/constants/battle_anim.h').read_text();start=int(re.search(r'^#define ANIM_SPRITES_START\s+(\d+)',tags,re.M)[1])
  for move in [80,301]:
   root=self.roots[move]
   for tag,value in root['spriteTags'].items():self.assertEqual(start+int(re.search(r'^#define '+tag+r'\s+\(ANIM_SPRITES_START \+ (\d+)\)',tags,re.M)[1]),value)
 def test_original_task_register_and_particle_creation_order_are_pinned(self):
  source=(REFERENCE/'src/battle_anim_ice.c').read_text();body=re.search(r'void AnimTask_GetRolloutCounter\(u8 taskId\)\s*\{(.*?)\n\}',source,re.S)[1];self.assertEqual('u8arg=gBattleAnimArgs[0];gBattleAnimArgs[arg]=gAnimDisableStructPtr->rolloutTimerStartValue-gAnimDisableStructPtr->rolloutTimer-1;DestroyAnimVisualTask(taskId);',re.sub(r'\s+','',body))
  body=re.search(r'static void InitIceBallParticle\(struct Sprite \*sprite\)\s*\{(.*?)\n\}',source,re.S)[1];self.assertEqual(2,body.count('Random()'));self.assertLess(body.index('tileNum += 8'),body.index('InitSpritePosToAnimTarget'))
 def test_native_source_pins_are_complete_and_current(self):
  pins=json.loads((ROOT/'tests/Reference/PetalIce/pinned-inputs.json').read_text());self.assertEqual(11,len(pins))
  for path,digest in pins.items():self.assertEqual(digest,hashlib.sha256((REFERENCE/path).read_bytes()).hexdigest(),path)
if __name__=='__main__':unittest.main()
