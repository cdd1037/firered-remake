#!/usr/bin/env python3
"""Rebuild Event Island pixels and cue metadata from pinned local blobs; no upstream execution."""
import argparse
import hashlib
import io
import json
import pathlib
import re
import runpy
import struct
import subprocess
import wave
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
# Reuse the established authored PNG/path/hash helpers; importing performs no rebuild.
common = runpy.run_path(str(ROOT / 'scripts/import-celio-presentation.py'))
need, sha, encode, safe, png = (common[k] for k in ('need', 'sha', 'encode', 'safe', 'png'))
COMMIT, TREE = common['COMMIT'], common['TREE']
PROFILE = 'firered-event-island-presentation-v1'
BASE = 'content/generated/event-island-presentation/'
RAW = 'content/raw/event-island-presentation/'
LOCK_SHA = '24f8691fae4700137fa0dbcfc6be277a8a68181cf88154a6ffb462d2b0d61b46'
MANIFEST_SHA = 'f71013483e4dfe651458570d13f16b457a2ea59856eea1d40324234d9a921843'
SPECIES = [('Lugia',249),('HoOh',250),('Deoxys',410)]
class Source:
    def __init__(self, root, reference, dependency):
        self.root, self.reference, self.dependency = root, reference, dependency
        self.files, self.raw, self.inherited = {}, {}, {}
        self.lock = None
        if reference:
            need(subprocess.check_output(['git', '-C', str(reference), 'rev-parse', COMMIT + '^{tree}']).decode().strip() == TREE, 'Pinned source tree differs')
        if LOCK_SHA:
            data = safe(root, 'scripts/event-island-presentation.lock.json').read_bytes()
            need(sha(data) == LOCK_SHA, 'Pinned presentation lock differs')
            self.lock = json.loads(data)
            need((self.lock['schemaVersion'], self.lock['profile'], self.lock['repository'], self.lock['commit'], self.lock['tree']) ==
                 (1, PROFILE, 'pret/pokefirered', COMMIT, TREE), 'Source lock identity differs')

    def read(self, path):
        if path not in self.raw:
            data = subprocess.check_output(['git', '-C', str(self.reference), 'show', COMMIT + ':' + path]) if self.reference else safe(self.root, RAW + path).read_bytes()
            row = dict(path=path, byteLength=len(data), sha256=sha(data),
                       gitBlobSha1=hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest())
            if self.lock:
                need(row in self.lock['files'], 'Source input differs: ' + path)
            self.raw[path], self.files[path] = data, row
        return self.raw[path]

    def text(self, path):
        return self.read(path).decode()

    def reuse(self, path):
        data = safe(self.dependency, path).read_bytes()
        row = dict(path=path, byteLength=len(data), sha256=sha(data))
        if self.lock:
            need(row in self.lock['inheritedInputs'], 'Inherited pixels differ: ' + path)
        self.inherited[path] = row
        return data


def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets = {}, []

    def emit(identity, width, height, rgba, inputs):
        need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
        out[identity + '.rgba'], out[identity + '.png'] = rgba, png(width, height, rgba)
        row = dict(id=identity, width=width, height=height, rgbaPath=BASE + identity + '.rgba',
                   texturePath=BASE + identity + '.png', rgbaSha256=sha(rgba), sha256=sha(out[identity + '.png']), sourcePaths=inputs)
        assets.append(row)
        return row

    def palette(path, floor=False):
        lines = source.text(path).splitlines()
        need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Source palette framing differs')
        return [tuple((int(c) >> 3) * 255 // 31 if floor else ((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2) for c in line.split()) for line in lines[3:]]

    def indexed(image, pal, shape, floor=False, first_frame=False, second_frame=False):
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.tobytes()[4096:8192] if second_frame else im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return indices, bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    sprites, icons, footprints = [], [], []
    graphics = source.text('src/data/graphics/pokemon.h')
    constants = source.text('include/constants/species.h')
    for name, species in SPECIES:
        stem = 'ho_oh' if name == 'HoOh' else name.lower()
        symbol = 'SPECIES_' + stem.upper()
        need(re.search(r'^#define ' + symbol + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Species ID differs')
        elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source.text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
        elevation = int(elevation_match[1]) if elevation_match else 0
        for side in ('front', 'back'):
            coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text(coordinates))
            need(match, 'Species coordinates missing')
            width, height, y_offset = map(int, match.groups())
            image = f'graphics/pokemon/{stem}/{side}.png'
            need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Species image binding differs')
            for variant in ('normal', 'shiny'):
                pal = f'graphics/pokemon/{stem}/{variant}.pal'
                palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Species palette binding differs')
                indices, rgba = indexed(image, pal, (64,128) if species==410 else (64,64), floor=True, second_frame=species==410)
                x, y = (144, 8 + y_offset - elevation) if side == 'front' else (40, 48 + y_offset)
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, renderSpeciesId=species, sourceFrameIndex=1 if species==410 else 0, form='attack' if species==410 else None, side=side, paletteVariant=variant,
                    x=x, y=y, sourceWidth=width, sourceHeight=height, sourceYOffset=y_offset, sourceEnemyElevation=elevation,
                    indexSha256=sha(indices), sourceCoordinatePath=coordinates, paletteExpansion='floor(rgb5 * 255 / 31)',
                    transparentIndex=0, **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
        icon_table = source.text('src/pokemon_icon.c')
        match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
        need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
        pal_index = int(match[1])
        image, pal = f'graphics/pokemon/{stem}/' + ('icon_attack.png' if species==410 else 'icon.png'), f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need((f'gMonIcon_{name}[] = INCBIN_U8("graphics/pokemon/deoxys/icon.4bpp", "{image[:-4]}.4bpp")' if species==410 else f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")') in graphics, 'Species icon image binding differs')
        indices, rgba = indexed(image, pal, (32, 64), floor=True, first_frame=True)
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, form='attack' if species==410 else None, paletteIndex=pal_index, sourceFrameIndex=0,
                          sourceFrameCount=2, indexSha256=sha(indices), **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source pixels differ')
        indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
        colors = palette(pal, floor=True)
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))

    pack = source.reuse('content/generated/synth-audio/firered.pack')
    cries = source.reuse('content/generated/synth-audio/normal-cries.bin')
    need(sha(pack) == '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d' and len(pack) == 0x086fbea4 - 0x08489968 and
         sha(cries) == '623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e' and len(cries) == 6188, 'Original audio pack identity differs')
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    cues, cry_evidence, inherited_cry_cues = [], [], []
    for name, species in SPECIES:
        row = cry_rows[species]
        need(cry_table[row[1]] == name and row[1] == (386 if species==410 else species-1), 'Species cry table differs')
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave_offset = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
        need(struct.unpack_from('<HHIII', pack, wave_offset) == (1, 0, 10764288, 0, row[2]), 'Species packed cry header differs')
        path = 'sound/direct_sound_samples/cries/' + ('ho_oh' if name=='HoOh' else name.lower()) + '.wav'
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
            decodedBytes=len(decoded), decodedSha256=sha(decoded), sourcePath=path, sourcePlayer=4, priority=priority))
        cues.append(dict(id='CRY_' + ('HO_OH' if name=='HoOh' else name.upper()) + '_NORMAL', sourceCommit=COMMIT, sourceSymbol='SPECIES_' + ('HO_OH' if name=='HoOh' else name.upper()),
            sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))

    cues += [dict(c,id=c['id'].replace('_NORMAL','_ENCOUNTER'),cryMode=2) for c in cues[:]]
    names = []
    for name, species in SPECIES:
        symbol='SPECIES_'+('HO_OH' if name=='HoOh' else name.upper())
        display=re.search(r'\['+symbol+r'\]\s*=\s*_\("([^"]+)"\)',source.text('src/data/text/species_names.h'))[1]
        names.append(dict(speciesId='pret:'+symbol,sourceSpeciesId=species,name=display))
    decompress=source.text('src/decompress.c')
    need('CpuCopy32(pointer + 0x800, pointer, 0x800)' in decompress,'Attack form frame-copy binding differs')
    need('iconSprite += 0x400;' in source.text('src/pokemon_icon.c'),'Attack icon offset differs')
    sound=source.text('src/sound.c')
    need(re.search(r'if \(songNum == MUS_NONE\)\s*songNum = 0;',sound),'Source silent-map song alias differs')
    need(re.search(r'case CRY_MODE_ENCOUNTER:\s*release = 225;\s*pitch = 15600;\s*chorus = 20;\s*volume = 90;\s*break;',sound),'Encounter cry parameters differ')
    setup=source.text('src/battle_setup.c')
    need(re.search(r'case SPECIES_DEOXYS:\s*CreateBattleStartTask\(B_TRANSITION_BLUR, MUS_VS_DEOXYS\);',setup),'Deoxys battle music differs')
    need(re.search(r'case SPECIES_HO_OH:\s*case SPECIES_LUGIA:\s*CreateBattleStartTask\(B_TRANSITION_BLUR, MUS_VS_LEGEND\);',setup),'Navel battle music differs')
    map_music=[]
    maps=['BirthIsland_Exterior','BirthIsland_Harbor','NavelRock_Summit','NavelRock_Base','NavelRock_Exterior','NavelRock_Harbor']
    for name in maps:
        path='data/maps/'+name+'/map.json'
        map_music.append(dict(map=name,music=json.loads(source.read(path))['music'],sourcePath=path))
    for name in ['BirthIsland_Exterior','NavelRock_Summit','NavelRock_Base']:
        source.read('data/maps/'+name+'/scripts.inc')
    all_cues=[]
    for path in ['content/generated/synth-audio/manifest.json','content/generated/sevii-ruby-presentation/synth-manifest.json','content/generated/six-island-presentation/synth-manifest.json']:
        all_cues+=json.loads(source.reuse(path))['cues']
    inherited_cues=[]
    songs=source.text('include/constants/songs.h')
    song_table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',source.text('sound/song_table.inc'),re.M)
    song_symbols={r['music'] for r in map_music}-{'MUS_NONE'}|{'MUS_VS_LEGEND','MUS_VS_DEOXYS','MUS_ENCOUNTER_DEOXYS','SE_DEOXYS_MOVE','SE_M_CONFUSE_RAY','SE_M_WING_ATTACK','SE_M_STRENGTH','SE_THUNDERSTORM_STOP','SE_THUNDER2','SE_THUNDER'}
    for symbol in sorted(song_symbols):
        number=int(re.search(r'^#define '+symbol+r'\s+(\d+)',songs,re.M)[1])
        address,player,second=struct.unpack_from('<IHH',pack,0x084A32CC+number*8-0x08489968)
        need(song_table[number]==(symbol.lower(),str(player),str(second)),'Source song table differs')
        path='sound/songs/midi/'+symbol.lower()+'.mid'
        midi=source.read(path);need(midi[:4]==b'MThd','MIDI framing differs')
        looping=b'\xff\x06\x01[' in midi
        need(looping==(b'\xff\x06\x01]' in midi),'MIDI loop markers differ')
        expected=dict(id=symbol,sourceCommit=COMMIT,sourceSymbol=symbol,sourcePath=path,sourcePlayer=player,priority=pack[address-0x08489968+2],sourceCounter=None,songAddress=address,species=0,looping=looping,cryMode=0)
        old=next((c for c in all_cues if c['id']==symbol),None)
        if old:
            need(dict(old,cryMode=old.get('cryMode',0))==expected,'Inherited song differs');inherited_cues.append(expected)
        else:cues.append(expected)
    geometry_path='content/generated/campaign-hit-art/manifest.json'
    geometry=json.loads(source.reuse(geometry_path))['geometry'];geometry_rows=[]
    for row in sprites:
        if row['paletteVariant']!='normal':continue
        expected=dict(species=row['sourceSpeciesId'],side=row['side'],sourceWidth=row['sourceWidth'],sourceHeight=row['sourceHeight'],yOffset=row['sourceYOffset'],elevation=row['sourceEnemyElevation'] if row['side']=='front' else 0)
        need(expected in geometry,'Inherited battle geometry differs');geometry_rows.append(expected)
    # Source object palette slot 10 is independent of the fixed normal overworld Deoxys.
    field_sources=['src/field_effect.c','src/field_specials.c','src/field_weather.c','src/field_weather_effects.c','src/data/object_events/object_event_graphics.h','src/data/object_events/object_event_graphics_info.h','src/data/object_events/object_event_anims.h','include/event_object_movement.h','src/palette.c']
    for path in field_sources:source.read(path)
    anim=source.text('src/data/object_events/object_event_anims.h')
    need(re.search(r'sAnim_HoOhFlapWings\[\] = \{\s*ANIMCMD_FRAME\(3, 8\),\s*ANIMCMD_FRAME\(4, 8\),\s*ANIMCMD_FRAME\(3, 8\),\s*ANIMCMD_FRAME\(4, 8\),\s*ANIMCMD_JUMP\(0\)',anim),'Ho-Oh source flap animation differs')
    graphics_source=source.text('src/data/object_events/object_event_graphics.h')
    meteorite_path=re.search(r'gObjectEventPic_BirthIslandStone\[\]\s*= INCBIN_U16\("([^"]+)',graphics_source)[1].replace('.4bpp','.png')
    field=source.text('src/field_specials.c')
    triangle=[]
    for state in range(11):
        pal=f'graphics/field_specials/deoxys_rock_{state}.pal'
        need(pal.replace('.pal','.gbapal') in field,'Triangle palette source binding differs')
        indices,rgba=indexed(meteorite_path,pal,(32,32))
        triangle.append(dict(interactionNumber=state,objectPaletteSlot=10,paletteRgb5=[[int(c)>>3 for c in line.split()] for line in source.text(pal).splitlines()[3:]],indexSha256=sha(indices),**emit(f'triangle/state-{state}',32,32,rgba,[meteorite_path,pal])))
    fragments=[]
    for ordinal,direction in enumerate(['top_left','top_right','bottom_left','bottom_right']):
        image=f'graphics/field_effects/pics/deoxys_rock_fragment_{direction}.png';pal='graphics/field_specials/deoxys_rock_10.pal'
        need(image.replace('.png','.4bpp') in source.text('src/field_effect.c'),'Fragment source binding differs')
        indices,rgba=indexed(image,pal,(8,8))
        fragments.append(dict(ordinal=ordinal,deltaX=-16 if ordinal%2==0 else 16,deltaY=-12 if ordinal<2 else 12,indexSha256=sha(indices),**emit('fragments/'+direction,8,8,rgba,[image,pal])))
    # Summit shade uses the same source gamma index 3 as Tanoby. Authenticate used BG palettes.
    layout_id=json.loads(source.read('data/maps/NavelRock_Summit/map.json'))['layout']
    layout=next(l for l in json.loads(source.read('data/layouts/layouts.json'))['layouts'] if l.get('id')==layout_id)
    tile_headers=source.text('src/data/tilesets/headers.h');tile_bindings=source.text('src/data/tilesets/metatiles.h');tables=[]
    for tag in ['primary_tileset','secondary_tileset']:
        record=re.search(r'const struct Tileset '+layout[tag]+r'\s*=\s*\{(.*?)\n\};',tile_headers,re.S)[1]
        symbol=re.search(r'\.metatiles = (\w+)',record)[1]
        path=re.search(r'const u16 '+symbol+r'\[\] = INCBIN_U16\("([^"]+)"\)',tile_bindings)[1]
        data=source.read(path);tables.append(struct.unpack('<'+'H'*(len(data)//2),data))
    used=set()
    for tag in ['border_filepath','blockdata_filepath']:
        data=source.read(layout[tag])
        for block in struct.unpack('<'+'H'*(len(data)//2),data):
            meta=block&1023;table=tables[meta>=640];local=meta-640 if meta>=640 else meta
            need(local*8+8<=len(table),'Summit shade metatile range differs')
            used.update(tile>>12 for tile in table[local*8:local*8+8])
    need(used and max(used)<=12,'Summit BG requires mixed shade treatment')
    out['synth-manifest.json']=encode(dict(schemaVersion=1,sourceCommit=COMMIT,cues=cues,inheritedCues=inherited_cues))
    lock=encode(dict(schemaVersion=1,profile=PROFILE,repository='pret/pokefirered',commit=COMMIT,tree=TREE,files=[source.files[p] for p in sorted(source.files)],inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)]))
    if LOCK_SHA:need(sha(lock)==LOCK_SHA,'Input inventory differs')
    manifest=encode(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,sourceTree=TREE,lockSha256=sha(lock),inputs=[source.files[p] for p in sorted(source.files)],inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)],assets=assets,sprites=sprites,pokemonIcons=icons,footprints=footprints,speciesNames=names,newOwnedSpecies=[s for _,s in SPECIES],mapMusic=map_music,moveAnimationGeometry=dict(inheritedManifest=geometry_path,rows=geometry_rows),audio=dict(additionalCueIds=[c['id'] for c in cues],inheritedCueIds=[c['id'] for c in inherited_cues],cries=cry_evidence,originalPackUnchanged=True,physicalAudioVerified=False),triangle=triangle,fragments=fragments,fieldPresentation=dict(sourcePaths=field_sources,paletteTint='ApplyGlobalFieldPaletteTint(10)',destroy=dict(fragmentFrame=122,totalFrames=206,amplitude=4,decayInterval=21,flashBlend=16,flashBackgroundOnly=True),hoOhFlap=dict(frames=[3,4,3,4],frameDuration=8),summitShade=dict(gammaIndex=3,paletteIndices=sorted(used),normalChannels=[(c*13)//16 for c in range(32)])),outputs=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(out.items())]))
    if MANIFEST_SHA:need(sha(manifest)==MANIFEST_SHA,'Reviewed manifest differs')
    out['manifest.json']=manifest
    result={BASE+p:b for p,b in out.items()};result.update({RAW+p:b for p,b in source.raw.items()});result['scripts/event-island-presentation.lock.json']=lock
    report=dict(passed=True,sourceFiles=len(source.raw),inheritedInputs=len(source.inherited),sprites=len(sprites),icons=len(icons),footprints=len(footprints),trianglePalettes=len(triangle),fragments=len(fragments),assets=len(assets),geometry=len(geometry_rows),outputs=len(out)-1,lockSha256=sha(lock),lockBytes=len(lock),manifestSha256=sha(manifest),manifestBytes=len(manifest),synthSha256=sha(out['synth-manifest.json']),synthBytes=len(out['synth-manifest.json']),newCues=len(cues),inheritedCues=len(inherited_cues),exactCryPcmCompared=True,nativeWindowVerified=False,physicalAudioVerified=False)
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
