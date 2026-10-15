#!/usr/bin/env python3
"""Rebuild trainer-only rematch presentation from pinned Git blobs; no source execution."""
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

ROOT = pathlib.Path(__file__).resolve().parents[1]
common = runpy.run_path(str(ROOT / 'scripts/import-celio-presentation.py'))
need, encode, safe, png = (common[k] for k in ('need', 'encode', 'safe', 'png'))
COMMIT, TREE = common['COMMIT'], common['TREE']
PROFILE = 'firered-league-rematch-presentation-v1'
BASE = 'content/generated/league-rematch-presentation/'
RAW = 'content/raw/pokefirered/037335f4/'
SPECIES = [('Crobat', 169), ('Misdreavus', 200), ('Steelix', 208), ('Heracross', 214), ('Kingdra', 230), ('Tyranitar', 248)]
MUSIC = ['MUS_VS_GYM_LEADER', 'MUS_VS_CHAMPION', 'MUS_VICTORY_TRAINER', 'MUS_VICTORY_GYM_LEADER']

class Source:
    def __init__(self, root, reference, dependency):
        self.root, self.reference, self.dependency = root, reference, dependency
        self.files, self.raw, self.inherited = {}, {}, {}
        self.lock = None
        data = safe(root, 'scripts/league-rematch-presentation.lock.json').read_bytes()
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
            need(row in self.lock['inheritedInputs'], 'Inherited resource differs: ' + path)
        self.inherited[path] = row
        return data


def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets, sprites, inherited_sprites, species_names = {}, [], [], [], []
    pewter_path = 'content/generated/pewter-pokemon/battle-art.json'
    pewter_sprites = json.loads(source.reuse(pewter_path))['sprites']
    graphics = source.text('src/data/graphics/pokemon.h')
    constants = source.text('include/constants/species.h')
    names_path = 'src/data/text/species_names.h'
    names = source.text(names_path)
    for name, species in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        need(re.search(r'^#define ' + symbol + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Species ID differs')
        name_match = re.search(r'\[' + symbol + r'\]\s*=\s*_\("([^"]+)"\)', names)
        need(name_match and name_match[1] == name.upper(), 'Species name differs')
        species_names.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, name=name_match[1], sourcePath=names_path))
        elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source.text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
        elevation = int(elevation_match[1]) if elevation_match else 0
        for side in ('front', 'back'):
            coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text(coordinates))
            need(match, 'Species coordinates missing')
            width, height, y_offset = map(int, match.groups())
            image = f'graphics/pokemon/{stem}/{side}.png'
            need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Species image binding differs')
            im = Image.open(io.BytesIO(source.read(image)))
            need(im.mode == 'P' and im.size == (64, 64) and max(im.tobytes()) < 16, 'Source indexed image differs')
            indices = im.tobytes()
            for variant in ('normal', 'shiny'):
                pal = f'graphics/pokemon/{stem}/{variant}.pal'
                palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Species palette binding differs')
                lines = source.text(pal).splitlines()
                need(lines[:3] == ['JASC-PAL', '0100', '16'] and len(lines) == 19, 'Source palette framing differs')
                colors = [tuple((int(c) >> 3) * 255 // 31 for c in line.split()) for line in lines[3:]]
                rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
                identity = f'{stem}-{side}-{variant}'
                if species == 208:
                    prefix = 'content/generated/pewter-pokemon/'
                    prior = next(p for p in pewter_sprites if p['sourceSpeciesId'] == species and p['side'] == side and p['paletteVariant'] == variant)
                    prior_png = source.reuse(prefix + prior['texturePath'])
                    need(Image.open(io.BytesIO(prior_png)).convert('RGBA').tobytes() == rgba, 'Inherited Steelix pixels differ')
                    need((prior['sourceWidth'], prior['sourceHeight'], prior['sourceYOffset'], prior['sourceEnemyElevation']) ==
                         (width, height, y_offset, elevation), 'Inherited Steelix coordinates differ')
                    row = dict(id=identity, width=64, height=64, texturePath=prefix + prior['texturePath'], sourcePaths=[image, pal, coordinates], inherited=True)
                else:
                    out[identity + '.png'] = png(64, 64, rgba)
                    row = dict(id=identity, width=64, height=64, texturePath=BASE + identity + '.png', sourcePaths=[image, pal, coordinates])
                assets.append(row)
                (inherited_sprites if species == 208 else sprites).append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, side=side, paletteVariant=variant,
                    x=144 if side == 'front' else 40, y=8 + y_offset - elevation if side == 'front' else 48 + y_offset,
                    sourceWidth=width, sourceHeight=height, sourceYOffset=y_offset, sourceEnemyElevation=elevation,
                     sourceCoordinatePath=coordinates, paletteExpansion='floor(rgb5 * 255 / 31)', transparentIndex=0, **row))

    pack = source.reuse('content/generated/synth-audio/firered.pack')
    cries = source.reuse('content/generated/synth-audio/normal-cries.bin')
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
        (inherited_cry_cues if species == 208 else cues).append(dict(id='CRY_' + name.upper() + '_NORMAL', sourceCommit=COMMIT, sourceSymbol='SPECIES_' + name.upper(),
            sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))

    inherited_cues = inherited_cry_cues[:]
    prior_cues = []
    for path in ('content/generated/synth-audio/manifest.json', 'content/generated/pokemon-league-presentation/synth-manifest.json'):
        prior_cues.extend(json.loads(source.reuse(path))['cues'])
    for expected in inherited_cry_cues:
        inherited = next(c for c in prior_cues if c['id'] == expected['id'])
        need(dict(inherited, cryMode=inherited.get('cryMode', 0)) == expected, 'Inherited Steelix cry cue differs')
    songs = source.text('include/constants/songs.h')
    song_table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source.text('sound/song_table.inc'), re.M)
    for symbol in MUSIC:
        song_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
        address, player, second = struct.unpack_from('<IHH', pack, 0x084A32CC + song_id * 8 - 0x08489968)
        need(song_table[song_id] == (symbol.lower(), str(player), str(second)) and player == 0, 'Inherited battle music binding differs')
        inherited = next(c for c in prior_cues if c['id'] == symbol)
        expected = dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath='sound/songs/midi/' + symbol.lower() + '.mid',
            sourcePlayer=player, priority=pack[address - 0x08489968 + 2], sourceCounter=None, songAddress=address, species=0, looping=True, cryMode=0)
        need(dict(inherited, cryMode=inherited.get('cryMode', 0)) == expected, 'Inherited battle music cue differs')
        inherited_cues.append(expected)

    geometry_manifest = 'content/generated/campaign-hit-art/manifest.json'
    geometry = json.loads(source.reuse(geometry_manifest))['geometry']
    inherited_geometry = []
    for row in sprites + inherited_sprites:
        if row['paletteVariant'] != 'normal':
            continue
        expected = dict(species=row['sourceSpeciesId'], side=row['side'], sourceWidth=row['sourceWidth'], sourceHeight=row['sourceHeight'],
            yOffset=row['sourceYOffset'], elevation=row['sourceEnemyElevation'] if row['side'] == 'front' else 0)
        need(expected in geometry, 'Inherited move-animation geometry differs')
        inherited_geometry.append(expected)

    # Rematches use the same original trainer graphics, names and shared music selection.
    league_manifest_path = 'content/generated/pokemon-league-presentation/manifest.json'
    league = json.loads(source.reuse(league_manifest_path))
    trainer_source_path = 'src/data/trainers.h'
    trainer_source = source.text(trainer_source_path)
    opponent_constants = source.text('include/constants/opponents.h')
    trainers = []
    for old in league['trainers']:
        before = old['trainer'].removeprefix('pret:')
        after = before + '_2' if before.startswith('TRAINER_ELITE_FOUR_') else before.replace('CHAMPION_FIRST_', 'CHAMPION_REMATCH_')
        match = re.search(r'\[' + after + r'\] = \{(.*?)\n    \}', trainer_source, re.S)
        need(match, 'Rematch trainer row missing')
        body = match[1]
        row = dict(old, trainer='pret:' + after, sourceTrainerId=int(re.search(r'^#define ' + after + r'\s+(\d+)', opponent_constants, re.M)[1]))
        for key, field in [('trainerClass', 'trainerClass'), ('trainerPic', 'trainerPic')]:
            need(re.search(r'\.' + field + r' = (\w+)', body)[1] == row[key], 'Inherited rematch trainer binding differs')
        need(re.search(r'\.trainerName = _\("([^"]+)"\)', body)[1] == row['name'], 'Inherited rematch trainer name differs')
        trainers.append(dict(row, inheritedTrainer=old['trainer'], sourcePath=trainer_source_path))
    need(len(trainers) == 7, 'Rematch trainer inventory differs')
    out['synth-manifest.json'] = encode(dict(schemaVersion=1, sourceCommit=COMMIT, cues=cues, inheritedCues=inherited_cues))
    lock = encode(dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', commit=COMMIT,
        files=[source.files[p] for p in sorted(source.files)], inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)]))
    manifest = encode(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        inputs=[source.files[p] for p in sorted(source.files)], inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)],
        assets=assets, sprites=sprites, inheritedSprites=inherited_sprites, inheritedSpriteManifest=pewter_path, speciesNames=species_names, pokemonIcons=[], footprints=[], newOwnedSpecies=[],
        newOpponentOnlySpecies=[species for _, species in SPECIES if species != 208], trainers=trainers, inheritedTrainerManifest=league_manifest_path,
        moveAnimationGeometry=dict(inheritedManifest=geometry_manifest, rows=inherited_geometry),
        audio=dict(additionalCueIds=[c['id'] for c in cues], inheritedCueIds=[c['id'] for c in inherited_cues], cries=cry_evidence,
            originalPackUnchanged=True, newAudioAssets=0, physicalAudioVerified=False),
        limits=['Trainer-only species do not extend player ownership.', 'Existing battle sprites, names, trainer portraits and music selection are reused.',
            'No party/storage icon or footprint consumer is admitted for these trainer-only species.',
            'Existing bounded move animation dispatch and geometry are reused; no new move animation coverage.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]))
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})
    result['scripts/league-rematch-presentation.lock.json'] = lock
    report = dict(passed=True, sourceFiles=len(source.raw), inheritedInputs=len(source.inherited), sprites=len(sprites),
        inheritedSprites=len(inherited_sprites), pokemonIcons=0, footprints=0, assets=len(assets), moveGeometry=len(inherited_geometry), trainers=len(trainers),
        outputs=len(out) - 1,  lockBytes=len(lock),  manifestBytes=len(manifest),
         synthBytes=len(out['synth-manifest.json']), newCries=len(cues),
        exactCryPcmCompared=True, sourceAndStaticChecksOnly=True, nativeWindowVerified=False, physicalAudioVerified=False)
    return result, report

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
