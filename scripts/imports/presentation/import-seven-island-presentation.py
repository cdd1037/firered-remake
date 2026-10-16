#!/usr/bin/env python3
"""Rebuild Seven Island pixels and cue metadata from pinned local blobs; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

from runtime_json import runtime_json_bytes
import argparse
import io
import json
import pathlib
import re
import runpy
import struct
import subprocess
import wave
from PIL import Image

ROOT = _PROJECT_ROOT
# Reuse the established authored PNG/path/hash helpers; importing performs no rebuild.
common = runpy.run_path(str(ROOT / 'scripts/imports/presentation/import-celio-presentation.py'))
need, encode, safe, png = (common[k] for k in ('need', 'encode', 'safe', 'png'))
COMMIT, TREE = common['COMMIT'], common['TREE']
PROFILE = 'firered-seven-island-presentation-v1'
BASE = 'content/generated/sprites/pokemon/seven-island/'
RAW = 'content/raw/pokefirered/037335f4/'
OWNED = [('Phanpy',231),('Donphan',232),('Larvitar',246),('Pupitar',247),('Tyranitar',248),('Skarmory',227),('Unown',201)]
OPPONENT_ONLY = [('Girafarig',203),('Hitmontop',237),('Miltank',241),('Pineco',204),('Smeargle',235),('Ursaring',217)]
SPECIES = OWNED + OPPONENT_ONLY

class Source:
    def __init__(self, root, reference, dependency):
        self.root, self.reference, self.dependency = root, reference, dependency
        self.files, self.raw, self.inherited = {}, {}, {}
        self.lock = None
        data = safe(root, 'scripts/recipes/presentation/seven-island-presentation.lock.json').read_bytes()
        self.lock = json.loads(data)
        need((self.lock['schemaVersion'], self.lock['profile'], self.lock['repository'], self.lock['commit']) == (1, PROFILE, 'pret/pokefirered', COMMIT), 'Source lock identity differs')

    def read(self, path):
        if path not in self.raw:
            data = subprocess.check_output(['git', '-C', str(self.reference), 'show', COMMIT + ':' + path]) if self.reference else safe(self.root, RAW + path).read_bytes()
            row = dict(path=path)
            if self.lock:
                need(row in self.lock['files'], 'Source input differs: ' + path)
            self.raw[path], self.files[path] = data, row
        return self.raw[path]

    def text(self, path):
        return self.read(path).decode()

    def reuse(self, path):
        data = safe(self.dependency, path).read_bytes()
        row = dict(path=path)
        if self.lock:
            need(row in self.lock['inheritedInputs'], 'Inherited pixels differ: ' + path)
        self.inherited[path] = row
        return data


TRAINERS = ('CRUSH_GIRL_CYNDY', 'TAMER_EVAN', 'PKMN_RANGER_JACKSON', 'PKMN_RANGER_KATELYN', 'COOLTRAINER_LEROY', 'COOLTRAINER_MICHELLE', 'COOL_COUPLE_LEX_NYA', 'AROMA_LADY_MIAH', 'YOUNG_COUPLE_EVE_JON', 'PKMN_RANGER_NICOLAS', 'PKMN_RANGER_MADELINE', 'JUGGLER_MASON', 'RUIN_MANIAC_BRANDON', 'RUIN_MANIAC_BENJAMIN', 'PAINTER_EDNA', 'GENTLEMAN_CLIFFORD', 'PSYCHIC_DARIO', 'PSYCHIC_RODETTE')
MAPS = ('SevenIsland', 'SevenIsland_Harbor', 'SevenIsland_House_Room1', 'SevenIsland_Mart', 'SevenIsland_PokemonCenter_1F', 'SevenIsland_SevaultCanyon', 'SevenIsland_SevaultCanyon_Entrance', 'SevenIsland_SevaultCanyon_House', 'SevenIsland_SevaultCanyon_TanobyKey', 'SevenIsland_TanobyRuins', 'SevenIsland_TanobyRuins_DilfordChamber', 'SevenIsland_TanobyRuins_LiptooChamber', 'SevenIsland_TanobyRuins_MoneanChamber', 'SevenIsland_TanobyRuins_RixyChamber', 'SevenIsland_TanobyRuins_ScufibChamber', 'SevenIsland_TanobyRuins_ViapoisChamber', 'SevenIsland_TanobyRuins_WeepthChamber', 'SevenIsland_TrainerTower')
INHERITED = {'CRUSH_GIRL': 'content/generated/sprites/pokemon/sevii-ruby/crush-girl-front.png', 'TAMER': 'content/generated/ui/fuchsia-gym-presentation/tamer-front.png', 'POKEMON_RANGER_M': 'content/generated/sprites/pokemon/sevii-ruby/pokemon-ranger-m-front.png', 'POKEMON_RANGER_F': 'content/generated/sprites/pokemon/sevii-ruby/pokemon-ranger-f-front.png', 'COOLTRAINER_M': 'content/generated/ui/viridian-gym-presentation/cooltrainer-m-front.png', 'COOLTRAINER_F': 'content/generated/sprites/trainers/celadon-gym/cooltrainer-f-front.png', 'AROMA_LADY': 'content/generated/ui/first-sevii-presentation/first-sevii-aroma-lady-front.png', 'JUGGLER': 'content/generated/ui/fuchsia-gym-presentation/juggler-front.png', 'RUIN_MANIAC': 'content/generated/sprites/pokemon/six-island/ruin-maniac-front.png', 'GENTLEMAN': 'content/generated/battle/art/ssanne-battle-art/gentleman-front.png', 'PSYCHIC_M': 'content/generated/sprites/trainers/saffron-gym/psychic-m-front.png'}

def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets = {}, []

    def emit(identity, width, height, rgba, inputs):
        need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
        out[identity + '.png'] = png(width, height, rgba)
        row = dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png')
        assets.append(row)
        return row

    def palette(path, floor=False):
        lines = source.text(path).splitlines()
        need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Source palette framing differs')
        return [tuple((int(c) >> 3) * 255 // 31 if floor else ((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2) for c in line.split()) for line in lines[3:]]

    def indexed(image, pal, shape, floor=False, first_frame=False):
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return indices, bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    sprites, icons, footprints, inherited_sprites, names = [], [], [], [], []
    graphics = source.text('src/data/graphics/pokemon.h')
    constants = source.text('include/constants/species.h')
    names_source = source.text('src/data/text/species_names.h')
    prior_manifest_path = 'content/generated/sprites/pokemon/league-rematch/manifest.json'
    prior_sprites = json.loads(source.reuse(prior_manifest_path))['sprites']
    for name, species in SPECIES:
        canonical = 'SPECIES_' + name.upper()
        need(re.search(r'^#define ' + canonical + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Canonical species ID differs')
        display_name = re.search(r'\[' + canonical + r'\]\s*=\s*_\("([^"]+)"\)', names_source)[1]
        names.append(dict(speciesId='pret:' + canonical, sourceSpeciesId=species, name=display_name))
        forms = list('ABCDEFGHIJKLMNOPQRSTUVWXYZ') + ['EXCLAMATION','QUESTION'] if species == 201 else [None]
        for form, label in enumerate(forms):
            render = 201 if form == 0 else 412 + form
            if species != 201: render = species
            symbol = canonical if form == 0 else 'SPECIES_UNOWN_' + ({'EXCLAMATION':'EMARK','QUESTION':'QMARK'}.get(label,label))
            render_name = name + (label.title() + ('Mark' if form >= 26 else '') if species == 201 else '')
            if species == 201 and form < 26: render_name = name + label
            stem = name.lower() + ('/' + (label.lower() + '_mark' if form >= 26 else label.lower()) if species == 201 else '')
            if form: need(re.search(r'^#define ' + symbol + r'\s+\(NUM_SPECIES \+ ' + str(form) + r'\)\s*$', constants,re.M),'Unown render ID differs')
            elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source.text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
            elevation = int(elevation_match[1]) if elevation_match else 0
            for side in ('front', 'back'):
                coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
                match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text(coordinates))
                need(match, 'Form-specific coordinates missing: ' + symbol)
                width,height,y_offset=map(int,match.groups())
                image=f'graphics/pokemon/{stem}/{side}.png'
                need(f'gMon{side.title()}Pic_{render_name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics,'Source picture binding differs')
                for variant in ('normal','shiny'):
                    pal=f'graphics/pokemon/{name.lower()}/{variant}.pal'
                    palette_table=source.text('src/data/pokemon_graphics/' + ('shiny_palette_table.h' if variant=='shiny' else 'palette_table.h'))
                    palette_symbol=('gMonShinyPalette_' if variant=='shiny' else 'gMonPalette_')+name
                    need((('SPECIES_SHINY_PAL(' if variant=='shiny' else 'SPECIES_PAL(')+symbol.removeprefix('SPECIES_')+', '+palette_symbol+'),') in palette_table,'Form palette binding differs: '+symbol+' '+variant)
                    indices,rgba=indexed(image,pal,(64,64),floor=True)
                    identity=stem.replace('/','-')+'-'+side+'-'+variant
                    if species==248:
                        old=next(p for p in prior_sprites if p['sourceSpeciesId']==248 and p['side']==side and p['paletteVariant']==variant)
                        need(Image.open(io.BytesIO(source.reuse(old['texturePath']))).convert('RGBA').tobytes() == rgba, 'Inherited Tyranitar pixels differ')
                        image_bytes=source.reuse(old['texturePath'])
                        need(Image.open(io.BytesIO(image_bytes)).convert('RGBA').tobytes()==rgba,'Inherited Tyranitar PNG differs')
                        asset = dict(id=identity, width=64, height=64, texturePath=old['texturePath'],  inherited=True)
                        assets.append(asset)
                    else: asset=emit(identity,64,64,rgba,[image,pal,coordinates])
                    row=dict(speciesId='pret:'+canonical,sourceSpeciesId=species,renderSpeciesId=render,form=form if species==201 else None,side=side,paletteVariant=variant,x=144 if side=='front' else 40,y=8+y_offset-elevation if side=='front' else 48+y_offset,sourceWidth=width,sourceHeight=height,sourceYOffset=y_offset,sourceEnemyElevation=elevation,sourceCoordinatePath=coordinates,paletteExpansion='floor(rgb5 * 255 / 31)',transparentIndex=0,**asset)
                    (inherited_sprites if species==248 else sprites).append(row)
            if (name,species) not in OWNED: continue
            icon_table=source.text('src/pokemon_icon.c')
            match=re.search(r'\['+symbol+r'\]\s*=\s*(\d+)\s*[,\n]',icon_table)
            need(match and re.search(r'\['+symbol+r'\]\s*=\s*gMonIcon_'+render_name+r'\s*[,\n]',icon_table),'Form icon binding differs: '+symbol+' '+render_name)
            pal_index=int(match[1]); image=f'graphics/pokemon/{stem}/icon.png';pal=f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
            need(f'gMonIcon_{render_name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics,'Form icon image binding differs')
            indices,rgba=indexed(image,pal,(32,64),floor=True,first_frame=True)
            icons.append(dict(speciesId='pret:'+canonical,sourceSpeciesId=species,renderSpeciesId=render,form=form if species==201 else None,paletteIndex=pal_index,sourceFrameIndex=0,sourceFrameCount=2,**emit('icons/'+stem.replace('/','-'),32,32,rgba,[image,pal])))
        if (name,species) not in OWNED: continue
        image,pal=f'graphics/pokemon/{name.lower()}/footprint.png','graphics/pokedex/kanto_dex_bgpals.pal'
        need('['+canonical+'] = gMonFootprint_'+name+',' in source.text('src/data/pokemon_graphics/footprint_table.h'),'Footprint binding differs')
        im=Image.open(io.BytesIO(source.read(image)))
        need(im.mode=='1' and im.size==(16,16),'Footprint pixels differ')
        indices=bytes(1 if v==0 else 0 for v in im.get_flattened_data());colors=palette(pal,True)
        rgba=bytes(v for i in indices for v in (*colors[i],255 if i else 0))
        footprints.append(dict(speciesId='pret:'+canonical,sourceSpeciesId=species,**emit(name.lower()+'-footprint',16,16,rgba,[image,pal])))
    trainers, portraits = [], []
    trainer_source, trainer_constants = source.text('src/data/trainers.h'), source.text('include/constants/trainers.h')
    opponents, classes = source.text('include/constants/opponents.h'), source.text('src/data/text/trainer_class_names.h')
    trainer_graphics = source.text('src/data/graphics/trainers.h')
    picture_table = source.text('src/data/trainer_graphics/front_pic_tables.h')
    coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', picture_table.split('const struct CompressedSpriteSheet')[0])
    trainer_pics = {}
    for name in TRAINERS:
        symbol = 'TRAINER_' + name
        match = re.search(r'\[' + symbol + r'\]\s*=\s*\{(.*?)\n    \}', trainer_source, re.S)
        need(match, 'Trainer source record missing: ' + symbol)
        body = match[1]
        cls, pic = re.search(r'\.trainerClass = (\w+)', body)[1], re.search(r'\.trainerPic = (\w+)', body)[1]
        display_name = re.search(r'\.trainerName = _\("([^\"]+)"\)', body)[1]
        cls_name = re.search(r'\[' + cls + r'\]\s*= _\("([^\"]+)"\)', classes)[1]
        source_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)\s*$', opponents, re.M)[1])
        pic_id = int(re.search(r'^#define ' + pic + r'\s+(\d+)\s*$', trainer_constants, re.M)[1])
        music = re.search(r'\.encounterMusic_gender = ([^,]+),', body)[1]
        music_value = 0
        for part in music.split('|'):
            part = part.strip()
            music_value |= 128 if part == 'F_TRAINER_FEMALE' else int(re.search(r'^#define ' + part + r'\s+(\d+)', trainer_constants, re.M)[1])
        key = pic.removeprefix('TRAINER_PIC_')
        portrait_id = pathlib.PurePosixPath(INHERITED[key]).stem if key in INHERITED else key.lower().replace('_', '-') + '-front'
        trainers.append(dict(trainer='pret:' + symbol, sourceTrainerId=source_id, name=display_name,
            trainerClass=cls, className=cls_name, trainerPic=pic, portrait=portrait_id,
            encounterMusicGender=music_value, doubleBattle='.doubleBattle = TRUE,' in body))
        trainer_pics[pic] = pic_id
    need(len(trainers)==18 and sum(t['doubleBattle'] for t in trainers)==2,'Trainer cohort differs')
    for pic, number in sorted(trainer_pics.items()):
        key = pic.removeprefix('TRAINER_PIC_')
        pic_symbol = re.search(r'TRAINER_SPRITE\(' + key + r', (\w+), 0x800\)', picture_table)[1]
        pal_symbol = re.search(r'TRAINER_PAL\(' + key + r', (\w+)\)', picture_table)[1]
        paths = []
        for symbol in (pic_symbol, pal_symbol):
            match = re.search(r'const u32 ' + symbol + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', trainer_graphics)
            need(match, 'Trainer graphic path missing')
            paths.append(match[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
        _, rgba = indexed(*paths, (64, 64))
        identity = pathlib.PurePosixPath(INHERITED[key]).stem if key in INHERITED else key.lower().replace('_', '-') + '-front'
        if key in INHERITED:
            path = INHERITED[key]
            need(Image.open(io.BytesIO(source.reuse(path))).convert('RGBA').tobytes() == rgba, 'Inherited trainer source pixels differ: ' + key)
            asset = dict(id=identity, width=64, height=64, texturePath=path)
            assets.append(asset)
        else:
            asset = emit(identity, 64, 64, rgba, paths)
        need(coords[number][0] == '8', 'Trainer portrait source size differs')
        portraits.append(dict(trainerPic=pic, sourceTrainerPicId=number, inherited=key in INHERITED,
                              x=144, y=8, sourceYOffset=int(coords[number][1]), **asset))

    pack = source.reuse('content/generated/audio/synth/firered.pack')
    cries = source.reuse('content/generated/audio/synth/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    cues, cry_evidence, inherited_cry_cues = [], [], []
    for name, species in SPECIES:
        row = cry_rows[species]
        need(cry_table[species - 1] == name and row[1] == species - 1, 'Species cry table differs')
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave_offset = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
        need(struct.unpack_from('<HHIII', pack, wave_offset) == (1, 0, 10764288, 0, row[2]), 'Species packed cry header differs')
        path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
        with wave.open(io.BytesIO(source.read(path))) as original:
            need((original.getnchannels(), original.getsampwidth(), original.getframerate(), original.getnframes(), original.getcomptype()) ==
                 (1, 1, 10512, row[2] + 1, 'NONE'), 'Source cry sample/header dimensions differ')
            pcm = original.readframes(original.getnframes())
        delta = (0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1)
        decoded = bytearray()
        for index in range(row[2] + row[4]):
            block, i = divmod(index, 64)
            start = wave_offset + 16 + block * 33
            if i == 0:
                value = pack[start]
            else:
                packed = pack[start + (1 if i == 1 else i // 2 + 1)]
                nibble = packed & 15 if i == 1 or i % 2 else packed >> 4
                value = (value + delta[nibble]) & 255
            decoded.append((value + 128) & 255)
        need(decoded == pcm[:len(decoded)], 'Cry pack/source PCM differs: ' + name)
        cry_evidence.append(dict(species=species, cryIndex=row[1], sampleCount=row[2], payloadBytes=row[3], guardStored=bool(row[4]),
            decodedBytes=len(decoded),  sourcePath=path, sourcePlayer=4, priority=priority))
        (inherited_cry_cues if species == 248 else cues).append(dict(id='CRY_' + name.upper() + '_NORMAL', sourceSymbol='SPECIES_' + name.upper(), sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))

    inherited_cues=inherited_cry_cues[:]
    all_cues=[]
    for path in ['content/generated/audio/synth/manifest.json','content/generated/sprites/pokemon/six-island/synth-manifest.json','content/generated/sprites/pokemon/league-rematch/synth-manifest.json']:
        all_cues += json.loads(source.reuse(path))['cues']
    need(next(c for c in all_cues if c['id']=='CRY_TYRANITAR_NORMAL')==inherited_cry_cues[0],'Inherited Tyranitar cry differs')
    songs=source.text('include/constants/songs.h')
    song_table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',source.text('sound/song_table.inc'),re.M)
    map_music=[]
    for map_name in MAPS:
        path='data/maps/'+map_name+'/map.json'; music=json.loads(source.read(path))['music']
        map_music.append(dict(map=map_name,sourcePath=path,music=music))
    dance_path='data/maps/SevenIsland_SevaultCanyon_House/scripts.inc'
    need('playbgm MUS_SCHOOL, 0' in source.text(dance_path),'Dance music binding differs')
    for symbol in sorted({r['music'] for r in map_music}|{'MUS_SCHOOL','SE_TRUCK_MOVE','SE_LAVARIDGE_FALL_WARP','SE_M_STRENGTH'}):
        song_id=int(re.search(r'^#define '+symbol+r'\s+(\d+)',songs,re.M)[1])
        address,player,second=struct.unpack_from('<IHH',pack,0x084A32CC+song_id*8-0x08489968)
        need(song_table[song_id]==(symbol.lower(),str(player),str(second)) and player==(0 if symbol.startswith('MUS_') else 1),'Music binding differs')
        path='sound/songs/midi/'+symbol.lower()+'.mid'
        midi=source.read(path);need(midi[:4]==b'MThd','MIDI source framing differs')
        looping=b'\xff\x06\x01[' in midi
        need(looping==(b'\xff\x06\x01]' in midi),'MIDI loop markers differ')
        expected=dict(id=symbol, sourceSymbol=symbol, sourcePlayer=player, priority=pack[address - 138975592 + 2], sourceCounter=None, songAddress=address, species=0, looping=looping, cryMode=0)
        old=next((c for c in all_cues if c['id']==symbol),None)
        if old:
            need(dict(old,cryMode=old.get('cryMode',0))==expected,'Inherited music differs');inherited_cues.append(expected)
        else:cues.append(expected)
    geometry_path='content/generated/battle/art/campaign-hit-art/manifest.json'
    geometry=json.loads(source.reuse(geometry_path))['geometry']; geometry_rows=[]
    for row in sprites+inherited_sprites:
        if row['paletteVariant']!='normal':continue
        expected=dict(species=row['renderSpeciesId'],side=row['side'],sourceWidth=row['sourceWidth'],sourceHeight=row['sourceHeight'],yOffset=row['sourceYOffset'],elevation=row['sourceEnemyElevation'] if row['side']=='front' else 0)
        need(expected in geometry,'Form-specific geometry differs');geometry_rows.append(expected)
    # Authenticate the display-only PID resolver's original consumer contracts.
    for path in ['src/decompress.c','src/pokedex_screen.c','include/pokemon.h']:
        source.read(path)
    shade_sources=['src/field_weather.c','src/field_weather_effects.c','src/field_specials.c','data/event_scripts.s','data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc','include/fieldmap.h']
    for path in shade_sources:source.read(path)
    weather=source.text('src/field_weather_effects.c')
    need(re.search(r'void Shade_InitVars\(void\).*?gammaTargetIndex = 3;.*?gammaStepDelay = 20;',weather,re.S),'Source shade gamma differs')
    layouts=json.loads(source.read('data/layouts/layouts.json'))['layouts']
    tileset_headers=source.text('src/data/tilesets/headers.h')
    tile_bindings=source.text('src/data/tilesets/metatiles.h')
    shade_maps=[]
    for map_name in MAPS:
        if 'Chamber' not in map_name:continue
        map_data=json.loads(source.read('data/maps/'+map_name+'/map.json'))
        layout=next(l for l in layouts if l.get('id')==map_data['layout'])
        tiles=[]
        for tag in ('primary_tileset','secondary_tileset'):
            match=re.search(r'const struct Tileset '+layout[tag]+r'\s*=\s*\{(.*?)\n\};',tileset_headers,re.S)
            need(match,'Shade tileset header missing')
            symbol=re.search(r'\.metatiles = (\w+)',match[1])[1]
            path=re.search(r'const u16 '+symbol+r'\[\] = INCBIN_U16\("([^"]+)"\)',tile_bindings)[1]
            data=source.read(path);tiles.append(struct.unpack('<'+'H'*(len(data)//2),data))
        used=set()
        for tag in ('border_filepath','blockdata_filepath'):
            data=source.read(layout[tag])
            for block in struct.unpack('<'+'H'*(len(data)//2),data):
                meta=block&1023;table=tiles[meta>=640];local=meta-640 if meta>=640 else meta
                need(local*8+8<=len(table),'Shade metatile exceeds source table')
                used.update(tile>>12 for tile in table[local*8:local*8+8])
        need(used and max(used)<=12,'Shade map needs mixed palette treatment')
        need(not map_data.get('object_events'),'Shade chamber contains unsupported OBJ palettes')
        shade_maps.append(dict(map=map_name,paletteIndices=sorted(used),normalGammaOnly=True))
    need(len(shade_maps)==7,'Shade chamber inventory differs')
    out['synth-manifest.json']=runtime_manifest_bytes(dict(schemaVersion=1, cues=cues, inheritedCues=inherited_cues))
    lock=encode(dict(schemaVersion=1,profile=PROFILE,repository='pret/pokefirered',commit=COMMIT,files=[source.files[p] for p in sorted(source.files)],inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)]))
    manifest = runtime_json_bytes(dict(schemaVersion=1,     assets=assets, sprites=sprites, inheritedSprites=inherited_sprites, pokemonIcons=icons, footprints=footprints, speciesNames=names, newOwnedSpecies=[s for _, s in OWNED], newOpponentOnlySpecies=[s for _, s in OPPONENT_ONLY], trainers=trainers, portraits=portraits, mapMusic=map_music, moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry_rows), audio=dict(additionalCueIds=[c['id'] for c in cues], inheritedCueIds=[c['id'] for c in inherited_cues], cries=cry_evidence, ), fieldPresentation=dict(shadeGammaIndex=3, shadeStepDelay=20, shadeMaps=shade_maps, shadeNormalChannels=[c * 13 // 16 for c in range(32)], shadeAlternateChannels=list(range(32)), shakeVectors=[[1, 1, 8, 3], [2, 2, 10, 3], [3, 3, 12, 2]]), unown=dict(canonicalSpecies=201, formCount=28, renderSpecies=[201] + list(range(413, 440)), dexTrainerId=8, iconFrame=0), outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json']=manifest
    result={BASE+p:b for p,b in out.items()};result.update({RAW+p:b for p,b in source.raw.items()});result['scripts/recipes/presentation/seven-island-presentation.lock.json']=lock
    report=dict(passed=True,sourceFiles=len(source.raw),inheritedInputs=len(source.inherited),sprites=len(sprites),inheritedSprites=len(inherited_sprites),icons=len(icons),footprints=len(footprints),assets=len(assets),geometry=len(geometry_rows),trainers=len(trainers),portraits=len(portraits),newPortraits=sum(not p['inherited'] for p in portraits),outputs=len(out)-1,lockBytes=len(lock),manifestBytes=len(manifest),synthBytes=len(out['synth-manifest.json']),newCues=len(cues),inheritedCues=len(inherited_cues),exactCryPcmCompared=True,nativeWindowVerified=False)
    return result,report
if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--dependency-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root, args.dependency_root or args.project_root)
    for relative, data in output.items():
        target = safe(args.project_root, relative)
        if args.check:
            need(target.read_bytes() == data, 'Generated bytes differ: ' + relative)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(report, sort_keys=True))
