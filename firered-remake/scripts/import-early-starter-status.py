#!/usr/bin/env python3
"""Import three pinned sleep indicators from the existing, read-only source atlas."""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import re
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-early-starter-status-ui-v1'
LOCK_SHA256 = '65c40b7f394c624f14eee023c234615515593d352d0361cca193d48f7a0067cb'
RAW_ROOT = 'content/raw/forest-status-ui'
BATTLE = 'graphics/battle_interface/'
PARTY = 'graphics/interface/status_icons.png'
PATHS = frozenset([
    'Makefile', 'src/battle_interface.c', 'src/party_menu.c', 'src/data/party_menu.h',
    'src/graphics.c', 'include/constants/party_menu.h', 'tools/gbagfx/gfx.c',
    'tools/gbagfx/convert_png.c', BATTLE + 'healthbox_elements.png', BATTLE + 'healthbox.pal', PARTY,
])
# Verified with the independent source-index/palette oracle.
EXPECTED_OUTPUT_HASHES = {
    "battle-opponent-sleep.png": "bfb791ea77322db36e3ceeb2df063868524bb883b473df12311016f8f9737668",
    "battle-opponent-sleep.rgba": "358a1a5cf8a823849c59ca1623b645edac0f94ba4be746be7e4aa74e980d5b2a",
    "battle-player-sleep.png": "bfb791ea77322db36e3ceeb2df063868524bb883b473df12311016f8f9737668",
    "battle-player-sleep.rgba": "358a1a5cf8a823849c59ca1623b645edac0f94ba4be746be7e4aa74e980d5b2a",
    "early-starter-status-ui.json": "89b61cca08d122eb2223e07145e2b5c7f345d1a93a0549dc0a8ce43c7f1bc6af",
    "manifest.json": "08d98187d952b0cfdc93073152e658413fe5a135b93509c23daa3c8b7d27fb23",
    "party-sleep.png": "f6a27e4aaeffc02a464a2705fb48745deb9c70d1681fc1cd140f1afb405eb3ff",
    "party-sleep.rgba": "f25aad714560e830c8085f3127ccb2d83e3f7a1b4a13b89bd483f3b04be0d096"
}
spec = importlib.util.spec_from_file_location('early_starter_status_codec', ROOT / 'scripts/import-lab-battle-ui.py')
codec = importlib.util.module_from_spec(spec)
spec.loader.exec_module(codec)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()


def pairs(entries):
    result = {}
    for key, value in entries:
        if key in result:
            raise ValueError('Duplicate JSON property: ' + key)
        result[key] = value
    return result


def safe(root, relative):
    root = pathlib.Path(root).absolute()
    parts = pathlib.PurePosixPath(relative)
    if not relative or parts.is_absolute() or any(p in ('', '.', '..') for p in relative.split('/')) or '\\' in relative:
        raise ValueError('Unsafe early-starter status path: ' + relative)
    target = root / relative
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Escaping early-starter status path: ' + relative)
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Early-starter status symlink rejected: ' + relative)
    return target


def source_hash(lock):
    return digest(''.join(e['path'] + '\0' + e['sha256'] + '\n'
                          for e in sorted(lock['files'], key=lambda e: e['path'])).encode())


def verify(data, entry):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
    if (len(data), digest(data), blob) != (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']):
        raise ValueError('Early-starter status source hash differs: ' + entry['path'])


def load_lock(path=ROOT / 'scripts/fetch-early-starter-status.lock.json', project_path=ROOT / 'content/source-lock.json'):
    data = pathlib.Path(path).read_bytes()
    if digest(data) != LOCK_SHA256:
        raise ValueError('Early-starter status lock hash differs')
    lock = json.loads(data, object_pairs_hook=pairs)
    if (lock['schemaVersion'], lock['profile'], lock['repository'], lock['commit']) != (1, PROFILE, REPOSITORY, COMMIT):
        raise ValueError('Early-starter status source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Early-starter status source set differs')
    project = json.loads(pathlib.Path(project_path).read_bytes(), object_pairs_hook=pairs)
    if (project['source']['repository'], project['source']['sourceCommit'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Early-starter status project edition/revision differs')
    for entry in lock['files']:
        if (type(entry['byteLength']) is not int or not 0 < entry['byteLength'] <= 1_000_000
                or not re.fullmatch('[0-9a-f]{64}', entry['sha256'])
                or not re.fullmatch('[0-9a-f]{40}', entry['gitBlobSha1'])
                or entry['sourceUrl'] != f"https://github.com/{REPOSITORY}/blob/{COMMIT}/{entry['path']}"):
            raise ValueError('Early-starter status source metadata differs')
    return lock


def read_sources(lock, raw):
    sources = {}
    for entry in lock['files']:
        data = safe(raw, entry['path']).read_bytes()
        verify(data, entry)
        sources[entry['path']] = data
    return sources


def semantics(lock, sources):
    refs = []

    def require(path, symbol, needle):
        source = sources[path].decode()
        if source.count(needle) != 1:
            raise ValueError('Missing/ambiguous sleep icon source semantics: ' + symbol)
        line = source.count('\n', 0, source.index(needle)) + 1
        end = line + needle.count('\n')
        entry = next(e for e in lock['files'] if e['path'] == path)
        refs.append(dict(path=path, symbol=symbol, startLine=line, endLine=end,
                         sha256=entry['sha256'], url=entry['sourceUrl'] + f'#L{line}-L{end}'))

    for path, symbol, needle in [
        ('src/battle_interface.c', 'player SLP tile', '#define B_INTERFACE_GFX_STATUS_SLP_BATTLER0     27'),
        ('src/battle_interface.c', 'opponent SLP tile', '#define B_INTERFACE_GFX_STATUS_SLP_BATTLER1     77'),
        ('src/battle_interface.c', 'SLP RGB555 color', '[PAL_STATUS_SLP] = RGB(20, 20, 17)'),
        ('src/battle_interface.c', 'sleep status selection',
         '    if (status & STATUS1_SLEEP)\n    {\n        statusGfxPtr = GetBattleInterfaceGfxPtr(GetStatusIconForBattlerId(B_INTERFACE_GFX_STATUS_SLP_BATTLER0, battlerId));\n        statusPalId = PAL_STATUS_SLP;\n    }'),
        ('src/battle_interface.c', 'single healthbox positions',
         '        if (GetBattlerSide(battler) != B_SIDE_PLAYER)\n            x = 44, y = 30;\n        else\n            x = 158, y = 88;'),
        ('src/battle_interface.c', 'single player destination tile',
         '        if (!IsDoubleBattle())\n            tileNumAdder = 0x1A;\n        else\n            tileNumAdder = 0x12;'),
        ('src/battle_interface.c', 'opponent destination tile',
         '        status = GetMonData(&gEnemyParty[gBattlerPartyIndexes[battlerId]], MON_DATA_STATUS);\n        tileNumAdder = 0x11;'),
        ('src/battle_interface.c', 'dynamic palette entry',
         '    pltAdder += battlerId + 12;\n\n    FillPalette(sStatusIconColors[statusPalId], pltAdder + OBJ_PLTT_OFFSET, PLTT_SIZEOF(1));'),
        ('src/battle_interface.c', 'three-tile copy',
         '    CpuCopy32(statusGfxPtr, (void *)(OBJ_VRAM0 + (gSprites[healthboxSpriteId].oam.tileNum + tileNumAdder) * TILE_SIZE_4BPP), 3 * TILE_SIZE_4BPP);'),
        ('src/graphics.c', 'battle tile binding', 'const u8 gBattleInterface_Gfx[] = INCBIN_U8("graphics/battle_interface/healthbox_elements.4bpp");'),
        ('src/graphics.c', 'battle palette binding', 'const u16 gBattleInterface_Healthbox_Pal[] = INCBIN_U16("graphics/battle_interface/healthbox.gbapal");'),
        ('src/graphics.c', 'party graphics and palette binding',
         'const u32 gStatusGfx_Icons[] = INCBIN_U32("graphics/interface/status_icons.4bpp.lz");\nconst u32 gStatusPal_Icons[] = INCBIN_U32("graphics/interface/status_icons.gbapal.lz");'),
        ('src/data/party_menu.h', 'party sleep tile',
         'static const union AnimCmd sSpriteAnim_StatusSleep[] =\n{\n    ANIMCMD_FRAME(8, 0),\n    ANIMCMD_END\n};'),
        ('src/data/party_menu.h', 'party animation index 2',
         '    sSpriteAnim_StatusPoison,\n    sSpriteAnim_StatusParalyzed,\n    sSpriteAnim_StatusSleep,'),
        ('src/data/party_menu.h', 'party 32x8 sprite', '.shape = SPRITE_SHAPE(32x8),\n    .x = 0,\n    .matrixNum = 0,\n    .size = SPRITE_SIZE(32x8),'),
        ('src/data/party_menu.h', 'single party icon centers',
         '        { 16,  40,  20,  50,  56,  52,  16,  34},\n        {104,  18, 108,  28, 144,  27, 102,  25},\n        {104,  42, 108,  52, 144,  51, 102,  49},\n        {104,  66, 108,  76, 144,  75, 102,  73},\n        {104,  90, 108, 100, 144,  99, 102,  97},\n        {104, 114, 108, 124, 144, 123, 102, 121},'),
        ('include/constants/party_menu.h', 'party sleep ailment', '#define AILMENT_SLP   3'),
        ('src/party_menu.c', 'party sleep selection', '    if (status & STATUS1_SLEEP)\n        return AILMENT_SLP;'),
        ('src/party_menu.c', 'party status animation dispatch', '        StartSpriteAnim(&gSprites[menuBox->statusSpriteId], status - 1);'),
        ('src/party_menu.c', 'fainting precedes sleep', '    if (GetMonData(mon, MON_DATA_HP) == 0)\n        return AILMENT_FNT;'),
        ('Makefile', 'PNG to 4bpp conversion', '%.4bpp:   %.png  ; $(GFX) $< $@'),
        ('Makefile', 'PNG to palette conversion', '%.gbapal: %.png  ; $(GFX) $< $@'),
        ('tools/gbagfx/gfx.c', 'RGB8 to RGB5', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
        ('tools/gbagfx/convert_png.c', '4bpp source index modulo', '% (1 << destBitDepth)'),
    ]:
        require(path, symbol, needle)
    return refs


def convert(lock, sources):
    if set(sources) != PATHS:
        raise ValueError('Incomplete early-starter status source data')
    for entry in lock['files']:
        verify(sources[entry['path']], entry)
    refs = semantics(lock, sources)
    bw, bh, _, _ = codec.indexed_png(sources[BATTLE + 'healthbox_elements.png'])
    if (bw, bh) != (320, 24):
        raise ValueError('Battle atlas dimensions differ')
    elements = codec.tile_list(sources[BATTLE + 'healthbox_elements.png'])
    health_palette = codec.palette(sources[BATTLE + 'healthbox.pal'])
    pw, ph, party_indices, party_rgb = codec.indexed_png(sources[PARTY])
    if (pw, ph, len(party_rgb), len(health_palette)) != (256, 8, 16, 16):
        raise ValueError('Party atlas or palette differs')
    outputs, assets = {}, []

    def add(id, indices, palette, width, x, y, tile, image_path, palette_path):
        pixels = codec.rgba(indices, palette)
        outputs[id + '.png'] = codec.png(width, 8, pixels)
        outputs[id + '.rgba'] = pixels
        assets.append(dict(id=id, texturePath=id + '.png', rgbaPath=id + '.rgba', width=width, height=8,
            x=x, y=y, context=id.removesuffix('-sleep'), status='sleep', sourceTile=tile,
            sourceImagePath=image_path, sourcePalettePath=palette_path, indexSha256=digest(indices),
            paletteRgb555=[(r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10) for r, g, b in palette],
            transparentIndex=0, sha256=digest(outputs[id + '.png']), rgbaSha256=digest(pixels)))

    for side, battler, tile, x, y in [('player', 0, 27, 142, 96), ('opponent', 1, 77, 20, 30)]:
        palette = list(health_palette)
        palette[12 + battler] = tuple((c << 3) | (c >> 2) for c in (20, 20, 17))
        indices = bytes(elements[tile + column // 8][row * 8 + column % 8]
                        for row in range(8) for column in range(24))
        add('battle-' + side + '-sleep', indices, palette, 24, x, y, tile,
            BATTLE + 'healthbox_elements.png', BATTLE + 'healthbox.pal')
    add('party-sleep', bytes(party_indices[y * pw + 64 + x] for y in range(8) for x in range(32)),
        codec.colors(party_rgb), 32, 0, 0, 8, PARTY, PARTY)
    document = dict(schemaVersion=1, profile=PROFILE, repository=REPOSITORY, sourceCommit=COMMIT,
        rawRoot=RAW_ROOT, fullSourceHash=source_hash(lock), inputs=lock['files'], assets=assets, sourceRefs=refs,
        partyStatusTopLeft=[dict(x=x, y=y) for x, y in [(40, 48), (128, 23), (128, 47), (128, 71), (128, 95), (128, 119)]],
        limitations=['Only ordinary single-battle player/opponent and party sleep pixels; no status-rule implementation.',
            'Nearest filtering, binary index-zero alpha, RGB555 expansion (rgb5 << 3) | (rgb5 >> 2).',
            'Party asset coordinates are local; use the six source slot top-left positions, and fainting precedes sleep.',
            'Source-pixel verification does not establish original-ROM frame, native-window or audible-output acceptance.',
            'Existing forest raw inputs are read-only; no ROM download, upstream script execution or public upload.'])
    outputs['early-starter-status-ui.json'] = canonical(document)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        fullSourceHash=source_hash(lock), outputs=[dict(path=name, byteLength=len(data), sha256=digest(data))
            for name, data in sorted(outputs.items())], upstreamScriptsExecuted=False, romDownloaded=False,
        publiclyUploaded=False, runtimeShadersUsed=False))
    return outputs


def run(lock_path, project_path, raw, output):
    if raw.resolve().is_relative_to(output.resolve()) or output.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Early-starter status raw/output roots overlap')
    lock = load_lock(lock_path, project_path)
    outputs = convert(lock, read_sources(lock, raw))
    if {name: digest(data) for name, data in outputs.items()} != EXPECTED_OUTPUT_HASHES:
        raise ValueError('Early-starter status reviewed output hash differs')
    targets = [(safe(output, name), data) for name, data in outputs.items()]
    for target, _ in targets:
        safe(target.parent, target.name + '.tmp')
    for target, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        staged = target.with_name(target.name + '.tmp')
        staged.write_bytes(data)
        staged.replace(target)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / RAW_ROOT)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/early-starter-status')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/fetch-early-starter-status.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
    args = parser.parse_args()
    outputs = run(args.lock, args.project_lock, args.raw, args.output)
    print(json.dumps(dict(status='verified', sourceFiles=len(PATHS), assets=3, outputs=len(outputs),
                          catalogSha256=digest(outputs['early-starter-status-ui.json']))))


if __name__ == '__main__':
    main()
