#!/usr/bin/env python3
"""Pinned Viridian Gym portraits and inherited presentation bindings; no upstream code runs."""
import argparse
import io
import json
import pathlib
import re
import struct
import subprocess
import zlib
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-viridian-gym-presentation-v1'
BASE = 'content/generated/viridian-gym-presentation/'
RAW = 'content/raw/pokefirered/037335f4/'
PORTRAITS = [('tamer-front', 'tamer', 'Tamer', 'TAMER', 103, 'content/generated/fuchsia-gym-presentation/tamer-front.png'), ('black-belt-front', 'black_belt', 'BlackBelt', 'BLACK_BELT', 105, None), ('cooltrainer-m-front', 'cool_trainer_m', 'CooltrainerM', 'COOLTRAINER_M', 110, None), ('giovanni-front', 'leader_giovanni', 'LeaderGiovanni', 'LEADER_GIOVANNI', 108, 'content/generated/rocket-hideout-art/giovanni-front.png')]
TRAINERS = [('TAMER_JASON', 296, 'JASON', 'TAMER', 'TAMER', 'HIKER', 11),
    ('TAMER_COLE', 297, 'COLE', 'TAMER', 'TAMER', 'HIKER', 11),
    ('BLACK_BELT_ATSUSHI', 322, 'ATSUSHI', 'BLACK_BELT', 'BLACK_BELT', 'INTENSE', 4),
    ('BLACK_BELT_KIYO', 323, 'KIYO', 'BLACK_BELT', 'BLACK_BELT', 'INTENSE', 4),
    ('BLACK_BELT_TAKASHI', 324, 'TAKASHI', 'BLACK_BELT', 'BLACK_BELT', 'INTENSE', 4),
    ('COOLTRAINER_SAMUEL', 392, 'SAMUEL', 'COOLTRAINER', 'COOLTRAINER_M', 'COOL', 5),
    ('COOLTRAINER_YUJI', 400, 'YUJI', 'COOLTRAINER', 'COOLTRAINER_M', 'COOL', 5),
    ('COOLTRAINER_WARREN', 401, 'WARREN', 'COOLTRAINER', 'COOLTRAINER_M', 'COOL', 5),
    ('LEADER_GIOVANNI', 350, 'GIOVANNI', 'LEADER', 'LEADER_GIOVANNI', 'AQUA', 6)]
CUES = ['MUS_GYM', 'MUS_ENCOUNTER_BOY', 'MUS_ENCOUNTER_ROCKET', 'MUS_VS_TRAINER',
    'MUS_VS_GYM_LEADER', 'MUS_VICTORY_TRAINER', 'MUS_VICTORY_GYM_LEADER', 'MUS_OBTAIN_BADGE',
    'MUS_LEVEL_UP', 'SE_M_RAZOR_WIND2']

def require(value, message):
    if not value:
        raise ValueError(message)


def encode(value):
    return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()

def safe(root, relative):
    require(relative and not pathlib.PurePosixPath(relative).is_absolute() and '\\' not in relative
        and all(p not in ('', '.', '..') for p in relative.split('/')), 'Unsafe resource path')
    path = pathlib.Path(root) / relative
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'Resource symlink rejected')
    return path

def png(width, height, rgba):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    scan = b''.join(b'\0' + rgba[y*width*4:(y+1)*width*4] for y in range(height))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(scan, 9)) + chunk(b'IEND', b'')

def pixels(image, palette):
    im = Image.open(io.BytesIO(image))
    lines = palette.decode().splitlines()
    require(im.mode == 'P' and max(im.tobytes()) < 16 and lines[:3] == ['JASC-PAL', '0100', '16'] and len(lines) == 19, 'Indexed source pixels differ')
    pal = [tuple(((int(c) >> 3) << 3) | (int(c) >> 5) for c in line.split()) for line in lines[3:]]
    require(all(len(rgb) == 3 for rgb in pal), 'Palette shape differs')
    return im.size, bytes(c for index in im.tobytes() for c in (*pal[index], 255 if index else 0))

def authenticate(root, inherited, reference=None):
    data = safe(root, 'scripts/viridian-gym-presentation.lock.json').read_bytes()
    lock = json.loads(data)
    require((lock['schemaVersion'], lock['profile'], lock['repository'], lock['commit'], len(lock['files'])) == (1, PROFILE, 'pret/pokefirered', COMMIT, 25), 'Presentation source scope differs')
    raw, reused = {}, {}
    for row in lock['files']:
        path = row['path']
        b = subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + path]) if reference else safe(root, RAW + path).read_bytes()
        raw[path] = b
    for row in lock['inheritedInputs']:
        b = safe(inherited, row['path']).read_bytes()
        reused[row['path']] = b
    return lock, raw, reused

def build(lock, raw, reused):
    source = lambda path: raw[path].decode()
    info = json.loads(raw['data/maps/ViridianCity_Gym/map.json'])
    require(info['music'] == 'MUS_GYM' and info['battle_scene'] == 'MAP_BATTLE_SCENE_GYM', 'Gym scene differs')
    table = source('src/data/trainer_graphics/front_pic_tables.h')
    coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', table.split('const struct CompressedSpriteSheet')[0])
    portraits, outputs = [], {}
    for identity, stem, symbol, picture, number, inherited in PORTRAITS:
        image, palette = f'graphics/trainers/front_pics/{stem}_front_pic.png', f'graphics/trainers/palettes/{stem}.pal'
        graphics = source('src/data/graphics/trainers.h')
        require(f'gTrainerFrontPic_{symbol}[] = INCBIN_U32("graphics/trainers/front_pics/{stem}_front_pic.4bpp.lz")' in graphics
            and f'gTrainerPalette_{symbol}[] = INCBIN_U32("graphics/trainers/palettes/{stem}.gbapal.lz")' in graphics
            and f'TRAINER_SPRITE({picture}, gTrainerFrontPic_{symbol}, 0x800)' in table
            and f'TRAINER_PAL({picture}, gTrainerPalette_{symbol})' in table, 'Portrait source binding differs')
        require(re.search(r'^#define TRAINER_PIC_' + picture + r'\s+' + str(number) + r'\s*$', source('include/constants/trainers.h'), re.M)
            and coords[number][0] == '8', 'Portrait source ID/position differs')
        size, rgba = pixels(raw[image], raw[palette]); require(size == (64, 64), 'Portrait size differs')
        if inherited:
            require(Image.open(io.BytesIO(reused[inherited])).convert('RGBA').tobytes() == rgba, 'Reused portrait pixels differ')
            texture_path = inherited
        else:
            outputs[identity + '.png'] = png(*size, rgba)
            texture_path = BASE + identity + '.png'
        portraits.append(dict(id=identity, trainerPic='TRAINER_PIC_' + picture, sourceTrainerPicId=number, width=64, height=64, x=144, y=8, texturePath=texture_path, inherited=inherited is not None, sourceImage=image, sourcePalette=palette))
    rows = []
    for symbol, number, name, cls, picture, music, category in TRAINERS:
        trainer = 'TRAINER_' + symbol
        block = re.search(r'\[' + trainer + r'\] = \{(.*?)\n    \}', source('src/data/trainers.h'), re.S)
        require(block is not None and re.search(r'^#define ' + trainer + r'\s+' + str(number) + r'\s*$', source('include/constants/opponents.h'), re.M), 'Trainer identity differs')
        for field, value in [('trainerClass', 'TRAINER_CLASS_' + cls), ('trainerPic', 'TRAINER_PIC_' + picture), ('encounterMusic_gender', 'TRAINER_ENCOUNTER_MUSIC_' + music)]:
            require(re.search(r'\.' + field + r'\s*=\s*' + value + r'\s*,', block[1]), 'Trainer presentation binding differs')
        require('.trainerName = _("' + name + '")' in block[1] and '.doubleBattle = FALSE' in block[1], 'Trainer name/mode differs')
        require(re.search(r'^#define TRAINER_ENCOUNTER_MUSIC_' + music + r'\s+' + str(category) + r'\s*(?://[^\n]*)?$', source('include/constants/trainers.h'), re.M), 'Encounter category differs')
        class_name = re.search(r'\[TRAINER_CLASS_' + cls + r'\]\s*= _\("([^\"]+)"\)', source('src/data/text/trainer_class_names.h'))
        require(class_name is not None, 'Trainer class missing')
        leader = number == 350
        rows.append(dict(trainer='pret:' + trainer, sourceTrainerId=number, name=name, trainerClass='TRAINER_CLASS_' + cls,
            className=class_name[1], trainerPic='TRAINER_PIC_' + picture, portrait=next(p['id'] for p in portraits if p['trainerPic'] == 'TRAINER_PIC_' + picture),
            encounterMusicGender=category, encounterMusic='MUS_ENCOUNTER_ROCKET' if leader else 'MUS_ENCOUNTER_BOY',
            encounterTrigger='final-intro-text-printer-eos' if leader else 'trainer-script', musicOption='NO_MUSIC' if leader else None,
            battleMusic='MUS_VS_GYM_LEADER' if leader else 'MUS_VS_TRAINER', victoryMusic='MUS_VICTORY_GYM_LEADER' if leader else 'MUS_VICTORY_TRAINER'))
    texts = source('data/maps/ViridianCity_Gym/text.inc'); scripts = source('data/maps/ViridianCity_Gym/scripts.inc')
    setup = source('src/battle_setup.c')
    for category in ['HIKER', 'INTENSE', 'COOL']:
        require(re.search(r'case TRAINER_ENCOUNTER_MUSIC_' + category + r':(?:(?!break;).)*music = MUS_ENCOUNTER_BOY;', setup, re.S), 'Ordinary encounter music dispatch differs')
    require('GIOVANNI, the greatest TRAINER!{PLAY_BGM}{MUS_ENCOUNTER_ROCKET}$' in texts
        and '{PAUSE_MUSIC}{PLAY_BGM}{MUS_OBTAIN_BADGE}{PAUSE 0xFE}{PAUSE 0x56}{RESUME_MUSIC}$' in texts
        and 'trainerbattle_single TRAINER_LEADER_GIOVANNI' in scripts and 'NO_MUSIC' in scripts, 'Giovanni text/music differs')
    for symbol in ['TM26', 'TM28']:
        require(re.search(r'\[ITEM_' + symbol + r'\]\s*= \{gItemIcon_TMHM, gItemIconPalette_GroundTMHM\}', source('src/data/item_icon_table.h')), 'Ground machine icon binding differs')
    size, icon = pixels(raw['graphics/items/icons/tm_hm.png'], raw['graphics/items/icon_palettes/ground_tm_hm.pal'])
    old_icon = Image.open(io.BytesIO(reused['content/generated/bill-machines/item_tm28.png'])).convert('RGBA')
    require(size == old_icon.size == (24, 24) and old_icon.tobytes() == icon, 'Inherited Ground TM pixels differ')
    card = json.loads(reused['content/generated/trainer-card/trainer-card.json'])
    require(any(b['flag'] == 'FLAG_BADGE08_GET' and b['assetId'] == 'badge-8' for b in card['layout']['badges']), 'Earth Badge art missing')
    audio = json.loads(reused['content/generated/synth-audio/manifest.json'])
    require(audio['sourceCommit'] == COMMIT and all(sum(c['id'] == cue for c in audio['cues']) == 1 for cue in CUES), 'Inherited synth closure differs')
    movement = source('src/event_object_movement.c')
    require('InitJumpRegular(objectEvent, sprite, DIR_SOUTH, JUMP_DISTANCE_FAR, JUMP_TYPE_HIGH);' in movement
        and '[JUMP_DISTANCE_FAR]      = 32,' in movement and '[JUMP_DISTANCE_FAR]      = 1,' in movement, 'Source jump timing differs')
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        inputs=lock['files'], inheritedInputs=lock['inheritedInputs'], portraits=portraits, trainers=rows,
        map=dict(id='map.viridian-city-gym', music='MUS_GYM', battleScene='MAP_BATTLE_SCENE_GYM'),
        reward=dict(item='pret:ITEM_TM26', reusedIconItem='pret:ITEM_TM28',  badgeFlag='FLAG_BADGE08_GET', badgeAsset='badge-8'),
        audio=dict(requiredCues=CUES, newCues=0, physicalAudioVerified=False),
        movement=dict(action='jump_2_down', reusedProjection='FuchsiaLedgeDrawYOffset', frames=32, distancePixels=32),
        outputs=[dict(path=p) for p,b in sorted(outputs.items())])
    outputs['manifest.json'] = encode(manifest)
    return outputs

def run(root=ROOT, inherited=None, reference=None, check=False):
    lock, raw, reused = authenticate(root, inherited or root, reference)
    outputs = build(lock, raw, reused)
    if check:
        for p, b in outputs.items():
            require(safe(root, BASE + p).read_bytes() == b, 'Generated resource differs: ' + p)
    else:
        # Authenticate every input and construct every output before writing.
        for p,b in [(RAW+p,b) for p,b in raw.items()] + [(BASE+p,b) for p,b in outputs.items()]:
            target = safe(root,p); target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(b)
    return dict(sourceFiles=len(raw), trainers=9, portraits=4, newPortraits=2, newSpecies=0, newAudioCues=0,
         manifestBytes=len(outputs['manifest.json']), sourceAndPixelChecksOnly=True)

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project-root',type=pathlib.Path,default=ROOT)
    p.add_argument('--inherited-root',type=pathlib.Path)
    p.add_argument('--reference-root',type=pathlib.Path)
    p.add_argument('--check',action='store_true')
    a=p.parse_args()
    print(json.dumps(run(a.project_root,a.inherited_root,a.reference_root,a.check)))
