#!/usr/bin/env python3
"""Revision-0 Self-Destruct/Explosion source import; no ROM input."""
import argparse,  io, json, re, struct, subprocess
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
OPS = {'loadspritegfx', 'createvisualtask', 'createsprite', 'call', 'return', 'delay', 'waitforvisualfinish', 'playsewithpan', 'end'}


def read_sources(reference):
    lock = json.loads(Path(__file__).with_name('source-explosion-animation.lock.json').read_bytes())
    if lock['sourceCommit'] != COMMIT:
        raise ValueError('Wrong source revision')
    result = {}
    for row in lock['files']:
        path = row['path']
        result[path] = (subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + path])
                        if (reference / '.git').exists() else (reference / path).read_bytes())
    return result

def build(reference):
    raw = read_sources(reference); source = raw['data/battle_anim_scripts.s'].decode()
    macros = set(re.findall(r'^\s*\.macro (\w+)', raw['asm/macros/battle_anim_script.inc'].decode(), re.M))
    def commands(label, end):
        start = re.search(r'^' + label + r':\n', source, re.M)
        if not start: raise ValueError('Missing root: ' + label)
        result = []; conditional = False; enabled = True
        for offset, line in enumerate(source[start.end():].splitlines()):
            text = line.split('@', 1)[0].strip()
            if not text: continue
            if text == '.if REVISION >= 0xA':
                if conditional: raise ValueError('Nested revision branch')
                conditional = True; enabled = False; continue
            if text == '.else' and conditional: enabled = True; continue
            if text == '.endif' and conditional: conditional = False; enabled = True; continue
            if not enabled: continue
            if text.endswith(':'): raise ValueError('Unexpected label')
            op, *tail = text.split(None, 1)
            args = [x.strip() for x in re.split(r',\s*(?![^()]*\))', tail[0])] if tail else []
            if op not in OPS or op not in macros: raise ValueError('Unadmitted command: ' + op)
            result.append(dict(op=op, args=args, line=source[:start.end()].count('\n') + offset + 1))
            if op == end:
                if conditional: raise ValueError('Unclosed revision branch')
                return result
        raise ValueError('Unterminated root')
    scripts = [dict(moveId=n, label='Move_' + name, commands=commands('Move_' + name, 'end')) for n, name in [(120, 'SELF_DESTRUCT'), (153, 'EXPLOSION')]]
    subs = {name: commands(name, 'return') for name in ['SelfDestructExplode', 'Explosion1']}
    source = raw['src/battle_anim_effects_2.c'].decode()
    template = re.search(r'const struct SpriteTemplate gExplosionSpriteTemplate =\s*\{.*?\};', source, re.S)
    if not template or any(x not in template[0] for x in ['.tileTag = ANIM_TAG_EXPLOSION', '.paletteTag = ANIM_TAG_EXPLOSION', '.oam = &gOamData_AffineOff_ObjNormal_32x32', '.anims = sExplosionAnimTable', '.callback = AnimSpriteOnMonPos']): raise ValueError('Explosion sprite recipe differs')
    animation = re.search(r'static const union AnimCmd sExplosionAnimCmds\[\] =\s*\{(.*?)\};', source, re.S)
    frames = [dict(tile=int(t), duration=int(d), hFlip=False, vFlip=False) for t, d in re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)', animation[1])]
    if frames != [dict(tile=t, duration=5, hFlip=False, vFlip=False) for t in (0, 16, 32, 48)] or not animation[1].strip().endswith('ANIMCMD_END,'): raise ValueError('Explosion frame recipe differs')
    graphics = raw['src/graphics.c'].decode(); tables = raw['src/data/battle_anim.h'].decode()
    for fragment in ['{gBattleAnimSpriteGfx_Explosion, 0x0800, ANIM_TAG_EXPLOSION}', '{gBattleAnimSpritePal_Explosion, ANIM_TAG_EXPLOSION}']:
        if fragment not in tables: raise ValueError('Source explosion graphic/palette membership differs')
    for fragment in ['gBattleAnimSpriteGfx_Explosion[] = INCBIN_U32("graphics/battle_anims/sprites/explosion.4bpp.lz")', 'gBattleAnimSpritePal_Explosion[] = INCBIN_U32("graphics/battle_anims/sprites/explosion.gbapal.lz")']:
        if fragment not in graphics: raise ValueError('Source explosion sheet/palette input differs')
    with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/explosion.png'])) as im:
        if im.mode != 'P' or im.size != (32, 128): raise ValueError('Explosion art shape differs')
        indices = im.tobytes(); palette = im.getpalette()
    if len(indices) != 4096 or max(indices) > 15: raise ValueError('Explosion 4bpp overflow')
    colors = [(palette[i*3]>>3) | ((palette[i*3+1]>>3)<<5) | ((palette[i*3+2]>>3)<<10) for i in range(16)]
    material = struct.pack('<HH', 32, 128) + struct.pack('<4096H', *[colors[i] | (0x8000 if i else 0) for i in indices])
    recipe = dict(template='gExplosionSpriteTemplate', asset='explosion', callback='AnimSpriteOnMonPos', blend=False, animations=[frames], sourcePath='src/battle_anim_effects_2.c', sourceLine=source.count('\n', 0, template.start())+1)
    manifest = dict(schemaVersion=1, sourceCommit=COMMIT,  revision=0, scripts=scripts, subroutines=subs, assets=[dict(id='explosion', path='explosion.rgb555')], spriteRecipes=[recipe])
    return (json.dumps(manifest, indent=2)+'\n').encode(), material

if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('--reference', type=Path, default=Path((ROOT/'.tools/reference-root').read_text().strip()) if (ROOT/'.tools/reference-root').exists() else None); p.add_argument('--output', type=Path, default=ROOT/'content/generated/source-explosion-animations'); p.add_argument('--check', action='store_true'); a = p.parse_args()
    if a.reference is None: p.error('--reference is required without .tools/reference-root')
    manifest, material = build(a.reference)
    for name, data in [('manifest.json', manifest), ('explosion.rgb555', material)]:
        if a.check:
            if (a.output/name).read_bytes() != data: raise SystemExit('Generated output differs: '+name)
        else:
            a.output.mkdir(parents=True, exist_ok=True); (a.output/name).write_bytes(data)
    print(json.dumps(dict( dedicatedMoveRoots=2, revision=0)))
