#!/usr/bin/env python3
"""Rebuild Mewtwo pixels and cue metadata from pinned local blobs; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from species_art import selected_species_art
from presentation_source import Source, need
from presentation_output import asset_emitter, publish
import argparse
import json
import pathlib
import re

ROOT = _PROJECT_ROOT
BASE = 'content/generated/sprites/pokemon/mewtwo/'
RAW = 'content/raw/pokefirered/037335f4/'
SPECIES = [('Mewtwo', 150)]


def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets = {}, []

    emit = asset_emitter(source, out, assets, BASE)

    sprites, icons, footprints = selected_species_art(source, SPECIES, SPECIES, emit)

    # Encounter configuration remains presentation data; cue compilation is shared.
    script_path = 'data/maps/CeruleanCave_B1F/scripts.inc'
    setup_path = 'src/battle_setup.c'
    script = source.text(script_path)
    need('playmoncry SPECIES_MEWTWO, CRY_MODE_ENCOUNTER' in script, 'Mewtwo encounter cry binding differs')
    inherited_symbol, symbol = 'MUS_ENCOUNTER_GYM_LEADER', 'MUS_VS_MEWTWO'
    song_id = int(re.search(r'^#define MUS_VS_MEWTWO\s+(\d+)', source.text('include/constants/songs.h'), re.M)[1])
    need(symbol in source.text(setup_path), 'Mewtwo battle music binding missing')
    # Existing source move animations already cover these source geometry rows.
    geometry_manifest = 'content/generated/battle/art/campaign-hit-art/manifest.json'
    geometry = json.loads(source.reuse(geometry_manifest))['geometry']
    inherited_geometry = []
    for row in sprites:
        if row['paletteVariant'] != 'normal':
            continue
        expected = dict(species=row['sourceSpeciesId'], side=row['side'], sourceWidth=row['sourceWidth'],
                        sourceHeight=row['sourceHeight'], yOffset=row['sourceYOffset'],
                        elevation=row['sourceEnemyElevation'] if row['side'] == 'front' else 0)
        need(expected in geometry, 'Inherited move-animation geometry differs')
        inherited_geometry.append(expected)

    manifest = runtime_json_bytes(dict(schemaVersion=1,

        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, newOwnedSpecies=[150],
        moveAnimationGeometry=dict(inheritedManifest=geometry_manifest, rows=inherited_geometry),
        encounter=dict(sourcePath=script_path, sourceLabel='CeruleanCave_B1F_EventScript_Mewtwo',
            cry='CRY_MEWTWO_ENCOUNTER', cryMode=2, cryParameters=dict(length=140, release=225, pitch=15600, chorus=20, volume=90),
            introductionMusic=inherited_symbol, battleMusic=symbol, battleMusicSourcePath=setup_path, sourceSongId=song_id,
            playbackAuthority='existing committed presentation effects and battle music selection'),

        limits=['No runtime rules are created by this presentation package.',
            'Party icon uses original frame zero shared by normal/shiny variants.',
            'Map planes and overworld Mewtwo are supplied by the Cerulean Cave map package.',
            'Existing bounded move animation dispatch and geometry are reused; no new move animation coverage.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})

    report = dict(passed=True, sourceFiles=len(source.raw), inheritedInputs=len(source.inherited), sprites=len(sprites),
        pokemonIcons=len(icons), footprints=len(footprints), assets=len(assets), moveGeometry=len(inherited_geometry),
        outputs=len(out) - 1,  manifestBytes=len(manifest),
          sourceAndStaticChecksOnly=True)
    return result, report

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--dependency-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root, args.dependency_root or args.project_root)
    publish(args.project_root, output, check=args.check)
    print(json.dumps(report, sort_keys=True))
