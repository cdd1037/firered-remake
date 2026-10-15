#!/usr/bin/env python3
"""Rebuild only three missing portraits; authenticate reused Gym, badge, TM and synth resources."""
import argparse
import importlib.util
import json
import pathlib
import re
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parents[1]
BASE = 'content/generated/fuchsia-gym-presentation/'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
# Initial-build witnesses are retained in the immutable lock, not used to generate portraits.
BUILD_WITNESSES = frozenset('src/Game.Godot/OriginalAudioCore_' + name + '.cs'
    for name in ('Cry', 'CryCatalog', 'Profiles', 'PsgSynth', 'Resources', 'Sequence', 'Synth'))
PORTRAITS = [('juggler-front', 'juggler', 'Juggler', 'JUGGLER'),
             ('tamer-front', 'tamer', 'Tamer', 'TAMER'),
             ('koga-front', 'leader_koga', 'LeaderKoga', 'LEADER_KOGA')]
TRAINERS = [('JUGGLER_KAYDEN', 'KAYDEN', 'JUGGLER'), ('JUGGLER_SHAWN', 'SHAWN', 'JUGGLER'),
            ('JUGGLER_KIRK', 'KIRK', 'JUGGLER'), ('TAMER_EDGAR', 'EDGAR', 'TAMER'),
            ('TAMER_PHIL', 'PHIL', 'TAMER'), ('JUGGLER_NATE', 'NATE', 'JUGGLER'),
            ('LEADER_KOGA', 'KOGA', 'LEADER')]
CUES = ['MUS_GYM', 'MUS_ENCOUNTER_BOY', 'MUS_ENCOUNTER_GYM_LEADER', 'MUS_VS_TRAINER',
        'MUS_VS_GYM_LEADER', 'MUS_VICTORY_TRAINER', 'MUS_VICTORY_GYM_LEADER', 'MUS_LEVEL_UP']


def encode(value):
    return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()


def safe(root, relative):
    if (not relative or pathlib.PurePosixPath(relative).is_absolute() or '\\' in relative or
            any(p in ('', '.', '..') for p in relative.split('/'))):
        raise ValueError('Unsafe Fuchsia Gym presentation path')
    path = pathlib.Path(root) / relative
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError('Fuchsia Gym presentation symlink rejected')
    return path


def authenticated(root=ROOT):
    raw = safe(root, BASE + 'source-lock.json').read_bytes()
    locked = json.loads(raw)
    if (locked['schemaVersion'], locked['repository'], locked['commit'], len(locked['files']), len(locked['inputs'])) != (1, 'pret/pokefirered', COMMIT, 18, 28):
        raise ValueError('Fuchsia Gym presentation source scope differs')
    source, inputs = {}, {}
    for row in locked['files']:
        data = safe(root, row['projectPath']).read_bytes()
        source[row['path']] = data
    for row in locked['inputs']:
        if row['path'] in BUILD_WITNESSES:
            continue
        data = safe(root, row['path']).read_bytes()
        inputs[row['path']] = data
    return locked, source, inputs


def need(condition, message):
    if not condition:
        raise ValueError(message)


def build(root, source, inputs):
    graphics = source['src/data/graphics/trainers.h'].decode()
    tables = source['src/data/trainer_graphics/front_pic_tables.h'].decode()
    definitions = source['src/data/trainers.h'].decode()
    class_names = source['src/data/text/trainer_class_names.h'].decode()
    constants = source['include/constants/trainers.h'].decode()
    battle_setup = source['src/battle_setup.c'].decode()
    scripts = source['data/maps/FuchsiaCity_Gym/scripts.inc'].decode()
    texts = source['data/maps/FuchsiaCity_Gym/text.inc'].decode()
    info = json.loads(source['data/maps/FuchsiaCity_Gym/map.json'])
    need(info['music'] == 'MUS_GYM' and info['battle_scene'] == 'MAP_BATTLE_SCENE_GYM', 'Gym map scene/music differs')
    need('MAP_BATTLE_SCENE_GYM' in source['src/battle_bg.c'].decode(), 'Gym source terrain selector differs')
    need(re.search(r'#define TRAINER_ENCOUNTER_MUSIC_HIKER\s+11\b', constants), 'Trainer music constant differs')
    need(re.search(r'case TRAINER_ENCOUNTER_MUSIC_HIKER:.*?music = MUS_ENCOUNTER_BOY;', battle_setup, re.S), 'Trainer encounter selector differs')
    catalog = json.loads(inputs['content/generated/fuchsia-gym-trainers/trainers.json'])
    rows = {t['id']['value']: t for t in catalog['trainers']}
    battles = {b['trainer']['value']: b for b in catalog['battles']}
    need(len(rows) == len(battles) == 7, 'Gym roster must contain seven exact source trainers')
    bindings = []
    for trainer, name, cls in TRAINERS:
        id = 'pret:TRAINER_' + trainer
        need(id in rows and id in battles, 'Missing source trainer: ' + id)
        row, battle = rows[id], battles[id]
        leader = cls == 'LEADER'
        picture = 'LEADER_KOGA' if leader else cls
        portrait = 'koga-front' if leader else cls.lower() + '-front'
        music_symbol = 'MALE' if leader else 'HIKER'
        music_gender = 0 if leader else 11
        block = re.search(r'\[TRAINER_' + trainer + r'\] = \{(.*?)\n    \}', definitions, re.S)
        need(block is not None, 'Source trainer missing: ' + trainer)
        for field, value in [('trainerClass', 'TRAINER_CLASS_' + cls),
                             ('trainerPic', 'TRAINER_PIC_' + picture),
                             ('encounterMusic_gender', 'TRAINER_ENCOUNTER_MUSIC_' + music_symbol)]:
            need(re.search(r'\.' + field + r'\s*=\s*' + value + r'\s*,', block[1]), 'Source trainer binding differs: ' + trainer + '/' + field)
        need('.trainerName = _("' + name + '")' in block[1], 'Trainer name differs')
        need(re.search(r'\[TRAINER_CLASS_' + cls + r'\]\s*= _\("' + cls + r'"\)', class_names), 'Trainer class text differs')
        need((row['sourceTrainerName'], row['trainerClass'], row['trainerPic'], row['encounterMusicGender']) ==
             (name, 'TRAINER_CLASS_' + cls, 'TRAINER_PIC_' + picture, music_gender), 'Generated trainer binding differs')
        need(not row['doubleBattle'] and battle['mode'] == (1 if leader else 0), 'Trainer battle presentation mode differs')
        need(battle['musicOption'] == ('NO_MUSIC' if leader else None), 'Trainer music option differs')
        bindings.append(dict(trainer=id, name=name, trainerClass=row['trainerClass'], className=cls,
                             portrait=portrait, trainerPic=row['trainerPic'], encounterMusicGender=music_gender,
                             gender='male', encounterMusic='MUS_ENCOUNTER_GYM_LEADER' if leader else 'MUS_ENCOUNTER_BOY',
                             encounterTrigger='final-intro-text-printer-eos' if leader else 'trainer-script',
                             battleMusic='MUS_VS_GYM_LEADER' if leader else 'MUS_VS_TRAINER',
                             victoryMusic='MUS_VICTORY_GYM_LEADER' if leader else 'MUS_VICTORY_TRAINER'))
    intro = texts.split('FuchsiaCity_Gym_Text_KogaIntro::', 1)[1].split('\n\n', 1)[0]
    defeat = texts.split('FuchsiaCity_Gym_Text_KogaDefeat::', 1)[1].split('\n\n', 1)[0]
    need('POISON-type POKéMON!{PLAY_BGM}{MUS_ENCOUNTER_GYM_LEADER}$' in intro, 'Koga intro cue differs')
    need('{PLAY_BGM}' not in defeat and 'MUS_OBTAIN_BADGE' not in texts and 'MUS_OBTAIN_BADGE' not in scripts,
         'Do not invent a Koga badge fanfare absent from the source')
    need('setflag FLAG_BADGE05_GET' in scripts and 'setflag FLAG_GOT_TM06_FROM_KOGA' in scripts and
         'giveitem_msg FuchsiaCity_Gym_Text_ReceivedTM06FromKoga, ITEM_TM06\n' in scripts, 'Koga reward script differs')
    need('.macro giveitem_msg msg:req, item:req, amount=1, fanfare=MUS_LEVEL_UP' in source['asm/macros/event.inc'].decode(),
         'TM06 source fanfare differs')
    card = json.loads(inputs['content/generated/trainer-card/trainer-card.json'])
    need(any(b['flag'] == 'FLAG_BADGE05_GET' and b['assetId'] == 'badge-5' for b in card['layout']['badges']), 'Soul Badge display differs')
    cues = []
    for path, data in inputs.items():
        if path.endswith('/manifest.json') and 'synth-audio' in path:
            manifest = json.loads(data)
            need(manifest['sourceCommit'] == COMMIT and manifest['schemaVersion'] == 1, 'Synth source identity differs')
            cues.extend(manifest['cues'])
    by_id = {c['id']: c for c in cues}
    need(len(cues) == len(by_id) == 303 and all(id in by_id for id in CUES), 'Existing synth cue closure differs')
    spec = importlib.util.spec_from_file_location('_fuchsia_gym_codec', safe(root, 'scripts/import-lab-battle-ui.py'))
    codec = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(codec)
    output, portraits = {}, []
    for id, stem, symbol, picture in PORTRAITS:
        for binding in ['gTrainerFrontPic_' + symbol, 'graphics/trainers/front_pics/' + stem + '_front_pic.4bpp.lz',
                        'gTrainerPalette_' + symbol, 'graphics/trainers/palettes/' + stem + '.gbapal.lz']:
            need(binding in graphics, 'Portrait graphics binding differs: ' + binding)
        need(f'TRAINER_SPRITE({picture}, gTrainerFrontPic_{symbol}, 0x800)' in tables and
             f'TRAINER_PAL({picture}, gTrainerPalette_{symbol})' in tables, 'Portrait table differs: ' + id)
        w, h, indices, _ = codec.indexed_png(source['graphics/trainers/front_pics/' + stem + '_front_pic.png'])
        need((w, h) == (64, 64), 'Portrait dimensions differ')
        rgba = codec.rgba(indices, codec.palette(source['graphics/trainers/palettes/' + stem + '.pal']))
        png = codec.png(w, h, rgba)
        output[id + '.png'] = png
        portraits.append(dict(id=id, width=w, height=h, texturePath=id + '.png', trainerPicture='TRAINER_PIC_' + picture))
    metadata = dict(trainers=bindings, portraits=portraits, trainerTopLeft=[144, 8], transparentIndex=0,
                    map=dict(id='map.fuchsia-city-gym', music='MUS_GYM', battleScene='MAP_BATTLE_SCENE_GYM',
                             gymTexture='content/generated/gym-battle-terrain/gym-scene.png',
                             leaderTexture='content/generated/gym-battle-terrain/leader-scene.png'),
                    rewards=dict(badgeFlag='FLAG_BADGE05_GET', badgeAsset='badge-5', badgeMusicControl=None,
                                 tmItem='pret:ITEM_TM06', tmOnceFlag='FLAG_GOT_TM06_FROM_KOGA', tmFanfare='MUS_LEVEL_UP',
                                 tmTexture='content/generated/fuchsia-gym-items/item_tm06.png'),
                    audio=dict(backend='original-csharp-source-filtered-v1', inheritedCueCount=303,
                               requiredCues=[by_id[id] for id in CUES], newCues=0, recordingsWritten=False,
                               audibleOutputVerified=False))
    return metadata, output


def run(root=ROOT, check=False):
    locked, source, inputs = authenticated(root)
    metadata, output = build(root, source, inputs)
    output['manifest.json'] = encode(dict(schemaVersion=1, profile='firered-fuchsia-gym-presentation-v1',
        sourceCommit=COMMIT,   inputs=locked['files'],
        reusedInputs=locked['inputs'], upstreamScriptsExecuted=False, **metadata,
        outputs=[dict(path=p) for p, b in sorted(output.items())]))
    for relative, data in output.items():
        path = safe(root, BASE + relative)
        if check:
            need(path.read_bytes() == data, 'Generated presentation differs: ' + relative)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    return dict(passed=True, sourceFiles=len(source), reusedInputs=len(inputs), historicalBuildWitnesses=len(BUILD_WITNESSES), portraits=3, trainers=7,
                inheritedCues=303, newCues=0, recordingsWritten=False,
                manifestBytes=len(output['manifest.json']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root, args.check), indent=2))
