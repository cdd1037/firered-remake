#!/usr/bin/env python3
"""Rebuild trainer-only rematch presentation from pinned Git blobs; no source execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from presentation_source import Source, safe, need
from image_io import png
import argparse
import io
import json
import pathlib
import re
import runpy
import subprocess
from PIL import Image

ROOT = _PROJECT_ROOT
BASE = 'content/generated/sprites/pokemon/league-rematch/'
RAW = 'content/raw/pokefirered/037335f4/'
SPECIES = [('Crobat', 169), ('Misdreavus', 200), ('Steelix', 208), ('Heracross', 214), ('Kingdra', 230), ('Tyranitar', 248)]


def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets, sprites, inherited_sprites, species_names = {}, [], [], [], []
    pewter_path = 'content/generated/sprites/pokemon/pewter/battle-art.json'
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
                    prefix = 'content/generated/sprites/pokemon/pewter/'
                    prior = next(p for p in pewter_sprites if p['sourceSpeciesId'] == species and p['side'] == side and p['paletteVariant'] == variant)
                    prior_png = source.reuse(prefix + prior['texturePath'])
                    need(Image.open(io.BytesIO(prior_png)).convert('RGBA').tobytes() == rgba, 'Inherited Steelix pixels differ')
                    need((prior['sourceWidth'], prior['sourceHeight'], prior['sourceYOffset'], prior['sourceEnemyElevation']) ==
                         (width, height, y_offset, elevation), 'Inherited Steelix coordinates differ')
                    row = dict(id=identity, width=64, height=64, texturePath=prefix + prior['texturePath'],  inherited=True)
                else:
                    out[identity + '.png'] = png(64, 64, rgba)
                    row = dict(id=identity, width=64, height=64, texturePath=BASE + identity + '.png')
                assets.append(row)
                (inherited_sprites if species == 208 else sprites).append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, side=side, paletteVariant=variant,
                    x=144 if side == 'front' else 40, y=8 + y_offset - elevation if side == 'front' else 48 + y_offset,
                    sourceWidth=width, sourceHeight=height, sourceYOffset=y_offset, sourceEnemyElevation=elevation,
                     sourceCoordinatePath=coordinates, paletteExpansion='floor(rgb5 * 255 / 31)', transparentIndex=0, **row))


    geometry_manifest = 'content/generated/battle/art/campaign-hit-art/manifest.json'
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
    league_manifest_path = 'content/generated/sprites/pokemon/pokemon-league/manifest.json'
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
    manifest = runtime_json_bytes(dict(schemaVersion=1,

        assets=assets, sprites=sprites, inheritedSprites=inherited_sprites, inheritedSpriteManifest=pewter_path, speciesNames=species_names, pokemonIcons=[], footprints=[], newOwnedSpecies=[],
        newOpponentOnlySpecies=[species for _, species in SPECIES if species != 208], trainers=trainers, inheritedTrainerManifest=league_manifest_path,
        moveAnimationGeometry=dict(inheritedManifest=geometry_manifest, rows=inherited_geometry),

        limits=['Trainer-only species do not extend player ownership.', 'Existing battle sprites, names, trainer portraits and music selection are reused.',
            'No party/storage icon or footprint consumer is admitted for these trainer-only species.',
            'Existing bounded move animation dispatch and geometry are reused; no new move animation coverage.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})

    report = dict(passed=True, sourceFiles=len(source.raw), inheritedInputs=len(source.inherited), sprites=len(sprites),
        inheritedSprites=len(inherited_sprites), pokemonIcons=0, footprints=0, assets=len(assets), moveGeometry=len(inherited_geometry), trainers=len(trainers),
        outputs=len(out) - 1,  manifestBytes=len(manifest),

         sourceAndStaticChecksOnly=True, nativeWindowVerified=False)
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
